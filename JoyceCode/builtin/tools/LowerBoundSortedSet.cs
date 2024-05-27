using System.Collections.Generic;
using System.Linq;

namespace builtin.tools;


public class LowerBoundSortedSet<T> : SortedSet<T> {

    private ComparerDecorator<T> _comparerDecorator;

    private class ComparerDecorator<T> : IComparer<T> {

        private IComparer<T> _comparer;

        public T LowerBound { get; private set; }
        public T UpperBound { get; private set; }
        
        private bool _resetLower = true;
        private bool _resetUpper = true;

        public void Reset()
        {
            _resetLower = true;
            _resetUpper = true;
        }

        public ComparerDecorator(IComparer<T> comparer)
        {
            _comparer = comparer;
        }

        public int Compare(T x, T y)
        {
            int num = _comparer.Compare(x, y);
            if (_resetLower)
            {
                LowerBound = y;
            }
            if (_resetUpper)
            {
                UpperBound = x;
            }
            
            if (num >= 0)
            {
                LowerBound = y;
                _resetLower = false;
            }

            if (num <= 0)
            {
                UpperBound = x;
                _resetUpper = false;
            }
            
            return num;
        }
    }

    public LowerBoundSortedSet()
        : this(Comparer<T>.Default) {}

    public LowerBoundSortedSet(IComparer<T> comparer)
        : base(new ComparerDecorator<T>(comparer)) {
        _comparerDecorator = (ComparerDecorator<T>)this.Comparer;
    }

    public T FindLowerBound(T key)
    {
        _comparerDecorator.Reset();
        this.Contains<T>(key);
        return _comparerDecorator.LowerBound;
    }
}

