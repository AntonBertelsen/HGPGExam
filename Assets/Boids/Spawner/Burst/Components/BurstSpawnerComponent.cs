using Unity.Entities;
using Unity.Mathematics;

public struct BurstSpawnerComponent : IComponentData
{
    public Entity prefab;
    public int count;
}
