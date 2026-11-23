using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using BoxCollider = UnityEngine.BoxCollider;
using CapsuleCollider = Unity.Physics.CapsuleCollider;

using UnityEngine.InputSystem;

/// <summary>Switches the camera/player between orbit, bounded free-camera, and FPS movement.</summary>
public sealed class PlayerPerspectiveController : MonoBehaviour
{
    public enum PerspectiveMode
    {
        Orbit,
        FreeCam,
        Fps
    }

    private const float FreeMoveSpeed = 12f;
    private const float FreeFastMultiplier = 4f;
    private const float MouseSensitivity = 0.12f;
    private const float PlayerHeight = 1.8f;
    private const float CameraHeight = 1.6f;
    private const float PlayerRadius = 0.35f;
    private const float WalkSpeed = 6f;
    private const float SprintSpeed = 10f;
    private const float SlideSpeed = 14f;
    private const float SlideDuration = 0.8f;
    private const float SlideDeceleration = 16f;
    private const float JumpSpeed = 7f;
    private const float WallJumpSpeed = 8f;
    private const float WallJumpUpSpeed = 7f;
    private const float SlideJumpForwardSpeed = 24f;
    private const float SlideJumpUpSpeed = 12f;
    private const float Gravity = 24f;
    private const float WallContactGrace = 0.2f;
    private const float SlideHeight = 0.9f;
    private const float SlideCameraHeight = 0.8f;
    private const float BoundaryPadding = 0.5f;
    private const float ResourceGroundProbeDistance = 0.12f;
    private static readonly Vector3 CharacterCenter = Vector3.up * (PlayerHeight * 0.5f);
    public static PlayerPerspectiveController Instance { get; private set; }
    public PerspectiveMode CurrentMode { get; private set; } = PerspectiveMode.Orbit;

    private void Awake()
    {
        Instance = this;
    }

    private Transform playerTransform;
    private Transform orbitPivot;
    private Transform cameraTransform;
    private CharacterController characterController;
    private BlasterTool blasterTool;
    private BlobAssetReference<Unity.Physics.Collider> fpsCapsuleCollider;
    private EntityQuery physicsWorldQuery;
    private float orbitDistance;
    private float yaw;
    private float pitch;
    private float verticalVelocity;
    private float slideTimeRemaining;
    private float slideSpeed;
    private float lastWallContactTime = float.NegativeInfinity;
    private Vector3 wallContactNormal;
    private Vector3 horizontalVelocity;
    private Vector2 areaMin;
    private Vector2 areaMax;
    private float ceilingHeight;
    private float groundHeight;
    private bool resourceGrounded;
    private bool cursorLocked;

