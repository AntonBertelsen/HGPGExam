using System;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
[UpdateAfter(typeof(SpatialHashingSystem))]

partial struct TurretSystem : ISystem
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
        var deadBirds = birdsQuery.ToComponentDataArray<BoidTag>(Allocator.TempJob);
        var gridData = SystemAPI.GetSingletonRW<SpatialGridData>();
        
        if (birds.Length == 0)
        {
            return;
        }
        
        foreach (var (turret, transform, toWorld) in
                 SystemAPI.Query<RefRW<TurretComponent>, RefRW<LocalTransform>, RefRW<LocalToWorld>>())
        {

            
            // TARGETING //
            var keyCounts = new NativeHashMap<int, int>(10, Allocator.Temp);
            var keyCountsUR = new NativeHashMap<int, int>(10, Allocator.Temp);
            var keyCountsUL = new NativeHashMap<int, int>(10, Allocator.Temp);
            var keyCountsDL = new NativeHashMap<int, int>(10, Allocator.Temp);
            var keyCountsDR = new NativeHashMap<int, int>(10, Allocator.Temp);

            
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
                    keyCounts[key] = count + 1;
                else
                    keyCounts.Add(key, 1);
                //Add to turret hashmaps
                if (dir.z > 0 && dir.y > 0)
                {
                    if (keyCountsUR.TryGetValue(key, out int count_))
                        keyCountsUR[key] = count_ + 1;
                    else
                        keyCountsUR.Add(key, 1);
                }
                else if (dir.z < 0 && dir.y > 0)
                {
                    if (keyCountsUL.TryGetValue(key, out int count_))
                        keyCountsUL[key] = count_ + 1;
                    else
                        keyCountsUL.Add(key, 1);
                }
                else if (dir.z < 0 && dir.y < 0)
                {
                    if (keyCountsDL.TryGetValue(key, out int count_))
                        keyCountsDL[key] = count_ + 1;
                    else
                        keyCountsDL.Add(key, 1);
                }
                else if(dir.z > 0 && dir.y < 0) {
                    if (keyCountsDR.TryGetValue(key, out int count_))
                        keyCountsDR[key] = count_ + 1;
                    else
                        keyCountsDR.Add(key, 1);
                }
            }
            
            //Target cluster with most birds for main platform
            var targetBirdPos = AquireTargetFromList(gridData, birds, keyCounts);
            var direction = targetBirdPos - transform.ValueRO.Position;
            //Target cluster with most birds for main platform

            var targetBirdUL = AquireTargetFromList(gridData, birds, keyCountsUL);
            var targetBirdUR = AquireTargetFromList(gridData, birds, keyCountsUR);
            var targetBirdDL = AquireTargetFromList(gridData, birds, keyCountsDL);
            var targetBirdDR = AquireTargetFromList(gridData, birds, keyCountsDR);
            // TARGETING //

            if (!targetBirdUL.Equals(float3.zero))
                (turret.ValueRW.turret_UL_targetingDirection, turret.ValueRW.cannon_UL_targetingDirection) = 
                    MoveTurret(ref state, turret.ValueRO.turret_UL_targetingDirection, turret.ValueRO.cannon_UL_targetingDirection,turret.ValueRW.turret_UL, turret.ValueRW.cannon_UL, targetBirdUL, true, false);

            if (!targetBirdUR.Equals(float3.zero))
                (turret.ValueRW.turret_UR_targetingDirection, turret.ValueRW.cannon_UR_targetingDirection) = 
                    MoveTurret(ref state, turret.ValueRO.turret_UR_targetingDirection, turret.ValueRO.cannon_UR_targetingDirection, turret.ValueRW.turret_UR, turret.ValueRW.cannon_UR, targetBirdUR, true, true);

            if (!targetBirdDL.Equals(float3.zero))
                (turret.ValueRW.turret_DL_targetingDirection, turret.ValueRW.cannon_DL_targetingDirection) = 
                    MoveTurret(ref state, turret.ValueRO.turret_DL_targetingDirection, turret.ValueRO.cannon_DL_targetingDirection, turret.ValueRW.turret_DL, turret.ValueRW.cannon_DL, targetBirdDL, false, false);

            if (!targetBirdDR.Equals(float3.zero))
                (turret.ValueRW.turret_DR_targetingDirection, turret.ValueRW.cannon_DR_targetingDirection) = 
                    MoveTurret(ref state, turret.ValueRO.turret_DR_targetingDirection, turret.ValueRO.cannon_DR_targetingDirection, turret.ValueRW.turret_DR, turret.ValueRW.cannon_DR, targetBirdDR, false, true);

            turret.ValueRW.lastFireTime += SystemAPI.Time.DeltaTime;
            if (turret.ValueRO.lastFireTime >= turret.ValueRO.fireRate)
            {
                turret.ValueRW.lastFireTime = 0;
                
                if (!targetBirdUL.Equals(float3.zero))
                    FireCannon(ref state, turret.ValueRW.cannon_UR, turret.ValueRO.bullet, turret.ValueRO.cannon_UR_targetingDirection);
                if (!targetBirdUR.Equals(float3.zero))
                    FireCannon(ref state, turret.ValueRW.cannon_UL, turret.ValueRO.bullet, turret.ValueRO.cannon_UL_targetingDirection);
                if (!targetBirdDL.Equals(float3.zero))
                    FireCannon(ref state, turret.ValueRW.cannon_DL, turret.ValueRO.bullet, turret.ValueRO.cannon_DL_targetingDirection);
                if (!targetBirdDR.Equals(float3.zero))
                    FireCannon(ref state, turret.ValueRW.cannon_DR, turret.ValueRO.bullet, turret.ValueRO.cannon_DR_targetingDirection);
            }

            // MOVE TURRET BASE //
            direction.y = 0;
            transform.ValueRW.Rotation = Quaternion.RotateTowards(Quaternion.LookRotation(turret.ValueRO.targetingDirection), Quaternion.LookRotation(direction), 0.1f);
            float3 forward = math.mul(transform.ValueRW.Rotation , new float3(0, 0, 1));
            turret.ValueRW.targetingDirection = forward;
            // MOVE TURRET BASE //
            
            keyCounts.Dispose();
            keyCountsUR.Dispose();
            keyCountsUL.Dispose();
            keyCountsDL.Dispose();
            keyCountsDR.Dispose();

            keyValues.Dispose();
            

        }
        
        birds.Dispose();
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
    [BurstCompile]
    public (float3,float3) MoveTurret(ref SystemState state, float3 turretPreviousTargetingDirection, float3 cannonPreviousTargetingDirection, Entity turret, Entity cannon, float3 target, bool up, bool right)
    {
        var entityManager = state.EntityManager;
        var turretTransform = state.EntityManager.GetComponentData<LocalTransform>(turret);
        var cannonTransform = state.EntityManager.GetComponentData<LocalTransform>(cannon);
        var turretTransformWorld = state.EntityManager.GetComponentData<LocalToWorld>(turret);
        var cannonTransformWorld = state.EntityManager.GetComponentData<LocalToWorld>(cannon);

        var returnValue = (new float3(0, 0, 1), new float3(0, 0, 1));
        
        var direction = target - turretTransformWorld.Position;
            
        var lookRotation = Quaternion.RotateTowards(Quaternion.LookRotation(turretPreviousTargetingDirection), Quaternion.LookRotation(direction), 1f);
        float magnitude = math.length(direction);
        Vector3 resultingDirection = lookRotation * Vector3.forward * magnitude;

        Vector3 euler = lookRotation.eulerAngles;
        if (!right) euler.z = up! ? -90f : 90f;
        if (right) euler.z = up! ? 90f : -90f;
        euler.y = 0;
        if(!up) euler.x *= -1;
        
        var tempRotation = Quaternion.Euler(euler.x, 0, euler.z);
        
        if (tempRotation.x > 10 || tempRotation.x < -180)
        {
            turretTransform.Rotation = turretTransform.Rotation;
            returnValue.Item1 = turretPreviousTargetingDirection;
        } else
        {
            turretTransform.Rotation = tempRotation;
            returnValue.Item1 = resultingDirection;
            entityManager.SetComponentData(turret, turretTransform);
        }
        
        var cannonDirection = target - cannonTransformWorld.Position;
        float originalMagnitude = math.length(cannonDirection);
        var cannonLookRotation = Quaternion.RotateTowards(Quaternion.LookRotation(cannonPreviousTargetingDirection), Quaternion.LookRotation(cannonDirection), 1f);
        Vector3 cannonResultingDirection = cannonLookRotation * Vector3.forward * originalMagnitude;

        Vector3 cannonEuler = cannonLookRotation.eulerAngles;
        cannonEuler.y = 0;
        cannonEuler.z = 0;
        if (up == false)
        {
            cannonEuler.x *= -1;
        }
            
        var cannonTempRotation = Quaternion.Euler(cannonEuler);
        
        if (cannonTempRotation.x > 30 || cannonTempRotation.x < -180)
        {
            cannonTransform.Rotation = cannonTransform.Rotation;
            returnValue.Item2 = cannonPreviousTargetingDirection;
        } else
        {
            cannonTransform.Rotation = cannonTempRotation;
            returnValue.Item2 = cannonResultingDirection;
            entityManager.SetComponentData(cannon, cannonTransform);
        }

        return returnValue;
    }

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

    public float3 AquireTargetFromList(RefRW<SpatialGridData> gridData, NativeArray<LocalTransform> birds, NativeHashMap<int, int> potentialTargets)
    {
        int keyWithMostBoids = 0;
        int mostBoids = 0;
        var keyArray = potentialTargets.GetKeyValueArrays(Allocator.Temp);
        for (int i = 0; i < keyArray.Keys.Length; i++)
        {
            int key = keyArray.Keys[i];
            int count = keyArray.Values[i];

            if (count > mostBoids)
            {
                mostBoids = count;
                keyWithMostBoids = key;
            }
        }
        if (mostBoids == 0) return new float3(0,0,0);
        gridData.ValueRO.CellMap.TryGetFirstValue(keyWithMostBoids, out var firsValue, out var it);
        keyArray.Dispose();
        return birds[firsValue].Position;
        
    }
}
