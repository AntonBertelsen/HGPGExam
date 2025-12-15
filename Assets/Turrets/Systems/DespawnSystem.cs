using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct DespawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var dt = SystemAPI.Time.DeltaTime;
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (timer, entity) in SystemAPI.Query<RefRW<PendingDespawn>>().WithEntityAccess())
        {
            timer.ValueRW.TimeRemaining -= dt;

            if (timer.ValueRW.TimeRemaining <= 0)
            {
                ecb.DestroyEntity(entity);
            }
        }
    }
}