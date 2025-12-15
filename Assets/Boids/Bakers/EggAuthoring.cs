using Unity.Entities;
using UnityEngine;

public struct EggComponent : IComponentData
{
    public int SpawnCount;
    public float ExplosionSpeed;
    public Entity BirdPrefab;
    public Entity ShellPrefab;
}

class EggAuthoring : MonoBehaviour
{
    [Header("Settings")]
    public int SpawnCount = 1000;
    public float ExplosionSpeed = 30f;

    [Header("References")]
    public GameObject BirdPrefab;
    public GameObject ShellPrefab;
}

class EggAuthoringBaker : Baker<EggAuthoring>
{
    public override void Bake(EggAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new EggComponent
        {
            SpawnCount = authoring.SpawnCount,
            ExplosionSpeed = authoring.ExplosionSpeed,
            BirdPrefab = GetEntity(authoring.BirdPrefab, TransformUsageFlags.Dynamic),
            ShellPrefab = GetEntity(authoring.ShellPrefab, TransformUsageFlags.Dynamic)
        });
    }
}
