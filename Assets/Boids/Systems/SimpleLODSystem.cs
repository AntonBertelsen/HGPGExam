using UnityEngine;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

// A managed component that will exist on a single entity (a singleton)
// to hold our mapping from Mesh objects to their registered BatchMeshIDs.
public class LODMeshMappingSingleton : IComponentData
{
    public Dictionary<Mesh, BatchMeshID> MeshMapping;
}

public struct CameraPositionSingleton : IComponentData
{
    public LocalToWorld CameraTransform;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class CameraPositionUpdateSystem : SystemBase
{
    protected override void OnCreate()
    {
        // Create an entity to act as the singleton
        EntityManager.CreateEntity(typeof(CameraPositionSingleton));
    }

    protected override void OnUpdate()
    {
        var camera = Camera.main;
        if (camera == null) return;

        // Update the singleton's data
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

    // The system now stores all the data the job will need.
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
        state.RequireForUpdate<SimpleLODConfig>(); // Depend on our new singleton.
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!m_IsInitialized)
        {
            Initialize(ref state, ref this);
            m_IsInitialized = true;
        }

        var cameraPosition = SystemAPI.GetSingleton<CameraPositionSingleton>().CameraTransform.Position;

        var lodJob = new LODJob
        {
            CameraPosition = cameraPosition,
            LOD0_ID = m_LOD0_ID,
            LOD1_ID = m_LOD1_ID,
            LOD2_ID = m_LOD2_ID,
            LOD3_ID = m_LOD3_ID,
            LOD1DistanceSq = m_LOD1_DistSq,
            LOD2DistanceSq = m_LOD2_DistSq,
            LOD3DistanceSq = m_LOD3_DistSq
        };
        
        // The job will run on all entities with the ApplyLOD tag.
        state.Dependency = lodJob.ScheduleParallel(state.Dependency);
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

        // Store the pre-calculated data in the system's fields.
        system.m_LOD0_ID = mapping[lodConfig.MeshLOD0];
        system.m_LOD1_ID = mapping[lodConfig.MeshLOD1];
        system.m_LOD2_ID = mapping[lodConfig.MeshLOD2];
        system.m_LOD3_ID = mapping[lodConfig.MeshLOD3];
        
        // Pre-calculate squared distances to avoid doing it every frame in the job.
        system.m_LOD1_DistSq = lodConfig.LOD1Distance * lodConfig.LOD1Distance;
        system.m_LOD2_DistSq = lodConfig.LOD2Distance * lodConfig.LOD2Distance;
        system.m_LOD3_DistSq = lodConfig.LOD3Distance * lodConfig.LOD3Distance;
    }

    private void RegisterMesh(EntitiesGraphicsSystem renderer, Dictionary<Mesh, BatchMeshID> mapping, Mesh mesh)
    {
        if (mesh != null && !mapping.ContainsKey(mesh))
        {
            mapping[mesh] = renderer.RegisterMesh(mesh);
        }
    }
}

// The job is now simpler, as it doesn't need to read settings from the entity.
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

    // The Execute method now only needs the tag, transform, and the MMI to change.
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