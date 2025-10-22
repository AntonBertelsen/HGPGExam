using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateBefore(typeof(MoveSystem))]
[UpdateAfter(typeof(SpatialHashingSystem))]
public partial struct BoidSystem : ISystem
{
    private EntityQuery boidQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();
        boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();
        var gridData = SystemAPI.GetSingleton<SpatialGridData>();

        var transforms = boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var velocities = boidQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);
        var entities   = boidQuery.ToEntityArray(Allocator.TempJob);

        if (transforms.Length == 0)
            return;

        var job = new BoidJob
        {
            Config = config,
            Entities = entities,
            Transforms = transforms,
            Velocities = velocities,
            Grid = gridData.CellMap,
            Hash = gridData.Grid
        };

        var handle = job.ScheduleParallel(state.Dependency);

        state.Dependency = JobHandle.CombineDependencies(
            entities.Dispose(handle),
            transforms.Dispose(handle),
            velocities.Dispose(handle)
        );
    }
}

[BurstCompile]
public partial struct BoidJob : IJobEntity
{
    public BoidSettings Config;

    [ReadOnly] public NativeArray<Entity> Entities;
    [ReadOnly] public NativeArray<LocalTransform> Transforms;
    [ReadOnly] public NativeArray<Velocity> Velocities;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
    [ReadOnly] public SpatialHashGrid3D Hash;

    public void Execute(Entity currentEntity, ref Velocity currentVelocity, in LocalTransform currentTransform)
    {
        var pos = currentTransform.Position;
        int3 cell = Hash.GetCellCoords(pos);

        var separation = float3.zero;
        var alignment = float3.zero;
        var cohesion = float3.zero;
        int neighborCount = 0;

        float r2 = Config.ViewRadius * Config.ViewRadius;

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
                    float3 diff = otherPos - pos;
                    float dist2 = math.lengthsq(diff);

                    if (dist2 <= r2)
                    {
                        neighborCount++;
                        float dist = math.sqrt(dist2);
                        if (dist < Config.SeparationRadius && dist > 1e-6f)
                            separation -= diff / dist;
                        alignment += Velocities[otherIdx].Value;
                        cohesion += otherPos;
                    }

                } while (Grid.TryGetNextValue(out otherIdx, ref it));
            }
        }

        if (neighborCount > 0)
        {
            cohesion = (cohesion / neighborCount) - pos;
            alignment /= neighborCount;

            float3 steerCoh = math.normalizesafe(cohesion) * Config.MaxSpeed - currentVelocity.Value;
            steerCoh = math.normalizesafe(steerCoh) * math.min(math.length(steerCoh), Config.MaxSteerForce);

            float3 steerAli = math.normalizesafe(alignment) * Config.MaxSpeed - currentVelocity.Value;
            steerAli = math.normalizesafe(steerAli) * math.min(math.length(steerAli), Config.MaxSteerForce);

            float3 steerSep = math.normalizesafe(separation) * Config.MaxSpeed - currentVelocity.Value;
            steerSep = math.normalizesafe(steerSep) * math.min(math.length(steerSep), Config.MaxSteerForce);

            var total = steerCoh * Config.CohesionWeight
                      + steerAli * Config.AlignmentWeight
                      + steerSep * Config.SeparationWeight;

            currentVelocity.Value += total;
        }

        float speed = math.length(currentVelocity.Value);
        speed = math.clamp(speed, Config.MinSpeed, Config.MaxSpeed);
        currentVelocity.Value = math.normalizesafe(currentVelocity.Value) * speed;
    }
}