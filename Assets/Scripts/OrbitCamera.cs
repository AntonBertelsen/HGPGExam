using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public class OrbitCameraRig : MonoBehaviour
{
    [Header("References")]
    public Transform target; // the thing we're orbiting
    public Transform cameraTransform; // the actual Camera (child of this object)

    [Header("Orbit Settings")]
    public float rotationSpeed = 120f; // degrees per second
    public float smoothing = 8f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 20f;
    public float minZoom = 5f;
    public float maxZoom = 60f;
    private float currentZoom = 20f;
    private float targetZoom;

    [Header("Panning Settings")]
    public float panSpeed = 10f;
    public bool allowKeyboardPan = true;
    
    [Header("Follow Settings")]
    public bool isFollowingBird = false;
    public float followSmoothing = 5f;

    private Vector3 targetPosition;
    private float yaw;
    private float pitch;
    
    // For dealing with UI sliders which should override the click drag rotate behaviour
    private bool _canRotate = false;
    private bool _canPan = false;
    
    private Entity _followedEntity = Entity.Null;
    private EntityManager _em;

    void Start()
    {
        if (!target)
        {
            Debug.LogWarning("OrbitCameraRig: No target assigned. Creating dummy target at origin.");
            GameObject dummy = new GameObject("Camera Target");
            target = dummy.transform;
        }

        if (!cameraTransform)
        {
            cameraTransform = Camera.main.transform;
        }

        targetPosition = target.position;
        targetZoom = currentZoom = (cameraTransform.localPosition.magnitude > 0f)
            ? cameraTransform.localPosition.magnitude
            : 20f;
        
        if (World.DefaultGameObjectInjectionWorld != null)
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }
    }

    void LateUpdate()
    {
        
        HandleRotation();
        HandleZoom();

        if (isFollowingBird)
        {
            UpdateFollowTarget();
        }
        else
        {
            HandlePanning();
        }
        
        // Update rig position smoothly
        float smoothVal = isFollowingBird ? followSmoothing : smoothing;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothVal);

        // Apply rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(pitch, yaw, 0), Time.deltaTime * smoothing);

        // Update camera local position (zoom)
        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            new Vector3(0, 0, -targetZoom),
            Time.deltaTime * smoothing * 1.5f
        );
    }
    
    public void ToggleFollowMode()
    {
        isFollowingBird = !isFollowingBird;

        if (isFollowingBird)
        {
            FindNewBirdTarget();
        }
        else
        {
            // When exiting follow mode, we just stay where we are (targetPosition is already set to the last known bird pos)
            _followedEntity = Entity.Null;
        }
    }

    private void FindNewBirdTarget()
    {
        // Safety check for World/EntityManager
        if (_em.World == null)
        {
            if (World.DefaultGameObjectInjectionWorld != null) 
                _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            else return;
        }

        // Query for ANY Boid
        var query = _em.CreateEntityQuery(typeof(BoidTag), typeof(LocalTransform));
        if (query.IsEmpty)
        {
            Debug.LogWarning("No birds found to follow!");
            isFollowingBird = false;
            return;
        }

        // Pick the first one (or random)
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (entities.Length > 0)
        {
            // Optional: Pick a random one so pressing 'F' repeatedly cycles through them
            int index = UnityEngine.Random.Range(0, entities.Length);
            _followedEntity = entities[index];
        }
        entities.Dispose();
    }

    private void UpdateFollowTarget()
    {
        // Check if entity is valid (it might have died or been cleared)
        if (_em.World == null || _followedEntity == Entity.Null || !_em.Exists(_followedEntity))
        {
            // Entity lost -> Return to free cam
            isFollowingBird = false;
            _followedEntity = Entity.Null;
            return;
        }

        // Read Position from ECS
        if (_em.HasComponent<LocalTransform>(_followedEntity))
        {
            float3 birdPos = _em.GetComponentData<LocalTransform>(_followedEntity).Position;
            targetPosition = birdPos;
        }
    }

    void HandleRotation()
    {
        
        if (Input.GetMouseButtonDown(0))
        {
            // If we are hovering UI, we are not allowed to rotate
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _canRotate = false;
            }
            else
            {
                _canRotate = true;
            }
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            _canRotate = false;
        }
        
        if (_canRotate &&Input.GetMouseButton(0)) // right mouse drag
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -60f, 85f);
        }
    }

    void HandleZoom()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }
        
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            targetZoom = Mathf.Clamp(targetZoom - scroll * zoomSpeed, minZoom, maxZoom);
        }
    }

    void HandlePanning()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _canPan = false;
            }
            else
            {
                _canPan = true;
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            _canPan = false;
        }
        
        // Click mouse pan
        if (_canPan && Input.GetMouseButton(1))
        {
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            Vector3 move = (-right * Input.GetAxis("Mouse X") - up * Input.GetAxis("Mouse Y")) * panSpeed * 0.02f;
            targetPosition += move;
        }

        // Keyboard panning (WASD)
        if (allowKeyboardPan)
        {
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward * 10;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward * 10;
            if (Input.GetKey(KeyCode.A)) move -= transform.right * 10;
            if (Input.GetKey(KeyCode.D)) move += transform.right * 10;
            if (Input.GetKey(KeyCode.E)) move += transform.up * 10;
            if (Input.GetKey(KeyCode.Q)) move -= transform.up * 10;
            

            // keep horizontal
            targetPosition += move * (panSpeed * 0.1f) * Time.deltaTime;
        }
    }
}