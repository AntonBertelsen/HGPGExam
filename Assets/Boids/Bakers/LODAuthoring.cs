using Unity.Entities;
using UnityEngine;

public class SimpleLODAuthoring : MonoBehaviour
{
    [Header("Meshes")]
    public Mesh LOD0;
    public Mesh LOD1;
    public Mesh LOD2;

    [Header("Material")]
    public Material Material;

    [Header("LOD Distances")]
    public float LOD1Distance = 50f;
    public float LOD2Distance = 100f;
}

public class SimpleLODBaker : Baker<SimpleLODAuthoring>
{
    public override void Bake(SimpleLODAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        
        AddComponentObject(entity, new SimpleLODConfig()
        {
            MeshLOD0 = authoring.LOD0,
            MeshLOD1 = authoring.LOD1,
            MeshLOD2 = authoring.LOD2,
            LOD1Distance = authoring.LOD1Distance,
            LOD2Distance = authoring.LOD2Distance
        });
    }
}