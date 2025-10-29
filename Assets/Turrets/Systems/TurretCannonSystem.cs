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

        foreach (var (turret, localTransform, localToWorldTransform) in
                 SystemAPI.Query<RefRW<TurretCannonComponent>, RefRW<LocalTransform>, RefRW<LocalToWorld>>())
        {
                
            if (birds.Length <= 0) {
                break;
            }
            
            var selectionIndex = 0;
            var shortestDistance = float.MaxValue;
            for (int i = 0; i < birds.Length; i++)
            {
                if (math.length(birds[i].Position - localToWorldTransform.ValueRO.Position) < shortestDistance)
                {
                    selectionIndex = i;
                    shortestDistance = math.length(birds[i].Position - localToWorldTransform.ValueRO.Position);
                }
            }

            var targetBirdPos = birds[selectionIndex].Position;
            var direction = targetBirdPos - localToWorldTransform.ValueRO.Position;
            
            var lookRotation = Quaternion.LookRotation(direction);
            
            Vector3 euler = lookRotation.eulerAngles;
            if (!turret.ValueRO.isRight) euler.z = turret.ValueRO.isDown ? -90f : 90f;
            if (turret.ValueRO.isRight) euler.z = turret.ValueRO.isDown ? 90f : -90f;
            euler.y = 0;

            
            var tempRotation = Quaternion.Euler(euler);


            if (tempRotation.x > 10 || tempRotation.x < -180)
            {
                localTransform.ValueRW.Rotation = localTransform.ValueRO.Rotation;
            } else
            {
                localTransform.ValueRW.Rotation = tempRotation;
                turret.ValueRW.targetingDirection = direction;
            }




        }
        
        birds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
