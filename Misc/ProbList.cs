using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ProbList<T> : IEnumerable<(T element, float weight)>
{
    [Serializable]
    class Element
    {
        [SerializeField]
        public T       value;
        [SerializeField]
        public float   weight;
    }

    private List<Element>   elements = new List<Element>();          // Current elements for selection
    [SerializeField]
    private List<Element>   originalElements = new List<Element>();       // Backup of the original elements
    private System.Random   systemRandom;
    [SerializeField]
    private bool            withReplacement = true;
    private bool            resetWhenEmpty = true;
    private float           totalWeight;
    private bool            init = false;

    // Constructor with optional Random generator and option for with/without replacement
    public ProbList(bool withReplacement = true, bool resetWhenEmpty = true, System.Random randomGenerator = null)
    {
        this.withReplacement = withReplacement;
        this.resetWhenEmpty = resetWhenEmpty;
        this.systemRandom = randomGenerator; // Default to System.Random if no Unity engine available
    }

    // Copy constructor that takes a collection and initializes with default counts
    public ProbList(ProbList<T> items, bool withReplacement = true, bool resetWhenEmpty = true, System.Random randomGenerator = null)
    {
        this.withReplacement = withReplacement;
        this.resetWhenEmpty = resetWhenEmpty;
        this.systemRandom = randomGenerator ?? new System.Random();

        foreach (var item in items)
        {
            Add(item.element, item.weight);
        }
    }

    public ProbList(T initialItem)
    {
        this.withReplacement = true;
        this.resetWhenEmpty = true;

        Add(initialItem, 1);
    }

    public int Count => originalElements.Count;
    public int CurrentCount => elements.Count;

    // Add an element with a specified occurrence count
    public void Add(T item, float weight)
    {
        if (weight <= 0) throw new ArgumentException("Count must be greater than zero.");

        // Check if this is a new item, or if it should just be added
        int index = IndexOf(item);
        if (index == -1)
        {
            originalElements.Add(new Element { value = item, weight = weight });
        }
        else
        {
            originalElements[index].weight += weight;
        }
        totalWeight += weight;
    }

    internal void Add(ProbList<T> otherList)
    {
        foreach (var e in otherList)
        {
            Add(e.element, e.weight);
        }
    }

    // Get an element based on probability
    public T Get()
    {
        if (!init) Reset();
        else if (elements.Count == 0)
        {
            if (resetWhenEmpty) Reset();
            else return default(T);
        }

        int index = GetRandomWeightedIndex();
        T selectedItem = elements[index].value;

        // Handle without replacement logic
        if (!withReplacement)
        {
            RemoveItemAtIndex(index);
        }

        return selectedItem;
    }

    public int IndexOf(T item)
    {
        for (int i = 0; i < originalElements.Count; i++)
        {
            if (originalElements[i].value.Equals(item)) return i;
        }
        return -1;
    }
    public int CurrentIndexOf(T item)
    {
        if (!init) Reset();

        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].value.Equals(item)) return i;
        }
        return -1;
    }

    // Helper to get a random index based on weights
    private int GetRandomWeightedIndex()
    {
        // Generate random number in range of total weight
        float randomValue;
        if (systemRandom == null) randomValue = UnityEngine.Random.Range(0, totalWeight);
        else randomValue = (float)systemRandom.NextDouble() * totalWeight;

        float cumulativeWeight = 0;

        for (int i = 0; i < elements.Count; i++)
        {
            cumulativeWeight += elements[i].weight;
            if (randomValue < cumulativeWeight)
            {
                return i;
            }
        }

        throw new InvalidOperationException("Should never reach here.");
    }

    // Helper to remove an item at the specified index
    private void RemoveItemAtIndex(int index)
    {
        totalWeight -= elements[index].weight;
        elements.RemoveAt(index);
    }

    // Resets the elements and counts back to their original states
    private void Reset()
    {
        elements = new List<Element>(originalElements);
        totalWeight = 0;
        foreach (var element in elements)
        {
            totalWeight += element.weight;
        }

        init = true;
    }

    public IEnumerator<(T element, float weight)> GetEnumerator()
    {
        if ((!init) || ((elements.Count == 0) && (originalElements.Count != 0) && (resetWhenEmpty))) Reset();

        for (int i = 0; i < elements.Count; i++)
        {
            yield return (elements[i].value, elements[i].weight);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal void Clear()
    {
        elements.Clear();
        originalElements.Clear();
        totalWeight = 0;
    }

    internal void Set(T element, float weight)
    {
        // Sets a specific element to the given value
        int index = IndexOf(element);
        if (index == -1)
        {
            if (weight > 0) Add(element, weight);
            else return;
        } 
        else
        {
            originalElements[index].weight = weight;
        }

        // Reset to copy
        Reset();
    }

    internal void Cleanup()
    {
        // Remove all elements that have a zero weight
        int index = 0;
        while (index < originalElements.Count)
        {
            if (originalElements[index].weight == 0)
            {
                originalElements.RemoveAt(index);
            }
            else
            {
                index++;
            }
        }

        // Reset to copy
        Reset();
    }

    internal float GetWeight(T element)
    {
        int index = IndexOf(element);
        if (index == -1) return 0;

        return originalElements[index].weight;
    }

    internal void Normalize()
    {        
        for (int i = 0; i < originalElements.Count; i++)
        {
            originalElements[i].weight /= totalWeight;
        }
        Reset();
    }

    internal void ReverseWeights()
    {
        for (int i = 0; i < originalElements.Count; i++)
        {
            originalElements[i].weight = 1.0f / originalElements[i].weight;
        }
        Reset();
    }

    public List<(T element, float weight)> GetTopN(int n)
    {
        if ((!init) || ((elements.Count == 0) && (originalElements.Count != 0) && (resetWhenEmpty))) Reset();

        if (n <= 0)
            throw new ArgumentException("N must be greater than zero.");

        if (n > elements.Count)
            n = elements.Count;

        // Create a list of tuples with elements and their weights
        return elements
            .OrderByDescending(e => e.weight)
            .Take(n)
            .Select(e => (e.value, e.weight))
            .ToList();
    }

    public void RemoveAll(Predicate<T> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        // Keep track of total weight adjustment
        float weightAdjustment = 0;

        // Remove from original elements
        int index = 0;
        while (index < originalElements.Count)
        {
            if (predicate(originalElements[index].value))
            {
                weightAdjustment += originalElements[index].weight;
                originalElements.RemoveAt(index);
            }
            else
            {
                index++;
            }
        }

        // Update total weight
        totalWeight -= weightAdjustment;

        // If we're already initialized, update the current elements list too
        if (init)
        {
            index = 0;
            while (index < elements.Count)
            {
                if (predicate(elements[index].value))
                {
                    elements.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }
    }

    internal string ToSimpleString()
    {
        string ret = "[ ";

        float w = 0.0f;
        foreach (var element in originalElements)
        {
            w += element.weight;
        }
        for (int i = 0; i < originalElements.Count; i++)
        {
            if (i > 0) ret += ", ";
            ret += $"({originalElements[i].value.ToString()}: {originalElements[i].weight * 100.0f / w}%)";
        }

        ret += " ]";

        return ret;
    }

    public List<T> KMeans(int n, Func<T, T, float> distanceFunc, int maxIterations = 10)
    {
        // Ensure there are at least `n` elements to choose from
        if (Count < n) throw new ArgumentException("Not enough elements to form the specified number of clusters.");

        // Initialize centroids with random elements from the list of highest-weighted items
        List<T> centroids = originalElements
            .OrderByDescending(e => e.weight)
            .Take(n)
            .Select(e => e.value)
            .ToList();

        List<List<(T element, float weight)>> clusters = new List<List<(T, float)>>();

        // Repeat clustering until convergence or for a fixed number of iterations
        for (int iteration = 0; iteration < maxIterations; iteration++) 
        {
            // Initialize clusters for this iteration
            clusters = Enumerable.Range(0, n).Select(_ => new List<(T, float)>()).ToList();

            // Assign each element to the nearest centroid
            foreach (var (element, weight) in this)
            {
                int closestCentroidIndex = GetClosestCentroid(element, centroids, distanceFunc);
                clusters[closestCentroidIndex].Add((element, weight));
            }

            // Update centroids based on the mean of each cluster
            for (int i = 0; i < n; i++)
            {
                if (clusters[i].Count > 0)
                {
                    centroids[i] = GetClusterMean(clusters[i], distanceFunc);
                }
            }
        }

        // Select the representative for each cluster based on the highest weight
        return clusters
            .Where(cluster => cluster.Count > 0)
            .Select(cluster => cluster.OrderByDescending(e => e.weight).First().element)
            .ToList();
    }

    private int GetClosestCentroid(T element, List<T> centroids, Func<T, T, float> distanceFunc)
    {
        return centroids
            .Select((centroid, index) => (index, distance: distanceFunc(element, centroid)))
            .OrderBy(result => result.distance)
            .First().index;
    }

    private T GetClusterMean(List<(T element, float weight)> cluster, Func<T, T, float> distanceFunc)
    {
        // Since T can be any type, we cannot calculate a true "mean."
        // Instead, choose the element that minimizes the sum of distances to all others in the cluster
        return cluster
            .OrderBy(e => cluster.Sum(c => distanceFunc(e.element, c.element) * c.weight))
            .First().element;
    }

    public List<T> GetAdaptiveTopN(int n, Func<T, T, float> distanceFunc)
    {
        // Ensure there are enough colors in the histogram
        if (Count < n) throw new ArgumentException($"Not enough elements to form the specified number of colors (Count = {Count}, n = {n}).");

        // Step 1: Start with the color with the highest weight
        var selectedColors = new List<T>();
        var sortedElements = originalElements.OrderByDescending(e => e.weight).ToList();
        selectedColors.Add(sortedElements[0].value);

        // Step 2: Iteratively add the most distinct color
        while (selectedColors.Count < n)
        {
            T bestCandidate = default;
            float bestScore = float.NegativeInfinity;

            foreach (var candidate in sortedElements)
            {
                if (selectedColors.Contains(candidate.value)) continue;

                // Calculate distance from candidate to all selected colors
                float minDistance = selectedColors.Min(selectedColor => distanceFunc(candidate.value, selectedColor));
                float score = minDistance * candidate.weight; // Balance diversity and prominence

                // Choose the candidate with the best combined score
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate.value;
                }
            }

            if (bestCandidate != null)
            {
                selectedColors.Add(bestCandidate);
            }
        }

        return selectedColors;
    }
}

#if UNITY_6000_0_OR_NEWER

[Serializable]
public class AudioClipProbList : ProbList<AudioClip> { }

#endif
