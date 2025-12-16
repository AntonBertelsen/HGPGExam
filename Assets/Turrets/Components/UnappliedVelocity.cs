using Unity.Entities;
using Unity.Mathematics;

public struct UnappliedVelocity : IComponentData
{
    public float3 Velocity;
}