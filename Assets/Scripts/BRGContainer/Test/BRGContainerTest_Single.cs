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

public class BRGContainerTest_Single : MonoBehaviour
{
    public int m_Count;
    public Mesh m_Mesh;
    public Material m_Material;
    private Camera MainCamera;

    private static readonly int m_ObjectToWorldPropertyId = Shader.PropertyToID("unity_ObjectToWorld");
    private static readonly int m_BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    
    private BRGContainer.Runtime.BRGContainer m_BrgContainer;
    private BatchHandle m_BatchHandles;
    
    
    private void Start()
    {
        
        var bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
        m_BrgContainer = new BRGContainer.Runtime.BRGContainer(bounds);

        m_BrgContainer.SetCamera(MainCamera);
    }

    private void Update()
    {
        m_BatchHandles.Upload();
    }


    private void AddBatch()
    {
        float3[] _targetPos = new float3[m_Count];

        for (int i = 0; i < m_Count; i++)
        {
            _targetPos[i] = new float3(i, 0, 0);
        }


        //
        var materialProperties = new NativeArray<MaterialProperty>(1, Allocator.Temp)
        {
            [0] = MaterialProperty.Create<Color>(m_BaseColorPropertyId, true)
        };
        var batchDescription = new BatchDescription(m_Count, materialProperties, Allocator.Persistent);
        materialProperties.Dispose();

        var rendererDescription = new RendererDescription(ShadowCastingMode.On, true, false, 1, 0, MotionVectorGenerationMode.Camera);
        BatchHandle batchHandle = m_BrgContainer.AddBatch(ref batchDescription, m_Mesh, 0, m_Material, rendererDescription);
        m_BatchHandles = (batchHandle);

        var dataBuffer = batchHandle.AsInstanceDataBuffer();
        dataBuffer.SetInstanceCount(m_Count);

        for (var i = 0; i < m_Count; i++)
        {
            var currentTransform = transform;
            var position = _targetPos[i];
            var rotation = Quaternion.identity;

            dataBuffer.SetTRS(i, position, rotation, Vector3.one);
            dataBuffer.SetColor(i, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
        }

        batchHandle.Upload();
    }
}