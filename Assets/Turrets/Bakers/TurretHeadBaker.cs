using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class TurretHeadBaker : MonoBehaviour
{
    public float _viewRadius;
    public float _fireRate;
    public float _lastFireTime;
    public bool _isRight;
    public GameObject _bulletPrefab;   
}

class TurretHeadBakerBaker : Baker<TurretHeadBaker>
{
    public override void Bake(TurretHeadBaker authoring)
    {
        Entity turretEntity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(turretEntity, new TurretHeadComponent
        {
            viewRadius = authoring._viewRadius,
            fireRate = authoring._fireRate,
            lastFireTime = authoring._lastFireTime,
            isRight = authoring._isRight,
            bullet = GetEntity(authoring._bulletPrefab, TransformUsageFlags.Dynamic),
            targetingDirection = float3.zero

        });
    }
}
