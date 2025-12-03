using Unity.Entities;
using Unity.Mathematics;

public struct Lander : IComponentData
{
    public const int MaxEnergy = 120;
    
    public LanderState State;
    public float Energy;
    public float3 Target;
    public int TargetIndex;
}

public enum LanderState : byte
{
    Flying,
    Landing,
    Landed,
}