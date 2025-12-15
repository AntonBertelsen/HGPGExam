using Unity.Entities;

public struct BurstSpawnerComponent : IComponentData
{
    public Entity prefab;
    public int count;
    public float InitialSpeed;
}
