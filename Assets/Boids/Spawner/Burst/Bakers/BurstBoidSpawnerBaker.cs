using Unity.Entities;
using UnityEngine;

class BurstBoidSpawnerBaker : MonoBehaviour
{
    public GameObject prefab;
    public int count = 10_000;
}

class BurstBoidSpawnerAuthoringBaker : Baker<BurstBoidSpawnerBaker>
{
    public override void Bake(BurstBoidSpawnerBaker baker)
    {
        Entity spawnerEntity = GetEntity(TransformUsageFlags.Dynamic);
        
        AddComponent(spawnerEntity, new BurstSpawnerComponent
        {
            prefab = GetEntity(baker.prefab, TransformUsageFlags.Dynamic),
            count = baker.count
        });
    }
}
