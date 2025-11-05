using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SocialPlatforms;

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
        var deadBirds = birdsQuery.ToComponentDataArray<BoidTag>(Allocator.TempJob);
        var birds = birdsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        foreach (var (turret, transform, localToWorldTransform, entity) in
                 SystemAPI.Query<RefRW<CannonComponent>, RefRW<LocalTransform>, RefRW<LocalToWorld>>().WithEntityAccess())
        {
                
            if (birds.Length <= 0) {
                break;
            }

            var selectionIndex = 0;
            var shortestDistance = float.MaxValue;
            for (int i = 0; i < birds.Length; i++)
            {
                if (deadBirds[i].dead) continue;
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
            euler.y = 0;
            euler.z = 0;
            if (turret.ValueRO.isDown)
            {
                euler.x *= -1;
            }

            var tempRotation = Quaternion.Euler(euler);

            if (tempRotation.x > 30 || tempRotation.x < -180)
            {
                transform.ValueRW.Rotation = transform.ValueRO.Rotation;
            } else
            {
                transform.ValueRW.Rotation = tempRotation;
                turret.ValueRW.targetingDirection = direction;
            }
            
            turret.ValueRW.lastFireTime += SystemAPI.Time.DeltaTime;
            if (turret.ValueRO.lastFireTime >= turret.ValueRO.fireRate)
            {
                
                turret.ValueRW.lastFireTime = 0;
                Entity newEntity = state.EntityManager.Instantiate(turret.ValueRO.bullet);

                var newTransform = SystemAPI.GetComponentRW<LocalTransform>(newEntity);
                var bulletComponent = SystemAPI.GetComponentRW<BulletComponent>(newEntity);
                bulletComponent.ValueRW.timeToExplode = math.length(direction) / 10;
                
                float3 dir = math.mul(localToWorldTransform.ValueRO.Rotation, new float3(0, 0, 1));

                newTransform.ValueRW.Position = localToWorldTransform.ValueRO.Position;
                newTransform.ValueRW.Position += dir  * 1.5f;
                newTransform.ValueRW.Rotation = transform.ValueRW.Rotation;
                

                var newVelocity = SystemAPI.GetComponentRW<BulletVelocity>(newEntity);
                newVelocity.ValueRW.Value = direction / math.length(direction) * 40;
            }
        }
        
        birds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
