using Unity.Collections;
using Unity.Entities;

// This singleton will hold the data structures for our spatial grid,
// allowing other systems to access them.
public struct SpatialGridComponent : IComponentData
{
    // The sorted list of all boids. The core of our grid.
    public NativeArray<Entity> SortedBoids;
    
    // The sorted list of corresponding hashes. We need this to find the "end" of a cell group.
    public NativeArray<int> SortedCellHashes;

    // The "card catalog" for fast lookups of cell start indices.
    public NativeHashMap<int, int> CellStartIndices;
}