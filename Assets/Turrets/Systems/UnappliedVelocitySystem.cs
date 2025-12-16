using Unity.Burst;
using Unity.Entities;
using Unity.Physics;

[BurstCompile]
[UpdateAfter(typeof(BoidSystem))]
public partial struct UnappliedVelocitySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (physicsVelocity, unappliedVelocity, entity) in SystemAPI
                     .Query<RefRW<PhysicsVelocity>, RefRO<UnappliedVelocity>>().WithEntityAccess())
        {
            physicsVelocity.ValueRW.Linear = unappliedVelocity.ValueRO.Velocity;
            ecb.RemoveComponent<UnappliedVelocity>(entity);
        }
    }
}