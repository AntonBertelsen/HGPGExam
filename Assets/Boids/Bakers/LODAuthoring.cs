using Unity.Entities;
using UnityEngine;

public class SimpleLODAuthoring : MonoBehaviour
{
    [Header("Meshes")]
    public Mesh LOD0;
    public Mesh LOD1;
    public Mesh LOD2;
    public Mesh LOD3;

    [Header("Material")]
    public Material Material;
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
            MeshLOD3 = authoring.LOD3,
        });
    }
}