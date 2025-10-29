using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class CannonBaker : MonoBehaviour
{
    public float _viewRadius;
    public float _fireRate;
    public float _lastFireTime;
    public bool isDown;
    public GameObject _bulletPrefab;   
}

class CannonBakerBaker : Baker<CannonBaker>
{
    public override void Bake(CannonBaker authoring)
    {
        Entity turretEntity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(turretEntity, new CannonComponent()
        {
            viewRadius = authoring._viewRadius,
            fireRate = authoring._fireRate,
            lastFireTime = authoring._lastFireTime,
            isDown = authoring.isDown,
            bullet = GetEntity(authoring._bulletPrefab, TransformUsageFlags.Dynamic),
            targetingDirection = float3.zero
        });
    }
}
