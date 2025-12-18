using TMPro;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

public class UIGameInteraction : MonoBehaviour
{
    public Transform spawnPoint;
    public Camera mainCam;
    public TMP_InputField spawnAmountInput;
    
    public float throwForce = 40f;
    
    private Entity _activeContinuousSpawner = Entity.Null;
    
    [Header("Input Settings")]
    public KeyCode eggSpawnKey = KeyCode.Space;
    public KeyCode birdSpawnKey = KeyCode.B;
    public KeyCode throwCubeKey = KeyCode.C;
    public KeyCode placeTurretKey = KeyCode.T;
    
    [Header("Camera Control")]
    public OrbitCameraRig cameraRig;
    public KeyCode followCamKey = KeyCode.F;
    
    private void SpawnBirds(int spawnCount)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        Entity spawnerPrefab = em.CreateEntityQuery(typeof(BoidSpawnerReference))
            .GetSingleton<BoidSpawnerReference>()
            .BurstSpawnerPrefab;
        
        Entity newSpawner = em.Instantiate(spawnerPrefab);
        
        var spawnerData = em.GetComponentData<BurstSpawnerComponent>(newSpawner);
        spawnerData.count = spawnCount;
        em.SetComponentData(newSpawner, spawnerData);
        
        float3 position = spawnPoint != null ? spawnPoint.position : float3.zero;
        em.SetComponentData(newSpawner, LocalTransform.FromPosition(position));
    }
    
    public void SpawnBirdsFromUI()
    {
        int count = 1000;
        
        if (spawnAmountInput != null && !string.IsNullOrEmpty(spawnAmountInput.text))
        {
            if (int.TryParse(spawnAmountInput.text, out int parsed))
            {
                count = parsed;
            }
        }

        SpawnBirds(count);
    }
    
    public void ClearSimulation()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(CleanupTag));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        em.DestroyEntity(entities);

        entities.Dispose();
    }
    
    public void ResetSettings()
    {
        BoidSettingsBridge boidSettings = FindFirstObjectByType<BoidSettingsBridge>();
        boidSettings.ResetToDefaults();
    }
    
    public void ThrowEgg()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        var query = em.CreateEntityQuery(typeof(BoidSpawnerReference));
        if (!query.HasSingleton<BoidSpawnerReference>()) return;
        
        var config = query.GetSingleton<BoidSpawnerReference>();

        Entity egg = em.Instantiate(config.EggPrefab);

        float3 startPos = mainCam.transform.position + mainCam.transform.forward * 2f;
        em.SetComponentData(egg, LocalTransform.FromPosition(startPos));

        em.SetComponentData(egg, new PhysicsVelocity
        {
            Linear = mainCam.transform.forward * 40f,
            Angular = new float3(1, 0, 0)
        });
    }
    
    public void ThrowObstacle()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        var query = em.CreateEntityQuery(typeof(BoidSpawnerReference));
        if (!query.HasSingleton<BoidSpawnerReference>()) return;
        var config = query.GetSingleton<BoidSpawnerReference>();

        Entity cube = em.Instantiate(config.ObstaclePrefab);
        
        var t = em.GetComponentData<LocalTransform>(cube);
        t.Position = mainCam.transform.position + mainCam.transform.forward * 5f;
        em.SetComponentData(cube, t);

        em.SetComponentData(cube, new PhysicsVelocity
        {
            Linear = mainCam.transform.forward * 20f, 
            Angular = new float3(1, 2, 3) // Add spin to make it look dynamic
        });
    }
    
    private void SpawnTurretAtCursor()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        var query = em.CreateEntityQuery(typeof(BoidSpawnerReference));
        if (!query.HasSingleton<BoidSpawnerReference>()) return;
        var config = query.GetSingleton<BoidSpawnerReference>();

        var physicsQuery = em.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        if (physicsQuery.HasSingleton<PhysicsWorldSingleton>())
        {
            var physicsWorld = physicsQuery.GetSingleton<PhysicsWorldSingleton>();
            CollisionWorld collisionWorld = physicsWorld.CollisionWorld;

            UnityEngine.Ray cameraRay = mainCam.ScreenPointToRay(Input.mousePosition);
            
            RaycastInput input = new RaycastInput
            {
                Start = cameraRay.origin,
                End = cameraRay.origin + (cameraRay.direction * 1000f),
                Filter = CollisionFilter.Default
            };

            if (collisionWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
            {
                Entity turret = em.Instantiate(config.TurretPrefab);
                
                quaternion rotation = quaternion.LookRotationSafe(new float3(0, 0, 1), hit.SurfaceNormal);

                em.SetComponentData(turret, new LocalTransform
                {
                    Position = hit.Position,
                    Rotation = rotation,
                    Scale = 1f
                });
            }
        }
    }
    
    private void HandleContinuousSpawnInput()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (Input.GetKeyDown(birdSpawnKey))
        {
            var query = em.CreateEntityQuery(typeof(BoidSpawnerReference));
            if (query.HasSingleton<BoidSpawnerReference>())
            {
                var config = query.GetSingleton<BoidSpawnerReference>();
                _activeContinuousSpawner = em.Instantiate(config.ContinuousSpawnerPrefab);
            }
        }

        if (Input.GetKey(birdSpawnKey) && _activeContinuousSpawner != Entity.Null)
        {
            var physicsQuery = em.CreateEntityQuery(typeof(PhysicsWorldSingleton));
            if (physicsQuery.HasSingleton<PhysicsWorldSingleton>())
            {
                var physicsWorld = physicsQuery.GetSingleton<PhysicsWorldSingleton>();
                CollisionWorld collisionWorld = physicsWorld.CollisionWorld;

                UnityEngine.Ray cameraRay = mainCam.ScreenPointToRay(Input.mousePosition);

                float3 rayStart = cameraRay.origin;
                float3 rayEnd = cameraRay.origin + (cameraRay.direction * 1000f);

                RaycastInput input = new RaycastInput
                {
                    Start = rayStart,
                    End = rayEnd,
                    Filter = CollisionFilter.Default
                };

                if (collisionWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
                {
                    em.SetComponentData(_activeContinuousSpawner, LocalTransform.FromPosition(hit.Position));
                }
            }
        }

        if (Input.GetKeyUp(birdSpawnKey))
        {
            if (_activeContinuousSpawner != Entity.Null)
            {
                em.DestroyEntity(_activeContinuousSpawner);
                _activeContinuousSpawner = Entity.Null;
            }
        }
    }

    public void Update()
    {
        if (Input.GetKeyDown(eggSpawnKey))
        {
            ThrowEgg();
        }
        
        if (Input.GetKeyDown(throwCubeKey))
        {
            ThrowObstacle();
        }
        if (Input.GetKeyDown(placeTurretKey))
        {
            SpawnTurretAtCursor();
        }
        
        if (Input.GetKeyDown(followCamKey))
        {
            if (cameraRig != null)
            {
                cameraRig.ToggleFollowMode();
            }
        }

        HandleContinuousSpawnInput();
    }
}