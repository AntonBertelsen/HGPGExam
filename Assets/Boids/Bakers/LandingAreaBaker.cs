using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Serialization;

public class LandingAreaBaker : MonoBehaviour
{
    public float spotSpacing = 1f;
}

public struct LandingAreaMeshBlob
{
    public BlobArray<float3> Vertices;
    public BlobArray<float3> Normals;
    public BlobArray<int> Triangles;
}

public struct LandingArea : IComponentData
{
    public float SpotSpacing;
    public BlobAssetReference<LandingAreaMeshBlob> MeshBlob;
}

public class LandingAreaAuthoringBaker : Baker<LandingAreaBaker>
{
    public override void Bake(LandingAreaBaker baker)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        var meshFilter = baker.GetComponent<MeshFilter>();
        if (meshFilter?.sharedMesh is not
            {
                vertices: { Length: > 0 } vertices,
                normals: { Length: > 0 } normals,
                triangles: { Length: > 0 } triangles
            })
        {
            Debug.LogWarning(
                $"{nameof(LandingAreaBaker)} on GameObject '{baker.gameObject.name}' requires a MeshFilter with a valid mesh.");
            return;
        }

        var builder = new BlobBuilder(Allocator.Temp);
        ref var blob = ref builder.ConstructRoot<LandingAreaMeshBlob>();

        var blobVertices = builder.Allocate(ref blob.Vertices, vertices.Length);
        for (var i = 0; i < vertices.Length; i++)
        {
            blobVertices[i] = vertices[i];
        }

        var blobNormals = builder.Allocate(ref blob.Normals, normals.Length);
        for (var i = 0; i < normals.Length; i++)
        {
            blobNormals[i] = normals[i];
        }

        var blobTriangles = builder.Allocate(ref blob.Triangles, triangles.Length);
        for (var i = 0; i < triangles.Length; i++)
        {
            blobTriangles[i] = triangles[i];
        }

        var blobRef = builder.CreateBlobAssetReference<LandingAreaMeshBlob>(Allocator.Persistent);
        builder.Dispose();

        AddComponent(entity, new LandingArea
        {
            SpotSpacing = baker.spotSpacing,
            MeshBlob = blobRef
        });
    }
}