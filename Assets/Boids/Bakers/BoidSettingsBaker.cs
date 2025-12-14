using Unity.Entities;
using UnityEngine;


public struct BoidSettings : IComponentData
{
    public float ViewRadius;
    public float SeparationRadius;
    public float AvoidanceRadius;
    public float LandingRadius;

    public float SeparationWeight;
    public float AlignmentWeight;
    public float CohesionWeight;
    public float AvoidanceWeight;
    public float LandingWeight;
    public float FlowmapWeight;

    public float MaxSpeed;
    public float MinSpeed;
    public float MaxSteerForce;
    
    public float BoundaryWeight;
    
    public float LOD1Distance;
    public float LOD2Distance;
    public float LOD3Distance;

    public bool UseParallel;
}

public struct BoidSpawnerReference : IComponentData
{
    public Entity SpawnerPrefab;
}

public class BoidSettingsBaker : MonoBehaviour
{
    public BoidConfigAsset DefaultConfig;
    public GameObject SpawnerPrefab;
}

public class BoidSettingsBakerBaker : Baker<BoidSettingsBaker>
{
    public override void Bake(BoidSettingsBaker authoring)
    {
        if (authoring.DefaultConfig == null) return;

        var entity = GetEntity(TransformUsageFlags.None);
        
        // Bake initial values directly from the Asset
        AddComponent(entity, new BoidSettings
        {
            ViewRadius = authoring.DefaultConfig.ViewRadius,
            SeparationRadius = authoring.DefaultConfig.SeparationRadius,
            AvoidanceRadius = authoring.DefaultConfig.AvoidanceRadius,
            LandingRadius = authoring.DefaultConfig.LandingRadius,

            SeparationWeight = authoring.DefaultConfig.SeparationWeight,
            AlignmentWeight = authoring.DefaultConfig.AlignmentWeight,
            CohesionWeight = authoring.DefaultConfig.CohesionWeight,
            AvoidanceWeight = authoring.DefaultConfig.AvoidanceWeight,
            LandingWeight = authoring.DefaultConfig.LandingWeight,
            FlowmapWeight = authoring.DefaultConfig.FlowmapWeight,
            BoundaryWeight = authoring.DefaultConfig.BoundaryWeight,

            MaxSpeed = authoring.DefaultConfig.MaxSpeed,
            MinSpeed = authoring.DefaultConfig.MinSpeed,
            MaxSteerForce = authoring.DefaultConfig.MaxSteerForce,
            
            LOD1Distance = authoring.DefaultConfig.LOD1Distance,
            LOD2Distance = authoring.DefaultConfig.LOD2Distance,
            LOD3Distance = authoring.DefaultConfig.LOD3Distance,
            
            UseParallel = authoring.DefaultConfig.UseParallel
        });

        if (authoring.SpawnerPrefab != null)
        {
            AddComponent(entity, new BoidSpawnerReference
            {
                SpawnerPrefab = GetEntity(authoring.SpawnerPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}