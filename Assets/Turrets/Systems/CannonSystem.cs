using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial struct CannonSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
        
        
        var birdsQuery = SystemAPI.QueryBuilder().WithAll<BoidTag,LocalTransform>().Build();
        var birds = birdsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        foreach (var (turret, transform) in
                 SystemAPI.Query<RefRW<CannonComponent>, RefRW<LocalTransform>>())
        {
                

            
            var targetBirdPos = birds[0].Position;
            var direction = transform.ValueRO.Position - targetBirdPos;
            
            var lookRotation = Quaternion.LookRotation(direction);
            
            Vector3 euler = lookRotation.eulerAngles;
            euler.y = 90;
            euler.z = 90;

            transform.ValueRW.Rotation = Quaternion.Euler(euler);

            
            turret.ValueRW.lastFireTime += SystemAPI.Time.DeltaTime;
            if (turret.ValueRO.lastFireTime >= turret.ValueRO.fireRate)
            {
                
                turret.ValueRW.lastFireTime = 0;
                Entity newEntity = state.EntityManager.Instantiate(turret.ValueRO.bullet);

                var newTransform = SystemAPI.GetComponentRW<LocalTransform>(newEntity);
                newTransform.ValueRW.Position = transform.ValueRO.Position;
                newTransform.ValueRW.Rotation = transform.ValueRW.Rotation;

                var newVelocity = SystemAPI.GetComponentRW<Velocity>(newEntity);
                newVelocity.ValueRW.Value = new float3(1, 1, 1);
            }
        }
        
        birds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
