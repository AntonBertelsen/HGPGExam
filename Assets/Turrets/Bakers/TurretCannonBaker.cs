using Unity.Entities;
using UnityEngine;

class TurretCannonBaker : MonoBehaviour
{
    public float _viewRadius;
    public float _fireRate;
    public float _lastFireTime;
    public bool _isDown;
    public bool _isRight;
    public GameObject _bulletPrefab;   
}

class TurretCannonBakerBaker : Baker<TurretCannonBaker>
{
    public override void Bake(TurretCannonBaker authoring)
    {
        Entity turretEntity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(turretEntity, new TurretCannonComponent
        {
            viewRadius = authoring._viewRadius,
            fireRate = authoring._fireRate,
            lastFireTime = authoring._lastFireTime,
            isDown = authoring._isDown,
            isRight = authoring._isRight,
            bullet = GetEntity(authoring._bulletPrefab, TransformUsageFlags.Dynamic)
        });
    }
}
