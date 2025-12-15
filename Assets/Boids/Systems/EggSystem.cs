using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
[BurstCompile]
public partial struct EggSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();
        state.RequireForUpdate<BoidSpawnerReference>(); 
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        var spawnerRef = SystemAPI.GetSingleton<BoidSpawnerReference>();
        var eggLookup = SystemAPI.GetComponentLookup<EggComponent>(true);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var simulation = SystemAPI.GetSingleton<SimulationSingleton>();

        var job = new EggCollisionJob
        {
            ECB = ecb,
            EggLookup = eggLookup,
            TransformLookup = transformLookup,
            SpawnerCommandPrefab = spawnerRef.BurstSpawnerPrefab
        };

        state.Dependency = job.Schedule(simulation, state.Dependency);
        state.Dependency.Complete(); 
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    struct EggCollisionJob : ICollisionEventsJob
    {
        public EntityCommandBuffer ECB;
        [ReadOnly] public ComponentLookup<EggComponent> EggLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        public Entity SpawnerCommandPrefab;

        public void Execute(CollisionEvent collisionEvent)
        {
            Entity entityA = collisionEvent.EntityA;
            Entity entityB = collisionEvent.EntityB;

            if (EggLookup.HasComponent(entityA)) Hatch(entityA);
            if (EggLookup.HasComponent(entityB)) Hatch(entityB);
        }

        private void Hatch(Entity eggEntity)
        {
            if (!TransformLookup.HasComponent(eggEntity)) return;

            EggComponent eggData = EggLookup[eggEntity];
            float3 position = TransformLookup[eggEntity].Position;

            if (eggData.BirdPrefab != Entity.Null)
            {
                Entity spawner = ECB.Instantiate(SpawnerCommandPrefab);
                ECB.SetComponent(spawner, LocalTransform.FromPosition(position));
                
                ECB.SetComponent(spawner, new BurstSpawnerComponent
                {
                    prefab = eggData.BirdPrefab,
                    count = eggData.SpawnCount,
                    InitialSpeed = eggData.ExplosionSpeed
                });
            }

            ECB.DestroyEntity(eggEntity);
        }
    }
}