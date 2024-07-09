#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Aiursoft.NiBot.Core.Util;

using DistType = Int32;

public delegate DistType CalculateDistance<in T>(T item1, T item2);

public sealed class VpTree<T>
{
    private T[] _items;
    private Node? _root;
    private readonly Random _rand; // Used in BuildFromPoints
    private CalculateDistance<T> _calculateDistance;

    public VpTree(T[] items, CalculateDistance<T> distanceCalculator)
    {
        _rand = new Random(); // Used in BuildFromPoints
        Create(items, distanceCalculator);
    }

    private void Create(T[] newItems, CalculateDistance<T> distanceCalculator)
    {
        _items = newItems;
        _calculateDistance = distanceCalculator;
        _root = BuildFromPoints(0, newItems.Length);
    }

    public IEnumerable<(T, DistType)> SearchByMaxDist(T query, int maxDist)
    {
        List<HeapItem> result = [];
        SearchByMaxd(_root, query, maxDist, result);
        return result.Select(t => (_items[t.Index], t.Dist));
    }

    private sealed class Node // This cannot be struct because Node referring to Node causes error CS0523
    {
        public int Index;
        public DistType Threshold;
        public Node? Left;
        public Node? Right;
    }

    private sealed class HeapItem(int index, DistType dist)
    {
        public readonly int Index = index;
        public readonly DistType Dist = dist;

        public static bool operator <(HeapItem h1, HeapItem h2)
        {
            return h1.Dist < h2.Dist;
        }

        public static bool operator >(HeapItem h1, HeapItem h2)
        {
            return h1.Dist > h2.Dist;
        }
    }

    private Node? BuildFromPoints(int lowerIndex, int upperIndex)
    {
        if (upperIndex == lowerIndex)
        {
            return null;
        }

        var node = new Node
        {
            Index = lowerIndex
        };

        if (upperIndex - lowerIndex > 1)
        {
            Swap(_items, lowerIndex, _rand.Next(lowerIndex + 1, upperIndex));

            var medianIndex = (upperIndex + lowerIndex) / 2;

            nth_element(_items, lowerIndex + 1, medianIndex, upperIndex - 1,
                (i1, i2) => Comparer<DistType>.Default.Compare(
                    _calculateDistance(_items[lowerIndex], i1), _calculateDistance(_items[lowerIndex], i2)));

            node.Threshold = _calculateDistance(_items[lowerIndex], _items[medianIndex]);

            node.Left = BuildFromPoints(lowerIndex + 1, medianIndex);
            node.Right = BuildFromPoints(medianIndex, upperIndex);
        }

        return node;
    }

    private void SearchByMaxd(Node? node, T target, DistType maxd, List<HeapItem> closestHits)
    {
        if (node == null)
        {
            return;
        }

        var dist = _calculateDistance(_items[node.Index], target);

        // We found entry with shorter distance
        if (dist < maxd)
        {
            // Add new hit
            closestHits.Add(new HeapItem(node.Index, dist));
        }

        if (node.Left == null && node.Right == null)
        {
            return;
        }

        if (dist - maxd <= node.Threshold)
        {
            SearchByMaxd(node.Left, target, maxd, closestHits);
        }

        if (dist + maxd >= node.Threshold)
        {
            SearchByMaxd(node.Right, target, maxd, closestHits);
        }
    }

    private static void Swap(T[] arr, int index1, int index2)
    {
        (arr[index1], arr[index2]) = (arr[index2], arr[index1]);
    }

    private static void nth_element(T[] array, int startIndex, int nthToSeek, int endIndex, Comparison<T> comparison)
    {
        var from = startIndex;
        var to = endIndex;

        // if from == to we reached the kth element
        while (from < to)
        {
            int r = from, w = to;
            var mid = array[(r + w) / 2];

            // stop if the reader and writer meets
            while (r < w)
            {
                if (comparison(array[r], mid) > -1)
                {
                    // put the large values at the end
                    (array[w], array[r]) = (array[r], array[w]);
                    w--;
                }
                else
                {
                    // the value is smaller than the pivot, skip
                    r++;
                }
            }

            // if we stepped up (r++) we need to step one down
            if (comparison(array[r], mid) > 0)
            {
                r--;
            }

            // the r pointer is on the end of the first k elements
            if (nthToSeek <= r)
            {
                to = r;
            }
            else
            {
                from = r + 1;
            }
        }
    }
}