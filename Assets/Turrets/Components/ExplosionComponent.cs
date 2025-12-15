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
