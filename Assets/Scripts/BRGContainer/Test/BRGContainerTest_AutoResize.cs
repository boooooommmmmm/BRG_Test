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
using MaterialProperty = BRGContainer.Runtime.MaterialProperty;
using Random = Unity.Mathematics.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
    private int aliveItemCount = 0;


    private void Start()
    {
        var bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
        m_BRGContainer = new BRGContainer.Runtime.BRGContainer(bounds);

        m_BRGContainer.SetMainCamera(MainCamera);

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

        if (aliveItemCount != m_Count)
        {
            OnItemCountChanged();
        }
    }


    private void AddBatch()
    {
        aliveItemCount = m_Count;
        
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
        
        for (var i = 0; i < m_Count; i++)
        {
            var currentTransform = transform;
            var position = _targetPos[i];
            var rotation = Quaternion.identity;

            dataBuffer.SetTRS(i, position, rotation, Vector3.one);
            dataBuffer.SetColor(i, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
            m_BatchHandles.SetPosition(i, position);
            m_BatchHandles.SetInstanceAlive(i, true);
            
            // Debug.LogError("SetAlive: " + i);
        }


        batchHandle.Upload();
    }

    private void OnItemCountChanged()
    {
        if (m_Count > aliveItemCount)
        {
            AddItem(aliveItemCount, m_Count);
            aliveItemCount = m_Count;
        }
        else if (m_Count < aliveItemCount)
        {
            int removeCount = aliveItemCount - m_Count;
            RemoveItem(removeCount);
            aliveItemCount -= removeCount;
        }
    }
    
    public void ChangeItemCount(int count = 0)
    {
        m_Count += count;
    }

    private void AddItem(int currentCount, int targetCount)
    {
        int addCount = targetCount - currentCount;

        for (int i = 1; i <= addCount; i++)
        {
            int index = m_BatchHandles.AddAliveInstance(ref m_BatchHandles);

            float3 targetPos = float3.zero;
            var rotation = Quaternion.identity;
            
            int dis = (index + 1) / 2 * 2;
            int sign = (index & 2) == 0 ? 1 : -1;
            targetPos = new float3(0, 0, dis * sign);
            
            var dataBuffer = m_BatchHandles.AsInstanceDataBuffer();
            
            dataBuffer.SetTRS(index, targetPos, rotation, Vector3.one);
            dataBuffer.SetColor(index, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
            m_BatchHandles.SetPosition(index, targetPos);
            m_BatchHandles.SetInstanceAlive(index, true);
        }

        m_BatchHandles.Upload();
    }

    private void RemoveItem(int count)
    {
        bool isRandomRemove = true;

        int totalCount = m_BatchHandles.InstanceCount;
        for (int i = 0; i < count; i++)
        {
            bool removeSuccess = false;
            while (!removeSuccess)
            {
                int removeIndex = 0;
                if (isRandomRemove)
                {
                    removeIndex = UnityEngine.Random.Range(0, totalCount);
                }
                else
                {
                    removeIndex = aliveItemCount - 1;
                }
                removeSuccess = RemoveItemAtIndex(removeIndex);
                // UnityEngine.Debug.LogError($"remove: {removeIndex}, status: {removeSuccess}");
            }
        }
    }

    private bool RemoveItemAtIndex(int index)
    {
        if (!m_BatchHandles.IsInstanceAlive(index))
            return false;
        else
            m_BatchHandles.SetInstanceAlive(index, false);
        return true;
    }

    private void OnDestroy()
    {
        m_BatchHandles.Destroy();
        m_BRGContainer?.Dispose();
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(BRGContainerTest_AutoResize))]
public class BRGContainerTest_AutoResize_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        BRGContainerTest_AutoResize testScript = (BRGContainerTest_AutoResize)target;
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-"))
            testScript.ChangeItemCount(-1);
        if (GUILayout.Button("+"))
            testScript.ChangeItemCount(1);
        
        GUILayout.EndHorizontal();
    }
}
#endif