using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
[UpdateAfter(typeof(SpatialHashingSystem))]
public partial struct BoidGridDebugSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridData>();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        // Disable Burst here for Debug drawing
        var gridData = SystemAPI.GetSingleton<SpatialGridData>();
        var grid = gridData.Grid;
        var map = gridData.CellMap;

        if (!map.IsCreated)
            return;
        
        // Draw each occupied cell as a wire cube
        var keys = map.GetKeyArray(Allocator.Temp);
        var drawn = new NativeParallelHashSet<int>(keys.Length, Allocator.Temp);

        foreach (var key in keys)
        {
            // Skip duplicates (since MultiHashMap has multiple values per cell)
            if (!drawn.Add(key))
                continue;

            int3 cell;
            GetCellCoordsFromIndex(key, grid, out cell);
            float3 worldPos = grid.Origin + (new float3(cell) + 0.5f) * grid.CellSize;
            float3 size = new float3(grid.CellSize);

            // draw a box around the cell
            //DrawWireCube(worldPos, size, Color.cyan);
        }

        drawn.Dispose();
        keys.Dispose();
    }

    // Helper to get cell coords from flat index
    private static void GetCellCoordsFromIndex(int index, SpatialHashGrid3D grid, out int3 cell)
    {
        int x = index % grid.GridDim.x;
        int y = (index / grid.GridDim.x) % grid.GridDim.y;
        int z = index / (grid.GridDim.x * grid.GridDim.y);
        cell = new int3(x, y, z);
    }

    // Helper to draw cube edges
    private static void DrawWireCube(float3 center, float3 size, Color color)
    {
        float3 ext = size * 0.5f;

        float3[] corners =
        {
            center + new float3(-ext.x, -ext.y, -ext.z),
            center + new float3(ext.x, -ext.y, -ext.z),
            center + new float3(ext.x, -ext.y, ext.z),
            center + new float3(-ext.x, -ext.y, ext.z),
            center + new float3(-ext.x, ext.y, -ext.z),
            center + new float3(ext.x, ext.y, -ext.z),
            center + new float3(ext.x, ext.y, ext.z),
            center + new float3(-ext.x, ext.y, ext.z)
        };

        // bottom square
        Debug.DrawLine(corners[0], corners[1], color);
        Debug.DrawLine(corners[1], corners[2], color);
        Debug.DrawLine(corners[2], corners[3], color);
        Debug.DrawLine(corners[3], corners[0], color);

        // top square
        Debug.DrawLine(corners[4], corners[5], color);
        Debug.DrawLine(corners[5], corners[6], color);
        Debug.DrawLine(corners[6], corners[7], color);
        Debug.DrawLine(corners[7], corners[4], color);

        // verticals
        Debug.DrawLine(corners[0], corners[4], color);
        Debug.DrawLine(corners[1], corners[5], color);
        Debug.DrawLine(corners[2], corners[6], color);
        Debug.DrawLine(corners[3], corners[7], color);
    }
}