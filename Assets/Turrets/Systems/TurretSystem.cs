using System;
using System.Linq;
using System.Security.Cryptography;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using Random = System.Random;

[UpdateAfter(typeof(SpatialHashingSystem))]

partial struct TurretSystem : ISystem
{
    private int frameCount;
    private Unity.Mathematics.Random _random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _random = new Unity.Mathematics.Random(42);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var birdsQuery = SystemAPI.QueryBuilder().WithAll<BoidTag, LocalTransform>().Build();
        var birds = birdsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var gridData = SystemAPI.GetSingletonRW<SpatialGridData>();


        if (birds.Length == 0)
        {
            return;
        }



        foreach (var (turret, transform, toWorld, turretEntity) in
                 SystemAPI.Query<RefRW<TurretComponent>, RefRW<LocalTransform>, RefRW<LocalToWorld>>()
                     .WithEntityAccess())
        {
            turret.ValueRW.frameCounter += _random.NextInt(1, 5);
                // TARGETING //
                if (turret.ValueRO.frameCounter % 60 == 0)
                {
                    if (turret.ValueRW.frameCounter > 100000)
                    {
                        turret.ValueRW.frameCounter = 0;
                    }

                    var keyCounts = new NativeHashMap<int, int>(10, Allocator.Temp);
                var keyCountsUR = new NativeHashMap<int, int>(10, Allocator.Temp);
                var keyCountsUL = new NativeHashMap<int, int>(10, Allocator.Temp);
                var keyCountsDL = new NativeHashMap<int, int>(10, Allocator.Temp);
                var keyCountsDR = new NativeHashMap<int, int>(10, Allocator.Temp);
                var countBase = 0;
                var countUR = 0;
                var countUL = 0;
                var countDL = 0;
                var countDR = 0;


                var keyBase = 0;
                var keyUR = 0;
                var keyUL = 0;
                var keyDL = 0;
                var keyDR = 0;

                var keyValues = gridData.ValueRO.CellMap.GetKeyValueArrays(Allocator.Temp);
                for (int i = 0; i < keyValues.Keys.Length; i++)
                {
                    int key = keyValues.Keys[i];

                    var region = gridData.ValueRO.CellMap.TryGetFirstValue(key, out var value, out var ite);
                    var birdPos = birds[value].Position;
                    var dir = birdPos - transform.ValueRO.Position;

                    if (math.length(dir) > turret.ValueRO.viewRadius) continue;
                    //Add to main platform hashmap
                    if (keyCounts.TryGetValue(key, out int count))
                    {
                        keyCounts[key] = count + 1;
                        if (count + 1 > countBase)
                        {
                            countBase = count + 1;
                            keyBase = key;
                        }
                    }
                    else
                    {
                        keyCounts.Add(key, 1);
                        if (countBase == 0)
                        {
                            countBase = 1;
                            keyBase = key;
                        }
                    }

                    //Add to turret hashmaps
                    if (dir.z > 0 && dir.y > 0)
                    {
                        if (keyCountsUL.TryGetValue(key, out int count_))
                        {
                            keyCountsUL[key] = count_ + 1;
                            if (count_ + 1 > countUL)
                            {
                                countUL = count_ + 1;
                                keyUL = key;
                            }
                        }
                        else
                        {
                            keyCountsUL.Add(key, 1);
                            if (countUL == 0)
                            {
                                countUL = 1;
                                keyUL = key;
                            }
                        }
                    }
                    else if (dir.z < 0 && dir.y > 0)
                    {
                        if (keyCountsUR.TryGetValue(key, out int count_))
                        {
                            keyCountsUR[key] = count_ + 1;
                            if (count_ + 1 > countUR)
                            {
                                countUR = count_ + 1;
                                keyUR = key;
                            }
                        }
                        else
                        {
                            keyCountsUR.Add(key, 1);
                            if (countUR == 0)
                            {
                                countUR = 1;
                                keyUR = key;
                            }
                        }
                    }
                    else if (dir.z < 0 && dir.y < 0)
                    {
                        if (keyCountsDL.TryGetValue(key, out int count_))
                        {
                            keyCountsDL[key] = count_ + 1;
                            if (count_ + 1 > countDL)
                            {
                                countDL = count_ + 1;
                                keyDL = key;
                            }
                        }
                        else
                        {
                            keyCountsDL.Add(key, 1);
                            if (countDL == 0)
                            {
                                countDL = 1;
                                keyDL = key;
                            }
                        }
                    }
                    else if (dir.z > 0 && dir.y < 0)
                    {
                        if (keyCountsDR.TryGetValue(key, out int count_))
                        {
                            keyCountsDR[key] = count_ + 1;
                            if (count_ + 1 > countDR)
                            {
                                countDR = count_ + 1;
                                keyDR = key;
                            }
                        }
                        else
                        {
                            keyCountsDR.Add(key, 1);
                            if (countDR == 0)
                            {
                                countDR = 1;
                                keyDR = key;
                            }
                        }
                    }
                }

            

             //Target cluster with most birds for main platform
             turret.ValueRW.target_center = countBase != 0 ? AquireTargetFromList(gridData, birds, keyBase) : new float3();
            //Target cluster with most birds for main platform

            turret.ValueRW.target_UR = countUL != 0 ? AquireTargetFromList(gridData, birds, keyUL) : float3.zero;
            turret.ValueRW.target_UL = countUR != 0 ? AquireTargetFromList(gridData, birds, keyUR) : float3.zero;
            turret.ValueRW.target_DL = countDL != 0 ? AquireTargetFromList(gridData, birds, keyDL) : float3.zero;
            turret.ValueRW.target_DR = countDR != 0 ? AquireTargetFromList(gridData, birds, keyDR) : float3.zero;
            
            // TARGETING //
            keyCounts.Dispose();
            keyCountsUR.Dispose();
            keyCountsUL.Dispose();
            keyCountsDL.Dispose();
            keyCountsDR.Dispose();

            keyValues.Dispose(); 
                }
            var urHasMoved = false;
            var ulHasMoved = false;
            var dlHasMoved = false;
            var drHasMoved = false;
            
            if (!turret.ValueRW.target_UL.Equals(float3.zero))
                (turret.ValueRW.turret_UL_targetingDirection, turret.ValueRW.cannon_UL_targetingDirection, ulHasMoved) = 
                    MoveTurret(ref state, turret.ValueRO.turret_UL_targetingDirection, turret.ValueRO.cannon_UL_targetingDirection,turret.ValueRW.turret_UL, turret.ValueRW.cannon_UL, turret.ValueRW.target_UL, state.EntityManager.GetComponentData<LocalTransform>(turretEntity).Rotation, true, false);

            if (!turret.ValueRW.target_UR.Equals(float3.zero))
                (turret.ValueRW.turret_UR_targetingDirection, turret.ValueRW.cannon_UR_targetingDirection, urHasMoved) = 
                    MoveTurret(ref state, turret.ValueRO.turret_UR_targetingDirection, turret.ValueRO.cannon_UR_targetingDirection, turret.ValueRW.turret_UR, turret.ValueRW.cannon_UR, turret.ValueRW.target_UR, state.EntityManager.GetComponentData<LocalTransform>(turretEntity).Rotation,true, true);

            if (!turret.ValueRW.target_DL.Equals(float3.zero))
                (turret.ValueRW.turret_DL_targetingDirection, turret.ValueRW.cannon_DL_targetingDirection, dlHasMoved) = 
                    MoveTurret(ref state, turret.ValueRO.turret_DL_targetingDirection, turret.ValueRO.cannon_DL_targetingDirection, turret.ValueRW.turret_DL, turret.ValueRW.cannon_DL, turret.ValueRW.target_DL, state.EntityManager.GetComponentData<LocalTransform>(turretEntity).Rotation, false, false);

            if (!turret.ValueRW.target_DR.Equals(float3.zero))
                (turret.ValueRW.turret_DR_targetingDirection, turret.ValueRW.cannon_DR_targetingDirection, drHasMoved) = 
                    MoveTurret(ref state, turret.ValueRO.turret_DR_targetingDirection, turret.ValueRO.cannon_DR_targetingDirection, turret.ValueRW.turret_DR, turret.ValueRW.cannon_DR, turret.ValueRW.target_DR, state.EntityManager.GetComponentData<LocalTransform>(turretEntity).Rotation,false, true);

            turret.ValueRW.lastFireTime += SystemAPI.Time.DeltaTime;
            if (turret.ValueRO.lastFireTime >= turret.ValueRO.fireRate)
            {
                turret.ValueRW.lastFireTime = 0;
                
                if (!turret.ValueRW.target_UL.Equals(float3.zero) && urHasMoved)
                    FireCannon(ref state, turret.ValueRW.cannon_UR, turret.ValueRO.bullet, turret.ValueRO.cannon_UR_targetingDirection);
                if (!turret.ValueRW.target_UR.Equals(float3.zero) && ulHasMoved)
                    FireCannon(ref state, turret.ValueRW.cannon_UL, turret.ValueRO.bullet, turret.ValueRO.cannon_UL_targetingDirection);
                if (!turret.ValueRW.target_DL.Equals(float3.zero) && dlHasMoved)
                    FireCannon(ref state, turret.ValueRW.cannon_DL, turret.ValueRO.bullet, turret.ValueRO.cannon_DL_targetingDirection);
                if (!turret.ValueRW.target_DR.Equals(float3.zero) && drHasMoved)
                    FireCannon(ref state, turret.ValueRW.cannon_DR, turret.ValueRO.bullet, turret.ValueRO.cannon_DR_targetingDirection);
            }
            
            // MOVE TURRET BASE //
            if (!turret.ValueRW.target_center.Equals(float3.zero))
            {
                var direction = turret.ValueRW.target_center - transform.ValueRO.Position;
                direction.y = 0;
                transform.ValueRW.Rotation = Quaternion.RotateTowards(Quaternion.LookRotation(turret.ValueRO.targetingDirection), Quaternion.LookRotation(direction), 0.1f);
                float3 forward = math.mul(transform.ValueRW.Rotation , new float3(0, 0, 1));
                turret.ValueRW.targetingDirection = forward;
            }


            // MOVE TURRET BASE //
            

            

        }
        
