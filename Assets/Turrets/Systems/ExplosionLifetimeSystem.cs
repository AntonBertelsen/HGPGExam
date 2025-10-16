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


        foreach (var (explosion, transform, entity) in
                 SystemAPI.Query<RefRW<ExplosionComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {

            explosion.ValueRW.timeLived += SystemAPI.Time.DeltaTime;
            transform.ValueRW.Scale = 1.0f * explosion.ValueRO.timeLived * 10;
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
