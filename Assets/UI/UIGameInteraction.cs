using TMPro;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public class UIGameInteraction : MonoBehaviour
{
    public Transform spawnPoint;
    public TMP_InputField spawnAmountInput;
    
    private void SpawnBirds(int spawnCount)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        Entity spawnerPrefab = em.CreateEntityQuery(typeof(BoidSpawnerReference))
            .GetSingleton<BoidSpawnerReference>()
            .SpawnerPrefab;
        
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
    
    public void ClearBoids()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(BoidTag));
        em.DestroyEntity(query);
    }
    
    public void ResetSettings()
    {
        BoidSettingsBridge boidSettings = FindFirstObjectByType<BoidSettingsBridge>();
        boidSettings.ResetToDefaults();
    }
}
