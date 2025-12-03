using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct BulletCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        // Iterate over entities that are pending despawn
        foreach (var (timer, entity) in SystemAPI.Query<RefRW<PendingDespawn>>().WithEntityAccess())
        {
            timer.ValueRW.TimeRemaining -= dt;

            // If time is up, actually delete the entity
            if (timer.ValueRW.TimeRemaining <= 0)
            {
                ecb.DestroyEntity(entity);
            }
        }
    }
}