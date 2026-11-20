using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class ComputeVoxelMeshGenerator : MonoBehaviour
{
    public int BufferCount = 5;
    public ComputeShader TerrainMeshShader;

    [SerializeField]
    public List<GPUVoxelMeshGenerator> GpuMeshGenerators = new();
    public List<TerrainChunk> ChunkQueue = new();
    public List<TerrainChunk> ChunkRoutineQueue = new();


    private ComputeBuffer EdgeIndexTableBuffer;
    private ComputeBuffer CornerTableBuffer;
    private ComputeBuffer TriangleTableBuffer;

    public Transform playerTransform;

    public void Awake()
    {
        SetGlobalBuffers();
        for (int i = 0; i < BufferCount; i++)
        {
            GpuMeshGenerators.Add(new GPUVoxelMeshGenerator(TerrainMeshShader, TerrainChunkManager.ChunkDataSize, CornerTableBuffer, EdgeIndexTableBuffer, TriangleTableBuffer));
        }
        StartCoroutine(AssignChunksToMeshGenerators());
    }

    public void LateUpdate()
    {
        foreach (var chunk in ChunkQueue)
        {
            if (chunk.Density.IsCreated)
                RequestMeshForChunk(chunk);
            else
                Debug.LogError($"{chunk.GetFileName} has been disposed but was trying to get generated");
        }
        ChunkQueue.Clear();
    }

    public void GenerateChunkUpdate(TerrainChunk chunk)
    {
        if(ChunkQueue.Contains(chunk)) return;
        ChunkQueue.Add(chunk);
    }

    public void RequestMeshForChunk(TerrainChunk chunk)
    {
        if(playerTransform == null)
        {

        }
        if(ChunkRoutineQueue.Contains(chunk))
            return;
        var chunkWorldPos = chunk.ChunkPositionToWorldPosition();
        var currentDiff = (chunkWorldPos - playerTransform?.position ?? Vector3.zero).sqrMagnitude;
        // 2) Insert by distance (not just vs First())
        int insertAt = 0;
        while (insertAt < ChunkRoutineQueue.Count)
        {
            var other = ChunkRoutineQueue[insertAt];
            var otherWorldPos = other.ChunkPositionToWorldPosition();
            float otherDiff = (otherWorldPos - playerTransform.position).sqrMagnitude;

            if (currentDiff < otherDiff) break;
            insertAt++;
        }

        ChunkRoutineQueue.Insert(insertAt, chunk);
    }

    public IEnumerator AssignChunksToMeshGenerators()
    {
        while (true)
        {
            if (ChunkRoutineQueue.Any())
            {
                foreach (var generator in GpuMeshGenerators)
                {
                    if (generator.IsBusy)
                        continue;
                    var nextChunk = ChunkRoutineQueue.First();
                    if(nextChunk != null)
                        StartCoroutine(generator.GenerateMeshRoutine(nextChunk));
                    ChunkRoutineQueue.RemoveAt(0);
                    break;
                }
            }
            yield return new WaitForFixedUpdate();
        }
    }

    public void OnDestroy()
    {
        foreach (var generator in GpuMeshGenerators)
        {
            generator.Release();
        }
        EdgeIndexTableBuffer.Dispose();
        CornerTableBuffer.Dispose();
        TriangleTableBuffer.Dispose();
    }

    public void SetGlobalBuffers()
    {
        var edgeIndexTableStride = sizeof(int) * 2;
        EdgeIndexTableBuffer = new ComputeBuffer(MarchingTables.bEdgeIndexTable.Length, edgeIndexTableStride);
        EdgeIndexTableBuffer.SetData(MarchingTables.bEdgeIndexTable);

        var CornerTableStride = sizeof(int) * 3;
        CornerTableBuffer = new ComputeBuffer(MarchingTables.bCornerTable.Length, CornerTableStride);
        CornerTableBuffer.SetData(MarchingTables.bCornerTable);

        TriangleTableBuffer = new ComputeBuffer(MarchingTables.bTriangleTable.Length, sizeof(ulong));
        TriangleTableBuffer.SetData(MarchingTables.bTriangleTable);
    }
}

