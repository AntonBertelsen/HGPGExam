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
        
        foreach (var (turret, transform) in
                 SystemAPI.Query<RefRW<TurretComponent>, RefRW<LocalTransform>>())
        {
            //Count the nr of times a key is in the MultiHashMap, select first boid in associated grid
            var keyCounts = new NativeHashMap<int, int>(10, Allocator.Temp);
            
            var keyValues = gridData.ValueRO.CellMap.GetKeyValueArrays(Allocator.Temp);
            for (int i = 0; i < keyValues.Keys.Length; i++)
            {
                int key = keyValues.Keys[i];

                if (keyCounts.TryGetValue(key, out int count))
                    keyCounts[key] = count + 1;
                else
                    keyCounts.Add(key, 1);
            }

            int keyWithMostBoids = 0;
            int mostBoids = 0;
            var keyArray = keyCounts.GetKeyValueArrays(Allocator.Temp);
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
            
            gridData.ValueRO.CellMap.TryGetFirstValue(keyWithMostBoids, out var firsValue, out var it);
            
            var targetBirdPos = birds[firsValue].Position;
            var direction = targetBirdPos - transform.ValueRO.Position;
            direction.y = 0;
            transform.ValueRW.Rotation = Quaternion.LookRotation(direction);
            turret.ValueRW.targetingDirection = direction;
            keyArray.Dispose();
            keyCounts.Dispose();
            keyValues.Dispose();
            

        }
        
        birds.Dispose();
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
