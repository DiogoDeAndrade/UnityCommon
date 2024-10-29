using System;
using System.Collections;
using System.Collections.Generic;

public class ProbList<T> : IEnumerable<(T element, float weight)>
{
    private List<T>         elements = new List<T>();              // Current elements for selection
    private List<float>     weights = new List<float>();             // Current counts for selection
    private List<T>         originalElements = new List<T>();       // Backup of the original elements
    private List<float>     originalWeights = new List<float>();     // Backup of the original counts
    private Random          systemRandom;
    private bool            withReplacement;
    private float           totalWeight;

    // Constructor with optional Random generator and option for with/without replacement
    public ProbList(bool withReplacement = true, Random randomGenerator = null)
    {
        this.withReplacement = withReplacement;
        this.systemRandom = randomGenerator; // Default to System.Random if no Unity engine available
    }

    // Copy constructor that takes a collection and initializes with default counts
    public ProbList(ProbList<T> items, bool withReplacement = true, Random randomGenerator = null)
    {
        this.withReplacement = withReplacement;
        this.systemRandom = randomGenerator ?? new Random();

        foreach (var item in items)
        {
            Add(item.element, item.weight);
        }
    }

    public ProbList(T initialItem)
    {
        this.withReplacement = true;

        Add(initialItem, 1);
    }

    public int Count => originalElements.Count;
    public int CurrentCount => elements.Count;

    // Add an element with a specified occurrence count
    public void Add(T item, float weight)
    {
        if (weight <= 0) throw new ArgumentException("Count must be greater than zero.");
        if (elements.Count != originalElements.Count) Reset();

        // Check if this is a new item, or if it should just be added
        int index = elements.IndexOf(item);
        if (index == -1)
        {
            elements.Add(item);
            weights.Add(weight);
            originalElements.Add(item);
            originalWeights.Add(weight);
        }
        else
        {
            weights[index] += weight;
            originalWeights[index] += weight;
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
        if (elements.Count == 0) Reset(); // Reset if all elements were used and we're not using replacement

        int index = GetRandomWeightedIndex();
        T selectedItem = elements[index];

        // Handle without replacement logic
        if (!withReplacement)
        {
            RemoveItemAtIndex(index);
        }

        return selectedItem;
    }

    public int IndexOf(T item)
    {
        return originalElements.IndexOf(item);
    }
    public int CurrentIndexOf(T item)
    {
        return elements.IndexOf(item);
    }

    // Helper to get a random index based on weights
    private int GetRandomWeightedIndex()
    {
        // Generate random number in range of total weight
        float randomValue;
        if (systemRandom == null) randomValue = UnityEngine.Random.Range(0, totalWeight);
        else randomValue = (float)systemRandom.NextDouble() * totalWeight;

        float cumulativeWeight = 0;

        for (int i = 0; i < weights.Count; i++)
        {
            cumulativeWeight += weights[i];
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
        totalWeight -= weights[index];
        elements.RemoveAt(index);
        weights.RemoveAt(index);
    }

    // Resets the elements and counts back to their original states
    private void Reset()
    {
        elements = new List<T>(originalElements);
        weights = new(originalWeights);
        totalWeight = 0;
        foreach (var count in weights)
        {
            totalWeight += count;
        }
    }

    public IEnumerator<(T element, float weight)> GetEnumerator()
    {
        for (int i = 0; i < elements.Count; i++)
        {
            yield return (elements[i], weights[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal void Clear()
    {
        elements.Clear();
        weights.Clear();
        originalElements.Clear();
        originalElements.Clear();
        totalWeight = 0;
    }

    internal void Set(T element, float weight)
    {
        // Sets a specific element to the given value
        int index = originalElements.IndexOf(element);
        if (index == -1)
        {
            if (weight > 0) Add(element, weight);
            else return;
        } 
        else
        {
            originalWeights[index] = weight;
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
            if (originalWeights[index] == 0)
            {
                originalElements.RemoveAt(index);
                originalWeights.RemoveAt(index);
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
        int index = originalElements.IndexOf(element);
        if (index == -1) return 0;

        return originalWeights[index];
    }

    internal void Normalize()
    {        
        for (int i = 0; i < originalWeights.Count; i++)
        {
            originalWeights[i] /= totalWeight;
        }
        Reset();
    }

    internal void ReverseWeights()
    {
        for (int i = 0; i < originalWeights.Count; i++)
        {
            originalWeights[i] = 1.0f / originalWeights[i];
        }
        Reset();
    }

    internal string ToSimpleString()
    {
        string ret = "[ ";

        float w = 0.0f;
        foreach (float count in originalWeights)
        {
            w += count;
        }
        for (int i = 0; i < originalElements.Count; i++)
        {
            if (i > 0) ret += ", ";
            ret += $"({originalElements[i].ToString()}: {originalWeights[i] * 100.0f / w}%)";
        }

        ret += " ]";

        return ret;
    }
}
