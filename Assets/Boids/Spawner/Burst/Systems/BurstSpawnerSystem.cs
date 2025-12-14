using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using MeshCollider = UnityEngine.MeshCollider;
using Random = Unity.Mathematics.Random;

[BurstCompile]
public partial struct BurstSpawnerSystem : ISystem
{
    
    private Random random;
    
    public void OnCreate(ref SystemState state)
    {
        random = new Random((uint)System.DateTime.Now.Ticks + 1);
        state.RequireForUpdate<BurstSpawnerComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        foreach (var (spawner, spawnerTransform, entity) in 
                 SystemAPI.Query<RefRO<BurstSpawnerComponent>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            // We spawn new boids
            var newBoids = state.EntityManager.Instantiate(spawner.ValueRO.prefab, spawner.ValueRO.count, Allocator.Temp);

            // Iterate over every new boid and update its settings
            foreach (var boid in newBoids)
            {
                var newTransform = SystemAPI.GetComponentRW<LocalTransform>(boid);
                newTransform.ValueRW.Position = spawnerTransform.ValueRO.Position;

                var newVelocity = SystemAPI.GetComponentRW<Velocity>(boid);
                newVelocity.ValueRW.Value = random.NextFloat3Direction() * 5f;
                var mass = state.EntityManager.GetComponentData<PhysicsMass>(boid);
                mass.InverseMass = 0;
                state.EntityManager.SetComponentData(boid, mass);
                // We set the energy level in the lander component to some randomized value. This is to prevent an issue
                // we had where every bird would run out of energy at the same time and try to land all at once
                var lander = SystemAPI.GetComponentRW<Lander>(boid);
                lander.ValueRW.Energy = random.NextFloat(20f, 95f);
                lander.ValueRW.State = LanderState.Flying;
            }
            newBoids.Dispose();
            
            // Destroy the spawner entity so it only triggers once
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}