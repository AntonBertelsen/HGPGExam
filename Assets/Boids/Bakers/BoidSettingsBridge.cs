using Unity.Entities;
using UnityEngine;

public class BoidSettingsBridge : MonoBehaviour
{
    public BoidConfigAsset DefaultConfig;
    
    [HideInInspector] public float ViewRadius;
    [HideInInspector] public float SeparationRadius;
    [HideInInspector] public float AvoidanceRadius;
    [HideInInspector] public float LandingRadius;

    [HideInInspector] public float SeparationWeight;
    [HideInInspector] public float AlignmentWeight;
    [HideInInspector] public float CohesionWeight;
    [HideInInspector] public float AvoidanceWeight;
    [HideInInspector] public float LandingWeight;
    [HideInInspector] public float FlowmapWeight;
    [HideInInspector] public float BoundaryWeight;

    [HideInInspector] public float MaxSpeed;
    [HideInInspector] public float MinSpeed;
    [HideInInspector] public float MaxSteerForce;
    
    [HideInInspector] public float LOD1Distance;
    [HideInInspector] public float LOD2Distance;
    [HideInInspector] public float LOD3Distance;

    private EntityManager _em;
    private Entity _settingsEntity;

    void Start()
    {
        ResetToDefaults();
        
        if (World.DefaultGameObjectInjectionWorld != null)
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }
    }

    void Update()
    {
        if (_em.World == null) 
        {
             if (World.DefaultGameObjectInjectionWorld != null)
                _em = World.DefaultGameObjectInjectionWorld.EntityManager;
             else return;
        }
        
        if (_settingsEntity == Entity.Null)
        {
            var query = _em.CreateEntityQuery(typeof(BoidSettings));
            if (!query.IsEmpty) _settingsEntity = query.GetSingletonEntity();
            else return;
        }

        // We sync bridge monobehaviour data (updated by ui sliders) to the ecs world every frame. Perhaps there is a more performant way to do this instead of just doing it every frame
        _em.SetComponentData(_settingsEntity, ToBoidSettings());
    }

    public void ResetToDefaults()
    {
        if (DefaultConfig == null) return;

        ViewRadius = DefaultConfig.ViewRadius;
        SeparationRadius = DefaultConfig.SeparationRadius;
        AvoidanceRadius = DefaultConfig.AvoidanceRadius;
        LandingRadius = DefaultConfig.LandingRadius;

        SeparationWeight = DefaultConfig.SeparationWeight;
        AlignmentWeight = DefaultConfig.AlignmentWeight;
        CohesionWeight = DefaultConfig.CohesionWeight;
        AvoidanceWeight = DefaultConfig.AvoidanceWeight;
        LandingWeight = DefaultConfig.LandingWeight;
        FlowmapWeight = DefaultConfig.FlowmapWeight;
        BoundaryWeight = DefaultConfig.BoundaryWeight;

        MaxSpeed = DefaultConfig.MaxSpeed;
        MinSpeed = DefaultConfig.MinSpeed;
        MaxSteerForce = DefaultConfig.MaxSteerForce;
        
        LOD1Distance = DefaultConfig.LOD1Distance;
        LOD2Distance = DefaultConfig.LOD2Distance;
        LOD3Distance = DefaultConfig.LOD3Distance;
    }

    private BoidSettings ToBoidSettings()
    {
        return new BoidSettings
        {
            ViewRadius = ViewRadius,
            SeparationRadius = SeparationRadius,
            AvoidanceRadius = AvoidanceRadius,
            LandingRadius = LandingRadius,
            SeparationWeight = SeparationWeight,
            AlignmentWeight = AlignmentWeight,
            CohesionWeight = CohesionWeight,
            AvoidanceWeight = AvoidanceWeight,
            LandingWeight = LandingWeight,
            FlowmapWeight = FlowmapWeight,
            BoundaryWeight = BoundaryWeight,
            MaxSpeed = MaxSpeed,
            MinSpeed = MinSpeed,
            MaxSteerForce = MaxSteerForce,
            LOD1Distance = LOD1Distance,
            LOD2Distance = LOD2Distance,
            LOD3Distance = LOD3Distance,
        };
    }
}