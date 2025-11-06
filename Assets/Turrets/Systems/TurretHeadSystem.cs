using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
[UpdateAfter(typeof(TurretSystem))]

partial struct TurretHeadSystem : ISystem
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


        foreach (var (turret, localTransform, localToWorldTransform) in
                 SystemAPI.Query<RefRW<TurretHeadComponent>, RefRW<LocalTransform>, RefRW<LocalToWorld>>())
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
            var direction = targetBirdPos - localToWorldTransform.ValueRO.Position;
            
            var lookRotation = Quaternion.LookRotation(direction);
            
            Vector3 euler = lookRotation.eulerAngles;

            euler.z = turret.ValueRO.isRight ? -90f : 90f;
            euler.y = 0;
            euler.x = 0;

            var tempRotation = Quaternion.Euler(euler);


            if (tempRotation.x > 30 || tempRotation.x < -210)
            {
                localTransform.ValueRW.Rotation = localTransform.ValueRO.Rotation;
            } else
            {
                localTransform.ValueRW.Rotation = tempRotation;
                turret.ValueRW.targetingDirection = direction;
            }
            
        }
        
        birds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
