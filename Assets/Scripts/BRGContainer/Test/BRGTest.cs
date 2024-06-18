//#define TEMP_TEST_MODE

#if TEMP_TEST_MODE
#else
using System;
using System.Collections.Generic;
using BRGContainer.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BRGContainer.Test
{

    [Serializable]
    public struct BRGData
    {
        public Mesh mesh;
        public Material material;
    }

    public partial class BRGTest : MonoBehaviour
    {
        public List<BRGData> m_BRGData = new List<BRGData>();

        public Camera mainCamera;
        public Text text;
        public Text workerCountText;
        private BatchRendererGroup m_BRG;

        private GraphicsBuffer[] m_InstanceData;
        private BatchID[] m_BatchID;
        private BatchMeshID[] m_MeshID;
        private BatchMaterialID[] m_MaterialID;

        // Some helper constants to make calculations more convenient.
        private const int kSizeOfMatrix = sizeof(float) * 4 * 4;
        private const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;
        private const int kSizeOfFloat4 = sizeof(float) * 4;
        private const int kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;
        private const int kExtraBytes = kSizeOfMatrix * 2;
        private int m_BatchCount = 10;
        [SerializeField] private int kNumInstances = 20000;
        [SerializeField] private int m_RowCount = 200;
        private List<Matrix4x4[]> matrices; // Create transform matrices for three example instances.
        private List<PackedMatrix[]> objectToWorld; // Convert the transform matrices into the packed format that shaders expects.
        private List<PackedMatrix[]> worldToObject; // Also create packed inverse matrices.
        private List<Vector4[]> colors; // Make all instances have unique colors.


        private void Start()
        {
            // m_BRG = new BatchRendererGroup(this.OnPerformCullingDefault, IntPtr.Zero);
            // m_BRG = new BatchRendererGroup(this.OnPerformCullingParallel, IntPtr.Zero);
            m_BRG = new BatchRendererGroup(this.OnPerformCullingMainThread, IntPtr.Zero);

            m_BatchCount = m_BRGData.Count;
            m_BatchCount = 1; //test
            m_BatchID = new BatchID[m_BatchCount];
            m_InstanceData = new GraphicsBuffer[m_BatchCount];

            m_MeshID = new BatchMeshID[m_BatchCount];
            m_MaterialID = new BatchMaterialID[m_BatchCount];

            matrices = new List<Matrix4x4[]>();
            objectToWorld = new List<PackedMatrix[]>();
            worldToObject = new List<PackedMatrix[]>();
            colors = new List<Vector4[]>();
            m_TargetPoints = new List<float3[]>();

            random = new Unity.Mathematics.Random(1);
            var offset = new Vector3(m_RowCount, 0, Mathf.CeilToInt(kNumInstances / (float)m_RowCount)) * 0.5f;
            m_randomRange = new float4(-offset.x, offset.x, -offset.z, offset.z);
            m_randomRange *= 10;
            for (int index = 0; index < m_BatchCount; ++index)
            {
                m_MeshID[index] = m_BRG.RegisterMesh(m_BRGData[index].mesh);
                m_MaterialID[index] = m_BRG.RegisterMaterial(m_BRGData[index].material);
                AllocateInstanceDateBuffer(index);
                PopulateInstanceDataBuffer(index);

                m_TargetPoints.Add(new float3[kNumInstances]);
                for (int i = 0; i < m_TargetPoints[index].Length; i++)
                {
                    var newTargetPos = new float3();
                    newTargetPos.x = random.NextFloat(m_randomRange.x, m_randomRange.y);
                    newTargetPos.z = random.NextFloat(m_randomRange.z, m_randomRange.w);
                    m_TargetPoints[index][i] = newTargetPos;
                }
            }

            tempMatrices = new NativeArray<Matrix4x4>(matrices[0], Allocator.TempJob);
            tempTargetPoints = new NativeArray<float3>(m_TargetPoints[0], Allocator.TempJob); //worldToObject
            tempobjectToWorldArr = new NativeArray<PackedMatrix>(matrices[0].Length, Allocator.TempJob);
            tempWorldToObjectArr = new NativeArray<PackedMatrix>(matrices[0].Length, Allocator.TempJob);

            text.text = kNumInstances.ToString();
        }

        List<float3[]> m_TargetPoints;
        Unity.Mathematics.Random random;
        Vector4 m_randomRange;
        private uint byteAddressObjectToWorld;
        private uint byteAddressWorldToObject;
        private uint byteAddressColor;

        private void Update()
        {
            UpdateObjPositionSync(m_BatchCount);

            workerCountText.text = $"JobWorkerCount:{JobsUtility.JobWorkerCount}";
        }

        private void AllocateInstanceDateBuffer(int index)
        {
            m_InstanceData[index] = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                BufferCountForInstances(kBytesPerInstance, kNumInstances, kExtraBytes),
                sizeof(int));
        }

        private void RefreshData(int index, int count)
        {
            m_InstanceData[index].SetData(objectToWorld[index], 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix), count);
            m_InstanceData[index].SetData(worldToObject[index], 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix), count);
        }

        private void PopulateInstanceDataBuffer(int index)
        {
            // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
            var zero = new Matrix4x4[1] { Matrix4x4.zero };

            var offset = new Vector3(m_RowCount, 0, Mathf.CeilToInt(kNumInstances / (float)m_RowCount)) * 0.5f;
            for (int i = 0; i < m_BatchCount; i++)
            {
                matrices.Add(new Matrix4x4[kNumInstances]);
                objectToWorld.Add(new PackedMatrix[kNumInstances]);
                worldToObject.Add(new PackedMatrix[kNumInstances]);
                colors.Add(new Vector4[kNumInstances]);

                for (int j = 0; j < kNumInstances; j++)
                {
                    matrices[i][j] = Matrix4x4.Translate(new Vector3(i % m_RowCount, 0, i / m_RowCount) - offset);
                    objectToWorld[i][j] = new PackedMatrix(matrices[i][j]);
                    worldToObject[i][j] = new PackedMatrix(matrices[i][0].inverse);
                    colors[i][j] = UnityEngine.Random.ColorHSV();
                }
            }


            // In this simple example, the instance data is placed into the buffer like this:
            // Offset | Description
            //      0 | 64 bytes of zeroes, so loads from address 0 return zeroes
            //     64 | 32 uninitialized bytes to make working with SetData easier, otherwise unnecessary
            //     96 | unity_ObjectToWorld, three packed float3x4 matrices
            //    240 | unity_WorldToObject, three packed float3x4 matrices
            //    384 | _BaseColor, three float4s

            // Calculates start addresses for the different instanced properties. unity_ObjectToWorld starts at 
            // address 96 instead of 64 which means 32 bits are left uninitialized. This is because the 
            // computeBufferStartIndex parameter requires the start offset to be divisible by the size of the source
            // array element type. In this case, it's the size of PackedMatrix, which is 48.
            byteAddressObjectToWorld = kSizeOfPackedMatrix * 2;
            byteAddressWorldToObject = byteAddressObjectToWorld + kSizeOfPackedMatrix * (uint)kNumInstances;
            byteAddressColor = byteAddressWorldToObject + kSizeOfPackedMatrix * (uint)kNumInstances;

            // Upload the instance data to the GraphicsBuffer so the shader can load them.
            m_InstanceData[index].SetData(zero, 0, 0, 1);
            m_InstanceData[index].SetData(objectToWorld[index], 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix), objectToWorld[index].Length);
            m_InstanceData[index].SetData(worldToObject[index], 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix), worldToObject[index].Length);
            // m_InstanceData[index].SetData(colors[index], 0, (int)(byteAddressColor / kSizeOfFloat4), colors[index].Length);

            // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
            // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
            // Any metadata values that the shader uses and not set here will be zero. When such a value is used with
            // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
            // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer which is
            // is a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
            var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
            metadata[0] = new MetadataValue { NameID = Shader.PropertyToID("unity_ObjectToWorld"), Value = 0x80000000 | byteAddressObjectToWorld, };
            metadata[1] = new MetadataValue { NameID = Shader.PropertyToID("unity_WorldToObject"), Value = 0x80000000 | byteAddressWorldToObject, };
            // metadata[2] = new MetadataValue { NameID = Shader.PropertyToID("_BaseColor"), Value = 0x80000000 | byteAddressColor, };

            // Finally, create a batch for the instances, and make the batch use the GraphicsBuffer with the
            // instance data, as well as the metadata values that specify where the properties are. 
            m_BatchID[index] = m_BRG.AddBatch(metadata, m_InstanceData[index].bufferHandle);
        }

        // Raw buffers are allocated in ints. This is a utility method that calculates
        // the required number of ints for the data.
        int BufferCountForInstances(int bytesPerInstance, int numInstances, int extraBytes = 0)
        {
            // Round byte counts to int multiples
            bytesPerInstance = (bytesPerInstance + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            extraBytes = (extraBytes + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            int totalBytes = bytesPerInstance * numInstances + extraBytes;
            return totalBytes / sizeof(int);
        }

        private NativeArray<Matrix4x4> tempMatrices;
        private NativeArray<float3> tempTargetPoints;
        private NativeArray<PackedMatrix> tempobjectToWorldArr;
        private NativeArray<PackedMatrix> tempWorldToObjectArr;

        private void UpdateObjPositionSync(int batchCount)
        {
            for (int index = 0; index < batchCount; ++index)
            {
                // NativeArray<Matrix4x4> tempMatrices = new NativeArray<Matrix4x4>(matrices[index], Allocator.TempJob);
                // NativeArray<float3> tempTargetPoints = new NativeArray<float3>(m_TargetPoints[index], Allocator.TempJob); //worldToObject
                // NativeArray<PackedMatrix> tempobjectToWorldArr = new NativeArray<PackedMatrix>(matrices[index].Length, Allocator.TempJob);
                // NativeArray<PackedMatrix> tempWorldToObjectArr = new NativeArray<PackedMatrix>(matrices[index].Length, Allocator.TempJob);
                //
                // tempMatrices = new NativeArray<Matrix4x4>(matrices[index], Allocator.TempJob);
                // tempTargetPoints = new NativeArray<float3>(m_TargetPoints[index], Allocator.TempJob); //worldToObject
                // tempobjectToWorldArr = new NativeArray<PackedMatrix>(matrices[index].Length, Allocator.TempJob);
                // tempWorldToObjectArr = new NativeArray<PackedMatrix>(matrices[index].Length, Allocator.TempJob);
                random = new Unity.Mathematics.Random((uint)Time.frameCount);
                var moveJob = new RandomMoveJob
                {
                    matrices = tempMatrices,
                    targetMovePoints = tempTargetPoints,
                    random = random,
                    m_DeltaTime = Time.deltaTime * 10f,
                    randomPostionRange = m_randomRange,
                    obj2WorldArr = tempobjectToWorldArr,
                    world2ObjArr = tempWorldToObjectArr
                };
                var moveJobHandle = moveJob.Schedule(tempMatrices.Length, 64);
                moveJobHandle.Complete();
                matrices[index] = moveJob.matrices.ToArray();
                m_TargetPoints[index] = moveJob.targetMovePoints.ToArray();
                m_InstanceData[index].SetData(moveJob.obj2WorldArr, 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix), objectToWorld[index].Length);
                m_InstanceData[index].SetData(moveJob.world2ObjArr, 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix), worldToObject[index].Length);
                // tempMatrices.Dispose();
                // tempTargetPoints.Dispose();
                // tempobjectToWorldArr.Dispose();
                // tempWorldToObjectArr.Dispose();
            }
        }


        private void OnDisable()
        {
            m_BRG.Dispose();
        }
    }
}
#endif