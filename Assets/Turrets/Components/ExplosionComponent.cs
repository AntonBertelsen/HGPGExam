using Unity.Entities;
using Unity.Mathematics;

public struct ExplosionComponent : IComponentData
{
    public float timeLived;
    public float lifeExpetancy;
    public bool hasExploded;
}
