using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// 1. The Component Data
public struct BoundaryComponent : IComponentData
{
    public float3 Center;
    public float3 Size;
    public float3 PositiveMargins; 
    public float3 NegativeMargins; 
}

// 2. The MonoBehaviour for the Editor
public class BoundaryAuthoring : MonoBehaviour
{
    [Header("Boundary Positioning")]
    [Tooltip("Offset from the GameObject's position (Local Space).")]
    public Vector3 CenterOffset = Vector3.zero;

    [Header("Boundary Dimensions")]
    [Tooltip("The total size of the boundary box in world units.")]
    public Vector3 Size = new Vector3(100, 50, 100);

    [Header("Margins (Distance from wall to start turning)")]
    [Tooltip("Margins for Right (+X), Top (+Y), Forward (+Z)")]
    public Vector3 PositiveMargins = new Vector3(10, 10, 10);

    [Tooltip("Margins for Left (-X), Bottom (-Y), Back (-Z)")]
    public Vector3 NegativeMargins = new Vector3(10, 10, 10);

    [Header("Gizmos")] 
    public bool ShowGizmos = true;
    public Color HardBoundColor = Color.yellow;
    public Color SoftBoundColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (!ShowGizmos) return;

        // Use a matrix that respects position & rotation but forces Scale to 1.
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // Draw Hard Bounds at the Offset
        Gizmos.color = HardBoundColor;
        Gizmos.DrawWireCube(CenterOffset, Size);

        // Draw Soft Bounds relative to the Offset
        Gizmos.color = SoftBoundColor;
        
        Vector3 halfSize = Size * 0.5f;
        
        // Calculate corners relative to the CenterOffset
        // Max corner of hard box (local to center)
        Vector3 hardMax = CenterOffset + halfSize;
        // Min corner of hard box (local to center)
        Vector3 hardMin = CenterOffset - halfSize;

        // Apply margins
        Vector3 softMax = hardMax - PositiveMargins;
        Vector3 softMin = hardMin + NegativeMargins;

        Vector3 softSize = softMax - softMin;
        Vector3 softCenter = (softMax + softMin) * 0.5f;

        // Prevent negative sizes if margins are larger than the box
        softSize.x = Mathf.Max(0, softSize.x);
        softSize.y = Mathf.Max(0, softSize.y);
        softSize.z = Mathf.Max(0, softSize.z);

        Gizmos.DrawWireCube(softCenter, softSize);
    }

    // 3. The Baker
    public class BoundaryBaker : Baker<BoundaryAuthoring>
    {
        public override void Bake(BoundaryAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            // Calculate the world space center including the offset.
            // We apply rotation to the offset to match the Gizmo visualization, 
            // but we ignore the GameObject's scale to keep the Size field accurate.
            float3 worldCenter = (float3)authoring.transform.position + math.mul(authoring.transform.rotation, authoring.CenterOffset);

            AddComponent(entity, new BoundaryComponent
            {
                Center = worldCenter,
                Size = authoring.Size,
                PositiveMargins = authoring.PositiveMargins,
                NegativeMargins = authoring.NegativeMargins
            });
        }
    }
}