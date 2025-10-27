using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

public class LandingAreaBaker : MonoBehaviour
{
}

public struct LandingAreaSurfaceBlob
{
    public BlobArray<float3> Positions;
}

public struct LandingArea : IComponentData
{
    public int Count => SurfaceBlob.Value.Positions.Length;
    public BlobAssetReference<LandingAreaSurfaceBlob> SurfaceBlob;
}

public class LandingAreaAuthoringBaker : Baker<LandingAreaBaker>
{
    public override void Bake(LandingAreaBaker baker)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        var transform = baker.GetComponent<Transform>();
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

        var triangleCount = triangles.Length / 3;

        var passingCentroids = new NativeList<float3>(Allocator.Temp);
        const float upThreshold = 0.7071f; // cos(45 degrees)
        var up = new float3(0, 1, 0);

        for (var i = 0; i < triangleCount; i++)
        {
            var i0 = triangles[i * 3];
            var i1 = triangles[i * 3 + 1];
            var i2 = triangles[i * 3 + 2];

            var centroid = (vertices[i0] + vertices[i1] + vertices[i2]) / 3f;
            var scaledCentroid = Vector3.Scale(centroid, transform.localScale);
            var worldCentroid = (float3)transform.position + math.mul(transform.rotation, scaledCentroid);

            var avgNormal = (normals[i0] + normals[i1] + normals[i2]) / 3f;
            var worldNormal = math.normalize(math.mul(transform.rotation, avgNormal));

            if (!(math.dot(worldNormal, up) >= upThreshold)) continue;

            passingCentroids.Add(worldCentroid);
        }

        var builder = new BlobBuilder(Allocator.Temp);
        ref var blob = ref builder.ConstructRoot<LandingAreaSurfaceBlob>();
        var trianglePositions = builder.Allocate(ref blob.Positions, passingCentroids.Length);

        for (var i = 0; i < passingCentroids.Length; i++)
        {
            trianglePositions[i] = passingCentroids[i];
        }

        passingCentroids.Dispose();

        var blobRef = builder.CreateBlobAssetReference<LandingAreaSurfaceBlob>(Allocator.Persistent);
        builder.Dispose();

        AddComponent(entity, new LandingArea { SurfaceBlob = blobRef });
    }
}