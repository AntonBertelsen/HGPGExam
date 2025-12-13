using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

public class GizmoSingleton : MonoBehaviour
{
    public bool drawBoidGizmos { get; set; } = false;
    public bool drawLandingAreaGizmos { get; set; } = false;
    public bool drawLanderStateGizmos { get; set; } = false;
    public bool drawKdTreeGizmos { get; set; } = false;
    public bool drawTurretGizmos { get; set; } = false;
    public bool drawTurretHeadGizmos { get; set; } = false;
    public bool drawTurretCannonGizmos { get; set; } = false;
    public bool drawCannonGizmos { get; set; } = false;
    public bool drawBoidGridGizmos { get; set; } = false;


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

        if (drawKdTreeGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawKdTreeGizmos();
        }

        if (drawTurretGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawTurretGizmos();
        }

        if (drawBoidGridGizmos)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawBoidGridGizmos();
        }
    }
}