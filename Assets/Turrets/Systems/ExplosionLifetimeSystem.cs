using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var gridData = SystemAPI.GetSingletonRW<SpatialGridData>();


        foreach (var (explosion, transform, entity) in
                 SystemAPI.Query<RefRW<ExplosionComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {

            explosion.ValueRW.timeLived += SystemAPI.Time.DeltaTime;
            //transform.ValueRW.Scale = explosion.ValueRO.timeLived * 2;

            if (!explosion.ValueRO.hasExploded)
            {
                explosion.ValueRW.hasExploded = true;
                foreach (var (boidTrans, boidVel, boidTag) in SystemAPI
                             .Query<RefRW<LocalTransform>, RefRW<Velocity>, RefRW<BoidTag>>().WithAll<BoidTag>())
                {
                    var direction = boidTrans.ValueRO.Position - transform.ValueRO.Position;
                    var dist = math.length(direction);
                    
                    if (dist < transform.ValueRO.Scale*2)
                    {
                        boidTag.ValueRW.dead = true;
                    }

                    var normalizedVec = direction/dist;

                    if (dist > 10) continue;
                    var falloff = 1 - (dist / 40);
                    float3 explosionForce = normalizedVec * falloff * 40;
                    
                    boidVel.ValueRW.Value += explosionForce;
                    
                } 
            }

            if (explosion.ValueRO.timeLived >= explosion.ValueRO.lifeExpetancy)
            {
                ecb.DestroyEntity(entity);
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
