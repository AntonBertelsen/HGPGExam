using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class TurretBaker : MonoBehaviour
{
    public float _viewRadius;
    public float _fireRate;
    public float _lastFireTime;
    public GameObject _bulletPrefab;   
}

class TurretBakerBaker : Baker<TurretBaker>
{
    public override void Bake(TurretBaker authoring)
    {
        Entity turretEntity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(turretEntity, new TurretComponent
        {
            viewRadius = authoring._viewRadius,
            fireRate = authoring._fireRate,
            lastFireTime = authoring._lastFireTime,
            bullet = GetEntity(authoring._bulletPrefab, TransformUsageFlags.Dynamic),
            targetingDirection = float3.zero
        });
    }
}
