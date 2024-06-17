using UnityEngine.Serialization;

namespace BRGContainer.Test
{
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
    using Random = UnityEngine.Random;


    public partial class BRGContainerTest : MonoBehaviour
    {
        [Serializable]
        public struct BRGData
        {
            public Mesh mesh;
            public Material material;
        }

        private static readonly int m_ObjectToWorldPropertyId = Shader.PropertyToID("unity_ObjectToWorld");
        private static readonly int m_BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        public List<BRGData> m_BRGData = new List<BRGData>();
        public Camera MainCamera;

        public int TotalBatchCount = 10;

        private BRGContainer m_BrgContainer;
        private List<BatchHandle> m_BatchHandles;


        private JobHandle m_JobHandle;


        private BatchID[] m_BatchID;
        private BatchMeshID[] m_MeshID;
        private BatchMaterialID[] m_MaterialID;

        // Some helper constants to make calculations more convenient.
        private int m_BatchCount;
        [SerializeField] private int kNumInstances = 20000;
        [SerializeField] private int m_RowCount = 200;

        private void Start()
        {
            JobsUtility.JobWorkerCount = 10;
            m_TargetPoints = new NativeArray<float3> [TotalBatchCount];

            //initialize brg
            m_BatchHandles = new List<BatchHandle>();
            m_BatchCount = 0;

            var bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
            m_BrgContainer = new BRGContainer(bounds);

            m_BrgContainer.SetCamera(MainCamera);
        }

        NativeArray<float3>[] m_TargetPoints;
        Unity.Mathematics.Random random;
        Vector4 m_randomRange;
        private uint byteAddressObjectToWorld;
        private uint byteAddressWorldToObject;
        private uint byteAddressColor;

        private void Update()
        {
            //add batch
            if (NeedAddBatch())
            {
                int brgDataIndex = Random.Range(0, m_BRGData.Count);
                AddBatch(m_BRGData[brgDataIndex], kNumInstances);
            }

            UpdateObjPositionSync(m_BatchCount);
        }

        private unsafe void UpdateObjPositionSync(int batchCount)
        {
            var batchJobHandles = stackalloc JobHandle[batchCount];
            random = new Unity.Mathematics.Random((uint)Time.frameCount);
            
            for (int batchIndex = 0; batchIndex < batchCount; ++batchIndex)
            {
                BatchInstanceDataBuffer dataBuffer = m_BatchHandles[batchIndex].AsInstanceDataBuffer();
                BatchGroup batchGroup = m_BrgContainer.GetBatchGroup(m_BatchHandles[batchIndex].m_BatchId);
                
                var moveJob = new BRGContainerTestRandomMoveJob
                {
                    targetMovePoints = m_TargetPoints[batchIndex],
                    random = random,
                    m_DeltaTime = Time.deltaTime * 10f,
                    randomPostionRange = m_randomRange,
                    BatchGroup = batchGroup,
                    InstanceDataBuffer = dataBuffer,
                    ObjectToWorldPropertyId = m_ObjectToWorldPropertyId,
                };
                var moveJobHandle = moveJob.Schedule(kNumInstances, 64);
                batchJobHandles[batchIndex] = moveJobHandle;
            }

            var movingHandle = JobHandleUnsafeUtility.CombineDependencies(batchJobHandles, batchCount);
            movingHandle.Complete();

            for (int batchIndex = 0; batchIndex < batchCount; ++batchIndex)
            {
                m_BatchHandles[batchIndex].Upload();
            }
        }

        private int addFrequence = 1;
        private int lastAddFrame = 0;

        private bool NeedAddBatch()
        {
            if (m_BatchCount < TotalBatchCount)
            {
                if ((Time.frameCount - lastAddFrame) > addFrequence)
                {
                    lastAddFrame = Time.frameCount;
                    return true;
                }
            }

            return false;
        }

