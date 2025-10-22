using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class BoidBaker : MonoBehaviour
{
    
}

class BoidAuthoringBaker : Baker<BoidBaker>
{
    // The Bake method is called by Unity to convert the GameObject to an Entity
    public override void Bake(BoidBaker baker)
    {
        // Get a handle to the entity we are baking
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<Velocity>(entity);
        AddComponent<BoidTag>(entity);
        AddComponent<ObstacleAvoidance>(entity);
    }
}
