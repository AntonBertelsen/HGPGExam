using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile]
[UpdateBefore(typeof(MoveSystem))] // Calculate new velocity before it's used for movement
[UpdateAfter(typeof(SpatialHashingSystem))]
public partial struct BoidSystem : ISystem
{
    private EntityQuery boidQuery;
    private NativeArray<float3> _directions;

    public void OnCreate(ref SystemState state)
    {
        // We require the config to exist.
        state.RequireForUpdate<BoidSettings>();

        _directions = BoidHelper.Directions;

        // We create a query to find all boids.
        boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();
        var gridData = SystemAPI.GetSingleton<SpatialGridData>();
        
        FlowFieldData flowField = default;
        bool hasFlowField = SystemAPI.TryGetSingleton(out flowField);

        // This is the brute-force part. We copy all boid data into arrays.
        var boids = boidQuery.ToEntityArray(Allocator.TempJob);
        var transforms = boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var velocities = boidQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);

        if (boids.Length == 0)
            return;

        var boidJob = new BoidJob
        {
            Config = config,
            Entities = boids,
            Transforms = transforms,
            Velocities = velocities,
            Directions = _directions,
            Grid = gridData.CellMap,
            Hash = gridData.Grid,
            DeltaTime = SystemAPI.Time.DeltaTime,
            
            HasFlowField = hasFlowField,
            FlowField = flowField
        };

        // Schedule the job and chain the disposal of our temporary arrays.
        var jobHandle = boidJob.ScheduleParallel(state.Dependency);

        jobHandle = boids.Dispose(jobHandle);
        jobHandle = transforms.Dispose(jobHandle);
        jobHandle = velocities.Dispose(jobHandle);

        state.Dependency = jobHandle;
    }
}

[BurstCompile]
public partial struct BoidJob : IJobEntity
{
    public BoidSettings Config;
    public float DeltaTime; 

    [ReadOnly] public NativeArray<float3> Directions;

    // These arrays contain the data for ALL boids in the simulation.
    [ReadOnly] public NativeArray<Entity> Entities;
    [ReadOnly] public NativeArray<LocalTransform> Transforms;
    [ReadOnly] public NativeArray<Velocity> Velocities;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
    [ReadOnly] public SpatialHashGrid3D Hash;
    
    public bool HasFlowField;
    [ReadOnly] public FlowFieldData FlowField;

    // This 'Execute' method runs for EACH boid.
    private void Execute(Entity currentEntity, ref Velocity currentVelocity, ref BoidTag boidTag, in LocalTransform currentTransform,
        in ObstacleAvoidance obstacleAvoidance, in Lander lander)
    {
        if (boidTag.dead) return;
        
        // Check if we have been blown away by an explosion (velocity > MaxSpeed)
        float currentSpeed = math.length(currentVelocity.Value);
        
        // If we are moving faster than the boid physics allows, we are "tumbling".
        // In this state, we ignore boid rules and simply decelerate due to drag.
        if (currentSpeed > Config.MaxSpeed)
        {
            // Calculate drag. A value of 2.0f means they recover control quickly. 
            // 0.5f means they fly helpless for longer.
            float recoveryDrag = 2.0f; 
            
            // Smoothly lerp the velocity down towards the MaxSpeed limit
            currentVelocity.Value = math.lerp(currentVelocity.Value, 
                math.normalizesafe(currentVelocity.Value) * Config.MaxSpeed, 
                DeltaTime * recoveryDrag);
            
            // Early return! 
            // We skip cohesion/alignment because you can't align with friends 
            // while you are tumbling through the air at 100mph.
            return; 
        }
        
        
        var flockSize = 0;
        var flockCentre = float3.zero;
        var flockVelocity = float3.zero;
        var flockSeparation = float3.zero;

        int3 cell = Hash.GetCellCoords(currentTransform.Position);

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            int3 neighborCell = cell + new int3(dx, dy, dz);
            int key = Hash.GetCellIndex(neighborCell);

            NativeParallelMultiHashMapIterator<int> it;
            int otherIdx;

            if (Grid.TryGetFirstValue(key, out otherIdx, out it))
            {
                do
                {
                    var otherEntity = Entities[otherIdx];
                    if (otherEntity == currentEntity)
                        continue;


                    var otherPos = Transforms[otherIdx].Position;
                    float3 diff = otherPos - currentTransform.Position;
                    float dist2 = math.lengthsq(diff); // We do this to avoid a square root calculation for non-neighbors

                    // Check if the other boid is a neighbor (It still may not be even though it's in a neighboring cell)
                    if (dist2 > Config.ViewRadius * Config.ViewRadius) continue;

                    float dist = math.sqrt(dist2);
                    flockSize++;
                    flockCentre += otherPos;

                    var otherVelocity = Velocities[otherIdx];
                    flockVelocity += otherVelocity.Value;

                    if (dist < Config.SeparationRadius && dist > 1e-6f) // Last check to avoid division by zero
                    {
                        var offset = currentTransform.Position - otherPos;
                        flockSeparation += offset / dist;
                    }
                } while (Grid.TryGetNextValue(out otherIdx, ref it));
            }
        }    
        var acceleration = float3.zero;
        
