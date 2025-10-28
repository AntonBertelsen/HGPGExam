using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial struct TurretSystem : ISystem
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
                 SystemAPI.Query<RefRW<TurretComponent>, RefRW<LocalTransform>>())
        {
                
            if (birds.Length <= 0) {
                break;
            }
            
            var targetBirdPos = birds[0].Position;
            var direction = targetBirdPos - transform.ValueRO.Position;
            direction.y = 0;
            transform.ValueRW.Rotation = Quaternion.LookRotation(direction);
            turret.ValueRW.targetingDirection = direction;

        }
        
        birds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
