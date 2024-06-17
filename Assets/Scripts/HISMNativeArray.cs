using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

// public unsafe struct HISMArrayParallelRead<T> where T : unmanaged
// {
//
// 	/// <summary>
// 	/// The data of the list.
// 	/// </summary>
// 	[NativeDisableUnsafePtrRestriction]
// 	public T* Ptr;
// 	/// <summary>
// 	/// List 的数量
// 	/// </summary>
// 	[NativeDisableUnsafePtrRestriction]
// 	public int length;
// 	public int Length => length;
//
//
// 	public unsafe HISMArrayParallelRead(HISMNativeArray<T> listData, bool formList = false)
// 	{
// 		Ptr = (T*)listData.GetUnsafePtr();
// 		length = listData.Num();
// 		if (formList) length = listData.Num();
// 	}
// 	public unsafe HISMArrayParallelRead(T[] listData)
// 	{
// 		Ptr = (T*)UnsafeUtility.AddressOf(ref listData[0]);
// 		length = listData.Length;
// 	}
// 	public unsafe HISMArrayParallelRead(void* listData, int listCount)
// 	{
// 		Ptr = (T*)listData;
// 		length = listCount;
//
// 	}
//
// 	/// <summary>
// 	/// Appends an element to the end of this list.
// 	/// </summary>
// 	/// <param name="value">The value to add to the end of this list.</param>
// 	/// <remarks>
// 	/// Increments the length by 1 unless doing so would exceed the current capacity.
// 	/// </remarks>
// 	/// <exception cref="Exception">Thrown if adding an element would exceed the capacity.</exception>
//
//
// 	public T this[int index]
// 	{
// 		[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 		get
// 		{
// 			unsafe
// 			{
// 				if (index >= length)
// 				{
// 					Debug.LogError($"裂开了，下表太大了， 需要去读取【{index}】arrayCount ：{length}");
// 					index = 0;
// 				}
// 				byte* bytePtr = (byte*)Ptr;
// 				return Ptr[index];
// 			}
// 		}
// 		[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 		set
// 		{
// 			unsafe
// 			{
// 				if (index >= length)
// 				{
// 					Debug.LogError($"裂开了，下表太大了， 需要去写入【{index}】arrayCount ：{length}");
// 					index = 0;
// 				}
// 				Ptr[index] = value;
// 			}
// 		}
//
// 	}
// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 	public T* GetPtr(int index)
// 	{
// 		if (index > length || Ptr == null)
// 		{
// 			Debug.LogError($"裂开了，下表太大了， 需要去获取【{index}】arrayCount ：{length}");
// 			index = 0;
// 		}
// 		return Ptr + index;
// 	}
//
// }

