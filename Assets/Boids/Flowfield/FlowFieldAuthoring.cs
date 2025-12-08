using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 1. THE AUTHORING COMPONENT
// This lives on a GameObject and holds the settings + the baked data (float3 array)
public class FlowFieldAuthoring : MonoBehaviour
{
    [Header("Grid Settings")]
    public float CellSize = 5.0f;
    public Vector3 GridSize = new Vector3(100, 50, 100);

    [Header("Avoidance")]
    public LayerMask ObstacleLayers;
    public float ObstacleRepulsionRadius = 5.0f;
    public float ObstacleWeight = 10.0f; 

    [Header("Terrain Following")]
    public LayerMask TerrainLayers;
    public float MinAltitude = 10.0f;
    public float MaxAltitude = 40.0f;
    public float TerrainWeight = 2.0f; 

    [Header("Directed Paths")]
    public List<Transform> PathWaypoints;
    public float PathRadius = 8.0f;
    public float PathWeight = 8.0f; 

    public enum DebugDrawMode { None, SimpleLines, Arrows }

    [Header("Debug Visualization")]
    public DebugDrawMode DrawMode = DebugDrawMode.SimpleLines;
    [Range(0.1f, 1f)] public float GizmoOpacity = 0.5f;
    public bool DrawSingleSlice = false;
    public int SliceYIndex = 5;
    [Tooltip("Skip cells to reduce lag. 1 = Draw All, 2 = Draw Half, etc.")]
    public int ResolutionStep = 2;

    // --- SERIALIZED DATA (The "Saved" Field) ---
    // We hide this from the inspector because drawing 100k vector fields slows down the UI massively.
    [HideInInspector] public float3[] BakedVectors;
    [HideInInspector] public int3 BakedDimensions;
    [HideInInspector] public float3 BakedOrigin;

