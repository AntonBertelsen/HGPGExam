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
        var birds = birdsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        foreach (var (turret, transform, localToWorldTransform, entity) in
                 SystemAPI.Query<RefRW<CannonComponent>, RefRW<LocalTransform>, RefRW<LocalToWorld>>().WithEntityAccess())
        {
                
            if (birds.Length <= 0) {
                break;
            }

            var targetBirdPos = birds[0].Position;
            var direction = targetBirdPos - localToWorldTransform.ValueRO.Position;
            
            var lookRotation = Quaternion.LookRotation(direction);
            
            Vector3 euler = lookRotation.eulerAngles;
            euler.y = 0;
            euler.z = 0;
          
            var tempRotation = Quaternion.Euler(euler);

            if (tempRotation.x > 0 || tempRotation.x < -180 && turret.ValueRO.isDown)
            {
                transform.ValueRW.Rotation = transform.ValueRO.Rotation;
            } else if (turret.ValueRO.isDown)
            {
                transform.ValueRW.Rotation = tempRotation;
                turret.ValueRW.targetingDirection = direction;
            }

            if (tempRotation.x < 0 || tempRotation.x > 180 && !turret.ValueRO.isDown)
            {
                transform.ValueRW.Rotation = transform.ValueRO.Rotation;
            } else if (!turret.ValueRO.isDown)
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
                
                float3 dir = math.mul(localToWorldTransform.ValueRO.Rotation, new float3(0, 0, 1));

                newTransform.ValueRW.Position = localToWorldTransform.ValueRO.Position;
                newTransform.ValueRW.Position += dir  * 1.5f;
                newTransform.ValueRW.Rotation = localToWorldTransform.ValueRW.Rotation;
                

                var newVelocity = SystemAPI.GetComponentRW<Velocity>(newEntity);
                newVelocity.ValueRW.Value = new float3(3,3,3);
            }
        }
        
        birds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
