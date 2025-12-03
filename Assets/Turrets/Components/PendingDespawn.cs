using Unity.Entities;

public struct PendingDespawn : IComponentData
{
    public float TimeRemaining;
}
