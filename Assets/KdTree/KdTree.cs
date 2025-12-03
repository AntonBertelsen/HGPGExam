using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public struct QueryResult
{
    public int Index;
    public float3 Position;
}

[BurstCompile]
public struct KdTree : IComponentData, INativeDisposable
{
    private NativeArray<float3> _data;
    private NativeArray<float3> _normals;
    private NativeArray<int> _occupied;

    // Exposed for debugging purposes
    internal NativeArray<float3> Data => _data;

    public bool IsValid(int index)
    {
        return _occupied[index] != -1;
    }

    public bool IsOccupied(int index)
    {
        return _occupied[index] != 0;
    }

    public void Occupy(int index)
    {
        if (!IsValid(index)) return;
        _occupied[index] += 1;
    }

    public void Free(int index)
    {
        if (!IsValid(index)) return;
        _occupied[index] = math.max(0, _occupied[index] - 1);
    }
    
    public float3 GetNormal(int index)
    {
        if (!IsValid(index)) return math.up();
        return _normals[index];
    }

    public QueryResult Query(float3 point)
    {
        if (_data.Length == 0)
        {
            return new QueryResult { Index = -1 };
        }

        var nearest = QueryNearest(point, 0, 0);

        if (IsOccupied(nearest.index))
        {
            return new QueryResult { Index = -1 };
        }

        return new QueryResult
        {
            Index = nearest.index,
            Position = nearest.point,
        };
    }

    private (float3 point, float distanceSq, int index) QueryNearest(float3 target, int treeIndex, int depth)
    {
        if (treeIndex >= _data.Length)
            return (float3.zero, float.MaxValue, -1);

        var currentNode = _data[treeIndex];

        var axis = depth % 3;
        var currentDistSq = math.distancesq(target, currentNode);

        var best = (point: currentNode, distanceSq: currentDistSq, index: treeIndex);

        var targetValue = target[axis];
        var nodeValue = currentNode[axis];

        var leftIndex = treeIndex * 2 + 1;
        var rightIndex = treeIndex * 2 + 2;

        int nearIndex, farIndex;
        if (targetValue < nodeValue)
        {
            nearIndex = leftIndex;
            farIndex = rightIndex;
        }
        else
        {
            nearIndex = rightIndex;
            farIndex = leftIndex;
        }

        if (nearIndex < _data.Length)
        {
            var nearResult = QueryNearest(target, nearIndex, depth + 1);
            if (nearResult.distanceSq < best.distanceSq && !IsOccupied(nearResult.index))
            {
                best = nearResult;
            }
        }

        var axisDiff = targetValue - nodeValue;
        var axisDiffSq = axisDiff * axisDiff;

        if (axisDiffSq < best.distanceSq && farIndex < _data.Length)
        {
            var farResult = QueryNearest(target, farIndex, depth + 1);
            if (farResult.distanceSq < best.distanceSq && !IsOccupied(farResult.index))
            {
                best = farResult;
            }
        }

        return best;
    }

    public static KdTree Create(NativeArray<float3> positions, NativeArray<float3> normals)
    {
        // Next multiple of two size for the tree array
        var length = positions.Length == 0 ? 0 : 1 << (int)math.ceil(math.log2(positions.Length + 1));
        var tree = new KdTree
        {
            _data = new NativeArray<float3>(length, Allocator.Persistent),
            _normals =  new NativeArray<float3>(length, Allocator.Persistent),
            _occupied = new NativeArray<int>(length, Allocator.Persistent),
        };

        var max = new float3(float.MaxValue);
        for (var i = 0; i < tree._data.Length; i++)
        {
            tree._data[i] = max;
            tree._normals[i] = math.up();
        }

        using var tempPos = new NativeArray<float3>(positions, Allocator.Temp);
        using var tempNorm = new NativeArray<float3>(normals, Allocator.Temp);
        
        BuildTree(tempPos, tempNorm, 0, tempPos.Length, 0, tree._data, tree._normals, 0);

        for (var i = 0; i < tree._occupied.Length; i++)
        {
            if (tree._data[i].Equals(max))
            {
                tree._occupied[i] = -1;
            }
        }

        return tree;
    }

    private static void BuildTree(NativeArray<float3> sourcePos, NativeArray<float3> sourceNorm, int start, int end, int depth, NativeArray<float3> treePos, NativeArray<float3> treeNorm,
        int treeIndex)
    {
        while (true)
        {
            if (start >= end || treeIndex >= treePos.Length) return;

            var axis = depth % 3;
            var count = end - start;

            if (count == 1)
            {
                treePos[treeIndex] = sourcePos[start];
                treeNorm[treeIndex] = sourceNorm[start];
                return;
            }

            var medianIndex = start + count / 2;
            QuickSelect(sourcePos, sourceNorm, start, end, medianIndex, axis);

            treePos[treeIndex] = sourcePos[medianIndex];
            treeNorm[treeIndex] = sourceNorm[medianIndex];

            BuildTree(sourcePos, sourceNorm, start, medianIndex, depth + 1, treePos, treeNorm, treeIndex * 2 + 1);
            start = medianIndex + 1;
            depth += 1;
            treeIndex = treeIndex * 2 + 2;
        }
    }

    private static void QuickSelect(NativeArray<float3> positions, NativeArray<float3> normals, int start, int end, int k, int axis)
    {
        while (start < end - 1)
        {
            var pivotIndex = Partition(positions, normals, start, end, axis);

            if (pivotIndex == k)
                return;
            if (k < pivotIndex)
                end = pivotIndex;
            else
                start = pivotIndex + 1;
        }
    }

    private static int Partition(NativeArray<float3> positions, NativeArray<float3> normals, int start, int end, int axis)
    {
        var pivotIndex = start + (end - start) / 2;
        var pivotValue = positions[pivotIndex][axis];

        Swap(positions, normals, pivotIndex, end - 1);

        var storeIndex = start;
        for (var i = start; i < end - 1; i++)
        {
            if (positions[i][axis] >= pivotValue) continue;
            Swap(positions, normals, i, storeIndex);
            storeIndex++;
        }

        Swap(positions, normals, storeIndex, end - 1);

        return storeIndex;
    }

    private static void Swap(NativeArray<float3> positions, NativeArray<float3> normals, int i, int j)
    {
        (positions[i], positions[j]) = (positions[j], positions[i]);
        (normals[i], normals[j]) = (normals[j], normals[i]);
    }

    public JobHandle Dispose(JobHandle inputDeps)
    {
        return _data.Dispose(inputDeps);
    }

    public void Dispose()
    {
        _data.Dispose();
    }
}