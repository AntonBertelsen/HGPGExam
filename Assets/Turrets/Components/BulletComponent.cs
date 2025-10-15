using Unity.Entities;
using Unity.Mathematics;

public struct BulletComponent : IComponentData
{
    public float timeToExplode;
    public float timeLived;
}
