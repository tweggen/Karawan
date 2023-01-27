using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace engine
{
    public interface IUI
    {
        public engine.IUINode CreateUI(string strUISpec);
        public void Render();
    }
}