    // --- THE GENERATION LOGIC ---
    [ContextMenu("Bake Flow Field")]
    public void GenerateField()
    {
        int3 dims = new int3(
            Mathf.CeilToInt(GridSize.x / CellSize),
            Mathf.CeilToInt(GridSize.y / CellSize),
            Mathf.CeilToInt(GridSize.z / CellSize)
        );

        int totalCells = dims.x * dims.y * dims.z;
        long memoryBytes = totalCells * 12; 
        float memoryMB = memoryBytes / (1024f * 1024f);

        BakedVectors = new float3[totalCells];
        BakedDimensions = dims;
        BakedOrigin = transform.position - (GridSize / 2.0f);

        Debug.Log($"Baking Flow Field: {dims.x}x{dims.y}x{dims.z} ({totalCells} cells). Estimated Memory: {memoryMB:F2} MB");

        // Temporary collider for ComputePenetration checks
        GameObject tempObj = new GameObject("TempBakerSphere");
        SphereCollider dummyCollider = tempObj.AddComponent<SphereCollider>();
        dummyCollider.radius = 0.5f; // Assume bird radius approx 0.5m
        
        for (int z = 0; z < dims.z; z++)
        {
            for (int y = 0; y < dims.y; y++)
            {
                for (int x = 0; x < dims.x; x++)
                {
                    int index = x + y * dims.x + z * dims.x * dims.y;
                    
                    float3 cellCenter = BakedOrigin + new float3(x * CellSize, y * CellSize, z * CellSize) + (CellSize * 0.5f);
                    float3 finalVector = float3.zero;

                    // 1. Obstacles (Avoidance)
                    float checkRadius = Mathf.Max(CellSize, ObstacleRepulsionRadius);
                    Collider[] hits = Physics.OverlapSphere(cellCenter, checkRadius, ObstacleLayers);
                    
                    if (hits.Length > 0)
                    {
                        Collider hitCol = hits[0]; 
                        
                        // A. Emergency Exit (Inside Collider)
                        dummyCollider.transform.position = cellCenter;
                        if (Physics.ComputePenetration(dummyCollider, dummyCollider.transform.position, dummyCollider.transform.rotation, 
                            hitCol, hitCol.transform.position, hitCol.transform.rotation, 
                            out Vector3 penDir, out float penDist))
                        {
                            // Inside! Push out violently.
                            finalVector += (float3)penDir * ObstacleWeight * 2.0f; 
                        }
                        else 
                        {
                            // B. Proximity Avoidance with TANGENT SLIDE
                            float3 closestPoint = hitCol.ClosestPoint(cellCenter);
                            float dist = math.distance(cellCenter, closestPoint);

                            if (dist < checkRadius)
                            {
                                // 1. Calculate the Normal (Directly away from wall)
                                float3 normal = math.normalizesafe(cellCenter - closestPoint);
            
                                // 2. Calculate the "Up" Tangent
                                // Use the helper function we defined
                                float3 upVector = new float3(0, 1, 0);
                                
                                // Projecting on plane to extract the upwards tangent
                                float dot = math.dot(upVector, normal);
                                float3 tangentUp = math.normalizesafe(upVector - (normal * dot));

                                // 3. Blend them
                                // If we are very close, use more Normal (Push away).
                                // If we are further out, use more Tangent (Slide along/up).
                                float strength = 1.0f - (dist / checkRadius);
            
                                // Blend: 40% Push Out, 60% Slide Up.
                                float3 slideForce = math.lerp(normal, tangentUp, 0.6f);

                                finalVector += slideForce * strength * ObstacleWeight;
                            }
                        }
                    }

                    // 2. Terrain & Buildings (SphereCast)
                    if (Physics.SphereCast(cellCenter, CellSize, Vector3.down, out RaycastHit hit, 2000f, TerrainLayers))
                    {
                        float altitude = hit.distance;
                        
                        // Push UP
                        if (altitude < MinAltitude)
                        {
                            float pushStrength = 1.0f - (altitude / MinAltitude);
                            float3 surfaceNormal = math.normalizesafe(cellCenter - (float3)hit.point);
                            surfaceNormal = math.normalize(surfaceNormal + new float3(0, 0.5f, 0)); // Bias Up

                            finalVector += surfaceNormal * pushStrength * TerrainWeight;
                        }
                        // Push DOWN (Soft Ceiling)
                        else if (altitude > MaxAltitude)
                        {
                            float distOver = altitude - MaxAltitude;
                            float pushStrength = math.clamp(distOver / 10.0f, 0f, 1f);
                            finalVector += new float3(0, -1, 0) * pushStrength * (TerrainWeight * 0.5f);
                        }
                    }

                    // 3. Paths
                    if (PathWaypoints != null && PathWaypoints.Count > 1)
                    {
                        for (int i = 0; i < PathWaypoints.Count - 1; i++)
                        {
                            if (PathWaypoints[i] == null || PathWaypoints[i + 1] == null) continue;
                            float3 p1 = PathWaypoints[i].position;
                            float3 p2 = PathWaypoints[i + 1].position;
                            
                            float3 closest = ClosestPointOnSegment(p1, p2, cellCenter);
                            float dist = math.distance(cellCenter, closest);

                            if (dist < PathRadius)
                            {
                                float3 pathDir = math.normalize(p2 - p1);
                                float strength = 1.0f - (dist / PathRadius);
                                finalVector += pathDir * strength * PathWeight; 
                            }
                        }
                    }

                    BakedVectors[index] = finalVector;
                }
            }
        }
        
        if(Application.isEditor) DestroyImmediate(tempObj);
        else Destroy(tempObj);

        Debug.Log("Flow Field Bake Complete.");
        
#if UNITY_EDITOR
        EditorUtility.SetDirty(this); // Ensure array is saved to scene file
        SceneView.RepaintAll();       // Force gizmos to update
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (DrawMode == DebugDrawMode.None) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, GridSize);

        if (BakedVectors == null || BakedVectors.Length == 0) return;

        int3 dims = BakedDimensions;
        float3 origin = BakedOrigin;
        