    /// <summary>Initializes the perspective controller using the existing player camera rig.</summary>
    public void Initialize(BlasterTool blaster)
    {
        playerTransform = blaster.transform.parent;
        orbitPivot = playerTransform.parent;
        blasterTool = blaster;
        cameraTransform = Camera.main.transform;
        orbitDistance = -playerTransform.localPosition.z;
        var westWall = GameObject.Find("ResourceWall_West").GetComponent<BoxCollider>().bounds;
        var eastWall = GameObject.Find("ResourceWall_East").GetComponent<BoxCollider>().bounds;
        var southWall = GameObject.Find("ResourceWall_South").GetComponent<BoxCollider>().bounds;
        var northWall = GameObject.Find("ResourceWall_North").GetComponent<BoxCollider>().bounds;
        var xWalls = westWall.size.x < westWall.size.z ? new[] { westWall, eastWall } : new[] { eastWall, westWall };
        var zWalls = southWall.size.z < southWall.size.x ? new[] { southWall, northWall } : new[] { northWall, southWall };
        if (xWalls[0].center.x > xWalls[1].center.x) (xWalls[0], xWalls[1]) = (xWalls[1], xWalls[0]);
        if (zWalls[0].center.z > zWalls[1].center.z) (zWalls[0], zWalls[1]) = (zWalls[1], zWalls[0]);
        areaMin = new Vector2(xWalls[0].max.x + BoundaryPadding, zWalls[0].max.z + BoundaryPadding);
        areaMax = new Vector2(xWalls[1].min.x - BoundaryPadding, zWalls[1].min.z - BoundaryPadding);
        groundHeight = GameObject.Find("Ground").GetComponent<BoxCollider>().bounds.max.y;
        ceilingHeight = GameObject.Find("ResourceCeiling").GetComponent<BoxCollider>().bounds.min.y;
        characterController = playerTransform.gameObject.AddComponent<CharacterController>();
        characterController.height = PlayerHeight;
        characterController.radius = PlayerRadius;
        characterController.center = CharacterCenter;
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 45f;
        characterController.enabled = false;
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        physicsWorldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
        var filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = 1u, GroupIndex = 0 };
        var capsule = new CapsuleGeometry { Vertex0 = new float3(0f, PlayerRadius, 0f), Vertex1 = new float3(0f, PlayerHeight - PlayerRadius, 0f), Radius = PlayerRadius };
        fpsCapsuleCollider = Unity.Physics.CapsuleCollider.Create(capsule, filter, Unity.Physics.Material.Default);
    }

    /// <summary>Cycles from Orbit to Free Cam to FPS and back to Orbit.</summary>
    public void CycleMode()
    {
        var nextMode = (PerspectiveMode)(((int)CurrentMode + 1) % System.Enum.GetValues(typeof(PerspectiveMode)).Length);
        SetMode(nextMode);
    }

    /// <summary>Changes the movement perspective while preserving the current camera view.</summary>
    public void SetMode(PerspectiveMode mode)
    {
        if (CurrentMode == mode) return;
        if (CurrentMode == PerspectiveMode.Orbit) orbitDistance = blasterTool.OrbitDistance;
        if (mode == PerspectiveMode.Orbit) EnterOrbitMode();
        else if (mode == PerspectiveMode.FreeCam) EnterFreeCamMode();
        else EnterFpsMode();
        CurrentMode = mode;
        SetCursorLocked(mode != PerspectiveMode.Orbit);
        ToolHotbar.Instance.SetPerspectiveCrosshairVisible(mode != PerspectiveMode.Orbit);
        ToolHotbar.Instance.RefreshPerspectiveControls();
    }

    private void Update()
    {
        if (CurrentMode == PerspectiveMode.Orbit) return;
        var mouse = Mouse.current;
        if (mouse.rightButton.wasPressedThisFrame) SetCursorLocked(!cursorLocked);
        if (UIController.Instance.IsGameplayInputBlocked || !cursorLocked) return;
        UpdateLook(mouse);
        if (CurrentMode == PerspectiveMode.FreeCam) UpdateFreeCam();
        else UpdateFps();
    }

    private void UpdateLook(Mouse mouse)
    {
        var delta = mouse.delta.ReadValue() * MouseSensitivity;
        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
        playerTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void UpdateFreeCam()
    {
        var keyboard = Keyboard.current;
        var move = new Vector3(ReadAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed), ReadAxis(keyboard.spaceKey.isPressed, keyboard.qKey.isPressed || keyboard.leftCtrlKey.isPressed), ReadAxis(keyboard.wKey.isPressed, keyboard.sKey.isPressed));
        var speed = FreeMoveSpeed * (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? FreeFastMultiplier : 1f);
        var nextPosition = playerTransform.position + cameraTransform.TransformDirection(move.normalized) * speed * Time.deltaTime;
        nextPosition.x = Mathf.Clamp(nextPosition.x, areaMin.x, areaMax.x);
        nextPosition.z = Mathf.Clamp(nextPosition.z, areaMin.y, areaMax.y);
        nextPosition.y = Mathf.Clamp(nextPosition.y, groundHeight + BoundaryPadding, ceilingHeight - BoundaryPadding);
        playerTransform.position = nextPosition;
    }

    private void UpdateFps()
    {
        var keyboard = Keyboard.current;
        var input = new Vector2(ReadAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed), ReadAxis(keyboard.wKey.isPressed, keyboard.sKey.isPressed));
        var inputDirection = (playerTransform.right * input.x + playerTransform.forward * input.y).normalized;
        resourceGrounded = verticalVelocity <= 0f && HasResourceGroundSupport(playerTransform.position);
        var isGrounded = characterController.isGrounded || resourceGrounded;
        var slidePressed = keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame;
        if (slidePressed && isGrounded && inputDirection.sqrMagnitude > 0f)
        {
            slideTimeRemaining = SlideDuration;
            slideSpeed = SlideSpeed;
            characterController.height = SlideHeight;
            characterController.center = Vector3.up * (SlideHeight * 0.5f);
            cameraTransform.localPosition = Vector3.up * SlideCameraHeight;
        }
        if (slideTimeRemaining > 0f)
        {
            slideTimeRemaining = Mathf.Max(0f, slideTimeRemaining - Time.deltaTime);
            slideSpeed = Mathf.Max(WalkSpeed, slideSpeed - SlideDeceleration * Time.deltaTime);
            horizontalVelocity = inputDirection.sqrMagnitude > 0f ? inputDirection * slideSpeed : horizontalVelocity;
            if (slideTimeRemaining == 0f) RestoreStandingHeight();
        }
        else
        {
            var speed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? SprintSpeed : WalkSpeed;
            horizontalVelocity = inputDirection * speed;
        }
        var slideJump = keyboard.spaceKey.wasPressedThisFrame && isGrounded && slideTimeRemaining > 0f;
        if (slideJump)
        {
            horizontalVelocity = inputDirection.sqrMagnitude > 0f ? inputDirection * SlideJumpForwardSpeed : playerTransform.forward * SlideJumpForwardSpeed;
            verticalVelocity = SlideJumpUpSpeed;
            resourceGrounded = false;
            slideTimeRemaining = 0f;
            RestoreStandingHeight();
        }
        else if (isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (!slideJump && keyboard.spaceKey.wasPressedThisFrame && isGrounded)
        {
            verticalVelocity = JumpSpeed;
            resourceGrounded = false;
        }
        else if (!slideJump && keyboard.spaceKey.wasPressedThisFrame && Time.time - lastWallContactTime <= WallContactGrace)
        {
            horizontalVelocity = wallContactNormal * WallJumpSpeed;
            verticalVelocity = WallJumpUpSpeed;
            resourceGrounded = false;
            lastWallContactTime = float.NegativeInfinity;
        }
        verticalVelocity -= Gravity * Time.deltaTime;
        var motion = (horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;
        characterController.Move(ResolveResourceCubeCollision(motion));
        if (resourceGrounded && verticalVelocity < 0f) verticalVelocity = 0f;
        var position = playerTransform.position;
        position.x = Mathf.Clamp(position.x, areaMin.x, areaMax.x);
        position.z = Mathf.Clamp(position.z, areaMin.y, areaMax.y);
        position.y = Mathf.Max(groundHeight, position.y);
        playerTransform.position = position;
    }

    private bool HasResourceGroundSupport(Vector3 position)
    {
        var start = new float3(position.x, position.y, position.z);
        var endPosition = position + Vector3.down * ResourceGroundProbeDistance;
        var end = new float3(endPosition.x, endPosition.y, endPosition.z);
        var rotation = playerTransform.rotation;
        var orientation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
        var cast = new ColliderCastInput(fpsCapsuleCollider, start, end, orientation);
        return physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld.CastCollider(cast, out var hit) && hit.SurfaceNormal.y > 0.5f;
    }

    private Vector3 ResolveResourceCubeCollision(Vector3 motion)
    {
        var remainingMotion = motion;
        var resolvedMotion = Vector3.zero;
        for (var iteration = 0; iteration < 3 && remainingMotion.sqrMagnitude > 0.000001f; iteration++)
        {
            var currentPosition = playerTransform.position + resolvedMotion;
            var start = new float3(currentPosition.x, currentPosition.y, currentPosition.z);
            var endPosition = currentPosition + remainingMotion;
            var end = new float3(endPosition.x, endPosition.y, endPosition.z);
            var rotation = playerTransform.rotation;
            var orientation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            var cast = new ColliderCastInput(fpsCapsuleCollider, start, end, orientation);
            if (!physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld.CastCollider(cast, out var hit))
            {
                resolvedMotion += remainingMotion;
                break;
            }
            var surfaceNormal = new Vector3(hit.SurfaceNormal.x, hit.SurfaceNormal.y, hit.SurfaceNormal.z).normalized;
            var motionIntoSurface = Vector3.Dot(remainingMotion, surfaceNormal);
            if (motionIntoSurface >= 0f)
            {
                resolvedMotion += remainingMotion;
                break;
            }
            if (surfaceNormal.y > 0.5f && remainingMotion.y < 0f) resourceGrounded = true;
            if (Mathf.Abs(surfaceNormal.y) <= 0.2f)
            {
                wallContactNormal = surfaceNormal;
                lastWallContactTime = Time.time;
            }
            var travelFraction = Mathf.Max(0f, hit.Fraction - 0.01f);
            var travel = remainingMotion * travelFraction;
            resolvedMotion += travel + surfaceNormal * 0.01f;
            remainingMotion *= 1f - travelFraction;
            var intoSurface = Vector3.Dot(remainingMotion, surfaceNormal);
            if (intoSurface < 0f) remainingMotion -= surfaceNormal * intoSurface;
        }
        return resolvedMotion;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Mathf.Abs(hit.normal.y) > 0.2f) return;
        wallContactNormal = hit.normal;
        lastWallContactTime = Time.time;
    }

    private void EnterFreeCamMode()
    {
        PreserveCameraPoseAsPlayerRoot();
        characterController.enabled = false;
        cameraTransform.localPosition = Vector3.zero;
        ClampPlayerPosition(false);
        SetLookFromCamera();
    }

    private void EnterFpsMode()
    {
        PreserveCameraPoseAsPlayerRoot();
        var position = playerTransform.position;
        position.x = Mathf.Clamp(position.x, areaMin.x, areaMax.x);
        position.z = Mathf.Clamp(position.z, areaMin.y, areaMax.y);
        position.y = Mathf.Clamp(position.y - CameraHeight, groundHeight, ceilingHeight - PlayerHeight);
        playerTransform.position = position;
        cameraTransform.localPosition = Vector3.up * CameraHeight;
        characterController.enabled = true;
        verticalVelocity = 0f;
        RestoreStandingHeight();
        SetLookFromCamera();
    }

    private void EnterOrbitMode()
    {
        characterController.enabled = false;
        orbitPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        orbitPivot.position = GameController.CubeOffset;
        playerTransform.SetParent(orbitPivot, false);
        playerTransform.localPosition = Vector3.back * orbitDistance;
        playerTransform.localRotation = Quaternion.identity;
        cameraTransform.localPosition = Vector3.zero;
        cameraTransform.localRotation = Quaternion.identity;
        blasterTool.SetOrbitState(yaw, pitch, orbitDistance);
    }

    private void PreserveCameraPoseAsPlayerRoot()
    {
        var cameraPosition = cameraTransform.position;
        var cameraRotation = cameraTransform.rotation;
        playerTransform.SetParent(null, true);
        playerTransform.position = cameraPosition;
        playerTransform.rotation = Quaternion.Euler(0f, cameraRotation.eulerAngles.y, 0f);
        cameraTransform.localPosition = Vector3.zero;
        yaw = cameraRotation.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, cameraRotation.eulerAngles.x);
    }

    private void SetLookFromCamera()
    {
        playerTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void ClampPlayerPosition(bool fps)
    {
        var position = playerTransform.position;
        position.x = Mathf.Clamp(position.x, areaMin.x, areaMax.x);
        position.z = Mathf.Clamp(position.z, areaMin.y, areaMax.y);
        position.y = Mathf.Clamp(position.y, groundHeight + (fps ? CameraHeight : BoundaryPadding), ceilingHeight - BoundaryPadding);
        playerTransform.position = position;
    }

    private void RestoreStandingHeight()
    {
        characterController.height = PlayerHeight;
        characterController.center = CharacterCenter;
        cameraTransform.localPosition = Vector3.up * CameraHeight;
    }

    private static float ReadAxis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

    private void OnDestroy()
    {
        fpsCapsuleCollider.Dispose();
        physicsWorldQuery.Dispose();
    }

    private void SetCursorLocked(bool isLocked)
    {
        cursorLocked = isLocked;
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}
