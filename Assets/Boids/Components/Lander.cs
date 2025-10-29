using Unity.Entities;
using Unity.Mathematics;

public struct Lander : IComponentData
{
    public const int MaxEnergy = 2000;
    
    public LanderState State;
    public int Energy;
    public float3 Target;
    public int TargetIndex;
}

public enum LanderState : byte
{
    Flying,
    Landing,
    Landed,
}