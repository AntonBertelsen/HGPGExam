using Unity.Entities;
using Unity.Rendering;

[MaterialProperty("_AnimationFrame")]
public struct BirdAnimationFrameProperty : IComponentData
{
    public float Value;
}

[MaterialProperty("_Scale")]
public struct BirdScaleProperty : IComponentData
{
    public float Value;
}