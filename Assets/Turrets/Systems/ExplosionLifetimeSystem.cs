using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

partial struct ExplosionLifetimeSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BulletComponent>();   
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var gridData = SystemAPI.GetSingletonRW<SpatialGridData>();


        float deltaTime = SystemAPI.Time.DeltaTime;
        
        foreach (var (explosion, transform, entity) in
                 SystemAPI.Query<RefRW<ExplosionComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {

            explosion.ValueRW.timeLived += deltaTime;

            if (explosion.ValueRO.timeLived > 0.1 && explosion.ValueRO.timeLived < 0.2)
            {
                explosion.ValueRW.hasExploded = true;
                foreach (var (boidTrans, boidVel, boidTag, entityBoid) in SystemAPI
                             .Query<RefRW<LocalTransform>, RefRW<Velocity>, RefRW<BoidTag>>().WithAll<BoidTag>().WithEntityAccess())
                {
                    var direction = boidTrans.ValueRO.Position - transform.ValueRO.Position;
                    var dist = math.length(direction);
                    
                    if (dist < transform.ValueRO.Scale*10)
                    {
                        boidTag.ValueRW.dead = true;
                        var bird = ecb.Instantiate(explosion.ValueRO.physicsBird);
                        ecb.SetComponent(bird, boidTrans.ValueRW);
                        ecb.SetComponent(bird, boidVel.ValueRW);
                        ecb.SetComponent(bird, boidTag.ValueRW);
                        ecb.DestroyEntity(entityBoid);
                    }
                    else
                    {
                        var normalizedVec = direction/dist;

                        float baseExplosionDistance = explosion.ValueRO.explosionDistance;
                        float baseExplosionForce = explosion.ValueRO.explosionForce;
                    
                        if (dist > baseExplosionDistance) continue;
                        var falloff = 1 - (dist / baseExplosionDistance);
                        float3 explosionForce = normalizedVec * falloff * baseExplosionForce * deltaTime;
                    
                        boidVel.ValueRW.Value += explosionForce;
                    }
                } 
            }

            if (explosion.ValueRO.timeLived >= explosion.ValueRO.lifeExpetancy)
            {
                ecb.DestroyEntity(entity);
            }
        }
        
        ecb.Playback(state.EntityManager);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
