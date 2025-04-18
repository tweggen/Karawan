﻿
using System.Numerics;

namespace engine.joyce
{
    /**
     * A texture can be used to render a mesh.
     * It can either be based on a sort of uri or an im memory buffer.
     */
    public class Texture
    {
        public static string BLACK = "joyce://col00000000"; 

        public string Source;


        private engine.draw.IFramebuffer _framebuffer;
        public engine.draw.IFramebuffer Framebuffer
        {
            get => _framebuffer;
            set  {
                _framebuffer = value;
                _computeKey();
            }
        }
        

        public enum FilteringModes
        {
            Smooth,
            
            /*
             * Nearest pixel within mipmap, mipmaps linearly blended.
             */
            Pixels,
            
            /*
             * No mipmap, strictest nearest sampling pixel output.
             */
            Framebuffer
        };


        private string _key;
        public string Key
        {
            get => _key;
        }

        
        private FilteringModes _filteringMode = FilteringModes.Pixels;
        public FilteringModes FilteringMode
        {
            get => _filteringMode;
            set
            {
                _filteringMode = value;
                _computeKey();
            }
        }

        
        public Vector2 UVOffset = new(0f, 0f);
        public Vector2 UVScale = new (1f, 1f);

        /**
         * We do not need has mipmap in the key, a texture either has a
         * mipmap or it doesn't.
         */
        private bool _hasMipmap = false;
        public bool HasMipmap {
            get => _hasMipmap;
            set
            {
                _hasMipmap = value;
                _computeKey();
            } 
        }
        
        public int Width { get; set; }
        public int Height { get; set; }
        
        public Vector2 Size2
        {
            get => new Vector2(Width, Height); 
        }
        
        
        public Vector2 InvSize2
        {
            get => new Vector2(1f/Width, 1f/Height); 
        }
        

        public bool IsMergableEqual(Texture o)
        {
            return o != null 
                   && FilteringMode == o.FilteringMode
                   && Source == o.Source
                   && Framebuffer == o.Framebuffer;
        }
        
        
        public int GetMergableHashCode()
        {
            int h = 0;
            h += (int)FilteringMode<<13;

            if (Source != null)
            {
                h ^= 10;
                h ^= Source.GetHashCode();
            }

            if (Framebuffer != null)
            {
                h ^= 20;
                h ^= Framebuffer.GetHashCode();
            }
            
            /*
             * Do not consider UVOffset and UVScale
             */
            return h;
        }
        
        
        public override string ToString()
        {
            if (Source != null)
            {
                return $"Texture {{ Source: \"{Source}\", UVOffset: {UVOffset}, UVScale: {UVScale} }}";
            }
            else
            {
                return $"Texture {{ Width: {Framebuffer.Width}, Height: {Framebuffer.Height}, Generation: {Framebuffer.Generation} }}";
            }
        }


        public bool IsValid()
        {
            return 
                (Source != null && Source != "")
                || (Framebuffer != null);
        }


        private void _computeKey()
        {
            if (Source != null)
            {
                _key = $"{Source}-{_filteringMode}";
            }
            else
            {
                if (Framebuffer != null)
                {
                    _key = $"{Framebuffer.Id}-{_filteringMode}-{HasMipmap}";
                }
                else
                {
                    _key = "(null)";
                }
            }
        }
        
        
        public Texture(string source)
        {
            Source = source;
            Framebuffer = null;
            _computeKey();
        }
        

        public Texture(engine.draw.IFramebuffer framebuffer)
        {
            Source = null;
            Framebuffer = framebuffer;
            _computeKey();
        }
    }
}
