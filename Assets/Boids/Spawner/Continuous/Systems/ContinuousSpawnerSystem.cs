using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[BurstCompile]
public partial struct ContinuousSpawnerSystem : ISystem
{
    private Random random;
    
    public void OnCreate(ref SystemState state)
    {
        random = new Random((uint)System.DateTime.Now.Ticks + 1);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (spawner, transform) in 
                 SystemAPI.Query<RefRW<ContinuousSpawnerComponent>, RefRO<LocalTransform>>())
        {
            if (spawner.ValueRO.nextSpawnTime < SystemAPI.Time.ElapsedTime)
            { 
                // The entity is created instantly.
                Entity newEntity = state.EntityManager.Instantiate(spawner.ValueRO.prefab);
                
                var newTransform = SystemAPI.GetComponentRW<LocalTransform>(newEntity);
                newTransform.ValueRW.Position = transform.ValueRO.Position;

                // Set unique velocity
                var newVelocity = SystemAPI.GetComponentRW<Velocity>(newEntity);
                newVelocity.ValueRW.Value = random.NextFloat3Direction() * 5f;

                // Update the timer on the spawner component (this is not a structural change)
                spawner.ValueRW.nextSpawnTime = (float)SystemAPI.Time.ElapsedTime + spawner.ValueRO.spawnRate;
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}