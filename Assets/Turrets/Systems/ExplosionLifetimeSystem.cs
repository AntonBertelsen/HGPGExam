using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateBefore(typeof(BoidSystem))] // Must run before boid system since it compiles a list of explosions that boid system needs to react to
public partial struct ExplosionLifetimeSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponent<ExplosionManagerTag>(entity);
        state.EntityManager.AddBuffer<ActiveExplosionElement>(entity);
        
        state.RequireForUpdate<ExplosionManagerTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {   
        var singletonEntity = SystemAPI.GetSingletonEntity<ExplosionManagerTag>();
        var buffer = SystemAPI.GetBuffer<ActiveExplosionElement>(singletonEntity);
        buffer.Clear();
        
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        
        // The number of explosions is relatively small so it's probably not worth the overhead of splitting into jobs. At least that is my intuition. Keeping it on the main thread allows us
        // to easily write into ActiveExplosions without race conditions and to easily access the ActiveExplosions list in the BoidSystem
        foreach (var (explosion, transform, entity) in
                 SystemAPI.Query<RefRW<ExplosionComponent>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            explosion.ValueRW.timeLived += deltaTime;

            // Explosions have a push force from 0.1 to 0.2 lifetime, this is kind of arbitrary but timed a bit with the visual effect to appear more natural
            if (explosion.ValueRO.timeLived > 0.1f && explosion.ValueRO.timeLived < 0.2f)
            {
                buffer.Add(new ActiveExplosionElement
                {
                    Position = transform.ValueRO.Position,
                    Force = explosion.ValueRO.explosionForce,
                    RadiusSq = explosion.ValueRO.explosionDistance * explosion.ValueRO.explosionDistance,
                    physicsBird = explosion.ValueRO.physicsBird
                });
            }

            if (explosion.ValueRO.timeLived >= explosion.ValueRO.lifeExpetancy)
            {
                ecb.DestroyEntity(entity);
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
