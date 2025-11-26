using Unity.Entities;
using Unity.Rendering;

public struct BirdAnimation : IComponentData
{
    public float AnimationFrame;
    public float Scale; // amplitude
    public float BaseSpeed; // natural flap rate
}

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