        if (flockSize != 0)
        {
            flockCentre /= flockSize;
            var flockOffset = flockCentre - currentTransform.Position;

            var cohesion = SteerTowards(flockOffset, currentVelocity.Value) * Config.CohesionWeight;
            var alignment = SteerTowards(flockVelocity, currentVelocity.Value) * Config.AlignmentWeight;
            var separation = SteerTowards(flockSeparation, currentVelocity.Value) * Config.SeparationWeight;

            acceleration += cohesion;
            acceleration += alignment;
            acceleration += separation;
        }
        
        if (HasFlowField)
        {
            // Sample the field at our current position
            float3 flowForce = GetFlowFieldForce(currentTransform.Position);
            acceleration += flowForce * Config.FlowmapWeight;
            
            //if (math.lengthsq(flowForce) > 0.0f)
            //{
            //    // Note: Debug.DrawLine works in Burst but requires 'using UnityEngine;'
            //    Debug.DrawLine(currentTransform.Position, currentTransform.Position + flowForce * 2.0f, Color.magenta);
            //}
        }

        if (obstacleAvoidance.DirectionIndex != 0)
        {
            acceleration +=
                SteerTowards(
                    BoidHelperMath.RelativeDirection(currentTransform.Rotation,
                        Directions[obstacleAvoidance.DirectionIndex]), currentVelocity.Value) *
                Config.AvoidanceWeight;
        }

        if (lander.State == LanderState.Landing)
        {
            var distanceToTarget = math.distance(currentTransform.Position, lander.Target);
            var distanceWeight = math.clamp(1f - (distanceToTarget / Config.LandingRadius), .2f, 1f);
            acceleration += SteerTowards(lander.Target - currentTransform.Position, currentVelocity.Value) *
                            (Config.LandingWeight * distanceWeight);
        }

        currentVelocity.Value += acceleration;

        // Limit the final speed
        currentVelocity.Value = math.clamp(math.length(currentVelocity.Value), Config.MinSpeed, Config.MaxSpeed) *
                                math.normalizesafe(currentVelocity.Value);
    }

    private float3 SteerTowards(float3 vector, float3 velocity)
    {
        var v = math.normalizesafe(vector) * Config.MaxSpeed - velocity;
        return math.normalizesafe(v) * math.min(math.length(vector), Config.MaxSteerForce);
    }
    
    // --- TRILINEAR INTERPOLATION SAMPLING ---
    private float3 GetFlowFieldForce(float3 position)
    {
        // 1. Convert world position to grid space (0.0 to Dimensions)
        float3 localPos = (position - FlowField.GridOrigin) / FlowField.CellSize;
        
        // 2. Check bounds - if outside the baked area, return zero force
        if (localPos.x < 0 || localPos.y < 0 || localPos.z < 0 ||
            localPos.x >= FlowField.GridDimensions.x - 1 || 
            localPos.y >= FlowField.GridDimensions.y - 1 || 
            localPos.z >= FlowField.GridDimensions.z - 1)
        {
            return float3.zero;
        }

        // 3. Get the bottom-left corner index (Integer part)
        int3 c000 = (int3)math.floor(localPos);
        int3 c111 = c000 + 1;

        // 4. Calculate fractional weights (0.0 to 1.0) for interpolation
        float3 w = localPos - c000;
        float3 invW = 1.0f - w;

        int gridDimensionX = FlowField.GridDimensions.x;
        int gridDimensionY = FlowField.GridDimensions.y;

        // Helper to get flat index safely
        int GetIdx(int x, int y, int z) => x + y * gridDimensionX + z * gridDimensionX * gridDimensionY;

        // 5. Sample the 8 neighbor vectors
        // x/y/z correspond to 0 or 1 offsets
        float3 v000 = FlowField.Blob.Value.Vectors[GetIdx(c000.x, c000.y, c000.z)];
        float3 v100 = FlowField.Blob.Value.Vectors[GetIdx(c111.x, c000.y, c000.z)];
        float3 v010 = FlowField.Blob.Value.Vectors[GetIdx(c000.x, c111.y, c000.z)];
        float3 v110 = FlowField.Blob.Value.Vectors[GetIdx(c111.x, c111.y, c000.z)];
        
        float3 v001 = FlowField.Blob.Value.Vectors[GetIdx(c000.x, c000.y, c111.z)];
        float3 v101 = FlowField.Blob.Value.Vectors[GetIdx(c111.x, c000.y, c111.z)];
        float3 v011 = FlowField.Blob.Value.Vectors[GetIdx(c000.x, c111.y, c111.z)];
        float3 v111 = FlowField.Blob.Value.Vectors[GetIdx(c111.x, c111.y, c111.z)];

        // 6. Trilinear Blend
        // Blend along X
        float3 x00 = v000 * invW.x + v100 * w.x;
        float3 x10 = v010 * invW.x + v110 * w.x;
        float3 x01 = v001 * invW.x + v101 * w.x;
        float3 x11 = v011 * invW.x + v111 * w.x;

        // Blend along Y
        float3 y0 = x00 * invW.y + x10 * w.y;
        float3 y1 = x01 * invW.y + x11 * w.y;

        // Blend along Z (Final result)
        return y0 * invW.z + y1 * w.z;
    }
}