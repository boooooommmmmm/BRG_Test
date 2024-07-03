// only used in main thread

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace BRGContainer.Runtime
{
    //@TODO: also need renderer data
    [Serializable]
    public struct BatchWorldObjectData
    {
        public List<BatchWorldObjectLODData> m_LODDatas;
        public int LODCount => m_LODDatas.Count;
        public BatchWorldObjectLODData this[int index] =>  m_LODDatas[index];
        // public BatchWorldObjectLODData this[uint index] =>  m_LODDatas[(int)index];
        public BatchWorldObjectLODData this[uint index] {
            get { return m_LODDatas[(int)index]; }
            set { m_LODDatas[(int)index] = value; }
        }
    }
    
    [Serializable]
    public struct BatchWorldObjectLODData
    {
        public readonly uint LODIndex;
        public List<BatchWorldObjectSubMeshData> m_SubmeshDatas;
        public int SubmeshCount => m_SubmeshDatas.Count;
        public BatchWorldObjectSubMeshData this[int index] =>  m_SubmeshDatas[index];
        // public BatchWorldObjectSubMeshData this[uint index] =>  m_SubmeshDatas[(int)index];
        public BatchWorldObjectSubMeshData this[uint index] {
            get { return m_SubmeshDatas[(int)index]; }
            set { m_SubmeshDatas[(int)index] = value; }
        }
    }
    
    [Serializable]
    public struct BatchWorldObjectSubMeshData
    {
        public readonly uint SubMeshIndex;
        public Mesh m_Mesh;
        public Material m_Material;
        public BatchRendererData m_RendererData;

        // private bool isRegistered => m_RendererData != default;

        public BatchWorldObjectSubMeshData(uint index, Mesh mesh, Material material)
        {
            SubMeshIndex = index;
            m_Mesh = mesh;
            m_Material = material;
            m_RendererData = default;
        }
    }
}