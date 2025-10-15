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
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        foreach (var (bullet, transform) in
                 SystemAPI.Query<RefRW<BulletComponent>, RefRW<LocalTransform>>())
        {

            bullet.ValueRW.timeLived += SystemAPI.Time.DeltaTime;
            if (bullet.ValueRO.timeLived >= bullet.ValueRO.timeToExplode)
            {
                Entity newEntity = state.EntityManager.Instantiate(turret.ValueRO.bullet);

                var newTransform = SystemAPI.GetComponentRW<LocalTransform>(newEntity);
                newTransform.ValueRW.Position = transform.ValueRO.Position;
                newTransform.ValueRW.Rotation = transform.ValueRW.Rotation;

                var newVelocity = SystemAPI.GetComponentRW<Velocity>(newEntity);
                newVelocity.ValueRW.Value = new float3(1, 1, 1);
            }
        }
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
