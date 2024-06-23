using System;
using System.Collections;
using System.Collections.Generic;
using BRGContainer;
using BRGContainer.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class BRGContainerTest_AutoResize : MonoBehaviour
{
    public int m_Count;
    public Mesh m_Mesh;
    public Material m_Material;
    public Camera MainCamera;

    private static readonly int m_ObjectToWorldPropertyId = Shader.PropertyToID("unity_ObjectToWorld");
    private static readonly int m_BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    private BRGContainer.Runtime.BRGContainer m_BRGContainer;
    private BatchHandle m_BatchHandles;


    private void Start()
    {
        var bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
        m_BRGContainer = new BRGContainer.Runtime.BRGContainer(bounds);

        m_BRGContainer.SetCamera(MainCamera);

        // AddBatch();
    }

    private bool _isAdded = false;

    private void Update()
    {
        if (_isAdded == false)
        {
            AddBatch();
            _isAdded = true;
        }
        // m_BatchHandles.Upload();

        if (m_Count > m_BatchHandles.InstanceCount)
        {
            AddItem(m_BatchHandles.InstanceCount, m_Count);
        }
    }


    private void AddBatch()
    {
        float3[] _targetPos = new float3[m_Count];

        for (int i = 0; i < m_Count; i++)
        {
            int dis = (i + 1) / 2 * 2;
            int sign = (i & 2) == 0 ? 1 : -1;
            _targetPos[i] = new float3(0, 0, dis * sign);
        }

        //
        var materialProperties = new NativeArray<MaterialProperty>(1, Allocator.Temp)
        {
            [0] = MaterialProperty.Create<Color>(m_BaseColorPropertyId, true)
        };
        var batchDescription = new BatchDescription(m_Count, materialProperties, Allocator.Persistent);
        materialProperties.Dispose();

        var rendererDescription = new RendererDescription(ShadowCastingMode.On, true, false, 1, 0, MotionVectorGenerationMode.Camera);
        BatchHandle batchHandle = m_BRGContainer.AddBatch(ref batchDescription, m_Mesh, 0, m_Material, rendererDescription);
        m_BatchHandles = batchHandle;

        var dataBuffer = batchHandle.AsInstanceDataBuffer();
        dataBuffer.SetInstanceCount(m_Count);

        BatchGroup batchGroup = m_BRGContainer.GetBatchGroup(batchHandle.m_BatchId);

        for (var i = 0; i < m_Count; i++)
        {
            var currentTransform = transform;
            var position = _targetPos[i];
            var rotation = Quaternion.identity;

            dataBuffer.SetTRS(i, position, rotation, Vector3.one);
            dataBuffer.SetColor(i, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
            batchGroup.SetPosition(i, position);
        }


        batchHandle.Upload();
    }

    private void AddItem(int currentCount, int targetCount)
    {
        
        int addCount = targetCount - currentCount;
        float3[] _targetPos = new float3[addCount];

        for (int i = currentCount; i < targetCount; i++)
        {
            int dis = (i + 1) / 2 * 2;
            int sign = (i & 2) == 0 ? 1 : -1;
            _targetPos[i - currentCount] = new float3(0, 0, dis * sign);
        }


        bool needUpdateBatchHandle = m_BRGContainer.ExtendInstanceCount(ref m_BatchHandles, addCount);
        if (needUpdateBatchHandle)
        {
            //m_BRGContainer.UpdateBatchHandle(ref m_BatchHandles);
        }
        else
        {
        }

        var dataBuffer = m_BatchHandles.AsInstanceDataBuffer();
        dataBuffer.SetInstanceCount(targetCount);

        BatchGroup batchGroup = m_BRGContainer.GetBatchGroup(m_BatchHandles.m_BatchId);

        for (var i = 0; i < addCount; i++)
        {
            int index = currentCount + i;
            var currentTransform = transform;
            var position = _targetPos[i];
            var rotation = Quaternion.identity;

            dataBuffer.SetTRS(index, position, rotation, Vector3.one);
            dataBuffer.SetColor(index, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
            batchGroup.SetPosition(index, position);
        }

        m_BatchHandles.Upload();
    }

    private void OnDestroy()
    {
        m_BatchHandles.Destroy();
        ;
        m_BRGContainer?.Dispose();
    }
}