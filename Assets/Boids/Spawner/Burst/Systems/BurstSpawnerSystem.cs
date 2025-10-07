using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[BurstCompile]
public partial struct BurstSpawnerSystem : ISystem
{
    
    private Random random;
    
    public void OnCreate(ref SystemState state)
    {
        random = new Random((uint)System.DateTime.Now.Ticks + 1);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // The query now iterates over all entities that have a spawner component and a transform.
        // We use RefRO (Read-Only) because we are not changing the spawner's data.
        foreach (var (spawner, transform) in 
            SystemAPI.Query<RefRO<BurstSpawnerComponent>, RefRO<LocalTransform>>())
        {
            // This is the key to high-performance burst spawning.
            // It tells the EntityManager to create all entities in one single operation.
            var newBoids = state.EntityManager.Instantiate(spawner.ValueRO.prefab, spawner.ValueRO.count, Allocator.Temp);

            // Now we loop through the newly created entities to set their unique properties.
            foreach (var entity in newBoids)
            {
                // Set the unique position, using the spawner's position as the center
                var newTransform = SystemAPI.GetComponentRW<LocalTransform>(entity);
                newTransform.ValueRW.Position = transform.ValueRO.Position;

                // Set unique velocity
                var newVelocity = SystemAPI.GetComponentRW<Velocity>(entity);
                newVelocity.ValueRW.Value = random.NextFloat3Direction() * 5f;
            }

            // The NativeArray created by Instantiate must be disposed.
            newBoids.Dispose();
            // This system should only run once.
            state.Enabled = false;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}