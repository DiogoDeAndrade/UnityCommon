using System;
using System.Collections.Generic;

public class PriorityQueue<T, VALUE_TYPE>
{
    private SortedDictionary<VALUE_TYPE, Queue<T>> _queue = new SortedDictionary<VALUE_TYPE, Queue<T>>();
    public int Count { get; private set; }

    public void Enqueue(T item, VALUE_TYPE priority)
    {
        if (!_queue.ContainsKey(priority))
        {
            _queue[priority] = new Queue<T>();
        }
        _queue[priority].Enqueue(item);
        Count++;
    }

    public T Dequeue()
    {
        if (Count == 0) throw new InvalidOperationException("Queue is empty");

        foreach (var pair in _queue)
        {
            if (pair.Value.Count > 0)
            {
                Count--;
                return pair.Value.Dequeue();
            }
        }
        throw new InvalidOperationException("Queue is empty");
    }
}
