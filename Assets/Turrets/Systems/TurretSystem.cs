using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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

        if (birds.Length == 0)
        {
            return;
        }
        
        foreach (var (turret, transform) in
                 SystemAPI.Query<RefRW<TurretComponent>, RefRW<LocalTransform>>())
        {
            
            var targetBirdPos = birds[0].Position;
            transform.ValueRW.Rotation = Quaternion.LookRotation(transform.ValueRO.Position - targetBirdPos);
            
        }
        
        birds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
