using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(PostBakingSystemGroup))]
public partial struct KdTreeBuilderSystem : ISystem
{
    private EntityQuery _landingAreaQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        _landingAreaQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LandingArea, LocalToWorld>()
            .Build(ref state);
        
        // This means OnUpdate will not run until this query has at least 1 entity. That prevents the issue of landing areas in the subscene not being baked
        // unless it is already loaded in the editor
        state.RequireForUpdate(_landingAreaQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        
        // 1. Get Entities array along with components
        var entities = _landingAreaQuery.ToEntityArray(Allocator.TempJob);
        
        
        var landingAreas = _landingAreaQuery.ToComponentDataArray<LandingArea>(Allocator.TempJob);
        var localToWorlds = _landingAreaQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);

        using var allLandingSpotsPos = new NativeList<float3>(Allocator.Temp);
        using var allLandingSpotsNorm = new NativeList<float3>(Allocator.Temp);
        
        for (var i = 0; i < landingAreas.Length; i++)
        {
            var landingArea = landingAreas[i];
            var localToWorld = localToWorlds[i];
            
            CalculateLandingSpots(entities[i], landingArea, localToWorld, physicsWorld, allLandingSpotsPos, allLandingSpotsNorm);
        }

        using var posArray = allLandingSpotsPos.ToArray(Allocator.Temp);
        using var normArray = allLandingSpotsNorm.ToArray(Allocator.Temp);
        
        var tree = KdTree.Create(posArray, normArray);
        state.EntityManager.AddComponentData(state.SystemHandle, tree);

        state.Enabled = false;
        
        entities.Dispose();
        landingAreas.Dispose();
        localToWorlds.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        state.EntityManager.RemoveComponent<KdTree>(state.SystemHandle);
    }

    private void CalculateLandingSpots(Entity self, LandingArea landingArea, LocalToWorld localToWorld, CollisionWorld collisionWorld, NativeList<float3> outPositions, NativeList<float3> outNormals)
    {
        ref var vertices = ref landingArea.MeshBlob.Value.Vertices;
        ref var normals = ref landingArea.MeshBlob.Value.Normals;
        ref var triangles = ref landingArea.MeshBlob.Value.Triangles;

        var triangleCount = triangles.Length / 3;
        var normalMatrix = math.transpose(math.inverse(new float3x3(localToWorld.Value)));

        using var passingPoints = new NativeList<float3>(Allocator.Temp);
        using var passingNormals = new NativeList<float3>(Allocator.Temp);
        
        var upThreshold = math.cos(math.radians(landingArea.MaxInclineDegrees));
        var up = new float3(0, 1, 0);

        for (var t = 0; t < triangleCount; t++)
        {
            var i0 = triangles[t * 3];
            var i1 = triangles[t * 3 + 1];
            var i2 = triangles[t * 3 + 2];

            var v0 = math.transform(localToWorld.Value, vertices[i0]);
            var v1 = math.transform(localToWorld.Value, vertices[i1]);
            var v2 = math.transform(localToWorld.Value, vertices[i2]);
            var n0 = normals[i0];
            var n1 = normals[i1];
            var n2 = normals[i2];

            var e0 = v1 - v0;
            var e1 = v2 - v0;
            var len0 = math.length(e0);
            var len1 = math.length(e1);

            var steps0 = math.max(1, (int)math.floor(len0 / landingArea.SpotSpacing));
            var steps1 = math.max(1, (int)math.floor(len1 / landingArea.SpotSpacing));

            for (var i = 0; i <= steps0; i++)
            {
                var a = (float)i / steps0;
                for (var j = 0; j <= steps1 - i * steps1 / steps0; j++)
                {
                    var b = (float)j / steps1;
                    if (a + b > 1f) continue;
                    var c = 1f - a - b;

                    var pos = a * v1 + b * v2 + c * v0;
                    var normal = math.normalize(a * n1 + b * n2 + c * n0);
                    var worldNormal = math.normalize(math.mul(normalMatrix, normal));

                    if (math.dot(worldNormal, up) >= upThreshold)
                    {
                        // --- UPDATED CLEARANCE CHECK ---
                        
                        // 1. Lift check origin slightly
                        float3 checkOrigin = pos + (worldNormal * landingArea.ClearanceRadius);

                        var input = new PointDistanceInput
                        {
                            Position = checkOrigin,
                            MaxDistance = landingArea.ClearanceRadius * 0.9f, 
                            Filter = landingArea.ObstacleFilter 
                        };

                        // 2. Use Custom Collector
                        var collector = new IgnoreSelfCollector(self);
                        
                        // 3. Run Query
                        // This iterates through potential hits. 
                        // It calls collector.AddHit(). If AddHit returns true, it stops.
                        collisionWorld.CalculateDistance(input, ref collector);

                        // 4. Check Result
                        // If FoundHit is true, we hit something that WASN'T us.
                        if (collector.FoundHit)
                        {
                            continue; // Blocked by foreign object
                        }
                        
                        // If we are here, we either hit nothing, or only hit ourselves.
                        // Valid Spot!
                        
                        
                        passingPoints.Add(pos);
                        passingNormals.Add(worldNormal);
                    }
                }
            }
        }

        // Deduplicate
        const float epsilon = 0.01f;
        for (int i = 0; i < passingPoints.Length; i++)
        {
            var point = passingPoints[i];
            var normal = passingNormals[i];
            var isDuplicate = false;
            
            // Simple linear check for duplicates (fine for small areas)
            for (int k = 0; k < outPositions.Length; k++)
            {
                if (math.distancesq(point, outPositions[k]) < epsilon)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
            {
                outPositions.Add(point);
                outNormals.Add(normal);
            }
        }
    }
}


// A custom collector that stops at the first hit NOT belonging to a specific entity
public struct IgnoreSelfCollector : ICollector<DistanceHit>
{
    private Entity _self;
    public bool FoundHit { get; private set; }
    public DistanceHit ClosestHit { get; private set; }

    public IgnoreSelfCollector(Entity self)
    {
        _self = self;
        FoundHit = false;
        ClosestHit = default;
        MaxFraction = 1.0f; // Check full distance
        NumHits = 0;
    }

    // Required by Interface
    public bool EarlyOutOnFirstHit => true; 
    public float MaxFraction { get; private set; }
    public int NumHits { get; private set; }

    public bool AddHit(DistanceHit hit)
    {
        // THE LOGIC:
        // If the thing we hit is US, ignore it and return false (keep searching).
        if (hit.Entity == _self)
        {
            return false; 
        }

        // If it's NOT us, we found a valid obstacle!
        ClosestHit = hit;
        FoundHit = true;
        return true; // Stop searching, we are blocked.
    }
}