[Serializable]
public class GPUVoxelMeshGenerator
{
    public ComputeShader TerrainMeshShader;
    
    public bool IsInitialised = false, IsBusy;

    private ComputeBuffer DensityFieldBuffer;
    private ComputeBuffer MaterialFieldBuffer;
    private ComputeBuffer VertexBuffer;
    private ComputeBuffer IndexBuffer;
    private ComputeBuffer NormalsBuffer;
    private ComputeBuffer UVBuffer;
    private ComputeBuffer UV1Buffer;
    //private ComputeBuffer ColorBuffer;

    private ComputeBuffer VertexCounter;
    private ComputeBuffer CounterReadBack;

    public GPUVoxelMeshGenerator(ComputeShader terrainMeshShader, int chunkSize, ComputeBuffer CornerTableBuffer, ComputeBuffer EdgeIndexTableBuffer, ComputeBuffer TriangleTableBuffer)
    {
        IsInitialised = true;
        TerrainMeshShader = Object.Instantiate(terrainMeshShader);

        TerrainMeshShader.SetBuffer(0, "CornerTable", CornerTableBuffer);
        TerrainMeshShader.SetBuffer(0, "EdgeIndexTable", EdgeIndexTableBuffer);
        TerrainMeshShader.SetBuffer(0, "TriangleTable", TriangleTableBuffer);
        TerrainMeshShader.SetFloat("CubeSize", TerrainChunkManager.CubeSize);
        TerrainMeshShader.SetInt("ChunkSize", chunkSize);
        TerrainMeshShader.SetInt("FieldSize", chunkSize + 1);
        TerrainMeshShader.SetInt("IsoLevel", TerrainChunkManager.IsoLevel);

        var arraySize = TerrainChunkManager.NativeArraySize;
        var vertexCountMax = arraySize * 16;

        DensityFieldBuffer = new ComputeBuffer(arraySize, sizeof(int));
        TerrainMeshShader.SetBuffer(0, "DensityBuffer", DensityFieldBuffer);
        MaterialFieldBuffer = new ComputeBuffer(arraySize, sizeof(int));
        TerrainMeshShader.SetBuffer(0, "MaterialBuffer", MaterialFieldBuffer);


        VertexCounter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
        TerrainMeshShader.SetBuffer(0, "VertexCounter", VertexCounter);

        VertexBuffer = new ComputeBuffer(vertexCountMax, sizeof(float) * 3, ComputeBufferType.Raw);
        TerrainMeshShader.SetBuffer(0, "VertexBuffer", VertexBuffer);

        NormalsBuffer = new ComputeBuffer(vertexCountMax, sizeof(float) * 3, ComputeBufferType.Raw);
        TerrainMeshShader.SetBuffer(0, "NormalBuffer", NormalsBuffer);

        IndexBuffer = new ComputeBuffer(vertexCountMax, sizeof(int), ComputeBufferType.Raw);
        TerrainMeshShader.SetBuffer(0, "IndexBuffer", IndexBuffer);

        UVBuffer = new ComputeBuffer(vertexCountMax, sizeof(float) * 2, ComputeBufferType.Raw);
        TerrainMeshShader.SetBuffer(0, "UVBuffer", UVBuffer);

        UV1Buffer = new ComputeBuffer(vertexCountMax, sizeof(float) * 2, ComputeBufferType.Raw);
        TerrainMeshShader.SetBuffer(0, "UV1Buffer", UV1Buffer);

        //ColorBuffer = new ComputeBuffer(vertexCountMax, 32, ComputeBufferType.Raw);
        //TerrainMeshShader.SetBuffer(0, "ColorBuffer", ColorBuffer);

        CounterReadBack = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
    }


