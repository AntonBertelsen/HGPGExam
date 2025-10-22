using Unity.Entities;
using UnityEngine;

public class GizmoSingleton : MonoBehaviour
{
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
        if (_world != null && _gizmoSystemHandle != SystemHandle.Null)
        {
            _world.Unmanaged.GetUnsafeSystemRef<GizmoSystem>(_gizmoSystemHandle).DrawGizmos();
        }
    }
}
