using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public struct CubeEcsRenderOwnedEntity : IComponentData
{
}

public sealed class CubeEcsFollowPrototype : MonoBehaviour
{
    private const int DefaultInstanceCount = 10000;
    private const int DefaultGridColumns = 100;
    private const int SpawnJobBatchSize = 128;
    private const float DefaultGridSpacing = 0.72f;
    private const float DefaultCubeSize = 0.48f;

    [SerializeField] private Mesh _mesh;
    [SerializeField] private Material _material;
    [SerializeField, Min(0)] private int _instanceCount = DefaultInstanceCount;
    [SerializeField, Min(1)] private int _gridColumns = DefaultGridColumns;
    [SerializeField, Min(0.01f)] private float _gridSpacing = DefaultGridSpacing;
    [SerializeField, Min(0.01f)] private float _cubeSize = DefaultCubeSize;
    [SerializeField] private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.Off;
    [SerializeField] private bool _receiveShadows;

    private World _world;

    private void Start()
    {
        CreateCubes();
    }

    private void OnDestroy()
    {
        if (_world == null || !_world.IsCreated) return;
        var ownedEntities = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CubeEcsRenderOwnedEntity>());
        _world.EntityManager.DestroyEntity(ownedEntities);
        ownedEntities.Dispose();
    }

    private void CreateCubes()
    {
        if (_mesh == null) _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (_mesh == null || _material == null)
        {
            Debug.LogError("CubeEcsFollowPrototype requires a cube mesh and material.", this);
            return;
        }

        _world = World.DefaultGameObjectInjectionWorld;
        if (_world == null || !_world.IsCreated)
        {
            Debug.LogError("CubeEcsFollowPrototype requires the default Entities world.", this);
            return;
        }

        var count = math.max(0, _instanceCount);
        if (count == 0) return;

        var entityManager = _world.EntityManager;
        var prototype = entityManager.CreateEntity();
        var renderDescription = new RenderMeshDescription(_shadowCastingMode, _receiveShadows);
        var renderMeshArray = new RenderMeshArray(new[] { _material }, new[] { _mesh });
        RenderMeshUtility.AddComponents(prototype, entityManager, renderDescription, renderMeshArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        entityManager.AddComponentData(prototype, LocalTransform.Identity);
        entityManager.AddComponentData(prototype, new LocalToWorld { Value = float4x4.identity });
        entityManager.AddComponent<CubeEcsRenderOwnedEntity>(prototype);
        entityManager.AddComponent<Prefab>(prototype);

        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        var spawnJob = new CubeEcsRenderSpawnJob
        {
            Prototype = prototype,
            CommandBuffer = commandBuffer.AsParallelWriter(),
            InstanceCount = count,
            GridColumns = math.max(1, _gridColumns),
            GridSpacing = _gridSpacing,
            CubeSize = _cubeSize,
            Origin = transform.position,
            AxisX = transform.TransformVector(Vector3.right),
            AxisZ = transform.TransformVector(Vector3.forward),
            Rotation = transform.rotation
        };

        spawnJob.Schedule(count, SpawnJobBatchSize).Complete();
        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
        entityManager.DestroyEntity(prototype);
    }
}

[BurstCompile]
public struct CubeEcsRenderSpawnJob : IJobParallelFor
{
    public Entity Prototype;
    public EntityCommandBuffer.ParallelWriter CommandBuffer;
    public int InstanceCount;
    public int GridColumns;
    public float GridSpacing;
    public float CubeSize;
    public float3 Origin;
    public float3 AxisX;
    public float3 AxisZ;
    public quaternion Rotation;

    /// <summary>Instantiates and positions one ECS-rendered cube.</summary>
    public void Execute(int index)
    {
        var entity = CommandBuffer.Instantiate(index, Prototype);
        var row = index / GridColumns;
        var rowCount = (InstanceCount + GridColumns - 1) / GridColumns;
        var columnOffset = (index % GridColumns - (GridColumns - 1) * 0.5f) * GridSpacing;
        var rowOffset = (row - (rowCount - 1) * 0.5f) * GridSpacing;
        var position = Origin + AxisX * columnOffset + AxisZ * rowOffset;
        CommandBuffer.SetComponent(index, entity, LocalTransform.FromPositionRotationScale(position, Rotation, CubeSize));
    }
}
