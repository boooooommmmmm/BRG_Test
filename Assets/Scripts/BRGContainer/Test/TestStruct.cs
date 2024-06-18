//#define TEMP_TEST_MODE

#if TEMP_TEST_MODE

using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public struct BatchID: IEquatable<BatchID>
{
    public bool Equals(BatchID other)
    {
        return true;
    }
}

public struct BatchMaterialID
{
}

public struct BatchMeshID
{
}

public struct BatchFilterSettings
{
    public uint renderingLayerMask;
    public int layer;
    public MotionVectorGenerationMode motionMode;
    public ShadowCastingMode shadowCastingMode;
    public bool receiveShadows;
    public bool staticShadowCaster;
    public bool allDepthSorted;
}

public struct BatchDrawRange
{
    public uint drawCommandsBegin;
    public uint drawCommandsCount;
    public BatchFilterSettings filterSettings;
}

public struct MetadataValue
{
    public int NameID;
    public uint Value;
}

public unsafe struct BatchCullingOutput
{
    public NativeArray<BatchCullingOutputDrawCommands> drawCommands;
}

public struct BatchDrawCommand
{
    public uint visibleOffset;
    public uint visibleCount;

    public BatchID batchID;
    public BatchMaterialID materialID;
    public BatchMeshID meshID;
    public int submeshIndex;
    public int splitVisibilityMask;
    public int flags;
    public int sortingPosition;
}


public unsafe struct BatchCullingOutputDrawCommands
{
    public int visibleInstanceCount;
    public int* visibleInstances;

    public int drawRangeCount;
    public BatchDrawRange* drawRanges;

    public int drawCommandCount;
    public BatchDrawCommand* drawCommands;

    public int* drawCommandPickingInstanceIDs;
    public int* instanceSortingPositions;
    public int instanceSortingPositionFloatCount;
}

public readonly struct GraphicsBufferHandle : IEquatable<GraphicsBufferHandle>
{
    public bool Equals(GraphicsBufferHandle other)
    {
        return true;
    }
}


public static class BRGExtentions
{
    public static int GetConstantBufferMaxWindowSize(this BatchRendererGroup brg)
    {
        return 0;
    }
}

#endif