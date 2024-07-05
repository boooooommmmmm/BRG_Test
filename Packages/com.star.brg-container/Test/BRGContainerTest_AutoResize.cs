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
    public int m_BatchCount = 1;
    public int m_InstanceCount;
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
            for (int i = 0; i < m_BatchCount; i++)
                AddBatch((i % 2) == 0 ? i : -i);
            _isAdded = true;
        }

        if (aliveItemCount != m_InstanceCount)
        {
            OnItemCountChanged();
        }
    }


    private void AddBatch(int offset = 0)
    {
        aliveItemCount = m_InstanceCount;

        float3[] _targetPos = new float3[m_InstanceCount];

        for (int i = 0; i < m_InstanceCount; i++)
        {
            int dis = (i + 1) / 2 * 2;
            int sign = (i & 2) == 0 ? 1 : -1;
            _targetPos[i] = new float3(0, offset, dis * sign);
        }
        
        var materialProperties = new NativeArray<MaterialProperty>(1, Allocator.Temp)
        {
            [0] = MaterialProperty.Create<Color>(m_BaseColorPropertyId, true)
        };
        var batchDescription = new BatchDescription(m_InstanceCount, materialProperties, Allocator.Persistent);
        materialProperties.Dispose();

        var rendererDescription = new RendererDescription(ShadowCastingMode.On, true, false, 1, 0, MotionVectorGenerationMode.Camera);
        var testData = m_TestDatas[0];
        // LODGroupBatchHandle lodGroupBatchHandle = m_BRGContainer.AddLODGroupWithData(ref batchDescription, in rendererDescription, ref testData);
        LODGroupBatchHandle lodGroupBatchHandle = m_BRGContainer.AddEmptyLODGroup(ref batchDescription);
        m_LODGroupBatchHandles = lodGroupBatchHandle;

        // var dataBuffer = lodGroupBatchHandle.AsInstanceDataBuffer();
        // dataBuffer.SetInstanceCount(m_InstanceCount);

        for (var i = 0; i < m_InstanceCount; i++)
        {
            AddItem(i, i + 1, offset, false);
            // Debug.LogError("SetAlive: " + i);
        }
        
        lodGroupBatchHandle.Upload();
    }

    private void OnItemCountChanged()
    {
        if (m_InstanceCount > aliveItemCount)
        {
            AddItem(aliveItemCount, m_InstanceCount, 0);
            aliveItemCount = m_InstanceCount;
        }
        else if (m_InstanceCount < aliveItemCount)
        {
            int removeCount = aliveItemCount - m_InstanceCount;
            
            aliveItemCount -= RemoveItem(removeCount);
        }
    }

    public void ChangeItemCount(int count = 0)
    {
        int newCount = m_InstanceCount + count;
        if (newCount > 0)
            m_InstanceCount += count;
    }

    private void AddItem(int currentCount, int targetCount, int offset, bool flushBuffer = true)
    {
        int addCount = targetCount - currentCount;

        for (int i = 1; i <= addCount; i++)
        {
            Tuple<int, bool> addRes = m_LODGroupBatchHandles.AddAliveInstance(ref m_LODGroupBatchHandles);
            int index = addRes.Item1;
            bool lodGroupBatchHandleChanged = addRes.Item2;

            float3 targetPos = float3.zero;
            var rotation = Quaternion.identity;

            int dis = (index + 1) / 2 * 2;
            int sign = (index & 2) == 0 ? 1 : -1;
            targetPos = new float3(0, offset, dis * sign);

            var dataBuffer = m_LODGroupBatchHandles.AsInstanceDataBuffer();

            dataBuffer.SetTRS(index, targetPos, rotation, Vector3.one);
            dataBuffer.SetColor(index, m_BaseColorPropertyId, UnityEngine.Random.ColorHSV());
            int randomLOD = UnityEngine.Random.Range(0, (int)m_LODGroupBatchHandles.LODCount);

            if (!m_LODGroupBatchHandles.IsLODDataInitialized((uint)randomLOD))
            {
                Mesh mesh = m_TestDatas[0][randomLOD].m_Mesh;
                Material[] materials = m_TestDatas[0][randomLOD].m_Materials;
                RendererDescription rendererDescription = new RendererDescription(ShadowCastingMode.On, true, false, 1, 0, MotionVectorGenerationMode.Camera);
                m_LODGroupBatchHandles.RegisterLODData(in rendererDescription, (uint)randomLOD, mesh, materials);
            }
                
            m_LODGroupBatchHandles.SetInstanceActive(index, (uint)randomLOD, true);
        }

        if (flushBuffer)
            m_LODGroupBatchHandles.Upload();
    }

    private int RemoveItem(int count)
    {
        bool isRandomRemove = true;

        int totalCount = m_LODGroupBatchHandles.InstanceCount;
        int removeCount = 0;
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
                if (removeSuccess)
                    removeCount++;

                // if all items are inactive
                maxTryCount++;
                if (maxTryCount > 100)
                    break;
                // UnityEngine.Debug.LogError($"remove: {removeIndex}, status: {removeSuccess}");
            }
        }

        return removeCount;
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
        try
        {
            m_LODGroupBatchHandles.Destroy();
        }
        finally
        {
            m_BRGContainer?.Dispose();
        }
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