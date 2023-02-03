using System;
using System.Collections.Generic;
using System.Text;

namespace engine.streets
{
    public class QuarterStore
    {
        private List<Quarter> _listQuarters;

        public void Add(in Quarter quarter)
        {
            _listQuarters.Add(quarter);
        }

        public List<Quarter> GetQuarters()
        {
            return _listQuarters;
        }

        public QuarterStore() {
            _listQuarters = new List<Quarter>();
        }
    }
}
