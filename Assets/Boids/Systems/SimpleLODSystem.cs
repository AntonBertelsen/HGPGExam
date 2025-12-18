using UnityEngine;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

public class LODMeshMappingSingleton : IComponentData
{
    public Dictionary<Mesh, BatchMeshID> MeshMapping;
}

public struct CameraPositionSingleton : IComponentData
{
    public LocalToWorld CameraTransform;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class CameraPositionUpdateSystem : SystemBase // SystemBase instead of ISystem because it interacts with teh managed world, so we can easily reference the camera gameobject
{
    protected override void OnCreate()
    {
        EntityManager.CreateEntity(typeof(CameraPositionSingleton));
    }

    protected override void OnUpdate()
    {
        var camera = Camera.main;
        if (camera == null) return;
        
        SystemAPI.SetSingleton(new CameraPositionSingleton
        {
            CameraTransform = new LocalToWorld { Value = camera.transform.localToWorldMatrix }
        });
    }
}

[RequireMatchingQueriesForUpdate]
public partial struct LODSystem_WithJob : ISystem
{
    private bool m_IsInitialized;
    
    private BatchMeshID m_LOD0_ID;
    private BatchMeshID m_LOD1_ID;
    private BatchMeshID m_LOD2_ID;
    private BatchMeshID m_LOD3_ID;
    private float m_LOD1_DistSq;
    private float m_LOD2_DistSq;
    private float m_LOD3_DistSq;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CameraPositionSingleton>();
        state.RequireForUpdate<SimpleLODConfig>();
        state.RequireForUpdate<BoidSettings>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!m_IsInitialized)
        {
            Initialize(ref state, ref this);
            m_IsInitialized = true;
        }
        
        var boidSettings = SystemAPI.GetSingleton<BoidSettings>();
        

        var cameraPosition = SystemAPI.GetSingleton<CameraPositionSingleton>().CameraTransform.Position;

        var lodJob = new LODJob
        {
            CameraPosition = cameraPosition,
            LOD0_ID = m_LOD0_ID,
            LOD1_ID = m_LOD1_ID,
            LOD2_ID = m_LOD2_ID,
            LOD3_ID = m_LOD3_ID,
            LOD1DistanceSq = boidSettings.LOD1Distance * boidSettings.LOD1Distance,
            LOD2DistanceSq = boidSettings.LOD2Distance * boidSettings.LOD2Distance,
            LOD3DistanceSq = boidSettings.LOD3Distance * boidSettings.LOD3Distance
        };
        
        if (boidSettings.UseParallel)
        {
            var jobHandle = lodJob.ScheduleParallel(state.Dependency);
            state.Dependency = jobHandle;
        }
        else
        {
            var jobHandle = lodJob.Schedule(state.Dependency);
            state.Dependency = jobHandle;
        }
    }

    private void Initialize(ref SystemState state, ref LODSystem_WithJob system)
    {
        var hybridRenderer = state.World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
        var lodConfig = SystemAPI.ManagedAPI.GetSingleton<SimpleLODConfig>();
        
        var mapping = new Dictionary<Mesh, BatchMeshID>();
        RegisterMesh(hybridRenderer, mapping, lodConfig.MeshLOD0);
        RegisterMesh(hybridRenderer, mapping, lodConfig.MeshLOD1);
        RegisterMesh(hybridRenderer, mapping, lodConfig.MeshLOD2);
        RegisterMesh(hybridRenderer, mapping, lodConfig.MeshLOD3);

        system.m_LOD0_ID = mapping[lodConfig.MeshLOD0];
        system.m_LOD1_ID = mapping[lodConfig.MeshLOD1];
        system.m_LOD2_ID = mapping[lodConfig.MeshLOD2];
        system.m_LOD3_ID = mapping[lodConfig.MeshLOD3];
    }

    private void RegisterMesh(EntitiesGraphicsSystem renderer, Dictionary<Mesh, BatchMeshID> mapping, Mesh mesh)
    {
        if (mesh != null && !mapping.ContainsKey(mesh))
        {
            mapping[mesh] = renderer.RegisterMesh(mesh);
        }
    }
}

[BurstCompile]
public partial struct LODJob : IJobEntity
{
    [ReadOnly] public float3 CameraPosition;
    [ReadOnly] public BatchMeshID LOD0_ID;
    [ReadOnly] public BatchMeshID LOD1_ID;
    [ReadOnly] public BatchMeshID LOD2_ID;
    [ReadOnly] public BatchMeshID LOD3_ID;
    [ReadOnly] public float LOD1DistanceSq;
    [ReadOnly] public float LOD2DistanceSq;
    [ReadOnly] public float LOD3DistanceSq;
    
    public void Execute(in SimpleLODTag tag, in LocalToWorld transform, ref MaterialMeshInfo mmi)
    {
        float distSq = math.distancesq(CameraPosition, transform.Position);

        if (distSq > LOD3DistanceSq)
        {
            mmi.MeshID = LOD3_ID;
        }
        else if (distSq > LOD2DistanceSq)
        {
            mmi.MeshID = LOD2_ID;
        }
        else if (distSq > LOD1DistanceSq)
        {
            mmi.MeshID = LOD1_ID;
        }
        else
        {
            mmi.MeshID = LOD0_ID;
        }
    }
}