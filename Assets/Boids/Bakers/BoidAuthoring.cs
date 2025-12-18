using Unity.Entities;
using UnityEngine;

class BoidAuthoring : MonoBehaviour
{
    
}

class BoidBaker : Baker<BoidAuthoring>
{
    public override void Bake(BoidAuthoring baker)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<Velocity>(entity);
        AddComponent<BoidTag>(entity);
        AddComponent<CellHashComponent>(entity);
        AddComponent<ObstacleAvoidance>(entity);
        AddComponent(entity, new Lander { Energy = Lander.MaxEnergy });
        AddComponent<BirdAnimationFrameProperty>(entity);
        AddComponent<BirdScaleProperty>(entity);
        AddComponent<SimpleLODTag>(entity);
    }
}