        birds.Dispose();
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
    [BurstCompile]
    public (float3,float3,bool) MoveTurret(ref SystemState state, float3 turretPreviousTargetingDirection, float3 cannonPreviousTargetingDirection, Entity turret, Entity cannon, float3 target, Quaternion motherTurretRotation, bool up, bool right)
    {
        var entityManager = state.EntityManager;
        var turretTransform = state.EntityManager.GetComponentData<LocalTransform>(turret);
        var cannonTransform = state.EntityManager.GetComponentData<LocalTransform>(cannon);
        var turretTransformWorld = state.EntityManager.GetComponentData<LocalToWorld>(turret);
        var cannonTransformWorld = state.EntityManager.GetComponentData<LocalToWorld>(cannon);

        var returnValue = (new float3(0, 0, 1), new float3(0, 0, 1), false);
        
        var direction = target - turretTransformWorld.Position;
            
        var lookRotation = Quaternion.RotateTowards(Quaternion.LookRotation(turretPreviousTargetingDirection), Quaternion.LookRotation(direction), 1f);
        float magnitude = math.length(direction);
        Vector3 resultingDirection = lookRotation * Vector3.forward * magnitude;

        Vector3 euler = lookRotation.eulerAngles;
        euler.x = 0;
        euler.y -= motherTurretRotation.eulerAngles.y;
        euler.z = 0;
        if(!up && !right || up && right) euler.y *= -1;
        
        
        var tempRotation = Quaternion.Euler(0, euler.y, 0);
        
       if (tempRotation.x > 30 || tempRotation.x < -180)
       {
            //turretTransform.Rotation = turretTransform.Rotation;
            returnValue.Item1 = turretPreviousTargetingDirection;
        } else
       {
            turretTransform.Rotation = tempRotation;
            returnValue.Item1 = resultingDirection;
            entityManager.SetComponentData(turret, turretTransform);
            returnValue.Item3 = true;
       }
        
        var cannonDirection = target - cannonTransformWorld.Position;
        float originalMagnitude = math.length(cannonDirection);
        var cannonLookRotation = Quaternion.RotateTowards(Quaternion.LookRotation(cannonPreviousTargetingDirection), Quaternion.LookRotation(cannonDirection), 1f);
        Vector3 cannonResultingDirection = cannonLookRotation * Vector3.forward * originalMagnitude;

        Vector3 cannonEuler = cannonLookRotation.eulerAngles;
        cannonEuler.y = euler.y;
        cannonEuler.z = 0;
        if (up == false)
        {
            cannonEuler.x *= -1;
        }
            
        var cannonTempRotation = Quaternion.Euler(cannonEuler);
        
        if (cannonTempRotation.x > 30 || cannonTempRotation.x < -180)
        {
            //cannonTransform.Rotation = cannonTransform.Rotation;
            returnValue.Item2 = cannonPreviousTargetingDirection;
        } else
        {
            cannonTransform.Rotation = cannonTempRotation;
            returnValue.Item2 = cannonResultingDirection;
            entityManager.SetComponentData(cannon, cannonTransform);
        }

        return returnValue;
    }
    [BurstCompile]
    public void FireCannon(ref SystemState state, Entity cannon, Entity bullet, float3 direction)
    {
            var cannonTransform = state.EntityManager.GetComponentData<LocalToWorld>(cannon);

            Entity newEntity = state.EntityManager.Instantiate(bullet);

            var newTransform = SystemAPI.GetComponentRW<LocalTransform>(newEntity);
            var bulletComponent = SystemAPI.GetComponentRW<BulletComponent>(newEntity);
            bulletComponent.ValueRW.timeToExplode = math.length(direction) / 50;
                
            float3 dir = math.mul(cannonTransform.Rotation, new float3(0, 0, 1));

            newTransform.ValueRW.Position = cannonTransform.Position;
            newTransform.ValueRW.Position += dir * 1.5f;
            newTransform.ValueRW.Rotation = cannonTransform.Rotation;
            
            var newVelocity = SystemAPI.GetComponentRW<BulletVelocity>(newEntity);
            newVelocity.ValueRW.Value = direction / math.length(direction) * 40;
    }
    [BurstCompile]
    public float3 AquireTargetFromList(RefRW<SpatialGridData> gridData, NativeArray<LocalTransform> birds, int key)
    {
        gridData.ValueRO.CellMap.TryGetFirstValue(key, out var firsValue, out var it);
        return birds[firsValue].Position;
        
    }
}
