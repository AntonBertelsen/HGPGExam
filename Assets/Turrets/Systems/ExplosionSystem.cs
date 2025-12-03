using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine.VFX;

partial struct ExplosionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BulletComponent>();   
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);


        foreach (var (bullet, transform, bulletEntity) in
                 SystemAPI.Query<RefRW<BulletComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            bullet.ValueRW.timeLived += SystemAPI.Time.DeltaTime;
            if (bullet.ValueRO.timeLived >= bullet.ValueRO.timeToExplode)
            {
                var newEntity = ecb.Instantiate(bullet.ValueRO.explosion);

                ecb.AddComponent(newEntity, new LocalTransform
                {
                    Position = transform.ValueRO.Position,
                    Rotation = transform.ValueRO.Rotation,
                    Scale = 1f
                });
        
                // To allow the smoke trail effect to fade out we do not destroy the entity right away
                ecb.RemoveComponent<BulletVelocity>(bulletEntity);
                ecb.RemoveComponent<BulletTag>(bulletEntity);
                ecb.RemoveComponent<BulletComponent>(bulletEntity);
                ecb.AddComponent(bulletEntity, new PendingDespawn { TimeRemaining = 2.0f });
                
                // Stop the smoke trail from spawning
                // TODO: Work out how to do this in a burst supported way
                VisualEffect smokeTrail = state.EntityManager.GetComponentObject<VisualEffect>(bulletEntity);
                smokeTrail.SetBool("Spawn", false);
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
