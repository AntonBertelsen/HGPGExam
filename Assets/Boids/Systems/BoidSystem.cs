using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

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
        state.RequireForUpdate<ExplosionManagerTag>();
        state.RequireForUpdate<SpatialGridData>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

        _directions = BoidHelper.Directions;

        // We create a query to find all boids.
        boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (boidQuery.CalculateEntityCount() == 0) return;
        
        var config = SystemAPI.GetSingleton<BoidSettings>();
        var gridData = SystemAPI.GetSingleton<SpatialGridData>();
        var boundary = SystemAPI.GetSingleton<BoundaryComponent>();
        
        FlowFieldData flowField = default;
        bool hasFlowField = SystemAPI.TryGetSingleton(out flowField);

        var singletonEntity = SystemAPI.GetSingletonEntity<ExplosionManagerTag>();
        var explosionBuffer = SystemAPI.GetBuffer<ActiveExplosionElement>(singletonEntity);
        
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        
        var boidJob = new BoidJob
        {
            Config = config,
            Directions = _directions,
            Grid = gridData.CellMap,
            Hash = gridData.Grid,
            DeltaTime = SystemAPI.Time.DeltaTime,
            
            HasFlowField = hasFlowField,
            FlowField = flowField,
            Bounds = boundary,
            Explosions = explosionBuffer.AsNativeArray(),
            ECB = ecb,
        };

        // Schedule the job and chain the disposal of our temporary arrays.
        if (config.UseParallel)
        {
            var jobHandle = boidJob.ScheduleParallel(state.Dependency);
            state.Dependency = jobHandle;
        }
        else
        {
            var jobHandle = boidJob.Schedule(state.Dependency);
            state.Dependency = jobHandle;
        }
    }
}

[BurstCompile]
public partial struct BoidJob : IJobEntity
{
    public BoidSettings Config;
    public float DeltaTime; 

    [ReadOnly] public NativeArray<float3> Directions;
    
    [ReadOnly] public NativeParallelMultiHashMap<int, BoidData> Grid;
    [ReadOnly] public SpatialHashGrid3D Hash;
    
    [ReadOnly] public bool HasFlowField;
    [ReadOnly] public FlowFieldData FlowField;

    [ReadOnly] public BoundaryComponent Bounds;
    
    [ReadOnly] public NativeArray<ActiveExplosionElement> Explosions;
    
