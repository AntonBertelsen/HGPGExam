using Unity.Entities;
using Unity.Mathematics;

public struct ContinuousSpawnerComponent : IComponentData
{
    public Entity prefab;
    public float nextSpawnTime;
    public float spawnRate;
}
