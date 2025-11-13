using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class TurretBaker : MonoBehaviour
{
    public float _viewRadius;
    public float _fireRate;
    public float _lastFireTime;
    public GameObject _bulletPrefab;   
    public GameObject turret_UR;   
    public GameObject cannon_UR;  
    public GameObject turret_UL;   
    public GameObject cannon_UL;  
    public GameObject turret_DR;   
    public GameObject cannon_DR;  
    public GameObject turret_DL;   
    public GameObject cannon_DL;  

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
            targetingDirection = float3.zero,
            turret_UR_targetingDirection = float3.zero,
            turret_UL_targetingDirection = float3.zero,
            turret_DR_targetingDirection = float3.zero,
            turret_DL_targetingDirection = float3.zero,
            cannon_UR_targetingDirection = float3.zero,
            cannon_UL_targetingDirection = float3.zero,
            cannon_DR_targetingDirection = float3.zero,
            cannon_DL_targetingDirection = float3.zero,
            turret_UR = GetEntity(authoring.turret_UR, TransformUsageFlags.Dynamic),
            cannon_UR = GetEntity(authoring.cannon_UR, TransformUsageFlags.Dynamic),
            turret_UL = GetEntity(authoring.turret_UL, TransformUsageFlags.Dynamic),
            cannon_UL = GetEntity(authoring.cannon_UL, TransformUsageFlags.Dynamic),
            turret_DL = GetEntity(authoring.turret_DL, TransformUsageFlags.Dynamic),
            cannon_DL = GetEntity(authoring.cannon_DL, TransformUsageFlags.Dynamic),
            turret_DR = GetEntity(authoring.turret_DR, TransformUsageFlags.Dynamic),
            cannon_DR = GetEntity(authoring.cannon_DR, TransformUsageFlags.Dynamic),
        });
    }
}