        // --- LOD LOGIC ---
#if UNITY_EDITOR
        Camera cam = SceneView.currentDrawingSceneView?.camera;
        Vector3 camPos = cam ? cam.transform.position : transform.position;
#else
        Vector3 camPos = transform.position;
#endif

        for (int z = 0; z < dims.z; z++)
        {
            for (int y = 0; y < dims.y; y++)
            {
                if (DrawSingleSlice && y != SliceYIndex) continue;

                for (int x = 0; x < dims.x; x++)
                {
                    float3 cellCenter = origin + new float3(x * CellSize, y * CellSize, z * CellSize) + (CellSize * 0.5f);

                    // Distance Culling (LOD)
                    float distToCam = Vector3.Distance(camPos, cellCenter);
                    int step = 1;
                    if (distToCam > 50f) step = 2;
                    if (distToCam > 150f) step = 4;
                    if (distToCam > 300f) step = 8;
                    
                    if (x % step != 0 || y % step != 0 || z % step != 0) continue;

                    int index = x + y * dims.x + z * dims.x * dims.y;
                    if (index >= BakedVectors.Length) continue;

                    float3 force = BakedVectors[index];
                    float forceLenSq = math.lengthsq(force);

                    if (forceLenSq > 0.1f)
                    {
                        float magnitude = math.sqrt(forceLenSq);
                        
                        // Color coding
                        Vector3 col = new Vector3(Mathf.Abs(force.x), Mathf.Abs(force.y), Mathf.Abs(force.z));
                        float maxC = Mathf.Max(col.x, Mathf.Max(col.y, col.z));
                        if(maxC > 0) col /= maxC;
                        
                        Gizmos.color = new Color(col.x, col.y, col.z, GizmoOpacity);
                        
                        // Scale line length by force magnitude
                        float visualLength = math.clamp(magnitude * 0.5f, CellSize * 0.2f, CellSize * 0.9f);
                        
                        float3 end = cellCenter + (math.normalize(force) * visualLength);
                        Gizmos.DrawLine(cellCenter, end);

                        if (DrawMode == DebugDrawMode.Arrows && step <= 2) 
                        {
                            Gizmos.DrawSphere(end, CellSize * 0.1f);
                        }
                    }
                    else if (DrawSingleSlice)
                    {
                        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.05f);
                        Gizmos.DrawLine(cellCenter - new float3(0.1f), cellCenter + new float3(0.1f));
                    }
                }
            }
        }
    }
    
    private static float3 ClosestPointOnSegment(float3 a, float3 b, float3 p)
    {
        float3 ab = b - a;
        float t = math.dot(p - a, ab) / math.dot(ab, ab);
        return a + math.saturate(t) * ab;
    }
}

// 2. THE BAKER (Converts Authoring Data to ECS Blob)
// This runs once when the subscene is processed.
public class FlowFieldBaker : Baker<FlowFieldAuthoring>
{
    public override void Bake(FlowFieldAuthoring authoring)
    {
        if (authoring.BakedVectors == null || authoring.BakedVectors.Length == 0)
        {
            Debug.LogWarning($"FlowFieldAuthoring on {authoring.name} has no baked data. Please click 'Bake Flow Field' in the context menu.");
            return;
        }

        var entity = GetEntity(TransformUsageFlags.None);
        
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<FlowFieldBlob>();
        
        var blobVectors = builder.Allocate(ref root.Vectors, authoring.BakedVectors.Length);
        
        for (int i = 0; i < authoring.BakedVectors.Length; i++)
        {
            blobVectors[i] = authoring.BakedVectors[i];
        }

        var blobRef = builder.CreateBlobAssetReference<FlowFieldBlob>(Allocator.Persistent);
        builder.Dispose();

        AddComponent(entity, new FlowFieldData
        {
            Blob = blobRef,
            GridOrigin = authoring.BakedOrigin,
            CellSize = authoring.CellSize,
            GridDimensions = authoring.BakedDimensions,
            Strength = 1.0f
        });
    }
}