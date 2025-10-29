using System;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(BoidSystem))]
[RequireMatchingQueriesForUpdate]
public partial struct LanderSystem : ISystem
{
    private EntityQuery _landingAreaQuery;
    private EntityQuery _landerQuery;
    private NativeArray<bool> _occupiedLandingSpots;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();

        _landingAreaQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LandingArea, LocalTransform>()
            .Build(ref state);

        _landerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Lander, LocalTransform>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        state.Dependency = _occupiedLandingSpots.Dispose(state.Dependency);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var landingAreas = _landingAreaQuery.ToComponentDataArray<LandingArea>(Allocator.TempJob);
        var transforms = _landingAreaQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        if (!_occupiedLandingSpots.IsCreated)
        {
            var spotCount = 0;
            for (var i = 0; i < landingAreas.Length; i++)
            {
                var landingArea = landingAreas[i];
                ref var positions = ref landingArea.SurfaceBlob.Value.Positions;
                spotCount += positions.Length;
            }

            _occupiedLandingSpots = new NativeArray<bool>(spotCount, Allocator.Persistent);
        }

        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        var updates = new NativeArray<(Update, int)>(_landerQuery.CalculateEntityCount(), Allocator.TempJob);

        var landerJob = new LanderJob
        {
            LandingAreas = landingAreas,
            Transforms = transforms,
            OccupiedLandingSpots = _occupiedLandingSpots,
            Updates = updates,
            Ecb = ecb.AsParallelWriter()
        };

        state.Dependency = landerJob.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();

        foreach (var (update, index) in updates)
        {
            switch (update)
            {
                case Update.None:
                    break;
                case Update.Occupied:
                    _occupiedLandingSpots[index] = true;
                    break;
                case Update.Freed:
                    _occupiedLandingSpots[index] = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        updates.Dispose();
    }
}

[BurstCompile]
public partial struct LanderJob : IJobEntity
{
    [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<LandingArea> LandingAreas;
    [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<LocalTransform> Transforms;
    [ReadOnly] public NativeArray<bool> OccupiedLandingSpots;
    [WriteOnly] public NativeArray<(Update update, int index)> Updates;
    public EntityCommandBuffer.ParallelWriter Ecb;

    private void Execute([ChunkIndexInQuery] int chunkIndex, [EntityIndexInQuery] int entityIndex, Entity entity,
        ref Lander lander, in LocalTransform transform)
    {
        switch (lander.State)
        {
            case LanderState.Flying:
            {
                if (lander.Energy <= 0)
                {
                    var target = AcquireTarget(transform);
                    if (target.index == -1)
                    {
                        lander.Energy += 100;
                        break;
                    }

                    lander.Target = target.position;
                    lander.TargetIndex = target.index;
                    lander.State = LanderState.Landing;
                }
                else
                {
                    // TODO: Something with delta time
                    lander.Energy -= 1;
                }

                break;
            }
            case LanderState.Landing:
            {
                if (OccupiedLandingSpots[lander.TargetIndex])
                {
                    lander.State = LanderState.Flying;
                    break;
                }

                var dist = math.length(transform.Position - lander.Target);
                if (dist < .3f)
                {
                    // What if two landers try to land on the same spot at the same time?
                    // Who cares; let them.
                    lander.State = LanderState.Landed;
                    Updates[entityIndex] = (Update.Occupied, lander.TargetIndex);
                    Ecb.RemoveComponent<BoidTag>(chunkIndex, entity);
                    Ecb.RemoveComponent<Velocity>(chunkIndex, entity);
                }

                break;
            }
            case LanderState.Landed:
            {
                if (lander.Energy < Lander.MaxEnergy)
                {
                    lander.Energy += 5;
                }
                else
                {
                    lander.State = LanderState.Flying;
                    Updates[entityIndex] = (Update.Freed, lander.TargetIndex);
                    Ecb.AddComponent<BoidTag>(chunkIndex, entity);
                    Ecb.AddComponent(chunkIndex, entity,
                        new Velocity { Value = math.rotate(transform.Rotation, math.forward()) });
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private (float3 position, int index) AcquireTarget(LocalTransform transform)
    {
        var closestLandingSpot = (float3.zero, -1);
        var min = double.MaxValue;

        var curr = 0;
        foreach (var landingArea in LandingAreas)
        {
            ref var positions = ref landingArea.SurfaceBlob.Value.Positions;

            for (var spotIndex = 0; spotIndex < landingArea.Count; spotIndex++)
            {
                var globalSpotIndex = curr + spotIndex;
                if (OccupiedLandingSpots[globalSpotIndex]) continue;

                var pos = positions[spotIndex];
                var dist = math.length(transform.Position - pos);
                if (dist >= min) continue;

                min = dist;
                closestLandingSpot = (pos, globalSpotIndex);
            }

            curr += landingArea.Count;
        }

        return closestLandingSpot;
    }
}

// I really want to pack these into the index, but this is cleaner :/
public enum Update : int
{
    None,
    Occupied,
    Freed,
}