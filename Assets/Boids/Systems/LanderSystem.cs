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
    private EntityQuery _landerQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<KdTree>();

        _landerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Lander, LocalTransform>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var tree = SystemAPI.GetSingleton<KdTree>();
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        var updates = new NativeArray<(Update, int)>(_landerQuery.CalculateEntityCount(), Allocator.TempJob);

        var landerJob = new LanderJob
        {
            KdTree = tree,
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
                    tree.Occupy(index);
                    break;
                case Update.Freed:
                    tree.Free(index);
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
    [ReadOnly] public KdTree KdTree;
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
                    var target = KdTree.Query(transform.Position);
                    if (target.Index == -1)
                    {
                        lander.Energy += 100;
                        break;
                    }

                    lander.Target = target.Position;
                    lander.TargetIndex = target.Index;
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
                if (KdTree.IsOccupied(lander.TargetIndex))
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
}

// I really want to pack these into the index, but this is cleaner :/
public enum Update : int
{
    None,
    Occupied,
    Freed,
}