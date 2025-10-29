using Unity.Entities;
using Unity.Mathematics;

// A component to hold a color for debug visualization.
// Note: This requires a custom shader to display.
public struct DebugColorComponent : IComponentData
{
    public float4 Value; // float4 is easily passed to a shader
}