using UnityEngine;

[CreateAssetMenu(fileName = "DefaultBoidConfig", menuName = "Boids/Config Asset")]
public class BoidConfigAsset : ScriptableObject
{
    [Header("Radii")]
    public float ViewRadius = 3f;
    public float SeparationRadius = 2.5f;
    public float AvoidanceRadius = 5f;
    public float LandingRadius = 30f;

    [Header("Behavior Weights")]
    public float SeparationWeight = 6.0f;
    public float AlignmentWeight = 1.0f;
    public float CohesionWeight = 3.0f;
    public float AvoidanceWeight = 6.0F;
    public float LandingWeight = 4.0F;
    public float FlowmapWeight = 1.0f;

    [Header("Limits")]
    public float MaxSpeed = 15f;
    public float MinSpeed = 10f;
    public float MaxSteerForce = 0.5f;

    [Header("Boundary")]
    public float BoundaryWeight = 50f;
    
    [Header("LOD Settings")]
    public float LOD1Distance = 50f;
    public float LOD2Distance = 100f;
    public float LOD3Distance = 150f;
    
    [Header("Performance")]
    public int MaxConsideredNeighbors = 5;
    public bool FlowMapEnabled = true;
    public bool DynamicAvoidanceEnabled = true;

    [Header("UseParallel")] 
    public bool UseParallel = true;
}