        private void AddBatch(BRGData data, int instanceCount)
        {
            m_BatchCount++;

            NativeArray<float3> _targetPos = new NativeArray<float3>(instanceCount, Allocator.Persistent);
            m_TargetPoints[m_BatchCount - 1] = _targetPos;

            random = new Unity.Mathematics.Random(1);
            var offset = new Vector3(m_RowCount, 0, Mathf.CeilToInt(kNumInstances / (float)m_RowCount)) * 0.5f;
            m_randomRange = new float4(-offset.x, offset.x, -offset.z, offset.z);
            m_randomRange *= 10;
            for (int j = 0; j < kNumInstances; j++)
            {
                var newTargetPos = new float3();
                newTargetPos.x = random.NextFloat(m_randomRange.x, m_randomRange.y);
                newTargetPos.z = random.NextFloat(m_randomRange.z, m_randomRange.w);
                _targetPos[j] = newTargetPos;
            }


            //
            var materialProperties = new NativeArray<MaterialProperty>(1, Allocator.Temp)
            {
                [0] = MaterialProperty.Create<Color>(m_BaseColorPropertyId, true)
            };
            var batchDescription = new BatchDescription(instanceCount, materialProperties, Allocator.Persistent);
            materialProperties.Dispose();

            var rendererDescription = new RendererDescription(ShadowCastingMode.On, true, false, 1, 0, MotionVectorGenerationMode.Camera);
            BatchHandle batchHandle = m_BrgContainer.AddBatch(ref batchDescription, data.mesh, 0, data.material, rendererDescription);
            m_BatchHandles.Add(batchHandle);

            var dataBuffer = batchHandle.AsInstanceDataBuffer();
            dataBuffer.SetInstanceCount(instanceCount);

            for (var i = 0; i < instanceCount; i++) // or use a IJobFor for initialization
            {
                var currentTransform = transform;
                var position = currentTransform.position + Random.insideUnitSphere * 1.0f;
                var rotation = Quaternion.Slerp(currentTransform.rotation, Random.rotation, 0.3f);

                dataBuffer.SetTRS(i, position, rotation, Vector3.one);
                dataBuffer.SetColor(i, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
            }

            batchHandle.Upload();
        }


        private void OnDisable()
        {
        }

        private void OnDestroy()
        {
            m_JobHandle.Complete();

            foreach (var batchHandle in m_BatchHandles)
            {
                batchHandle.Destroy();
            }

            foreach (var _targetPoints in m_TargetPoints)
            {
                _targetPoints.Dispose();
            }

            m_BrgContainer?.Dispose();
        }

        #region Editor

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            DrawFrustum(MainCamera);
        }

        void DrawFrustum(Camera cam)
        {
            Vector3[] nearCorners = new Vector3[4]; //Approx'd nearplane corners
            Vector3[] farCorners = new Vector3[4]; //Approx'd farplane corners
            Plane[] camPlanes = GeometryUtility.CalculateFrustumPlanes(cam); //get planes from matrix
            Plane temp = camPlanes[1];
            camPlanes[1] = camPlanes[2];
            camPlanes[2] = temp; //swap [1] and [2] so the order is better for the loop

            for (int i = 0; i < 4; i++)
            {
                nearCorners[i] = Plane3Intersect(camPlanes[4], camPlanes[i], camPlanes[(i + 1) % 4]); //near corners on the created projection matrix
                farCorners[i] = Plane3Intersect(camPlanes[5], camPlanes[i], camPlanes[(i + 1) % 4]); //far corners on the created projection matrix
            }

            for (int i = 0; i < 4; i++)
            {
                Debug.DrawLine(nearCorners[i], nearCorners[(i + 1) % 4], Color.red, Time.deltaTime, true); //near corners on the created projection matrix
                Debug.DrawLine(farCorners[i], farCorners[(i + 1) % 4], Color.blue, Time.deltaTime, true); //far corners on the created projection matrix
                Debug.DrawLine(nearCorners[i], farCorners[i], Color.green, Time.deltaTime, true); //sides of the created projection matrix
            }
        }

        Vector3 Plane3Intersect(Plane p1, Plane p2, Plane p3)
        {
            //get the intersection point of 3 planes
            return ((-p1.distance * Vector3.Cross(p2.normal, p3.normal)) +
                    (-p2.distance * Vector3.Cross(p3.normal, p1.normal)) +
                    (-p3.distance * Vector3.Cross(p1.normal, p2.normal))) /
                   (Vector3.Dot(p1.normal, Vector3.Cross(p2.normal, p3.normal)));
        }
#endif

        #endregion
    }
}