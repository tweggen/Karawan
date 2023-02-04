using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace engine.joyce
{
    public class Material
    {
        public Texture Texture { get; set; }

        public Material(in Texture texture) 
        { 
            Texture = texture;
        }
    }
}
