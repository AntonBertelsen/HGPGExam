using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct TurretComponent : IComponentData
{
    public float viewRadius;
    public float fireRate;
    public float lastFireTime;
    public Entity bullet;
    public float3 targetingDirection;
    
    public float3 turret_UR_targetingDirection;
    public float3 turret_UL_targetingDirection;
    public float3 turret_DR_targetingDirection;
    public float3 turret_DL_targetingDirection;
    
    
    public float3 cannon_UR_targetingDirection;
    public float3 cannon_UL_targetingDirection;
    public float3 cannon_DR_targetingDirection;
    public float3 cannon_DL_targetingDirection;
    
    public float3 target_UR;
    public float3 target_UL;
    public float3 target_DR;
    public float3 target_DL;
    public float3 target_center;
    
    public Entity turret_UR;   
    public Entity cannon_UR;  
    public Entity turret_UL;   
    public Entity cannon_UL;  
    public Entity turret_DR;   
    public Entity cannon_DR;  
    public Entity turret_DL;   
    public Entity cannon_DL; 
}
