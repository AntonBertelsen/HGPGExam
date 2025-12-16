using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine.VFX;

public struct CleanupVisualsTag : IComponentData { }

partial struct ProjectileSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // We start by turning off smoke trails from the previous frame. The reason we do it in this way is so that we can run the main job as a burst compiled job, which accessing VisualEffect prevents
        foreach (var (_, entity) in SystemAPI.Query<RefRO<CleanupVisualsTag>>().WithEntityAccess())
        {
            if (entityManager.HasComponent<VisualEffect>(entity))
            {
                VisualEffect smokeTrail = entityManager.GetComponentObject<VisualEffect>(entity);
                if (smokeTrail != null)
                {
                    smokeTrail.SetBool("Spawn", false);
                }
            }
            ecb.RemoveComponent<CleanupVisualsTag>(entity);
        }
        
        ecb.Playback(entityManager);
        ecb.Dispose();
        
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var parallelEcb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new ProjectileLogicJob
        {
            ECB = parallelEcb,
            DeltaTime = SystemAPI.Time.DeltaTime
        };
        
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct ProjectileLogicJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;
    public float DeltaTime;

    private void Execute(Entity entity, [EntityIndexInQuery] int sortKey, 
        ref BulletComponent bullet, 
        in LocalTransform transform)
    {
        bullet.timeLived += DeltaTime;

        if (bullet.timeLived >= bullet.timeToExplode)
        {
            // Spawn Explosion
            var newEntity = ECB.Instantiate(sortKey, bullet.explosion);
            
            ECB.AddComponent(sortKey, newEntity, new LocalTransform
            {
                Position = transform.Position,
                Rotation = transform.Rotation,
                Scale = 1f
            });

            // To allow the smoke trail effect to fade out we do not destroy the entity right away
            ECB.RemoveComponent<BulletVelocity>(sortKey, entity);
            ECB.RemoveComponent<BulletTag>(sortKey, entity);
            ECB.RemoveComponent<BulletComponent>(sortKey, entity);
            
            ECB.AddComponent(sortKey, entity, new PendingDespawn { TimeRemaining = 2.0f });

            // Stop the smoke trail from spawning next frame
            ECB.AddComponent<CleanupVisualsTag>(sortKey, entity);
        }
    }
}
