using System;
using System.Collections.Generic;

namespace builtin.tools;

public class LowerBoundsSortedDictionary<TKey, TValue> : SortedDictionary<TKey, TValue> where TKey : IEquatable<TKey> 
{

    private ComparerDecorator<TKey> _comparerDecorator;

    private class ComparerDecorator<T> : IComparer<T> where T : IEquatable<T>
    {

        private IComparer<T> _comparer;

        public T LowerBound { get; private set; }
        public T UpperBound { get; private set; }
        
        private bool _reset = true;
        private T _key;

        public void Reset(T key)
        {
            _key = key;
            _reset = true;
        }

        public ComparerDecorator(IComparer<T> comparer)
        {
            _comparer = comparer;
        }

        public int Compare(T x, T y)
        {
            int num = _comparer.Compare(x, y);

            if (_reset)
            {
                if (!x.Equals(_key)) UpperBound = x;
                if (!y.Equals(_key)) LowerBound = y;
            }

            if (num >= 0)
            {
                if (!x.Equals(_key)) UpperBound = x;
                if (!y.Equals(_key)) LowerBound = y;
                _reset = false;
            }

            if (num <= 0)
            {
                if (!x.Equals(_key)) LowerBound = x;
                if (!y.Equals(_key)) UpperBound = y;
                _reset = false;
            }

            return num;
        }
    }

    public LowerBoundsSortedDictionary()
        : this(Comparer<TKey>.Default) {}

    public LowerBoundsSortedDictionary(IComparer<TKey> comparer)
        : base(new ComparerDecorator<TKey>(comparer)) {
        _comparerDecorator = (ComparerDecorator<TKey>)this.Comparer;
    }

    public TKey FindLowerBound(TKey key)
    {
        _comparerDecorator.Reset(key);
        if (this.ContainsKey(key))
        {
            return key;
        };
        return _comparerDecorator.LowerBound;
    }
    public TKey FindUpperBound(TKey key)
    {
        _comparerDecorator.Reset(key);
        if (this.ContainsKey(key))
        {
            return key;
        }
        return _comparerDecorator.UpperBound;
    }

    public void FindBounds(TKey key, out TKey lowerBound, out TKey upperBound)
    {
        _comparerDecorator.Reset(key);
        if (this.ContainsKey(key))
        {
            lowerBound = key;
            upperBound = key;
        }
        else
        {
            lowerBound = _comparerDecorator.LowerBound;
            upperBound = _comparerDecorator.UpperBound;
        }
    }
}

