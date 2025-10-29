using Unity.Entities;

// A temporary component to store the calculated hash for each boid.
public struct CellHashComponent : IComponentData
{
    public int Value;
}