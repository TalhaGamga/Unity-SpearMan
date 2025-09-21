using System;
using System.Collections.Generic;

public class GenericPool<T>
{
    private readonly Stack<T> _items = new();
    private readonly Func<T> _createFunc;
    private readonly Action<T> _onGet;
    private readonly Action<T> _onRelease;
    private readonly int _maxSize;

    public int Count => _items.Count;

    public GenericPool(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null
        , int initialCapacity = 0, int maxSize = 0)
    {
        _createFunc = createFunc;
        _onGet = onGet;
        _onRelease = onRelease;
        _maxSize = maxSize;

        for (int i = 0; i < initialCapacity; i++)
        {
            _items.Push(createFunc());
        }
    }

    public T Get()
    {
        T item = _items.Count > 0 ? _items.Pop() : _createFunc();
        _onGet?.Invoke(item);
        return item;
    }

    public void Release(T item)
    {
        if (item == null)
            return;

        if (_maxSize > 0 && _items.Count >= _maxSize)
            return;
        _onRelease?.Invoke(item);
        _items.Push(item);
    }

    public void Clear() => _items.Clear();
}