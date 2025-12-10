using System;
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

    // Stores startle events (positions) from the previous frame
    private NativeList<float3> _activeStartles;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<KdTree>();

        _landerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Lander, LocalTransform>()
            .Build(ref state);

        _activeStartles = new NativeList<float3>(Allocator.Persistent);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_activeStartles.IsCreated)
            _activeStartles.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var tree = SystemAPI.GetSingleton<KdTree>();
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        var nextFrameStartles = new NativeQueue<float3>(Allocator.TempJob);

        var updates = new NativeArray<(Update, int)>(_landerQuery.CalculateEntityCount(), Allocator.TempJob);

        var landerJob = new LanderJob
        {
            KdTree = tree,
            Updates = updates,
            Ecb = ecb.AsParallelWriter(),
            DeltaTime = SystemAPI.Time.DeltaTime,
            // Pass the startles we recorded last frame
            IncomingStartles = _activeStartles.AsDeferredJobArray(),
            // Pass the writer for startles happening right now
            OutgoingStartles = nextFrameStartles.AsParallelWriter()
        };

        state.Dependency = landerJob.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();

        foreach (var (update, index) in updates)
        {
            switch (update)
            {
                case Update.None: break;
                case Update.Occupied: tree.Occupy(index); break;
                case Update.Freed: tree.Free(index); break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        // 2. Cycle Startle Events
        // Clear the old startles and copy the new ones we just collected
        _activeStartles.Clear();
        while (nextFrameStartles.TryDequeue(out var pos))
        {
            _activeStartles.Add(pos);
        }

        updates.Dispose();
        nextFrameStartles.Dispose();
    }
}

[BurstCompile]
public partial struct LanderJob : IJobEntity
{
    [ReadOnly] public KdTree KdTree;
    [WriteOnly] public NativeArray<(Update update, int index)> Updates;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public float DeltaTime;

    [ReadOnly] public NativeArray<float3> IncomingStartles;
    [WriteOnly] public NativeQueue<float3>.ParallelWriter OutgoingStartles;

    // Constants for tuning
    private const float BaseEnergyDepletion = 0.55f;
    private const float BaseEnergyRecovery = 0.65f;
    private const float MaxEnergy = 100.0f;

    // Influence Settings
    private const float StartleRadius = 6.0f; // Range of influence
    private const float StartleChanceMultiplier = 1.0f;

    private const float DockingDistance = 3.0f;

    private void Execute([ChunkIndexInQuery] int chunkIndex, [EntityIndexInQuery] int entityIndex, Entity entity,
        ref Lander lander, ref LocalTransform transform)
    {
        var random = Unity.Mathematics.Random.CreateFromIndex((uint)entityIndex * 0x9F6ABC1);
        var metabolicRate = 0.8f + (random.NextFloat() * 0.4f);

        switch (lander.State)
        {
            case LanderState.Flying:
            {
                if (lander.Energy <= 0)
                {
                    var target = KdTree.Query(transform.Position);
                    if (target.Index == -1)
                    {
                        lander.Energy += 15.0f * metabolicRate;
                        break;
                    }

                    var spotNormal = KdTree.GetNormal(target.Index);

                    var offset = 0.15f;

                    lander.Target = target.Position + (spotNormal * offset);
                    lander.TargetIndex = target.Index;
                    lander.State = LanderState.Landing;
                }
                else
                {
                    lander.Energy -= BaseEnergyDepletion * metabolicRate * DeltaTime;
                }

                break;
            }
            case LanderState.Landing:
            {
                if (KdTree.IsOccupied(lander.TargetIndex))
                {
                    lander.Energy += 10.0f;
                    lander.State = LanderState.Flying;
                    break;
                }

                if (math.distancesq(transform.Position, lander.Target) < DockingDistance)
                {
                    // What if two birds try to land on the same spot at the same time?
                    // Who cares; let them.
                    Updates[entityIndex] = (Update.Occupied, lander.TargetIndex);

                    lander.State = LanderState.Docking;

                    Ecb.RemoveComponent<BoidTag>(chunkIndex, entity);
                    Ecb.RemoveComponent<Velocity>(chunkIndex, entity);
                    Ecb.AddComponent<BirdPerchedProperty>(chunkIndex, entity);
                }

                break;
            }
            case LanderState.Docking:
            {
                // Animating the final landing is handled in BirdPerchAnimationJob
                var distSq = math.distancesq(transform.Position, lander.Target);
                if (distSq < 0.005f)
                {
                    lander.State = LanderState.Landed;
                    lander.Energy = random.NextFloat(0.0f, 80.0f);
                }

                break;
            }
            case LanderState.Landed:
            {
                // --- Startle Logic ---
                var isStartled = false;

                var startlePersonality = 0.1f + (random.NextFloat() * 0.5f);

                var startleChance = startlePersonality * StartleChanceMultiplier;

                // Optimization: Don't check startles if we are already full energy (we are taking off anyway)
                if (lander.Energy < MaxEnergy)
                {
                    // Check if any bird took off near us in the previous frame
                    foreach (var t in IncomingStartles)
                    {
                        if (math.distancesq(transform.Position, t) < StartleRadius * StartleRadius
                            && random.NextFloat() < startleChance)
                        {
                            isStartled = true;
                            break;
                        }
                    }
                }

                // If startled, we boost energy to Max immediately.
                // This forces the "Takeoff" block below to execute in this same tick.
                if (isStartled)
                {
                    lander.Energy = MaxEnergy - 0.2f;
                }
                else if (lander.Energy < MaxEnergy)
                {
                    lander.Energy += BaseEnergyRecovery * metabolicRate * DeltaTime;
                }

                // --- Takeoff Logic ---
                if (lander.Energy >= MaxEnergy)
                {
                    lander.State = LanderState.Flying;
                    Updates[entityIndex] = (Update.Freed, lander.TargetIndex);

                    var randomStartVariance = random.NextFloat(0.85f, 1.0f);
                    lander.Energy = MaxEnergy * randomStartVariance;


                    // Takeoff velocity
                    var normal = KdTree.GetNormal(lander.TargetIndex);
                    var randomDir = random.NextFloat3Direction();
                    if (math.dot(randomDir, normal) < 0) randomDir = -randomDir;

                    // Eject Up/Out from surface
                    var takeoffDir = math.normalize(normal * 2.0f + randomDir);

                    Ecb.AddComponent<BoidTag>(chunkIndex, entity);
                    Ecb.AddComponent(chunkIndex, entity, new Velocity { Value = takeoffDir * 5.0f });
                    Ecb.RemoveComponent<BirdPerchedProperty>(chunkIndex, entity);

                    // Broadcast our position to scare others next frame
                    OutgoingStartles.Enqueue(transform.Position);
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