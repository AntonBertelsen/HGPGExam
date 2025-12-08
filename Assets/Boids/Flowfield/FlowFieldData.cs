using Unity.Entities;
using Unity.Mathematics;

public struct FlowFieldData : IComponentData
{
    public BlobAssetReference<FlowFieldBlob> Blob;
    public float3 GridOrigin;
    public float CellSize;
    public int3 GridDimensions;
    public float Strength;
}

public struct FlowFieldBlob
{
    // We store the direction vectors for every cell in the grid
    public BlobArray<float3> Vectors;
}