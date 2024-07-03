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
    public List<BatchWorldObjectData> m_TestDatas;
    public Camera MainCamera;

    private static readonly int m_ObjectToWorldPropertyId = Shader.PropertyToID("unity_ObjectToWorld");
    private static readonly int m_BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    private BRGContainer.Runtime.BRGContainer m_BRGContainer;
    private LODGroupBatchHandle m_LODGroupBatchHandles;
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
            for (int i = 0; i < 100; i++)
                AddBatch((i % 2) == 0 ? i : -i);
            // AddBatch();
            _isAdded = true;
        }

        if (aliveItemCount != m_Count)
        {
            OnItemCountChanged();
        }
    }


    private void AddBatch(int offset = 0)
    {
        aliveItemCount = m_Count;

        float3[] _targetPos = new float3[m_Count];

        for (int i = 0; i < m_Count; i++)
        {
            int dis = (i + 1) / 2 * 2;
            int sign = (i & 2) == 0 ? 1 : -1;
            _targetPos[i] = new float3(0, offset, dis * sign);
        }
        
        var materialProperties = new NativeArray<MaterialProperty>(1, Allocator.Temp)
        {
            [0] = MaterialProperty.Create<Color>(m_BaseColorPropertyId, true)
        };
        var batchDescription = new BatchDescription(m_Count, materialProperties, Allocator.Persistent);
        materialProperties.Dispose();

        var rendererDescription = new RendererDescription(ShadowCastingMode.On, true, false, 1, 0, MotionVectorGenerationMode.Camera);
        var testData = m_TestDatas[0];
        LODGroupBatchHandle lodGroupBatchHandle = m_BRGContainer.AddLODGroup(ref batchDescription, in rendererDescription, ref testData);
        m_LODGroupBatchHandles = lodGroupBatchHandle;

        var dataBuffer = lodGroupBatchHandle.AsInstanceDataBuffer();
        dataBuffer.SetInstanceCount(m_Count);

        for (var i = 0; i < m_Count; i++)
        {
            var currentTransform = transform;
            var position = _targetPos[i];
            var rotation = Quaternion.identity;

            dataBuffer.SetTRS(i, position, rotation, Vector3.one);
            dataBuffer.SetColor(i, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
            
            int randomLOD = UnityEngine.Random.Range(0, (int)lodGroupBatchHandle.LODCount);
            m_LODGroupBatchHandles.SetInstanceActive(i, (uint)randomLOD, true);

            // Debug.LogError("SetAlive: " + i);
        }
        
        lodGroupBatchHandle.Upload();
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
            int index = m_LODGroupBatchHandles.AddAliveInstance(ref m_LODGroupBatchHandles);

            float3 targetPos = float3.zero;
            var rotation = Quaternion.identity;

            int dis = (index + 1) / 2 * 2;
            int sign = (index & 2) == 0 ? 1 : -1;
            targetPos = new float3(0, 0, dis * sign);

            var dataBuffer = m_LODGroupBatchHandles.AsInstanceDataBuffer();

            dataBuffer.SetTRS(index, targetPos, rotation, Vector3.one);
            dataBuffer.SetColor(index, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
            int randomLOD = UnityEngine.Random.Range(0, (int)m_LODGroupBatchHandles.LODCount);
            m_LODGroupBatchHandles.SetInstanceActive(index, (uint)randomLOD, true);
        }

        m_LODGroupBatchHandles.Upload();
    }

    private void RemoveItem(int count)
    {
        bool isRandomRemove = true;

        int totalCount = m_LODGroupBatchHandles.InstanceCount;
        for (int i = 0; i < count; i++)
        {
            bool removeSuccess = false;
            int maxTryCount = 0;
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

                // if all items are inactive
                maxTryCount++;
                if (maxTryCount > 100)
                    break;
                // UnityEngine.Debug.LogError($"remove: {removeIndex}, status: {removeSuccess}");
            }
        }
    }

    private bool RemoveItemAtIndex(int index)
    {
        if (!m_LODGroupBatchHandles.IsInstanceActive(index))
            return false;
        else
            m_LODGroupBatchHandles.SetInstanceInactive(index);
        return true;
    }

    private void OnDestroy()
    {
        m_LODGroupBatchHandles.Destroy();
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