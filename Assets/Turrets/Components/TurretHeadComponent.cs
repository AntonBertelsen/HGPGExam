using Unity.Entities;
using Unity.Mathematics;

public struct TurretHeadComponent : IComponentData
{
    public float viewRadius;
    public float fireRate;
    public float lastFireTime;
    public bool isRight;
    public Entity bullet;
}
