using Unity.Entities;
using UnityEngine;


public struct BoidSettings : IComponentData
{
    public float ViewRadius;
    public float SeparationRadius;
    public float AvoidanceRadius;
    
    public float SeparationWeight;
    public float AlignmentWeight;
    public float CohesionWeight;
    public float AvoidanceWeight;

    public float MaxSpeed;
    public float MinSpeed;
    public float MaxSteerForce;
    
    public float BoundaryBounds;
    public float BoundaryTurnDistance;
}

class BoidSettingsBaker : MonoBehaviour
{
    public float ViewRadius = 7f;
    public float SeparationRadius = 3f;
    public float AvoidanceRadius = 3f;
    
    [Header("Behavior Weights")]
    public float SeparationWeight = 2.0f;
    public float AlignmentWeight = 1.0f;
    public float CohesionWeight = 1.0f;
    public float AvoidanceWeight = 1.0F;

    [Header("Limits")]
    public float MaxSpeed = 15f;
    public float MinSpeed = 10f;
    public float MaxSteerForce = 0.5f;
    
    [Header("Boundaries")]
    public float BoundaryBounds = 50f;
    public float BoundaryTurnDistance = 10f; // Boids will start turning when 10 units from a wall
}

class BoidSettingsBakerBaker : Baker<BoidSettingsBaker>
{
    public override void Bake(BoidSettingsBaker authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new BoidSettings
        {
            ViewRadius = authoring.ViewRadius,
            SeparationRadius = authoring.SeparationRadius,
            AvoidanceRadius = authoring.AvoidanceRadius,
            SeparationWeight = authoring.SeparationWeight,
            AlignmentWeight = authoring.AlignmentWeight,
            AvoidanceWeight = authoring.AvoidanceWeight,
            CohesionWeight = authoring.CohesionWeight,
            MaxSpeed = authoring.MaxSpeed,
            MinSpeed = authoring.MinSpeed,
            MaxSteerForce = authoring.MaxSteerForce,
            BoundaryBounds = authoring.BoundaryBounds,
            BoundaryTurnDistance = authoring.BoundaryTurnDistance
        });
    }
}
