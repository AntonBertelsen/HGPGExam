using Unity.Entities;
using UnityEngine;

class ContinuousBoidSpawnerBaker : MonoBehaviour
{
    public GameObject prefab;
    public float spawnRate = 1.0f;
}

class ContinuousBoidSpawnerAuthoringBaker : Baker<ContinuousBoidSpawnerBaker>
{
    public override void Bake(ContinuousBoidSpawnerBaker baker)
    {
        Entity spawnerEntity = GetEntity(TransformUsageFlags.Dynamic);
        
        AddComponent(spawnerEntity, new ContinuousSpawnerComponent
        {
            prefab = GetEntity(baker.prefab, TransformUsageFlags.Dynamic),
            nextSpawnTime = 0.0f,
            spawnRate = baker.spawnRate
        });
    }
}