using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialHashingSystem))]

partial struct TurretSystem : ISystem
{
    private int frameCount;
    private Random _random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _random = new Random(42);
        
        state.RequireForUpdate<SpatialGridData>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gridData = SystemAPI.GetSingletonRW<SpatialGridData>();

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        // We need to use these lookups in order to read / write to entities (the cannons) inside the job
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);
        var ltwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        var bulletLookup = SystemAPI.GetComponentLookup<BulletComponent>(true); // This is kind of hacky but we need to reference this to add the explosion prefab back when overwriting the bullet position. there is definitely a better way to handle this

        var job = new TurretJob
        {
            ClusterMap = gridData.ValueRO.ClusterMap,
            Grid = gridData.ValueRO.Grid,
            TransformLookup = transformLookup,
            LtwLookup = ltwLookup,
            BulletLookup = bulletLookup,
            ECB = ecb,
            DeltaTime = SystemAPI.Time.DeltaTime,
            CurrentTime = (uint)(SystemAPI.Time.ElapsedTime * 1000.0)
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct TurretJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<int, int> ClusterMap;
    public SpatialHashGrid3D Grid;

    // We have to disable safety checks because we write to child entities (the cannons) 
    // while iterating parent entities (the turrets). This is safe to do here because the hierarchies don't overlap. (I think)
    [NativeDisableContainerSafetyRestriction] 
    public ComponentLookup<LocalTransform> TransformLookup;
    
    [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;
    
    [ReadOnly] public ComponentLookup<BulletComponent> BulletLookup;

    public EntityCommandBuffer.ParallelWriter ECB;
    public float DeltaTime;
    public uint CurrentTime;

    private void Execute(Entity entity, [EntityIndexInQuery] int sortKey, 
                         ref TurretComponent turret, 
                         ref LocalTransform transform)
    {
        var random = Random.CreateFromIndex(CurrentTime ^ (uint)sortKey);
        turret.frameCounter += random.NextInt(1, 5);

        // TARGETING //
        if (turret.frameCounter % 60 == 0)
        {
            if (turret.frameCounter > 100000)
            {
                turret.frameCounter = 0;
            }
            
            FindBestTargets(ref turret, transform.Position);
        }

        // TARGETING //
        var urHasMoved = false;
        var ulHasMoved = false;
        var dlHasMoved = false;
        var drHasMoved = false;

        if (!turret.target_UL.Equals(float3.zero))
            (turret.turret_UL_targetingDirection, turret.cannon_UL_targetingDirection, ulHasMoved) = 
                MoveTurret(turret.turret_UL_targetingDirection, turret.cannon_UL_targetingDirection, 
                           turret.turret_UL, turret.cannon_UL, turret.target_UL, transform.Rotation, true, false);

        if (!turret.target_UR.Equals(float3.zero))
            (turret.turret_UR_targetingDirection, turret.cannon_UR_targetingDirection, urHasMoved) = 
                MoveTurret(turret.turret_UR_targetingDirection, turret.cannon_UR_targetingDirection, 
                           turret.turret_UR, turret.cannon_UR, turret.target_UR, transform.Rotation, true, true);
        
        if (!turret.target_DL.Equals(float3.zero))
            (turret.turret_DL_targetingDirection, turret.cannon_DL_targetingDirection, dlHasMoved) = 
                MoveTurret(turret.turret_DL_targetingDirection, turret.cannon_DL_targetingDirection, 
                           turret.turret_DL, turret.cannon_DL, turret.target_DL, transform.Rotation, false, false);

        if (!turret.target_DR.Equals(float3.zero))
            (turret.turret_DR_targetingDirection, turret.cannon_DR_targetingDirection, drHasMoved) = 
                MoveTurret(turret.turret_DR_targetingDirection, turret.cannon_DR_targetingDirection, 
                           turret.turret_DR, turret.cannon_DR, turret.target_DR, transform.Rotation, false, true);
        
        
        turret.lastFireTime += DeltaTime;
        if (turret.lastFireTime >= turret.fireRate)
        {
            turret.lastFireTime = 0;
            if (!turret.target_UR.Equals(float3.zero) && urHasMoved) 
                FireCannon(sortKey, turret.cannon_UR, turret.bullet, turret.cannon_UR_targetingDirection);
            if (!turret.target_UL.Equals(float3.zero) && ulHasMoved)
                FireCannon(sortKey, turret.cannon_UL, turret.bullet, turret.cannon_UL_targetingDirection);
            if (!turret.target_DL.Equals(float3.zero) && dlHasMoved)
                FireCannon(sortKey, turret.cannon_DL, turret.bullet, turret.cannon_DL_targetingDirection);
            if (!turret.target_DR.Equals(float3.zero) && drHasMoved)
                FireCannon(sortKey, turret.cannon_DR, turret.bullet, turret.cannon_DR_targetingDirection);
        }

        // MOVE TURRET BASE //
        if (!turret.target_center.Equals(float3.zero)) 
        {
            float3 direction = turret.target_center - transform.Position;
            direction.y = 0;
            quaternion targetRot = quaternion.LookRotationSafe(direction, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRot, 0.1f);
            float3 forward = math.mul(transform.Rotation , new float3(0, 0, 1));
            turret.targetingDirection = forward;
        }
        
        // MOVE TURRET BASE //
    }

    private void FindBestTargets(ref TurretComponent turret, float3 turretPos)
    {
        int bestBase = 0, bestUL = 0, bestUR = 0, bestDL = 0, bestDR = 0;
        turret.target_center = float3.zero;
        turret.target_UL = float3.zero;
        turret.target_UR = float3.zero;
        turret.target_DL = float3.zero;
        turret.target_DR = float3.zero;
        
        float3 cellHalfSize = new float3(Grid.CellSize * 0.5f);

        foreach (var kvp in ClusterMap)
        {
            int key = kvp.Key;
            int count = kvp.Value;
            
            var coords = Grid.GetCoordsFromIndex(key);
            var cellPos = Grid.Origin + (new float3(coords) * Grid.CellSize) + cellHalfSize;
            var dir = cellPos - turretPos;

            if (math.length(dir) > turret.viewRadius) continue;
            
            bool forward = dir.z > 0;
            bool right = dir.y > 0;

            if (count > bestBase) { bestBase = count; turret.target_center = cellPos; }

            if (forward && right)      { if (count > bestUL) { bestUL = count; turret.target_UL = cellPos; } }
            else if (!forward && right){ if (count > bestUR) { bestUR = count; turret.target_UR = cellPos; } }
            else if (!forward && !right){ if (count > bestDL) { bestDL = count; turret.target_DL = cellPos; } }
            else                       { if (count > bestDR) { bestDR = count; turret.target_DR = cellPos; } }
        }
    }
    
    [BurstCompile]
    private (float3, float3, bool) MoveTurret(float3 turretPreviousTargetingDirection, float3 cannonPreviousTargetingDirection, 
                                               Entity turret, Entity cannon, 
                                               float3 target, quaternion motherTurretRotation, bool up, bool right)
    {
        var turretTransform = TransformLookup[turret];
        var cannonTransform = TransformLookup[cannon];
        var turretTransformWorld = LtwLookup[turret];
        var cannonTransformWorld = LtwLookup[cannon];

        var returnValue = (new float3(0, 0, 1), new float3(0, 0, 1), false);
        
        var direction = target - turretTransformWorld.Position;
        
        quaternion currentLook = quaternion.LookRotationSafe(turretPreviousTargetingDirection, math.up());
        quaternion targetLook = quaternion.LookRotationSafe(direction, math.up());
        
        var lookRotation = math.slerp(currentLook, targetLook, 0.1f); 
        float magnitude = math.length(direction);
        float3 resultingDirection = math.mul(lookRotation, new float3(0, 0, 1)) * magnitude;
        
        float3 euler = math.degrees(math.Euler(lookRotation)); 
        float3 motherEuler = math.degrees(math.Euler(motherTurretRotation));

        euler.x = 0;
        euler.y -= motherEuler.y;
        euler.z = 0;
        if ((!up && !right) || (up && right)) euler.y *= -1;
        
        var tempRotation = quaternion.Euler(math.radians(euler.x), math.radians(euler.y), math.radians(euler.z));
        
        if (euler.x > 30 || euler.x < -180)
        {
            returnValue.Item1 = turretPreviousTargetingDirection;
        }
        else 
        {
            turretTransform.Rotation = tempRotation;
            returnValue.Item1 = resultingDirection;
            TransformLookup[turret] = turretTransform;
            returnValue.Item3 = true;
        }
        
        var cannonDirection = target - cannonTransformWorld.Position;
        float originalMagnitude = math.length(cannonDirection);
        
        var cannonCurrentLook = quaternion.LookRotationSafe(cannonPreviousTargetingDirection, math.up());
        var cannonTargetLook = quaternion.LookRotationSafe(cannonDirection, math.up());
        var cannonLookRotation = math.slerp(cannonCurrentLook, cannonTargetLook, 0.1f);
        float3 cannonResultingDirection = math.mul(cannonLookRotation, new float3(0, 0, 1)) * originalMagnitude;

        float3 cannonEuler = math.degrees(math.Euler(cannonLookRotation));
        cannonEuler.y = euler.y;
        cannonEuler.z = 0;
        if (up == false)
        {
            cannonEuler.x *= -1;
        }
            
        var cannonTempRotation = quaternion.Euler(math.radians(cannonEuler.x), math.radians(cannonEuler.y), math.radians(cannonEuler.z));
        
        if (cannonEuler.x > 30 || cannonEuler.x < -180)
        {
            returnValue.Item2 = cannonPreviousTargetingDirection;
        }
        else
        {
            cannonTransform.Rotation = cannonTempRotation;
            returnValue.Item2 = cannonResultingDirection;
            TransformLookup[cannon] = cannonTransform;
        }

        return returnValue;
    }

    [BurstCompile]
    public void FireCannon(int sortKey, Entity cannon, Entity bulletPrefab, float3 aimingDir)
    {
        var cannonTransform = LtwLookup[cannon];

        Entity newEntity = ECB.Instantiate(sortKey, bulletPrefab);
        
        float3 dir = math.normalize(aimingDir);
        float3 spawnPos = cannonTransform.Position + (dir * 1.5f);

        ECB.SetComponent(sortKey, newEntity, new LocalTransform
        {
            Position = spawnPos,
            Rotation = cannonTransform.Rotation,
            Scale = 1f
        });
        
        // Hacky way to overwrite the timeToExplode and keeping the explosion reference from the prefab. Perhaps we should split it into a separate component to avoid having to pass this reference.
        var bulletDefaults = BulletLookup[bulletPrefab];
        
        ECB.SetComponent(sortKey, newEntity, new BulletComponent
        {
            timeToExplode = math.length(aimingDir) / 50f,
            explosion = bulletDefaults.explosion, 
        });

        ECB.SetComponent(sortKey, newEntity, new BulletVelocity
        {
            Value = dir * 40f
        });
    }
}