    public EntityCommandBuffer.ParallelWriter ECB;
    
    
    private void Execute(Entity currentEntity, [EntityIndexInQuery] int sortKey, ref Velocity currentVelocity,
        in LocalTransform currentTransform, in ObstacleAvoidance obstacleAvoidance, in Lander lander)
    {
        // Check if we have been blown away by an explosion (velocity > MaxSpeed)
        float currentSpeedSq = math.lengthsq(currentVelocity.Value);

        // Precalculate squared speeds to avoid square roots later
        float maxSpeedSq = Config.MaxSpeed * Config.MaxSpeed;
        float minSpeedSq = Config.MinSpeed * Config.MinSpeed;
        
        // If we are moving faster than the boid physics allows, we are "tumbling".
        // In this state, we ignore boid rules and simply decelerate due to drag.
        if (currentSpeedSq > maxSpeedSq * 1.2f)
        {
            // Calculate drag. A value of 2.0f means they recover control quickly. 
            // 0.5f means they fly helpless for longer.
            float recoveryDrag = 2.0f; 
            
            // Smoothly lerp the velocity down towards the MaxSpeed limit
            currentVelocity.Value = math.lerp(currentVelocity.Value, 
                math.normalizesafe(currentVelocity.Value) * Config.MaxSpeed, 
                DeltaTime * recoveryDrag);
            
            return; 
        }

        var random = new Random((uint)currentEntity.Index);
        
        var flockSize = 0;
        var flockCentre = float3.zero;
        var flockVelocity = float3.zero;
        var flockSeparation = float3.zero;

        var cell = Hash.GetCellCoords(currentTransform.Position);

        int consideredNeighbors = Config.MaxConsideredNeighbors;
        var upperNeighborBound = consideredNeighbors * consideredNeighbors;
        int neighborsSeen = 0;
        float separationSq = Config.SeparationRadius * Config.SeparationRadius;
        float viewRadiusSq = Config.ViewRadius * Config.ViewRadius;

        // The center of a 3x3x3 grid (0..26) is index 13.
        // 1 * 1 + 1 * 3 + 1 * 9 = 13  (mapping 0,0,0 to 1,1,1 in 0-2 coordinates)
        const int centerIndex = 13;
        int startOffset = random.NextInt(0, 27);
        var neighbors = new NativeArray<BoidData>(upperNeighborBound, Allocator.Temp);

        if (consideredNeighbors == 0)
        {
            goto FlockCalculated;
        }

        for (int i = 0; i < 27; i++)
        {
            int index;

            // Always check our cell first because separation is most important
            if (i == 0)
            {
                index = centerIndex;
            }
            else
            {
                // Calculate the random index based on offset
                int rawIndex = (startOffset + i) % 27;

                // If the random index happens to be 13 (which we already did at i=0),
                // we swap it with the index we overwrote at i=0 (which was startOffset).
                if (rawIndex == centerIndex)
                {
                    index = startOffset;
                }
                else
                {
                    index = rawIndex;
                }
            }

            // decode index into dx, dy, dz (-1 to 1)
            int dx = (index % 3) - 1;
            int dy = ((index / 3) % 3) - 1;
            int dz = ((index / 9) % 3) - 1;

            var neighborCell = cell + new int3(dx, dy, dz);
            var key = Hash.GetCellIndex(neighborCell);

            if (!Grid.TryGetFirstValue(key, out BoidData otherBoid, out var it))
            {
                continue;
            }
            
            do
            {
                if (otherBoid.Entity == currentEntity)
                {
                    continue;
                }

                neighbors[neighborsSeen++] = otherBoid;

                if (neighborsSeen == upperNeighborBound)
                {
                    goto FlockCalculated;
                }
            } while (Grid.TryGetNextValue(out otherBoid, ref it));
        }

        FlockCalculated:
        for (int n = 0; n < consideredNeighbors && neighborsSeen > 0; n++)
        {
            var i = random.NextInt(0, neighborsSeen);
            var otherBoid = neighbors[i];
            
            // Swap remove to avoid processing the same neighbor again
            neighborsSeen--;
            if (neighborsSeen >= 0)
            {
                neighbors[i] = neighbors[neighborsSeen];
            }

            var otherPos = otherBoid.Position;
            var otherVel = otherBoid.Velocity;

            var diff = otherPos - currentTransform.Position;
            var dist2 = math.lengthsq(diff);

            if (dist2 > viewRadiusSq) continue;

            flockSize++;
            flockCentre += otherPos;

            flockVelocity += otherVel;

            if (dist2 < Config.SeparationRadius * Config.SeparationRadius &&
                dist2 > 1e-6f) // Last check to avoid division by zero
            {
                flockSeparation += (currentTransform.Position - otherPos) / dist2;
            }
        }
        
        var acceleration = float3.zero;
        
        if (flockSize != 0)
        {
            flockCentre /= flockSize;
            var cohesionVec = flockCentre - currentTransform.Position;
            var alignmentVec = flockVelocity;
            var separationVec = flockSeparation;
            
            float3 desiredDir = (SafeNormalize(cohesionVec) * Config.CohesionWeight) +
                                (SafeNormalize(alignmentVec) * Config.AlignmentWeight) +
                                (SafeNormalize(separationVec) * Config.SeparationWeight);
            
            acceleration += SteerTowards(desiredDir, currentVelocity.Value, Config.MaxSteerForce);
        }
        
        float3 boundaryDir = CalculateBoundaryForce(currentTransform.Position, Bounds);
        if (math.lengthsq(boundaryDir) > 0)
        {
            float3 steerForce = SteerTowards(boundaryDir, currentVelocity.Value, Config.MaxSteerForce);
            acceleration += steerForce * Config.BoundaryWeight;
        }
        
        
        if (HasFlowField && Config.FlowmapWeight > 0.001f && Config.FlowMapEnabled)
        {
            // Sample the field at our current position
            //float3 flowForce = GetFlowFieldForce(currentTransform.Position);
            //acceleration += flowForce * Config.FlowmapWeight;
            float3 flowDir = GetFlowFieldForce(currentTransform.Position);
            if (math.lengthsq(flowDir) > 0)
            {
                float3 steerForce = SteerTowards(flowDir, currentVelocity.Value, Config.MaxSteerForce * 5f);
                acceleration += steerForce * Config.FlowmapWeight;
            }
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
        float speedSq = math.lengthsq(currentVelocity.Value);
        if (speedSq > maxSpeedSq)
        {
            currentVelocity.Value = math.normalize(currentVelocity.Value) * Config.MaxSpeed;
        }
        else if (speedSq < minSpeedSq)
        {
            currentVelocity.Value = math.normalize(currentVelocity.Value) * Config.MinSpeed;
        }
        
        // Explosion force is applied after clamping to allow velocities above maxSpeed
        for (int e = 0; e < Explosions.Length; e++)
        {
            var exp = Explosions[e];
            float3 toBoid = currentTransform.Position - exp.Position;
            float distSq = math.lengthsq(toBoid);

            if (distSq < exp.RadiusSq)
            {
                if (distSq < (exp.RadiusSq * 0.15f)) 
                {
                    var deadBird = ECB.Instantiate(sortKey, exp.physicsBird);
                    ECB.SetComponent(sortKey, deadBird, currentTransform);
                    //ECB.AddComponent(sortKey, deadBird, currentVelocity);
                    ECB.AddComponent(sortKey, deadBird, new PendingDespawn { TimeRemaining = 3.5f });
                    ECB.DestroyEntity(sortKey, currentEntity);
                    return; 
                }
                
                float dist = math.sqrt(distSq);
                
                float3 dir = (dist > 0.001f) ? toBoid / dist : new float3(0,1,0); // protect against divide by zero 
            
                // linear falloff
                float falloff = 1.0f - (distSq / exp.RadiusSq); 
                
                currentVelocity.Value += dir * (exp.Force * falloff);
            }
        }
    }
    
    private float3 SafeNormalize(float3 v)
    {
        float lenSq = math.lengthsq(v);
        if (lenSq < 0.00001f) return float3.zero;
        return v * math.rsqrt(lenSq);
    }

    private float3 SteerTowards(float3 vector, float3 velocity, float maxSteer)
    {
        // "vector" is the desired direction/position
        var desired = math.normalizesafe(vector) * Config.MaxSpeed;
        var steer = desired - velocity;
        
        float steerLenSq = math.lengthsq(steer);
        if (steerLenSq > maxSteer * maxSteer)
        {
            return steer * (maxSteer * math.rsqrt(steerLenSq));
        }
        return steer;
    }
    
    private float3 SteerTowards(float3 vector, float3 velocity)
    {
        return SteerTowards(vector, velocity, Config.MaxSteerForce);
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
    
    private float3 CalculateBoundaryForce(float3 pos, BoundaryComponent bounds)
    {
        float3 center = bounds.Center;
        float3 extents = bounds.Size * 0.5f; 
        float3 offset = pos - center;
        float3 desiredVel = float3.zero;

        // X Axis
        if (offset.x > extents.x - bounds.PositiveMargins.x)
        {
            float t = (offset.x - (extents.x - bounds.PositiveMargins.x)) / bounds.PositiveMargins.x;
            desiredVel.x = -t; // Push Left
        }
        else if (offset.x < -extents.x + bounds.NegativeMargins.x)
        {
            float t = ((-extents.x + bounds.NegativeMargins.x) - offset.x) / bounds.NegativeMargins.x;
            desiredVel.x = t; // Push Right
        }

        // Y Axis
        if (offset.y > extents.y - bounds.PositiveMargins.y)
        {
            float t = (offset.y - (extents.y - bounds.PositiveMargins.y)) / bounds.PositiveMargins.y;
            desiredVel.y = -t; // Push DOwn
        }
        else if (offset.y < -extents.y + bounds.NegativeMargins.y)
        {
            float t = ((-extents.y + bounds.NegativeMargins.y) - offset.y) / bounds.NegativeMargins.y;
            desiredVel.y = t; // Push Up
        }

        // Z Axis
        if (offset.z > extents.z - bounds.PositiveMargins.z)
        {
            float t = (offset.z - (extents.z - bounds.PositiveMargins.z)) / bounds.PositiveMargins.z;
            desiredVel.z = -t; // Push Back
        }
        else if (offset.z < -extents.z + bounds.NegativeMargins.z)
        {
            float t = ((-extents.z + bounds.NegativeMargins.z) - offset.z) / bounds.NegativeMargins.z;
            desiredVel.z = t; // Push Forward
        }
    
        if (math.lengthsq(desiredVel) > 0)
        {
            return desiredVel; 
        }

        return float3.zero;
    }
}