    public void Release()
    {
        if (!IsInitialised) return;

        VertexCounter.Dispose();
        DensityFieldBuffer.Dispose();
        MaterialFieldBuffer.Dispose();
        VertexBuffer.Dispose();
        NormalsBuffer.Dispose();
        IndexBuffer.Dispose();
        UVBuffer.Dispose();
        UV1Buffer.Dispose();
        //ColorBuffer.Dispose();
        CounterReadBack.Dispose();
    }

    public void Dispatch(TerrainChunk chunk)
    {
        VertexCounter.SetCounterValue(0);
        VertexBuffer.SetCounterValue(0);
        NormalsBuffer.SetCounterValue(0);
        IndexBuffer.SetCounterValue(0);
        UVBuffer.SetCounterValue(0);
        UV1Buffer.SetCounterValue(0);
        //ColorBuffer.SetCounterValue(0);

        VertexCounter.SetData(new uint[] { 0 });

        DensityFieldBuffer.SetData(chunk.Density);

        MaterialFieldBuffer.SetData(chunk.Material);
        var threadcount = Mathf.CeilToInt(chunk.Size / 4);

        TerrainMeshShader.SetInts("PositionOffset", new int[] { chunk.Offset.x, chunk.Offset.y, chunk.Offset.z });

        TerrainMeshShader.Dispatch(0, threadcount, threadcount, threadcount);
    }

    public Mesh Read()
    {
        uint[] readBack = new uint[1];
        ComputeBuffer.CopyCount(VertexCounter, CounterReadBack, 0);
        CounterReadBack.GetData(readBack);
        var vertexCounter = readBack[0] * 3;

        var vertices = new Vector3[vertexCounter];
        VertexBuffer.GetData(vertices);

        var normals = new Vector3[vertexCounter];
        NormalsBuffer.GetData(normals);

        var indices = new int[vertexCounter];
        IndexBuffer.GetData(indices);

        var uv0s = new Vector2[vertexCounter];
        UVBuffer.GetData(uv0s);

        var uv1s = new Vector2[vertexCounter];
        UV1Buffer.GetData(uv1s);

        //var color = new Color32[vertexCounter];
        //ColorBuffer.GetData(color);

        //Debug.Log("Vertices: " + vertices.Length + "\n" + string.Join("\n", vertices));
        //Debug.Log("Indices: " + indices.Length + "\n" + string.Join("\n", indices));

        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        if (vertices.Length > 0 && vertices[0].sqrMagnitude > 0)
        {
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.normals = normals;
            mesh.uv = uv0s; 
            mesh.uv2 = uv1s; //Controls material
            //mesh.colors32 = color;

            //var boundsSize = chunk.Size * Vector3.one;
            //mesh.bounds = new Bounds(chunk.Offset * chunk.Size + 0.5f * boundsSize, boundsSize * 10);
            //Debug.DrawLine(mesh.bounds.center, mesh.bounds.center + mesh.bounds.size, Color.red, 20f);
            //Debug.DrawLine(mesh.bounds.center, mesh.bounds.center - mesh.bounds.size, Color.red, 20f);

            //Debug.Log("Focused Vertices: " + vertices.Length + "\n" + string.Join("\n", vertices.Where(x => x.sqrMagnitude > 0)));
            //Debug.Log("Focused Indices: " + indices.Length + "\n" + string.Join("\n", indices.Where(x => x > 0)));
        }
        return mesh;
    }

    public IEnumerator GenerateMeshRoutine(TerrainChunk chunk)
    {
        IsBusy = true;
        Dispatch(chunk);

        var isDone = false;
        AsyncGPUReadback.Request(VertexCounter, r =>
        {
            isDone = true;
        });
        yield return new WaitUntil(() => isDone);

        if (chunk) //TODO: Fix random chunks missing
            chunk.AssignMesh(Read());
        else
            Debug.Log("Tried to assign mesh to unloaded chunk: " + chunk?.Offset);
        IsBusy = false;
    }

    public void GenerateMesh(TerrainChunk chunk)
    {
        Dispatch(chunk);
        chunk.AssignMesh(Read());
    }
}
