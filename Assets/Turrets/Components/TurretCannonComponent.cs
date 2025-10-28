using Unity.Entities;
using Unity.Mathematics;

public struct TurretCannonComponent : IComponentData
{
    public float viewRadius;
    public float fireRate;
    public float lastFireTime;
    public bool isDown;
    public bool isRight;
    public Entity bullet;
}
