using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
            Hash = gridData.Grid
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

    [ReadOnly] public NativeArray<float3> Directions;

    // These arrays contain the data for ALL boids in the simulation.
    [ReadOnly] public NativeArray<Entity> Entities;
    [ReadOnly] public NativeArray<LocalTransform> Transforms;
    [ReadOnly] public NativeArray<Velocity> Velocities;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
    [ReadOnly] public SpatialHashGrid3D Hash;

    // This 'Execute' method runs for EACH boid.
    private void Execute(Entity currentEntity, ref Velocity currentVelocity, ref BoidTag boidTag, in LocalTransform currentTransform,
        in ObstacleAvoidance obstacleAvoidance, in Lander lander)
    {
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

        if (boidTag.dead) return;
        
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
}