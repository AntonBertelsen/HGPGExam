using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial struct TurretCannonSystem : ISystem
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
                 SystemAPI.Query<RefRW<TurretCannonComponent>, RefRW<LocalTransform>>())
        {
                

            
            var targetBirdPos = birds[0].Position;
            var direction = transform.ValueRO.Position - targetBirdPos;
            
            var lookRotation = Quaternion.LookRotation(direction);
            
            Vector3 euler = lookRotation.eulerAngles;
            if (!turret.ValueRO.isRight) euler.z = turret.ValueRO.isDown ? -90f : 90f;
            if (turret.ValueRO.isRight) euler.z = turret.ValueRO.isDown ? 90f : -90f;
            euler.y = 0;

            transform.ValueRW.Rotation = Quaternion.Euler(euler);

        }
        
        birds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
