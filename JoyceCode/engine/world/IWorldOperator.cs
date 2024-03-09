
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace engine.world
{
    public interface IWorldOperator
    {
        public string WorldOperatorGetPath();

        public System.Func<Task> WorldOperatorApply();
    }
}
