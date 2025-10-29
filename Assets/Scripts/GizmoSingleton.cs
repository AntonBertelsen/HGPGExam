using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

public class GizmoSingleton : MonoBehaviour
{
    public bool drawBoidGizmos = true;
    public bool drawLandingAreaGizmos = true;
    public bool drawLanderStateGizmos = true;
    public bool drawTurretGizmos = true;
    public bool drawTurretHeadGizmos = true;
    public bool drawTurretCannonGizmos = true;
    public bool drawCannonGizmos = true;

    private SystemHandle _gizmoSystemHandle;
    private World _world;

    void Start()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        if (_world != null)
        {
            _gizmoSystemHandle = _world.GetExistingSystem<GizmoSystem>();
        }
    }

    void OnDrawGizmos()
    {
        if (_world == null || _gizmoSystemHandle == SystemHandle.Null) return;

        if (drawBoidGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawBoidGizmos();
        }

        if (drawLandingAreaGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawLandingAreaGizmos();
        }

        if (drawLanderStateGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawLanderStateGizmos();
        }
        
        if (drawTurretGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawTurretGizmos();
        }
        
        if (drawTurretHeadGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawTurretHeadGizmos();
        }
        
        if (drawTurretCannonGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawTurretCannonGizmos();
        }
        
        if (drawCannonGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawCannonGizmos();
        }
    }
}