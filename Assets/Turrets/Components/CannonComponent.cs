using Unity.Entities;
using Unity.Mathematics;

public struct CannonComponent : IComponentData
{
    public float viewRadius;
    public float fireRate;
    public bool isDown;
    public float lastFireTime;
    public Entity bullet;
}
