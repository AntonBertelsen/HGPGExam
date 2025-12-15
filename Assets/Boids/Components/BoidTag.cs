using Unity.Entities;

public struct BoidTag : IComponentData
{
    public bool dead;
    public float timeBeingDead;
}
