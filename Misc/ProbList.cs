using System;
using System.Collections;
using System.Collections.Generic;

public class ProbList<T> : IEnumerable<(T element, int count)>
{
    private List<T>         elements = new List<T>();              // Current elements for selection
    private List<int>       counts = new List<int>();             // Current counts for selection
    private List<T>         originalElements = new List<T>();       // Backup of the original elements
    private List<int>       originalCounts = new List<int>();     // Backup of the original counts
    private Random          systemRandom;
    private bool            withReplacement;
    private int             totalWeight;

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
            Add(item.element, item.count);
        }
    }

    public ProbList(T newTile)
    {
        this.withReplacement = true;

        Add(newTile, 1);
    }

    public int Count => originalElements.Count;
    public int CurrentCount => elements.Count;

    // Add an element with a specified occurrence count
    public void Add(T item, int count)
    {
        if (count <= 0) throw new ArgumentException("Count must be greater than zero.");
        if (elements.Count != originalElements.Count) Reset();

        // Check if this is a new item, or if it should just be added
        int index = elements.IndexOf(item);
        if (index == -1)
        {
            elements.Add(item);
            counts.Add(count);
            originalElements.Add(item);
            originalCounts.Add(count);
        }
        else
        {
            counts[index] += count;
            originalCounts[index] += count;
        }
        totalWeight += count;
    }

    internal void Add(ProbList<T> otherList)
    {
        foreach (var e in otherList)
        {
            Add(e.element, e.count);
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
        int randomValue;
        if (systemRandom == null) randomValue = UnityEngine.Random.Range(0, totalWeight);
        else randomValue = systemRandom.Next(0, totalWeight);

        int cumulativeWeight = 0;

        for (int i = 0; i < counts.Count; i++)
        {
            cumulativeWeight += counts[i];
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
        totalWeight -= counts[index];
        elements.RemoveAt(index);
        counts.RemoveAt(index);
    }

    // Resets the elements and counts back to their original states
    private void Reset()
    {
        elements = new List<T>(originalElements);
        counts = new List<int>(originalCounts);
        totalWeight = 0;
        foreach (int count in counts)
        {
            totalWeight += count;
        }
    }

    public IEnumerator<(T element, int count)> GetEnumerator()
    {
        for (int i = 0; i < elements.Count; i++)
        {
            yield return (elements[i], counts[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal void Clear()
    {
        elements.Clear();
        counts.Clear();
        originalElements.Clear();
        originalElements.Clear();
        totalWeight = 0;
    }

    internal void Set(T element, int count)
    {
        // Sets a specific element to the given value
        int index = originalElements.IndexOf(element);
        if (index == -1)
        {
            if (count > 0) Add(element, count);
            else return;
        } 
        else
        {
            originalCounts[index] = count;
        }

        // Reset to copy
        Reset();
    }

    internal void Cleanup()
    {
        // Remove all elements that have a zero count
        int index = 0;
        while (index < originalElements.Count)
        {
            if (originalCounts[index] == 0)
            {
                originalElements.RemoveAt(index);
                originalCounts.RemoveAt(index);
            }
            else
            {
                index++;
            }
        }

        // Reset to copy
        Reset();
    }

    internal int GetCount(T element)
    {
        int index = originalElements.IndexOf(element);
        if (index == -1) return 0;

        return originalCounts[index];
    }
}
