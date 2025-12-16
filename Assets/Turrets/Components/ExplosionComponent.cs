using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ExplosionComponent : IComponentData
{
    public float timeLived;
    public float lifeExpetancy;
    public bool hasExploded;
    public float explosionForce;
    public float explosionDistance;
    public Entity physicsBird;
}

public struct ActiveExplosionElement : IBufferElementData
{
    public float3 Position;
    public float Force;
    public float RadiusSq;
}
public struct ExplosionManagerTag : IComponentData { }