// /// <summary>
// /// 并发数组写入帮助类
// /// </summary>
// /// <typeparam name="T"></typeparam>
// public unsafe struct HISMArrayParallelWriter<T> where T : unmanaged
// {
//
// 	///// <summary>
// 	///// The data of the list.
// 	///// </summary>
// 	//[NativeDisableUnsafePtrRestriction]
// 	//public T* Ptr;
// 	///// <summary>
// 	///// List 的数量
// 	///// </summary>
// 	//[NativeDisableUnsafePtrRestriction]
// 	//public int* listCount;
//
// 	[NativeDisableUnsafePtrRestriction]
// 	HISMNativeArray<T>* listData;
//
//
// 	//public unsafe ArrayParallelWriter(HISMNativeArray<T> listData)
// 	//{
// 	//	Ptr = listData.GetUnsafePtr();
// 	//	listCount = (int*)UnsafeUtility.AddressOf(ref listData.listCount);
//
// 	//}
// 	public unsafe HISMArrayParallelWriter(HISMNativeArray<T>* _Ptr)
// 	{
// 		//Ptr = _Ptr;
// 		//listCount = _listCount;
// 		listData = _Ptr;
//
// 	}
// 	public int Length => listData != null ? listData->Num() : 0;
//
//
//
// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 	public int AddBack()
// 	{
// 		if (listData != null)
// 		{
// 			int rs = listData->Num();
// 			listData->ResizeUninitialized(rs + 1);
// 			return rs;
// 		}
// 		return 0;
// 	}
// 	public T this[int index]
// 	{
// 		[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 		get
// 		{
// 			unsafe
// 			{
// 				if (listData == null)
// 				{
// 					return default;
// 				}
// 				return *listData->GetUnsafePtr(index);
// 			}
// 		}
// 		[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 		set
// 		{
// 			unsafe
// 			{
// 				if (listData == null)
// 				{
// 					return;
// 				}
// 				listData->SetValue(index, value);
// 			}
// 		}
//
// 	}
//
// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 	public T* GetPtr(int index)
// 	{
// 		if (listData == null)
// 		{
// 			return default;
// 		}
// 		return listData->GetUnsafePtr(index);
// 	}
// }
// public unsafe struct ArrayParallelWriterNoThread<T> where T : unmanaged
// {
// 	/// <summary>
// 	/// The data of the list.
// 	/// </summary>
// 	[NativeDisableUnsafePtrRestriction]
// 	public void* Ptr;
//
// 	public long arrayCount;
// 	public HISMNativeArray<T> listData;
//
// 	public unsafe int Length
// 	{
// 		get
// 		{
// 				return listData.Num();
// 		}
// 	}
//
//
// 	public unsafe ArrayParallelWriterNoThread(HISMNativeArray<T> _listData)
// 	{
// 		Ptr = _listData.GetUnsafePtr();
// 		listData = _listData;
// 		arrayCount = listData.Num();
//
// 	}
//
// 	/// <summary>
// 	/// Appends an element to the end of this list.
// 	/// </summary>
// 	/// <param name="value">The value to add to the end of this list.</param>
// 	/// <remarks>
// 	/// Increments the length by 1 unless doing so would exceed the current capacity.
// 	/// </remarks>
// 	/// <exception cref="Exception">Thrown if adding an element would exceed the capacity.</exception>
// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 	public void AddNoResize(in T value)
// 	{
//
// 	}
//
// 	/// <summary>
// 	/// Appends elements from a buffer to the end of this list.
// 	/// </summary>
// 	/// <param name="ptr">The buffer to copy from.</param>
// 	/// <param name="count">The number of elements to copy from the buffer.</param>
// 	/// <remarks>
// 	/// Increments the length by `count` unless doing so would exceed the current capacity.
// 	/// </remarks>
// 	/// <exception cref="Exception">Thrown if adding the elements would exceed the capacity.</exception>
// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 	public void AddRangeNoResize(void* ptr, int count)
// 	{
//
// 	}
// 	/// <summary>
// 	/// 增加一个数据,会自动改变大小
// 	/// </summary>
// 	/// <param name="ptr"></param>
// 	/// <param name="count"></param>
// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 	public void AddRange(void* ptr, int count, int next_add_count = 100)
// 	{
//
// 	}
// 	/// <summary>
// 	/// 增加一个数据,会自动改变大小
// 	/// </summary>
// 	/// <param name="ptr"></param>
// 	/// <param name="count"></param>
// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 	public void Add(ref T _data, int next_add_count = 100)
// 	{
//
// 	}
// }
public unsafe class HISMNativeArray<T> : System.IDisposable where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction] private T* data;
    private int arrayLength;

    private long dataSize;

    //private void* defValue;
    /// <summary>
    /// 这个值只有当作list的时候才有效，需要外部写入
    /// </summary>
    private int listCount;

    public HISMNativeArray(int length = 8, bool resize = false, SortFunction sortAction = null, Allocator allocator = Allocator.None)
    {
        dataSize = UnsafeUtility.SizeOf<T>() * (long)length;
        data = (T*)UnsafeUtility.Malloc(dataSize, 8, Allocator.Persistent);
        if (resize)
        {
            listCount = length;
        }
        else
        {
            listCount = 0;
        }

        arrayLength = length;
        SortAction = sortAction;
    }
    
    // ReSharper disable Unity.PerformanceAnalysis
    /// <summary>
    /// 自动重置大小
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void AutoResize(int length, int next_length)
    {
        if (arrayLength < length)
        {
            if (next_length < length)
            {
                Debug.LogError("参数设置错误，next_length 要大于length ！");
                next_length = length;
            }

            long new_dataSize = UnsafeUtility.SizeOf<T>() * (long)next_length;
            var new_data = UnsafeUtility.Malloc(new_dataSize, 8, Allocator.Persistent);
            if (data != null)
            {
                UnsafeUtility.MemCpy(new_data, data, dataSize);
                UnsafeUtility.Free(data, Allocator.Persistent);
            }

            data = (T*)new_data;
            arrayLength = next_length;
            dataSize = new_dataSize;
        }
    }

    public unsafe HISMNativeArray<T> ListToArray()
    {
        if (listCount == 0)
        {
            return new HISMNativeArray<T>(0);
        }

        var ret = new HISMNativeArray<T>(listCount);
        ret.CopyFrom(GetUnsafePtr(), listCount);
        return ret;
    }

    public unsafe void ZeroMemory()
    {
        var ptr = GetUnsafePtr();
        if (ptr != null)
        {
            UnsafeUtility.MemClear(ptr, UnsafeUtility.SizeOf<T>() * Num());
        }
    }

    public unsafe void MemSet(byte value)
    {
        var ptr = GetUnsafePtr();
        if (ptr != null)
        {
            UnsafeUtility.MemSet(ptr, value, UnsafeUtility.SizeOf<T>() * Num());
        }
    }

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (data == null || index < 0 || index >= listCount)
            {
                throw new Exception("非法地址位置");
            }

            return this.data[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            {
                if (data == null || index < 0 || index >= listCount)
                {
                    throw new Exception("非法地址位置");
                }

                this.data[index] = value;
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SafeDispose()
    {
        data = null;
    }

    /// <summary>
    /// 删除
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        data = null;
        listCount = 0;
        arrayLength = 0;
    }

    /// <summary>
    /// 获取指针地址
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe T* GetUnsafePtr(int index = 0)
    {
        if (data == null || index < 0 || index >= listCount)
        {
            throw new Exception("非法地址位置");
        }

        return data + index;
    }

    public IntPtr GetIntPtr()
    {
        return new IntPtr(data);
    }

    /// <summary>
    /// 获取指针地址
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void* GetUnsafeReadOnlyPtr(int index = 0)
    {
        return GetUnsafePtr(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void CopyFrom(void* _array, int array_count)
    {
        if (array_count <= 0)
        {
            return;
        }

        AutoResize(array_count, array_count);
        if (data != null)
        {
            UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref data[0]), _array, UnsafeUtility.SizeOf<T>() * array_count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void CopyFrom(int dstIndex, int srcIndex, void* src, int length)
    {
        if (src == null || length <= 0)
        {
            return;
        }

        AutoResize(dstIndex + length, dstIndex + length);
        if (data != null)
        {
            byte* bsrc = (byte*)src;
            UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref data[dstIndex]), bsrc + UnsafeUtility.SizeOf<T>() * srcIndex, UnsafeUtility.SizeOf<T>() * length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResizeUninitialized(int length, int addCount = 0)
    {
        AutoResize(length, length + addCount);
        listCount = length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void SetValue(int index, in T value)
    {
        if (index > listCount)
        {
            Debug.LogError($"参数设置错误，index[{index}] 要小于listCount[{listCount}]！");
            return;
        }

        data[index] = value;
    }

    public void Reset()
    {
        listCount = 0;
    }

    public delegate bool SortFunction(in T l, in T r);

    public SortFunction SortAction;

    public void Sort()
    {
        if (SortAction == null) return;
        for (int i = 0; i < listCount; i++)
        {
            ref T left = ref *(data + i);
            for (int j = 0; j < listCount; j++)
            {
                ref T right = ref *(data + j);
                if (SortAction(left, right))
                {
                    (left, right) = (right, left);
                }
            }
        }
    }

    public int Num()
    {
        return listCount;
    }

    public void Add(in T value)
    {
        AutoResize(listCount + 1, Num() + 8);
        data[listCount] = value;
        listCount += 1;
    }

    public void Append(in HISMNativeArray<T> src)
    {
        CopyFrom(listCount, 0, src.GetUnsafePtr(), src.Num());
        listCount += src.Num();
    }

    public unsafe void Insert(in T item, int index)
    {
        AutoResize(listCount + 1, listCount + 1 + 10);

        UnsafeUtility.MemCpy(data + index + 1, data + index, UnsafeUtility.SizeOf<T>() * (listCount - index));
        data[index] = item;
        listCount += 1;
    }

    public void Init(in T item, int number)
    {
        listCount = 0;
        AutoResize(number, number + 10);
        for (int i = 0; i < number; i++)
        {
            data[i] = item;
        }

        listCount = number;
    }

    public T* AddUninitialized(int number)
    {
        int newCount = number += listCount;
        AutoResize(newCount, number + 10);
        listCount = newCount;
        return data + listCount - 1;
    }

    public static void Swap(ref HISMNativeArray<T> t0, ref HISMNativeArray<T> t1)
    {
        T* data = t0.data;
        int arrayLength = t0.arrayLength;
        long dataSize = t0.dataSize;
        int listCount = t0.listCount;

        t0.data = t1.data;
        t0.arrayLength = t1.arrayLength;
        t0.dataSize = t1.dataSize;
        t0.listCount = t1.listCount;


        t1.data = data;
        t1.arrayLength = arrayLength;
        t1.dataSize = dataSize;
        t1.listCount = listCount;
        // private T* data;
        // private int arrayLength;
        // private long dataSize;
        // //private void* defValue;
        // /// <summary>
        // /// 这个值只有当作list的时候才有效，需要外部写入
        // /// </summary>
        // private int listCount;
    }

    public void CreateFromRawData(byte[] rawData)
    {
        int count = rawData.Length / UnsafeUtility.SizeOf<T>();
        AutoResize(count, count + 1);
        Reset();
        AddUninitialized(count);
        UnsafeUtility.MemCpy(data, UnsafeUtility.AddressOf(ref rawData[0]), rawData.Length);
    }

    public byte[] GetRawData()
    {
        int dataSize = Num() * UnsafeUtility.SizeOf<T>();
        byte[] destData = new byte[dataSize];
        UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref destData[0]), data, dataSize);
        return destData;
    }

    public void ResetZero()
    {
        UnsafeUtility.MemSet(data, 0, listCount * UnsafeUtility.SizeOf<T>());
    }

    // public void AddUninitialized(int number,in T def)
    // {
    // 	int star = listCount;
    // 	int end = listCount + number;
    // 	AddUninitialized(number);
    // 	for (int i = star; i < end; i++)
    // 	{
    // 		data[i] = def;
    // 	}
    // }
    /// <summary>
    /// 是否创建了
    /// </summary>
    public bool IsCreated => data != null;
}