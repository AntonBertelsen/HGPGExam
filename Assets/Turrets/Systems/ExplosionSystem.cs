using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial struct ExplosionSystem : ISystem
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


        foreach (var (bullet, transform, bullets) in
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
                
        
                ecb.DestroyEntity(bullets);
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
