using Unity.Entities;
using Unity.Mathematics;

public struct TurretComponent : IComponentData
{
    public float viewRadius;
    public float fireRate;
    public float lastFireTime;
    public Entity bullet;
}
