using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

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
        var deadBirds = birdsQuery.ToComponentDataArray<BoidTag>(Allocator.TempJob);

        if (birds.Length == 0)
        {
            return;
        }
        
        foreach (var (turret, transform) in
                 SystemAPI.Query<RefRW<TurretComponent>, RefRW<LocalTransform>>())
        {
            var selectionIndex = 0;
            var shortestDistance = float.MaxValue;
            for (int i = 0; i < birds.Length; i++)
            {
                if (deadBirds[i].dead) continue;
                if (math.length(birds[i].Position - transform.ValueRO.Position) < shortestDistance)
                {
                    selectionIndex = i;
                    shortestDistance = math.length(birds[i].Position - transform.ValueRO.Position);

                }
            }

            var targetBirdPos = birds[selectionIndex].Position;
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
