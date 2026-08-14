
using System.Numerics;
using MessagePack;

namespace engine.joyce
{
    /**
     * A texture can be used to render a mesh.
     * It can either be based on a sort of uri or an im memory buffer.
     *
     * WP-4.1: only the uri-based form is persisted into a baked mo-{hash} model.
     * A framebuffer-backed texture is a live render target created at runtime and
     * has no meaning in a file - no model loader ever produces one - so
     * Framebuffer is ignored and Key is recomputed after load rather than stored,
     * since it is a pure function of the fields that are.
     */
    [MessagePackObject(AllowPrivate = true)]
    public partial class Texture : IMessagePackSerializationCallbackReceiver
    {
        [IgnoreMember]
        public static string BLACK = "joyce://col00000000";

        [Key(0)]
        public string Source;


        [IgnoreMember]
        private engine.draw.IFramebuffer _framebuffer;

        [IgnoreMember]
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


        [IgnoreMember]
        private string _key;

        [IgnoreMember]
        public string Key
        {
            get => _key;
        }


        [Key(1)]
        private FilteringModes _filteringMode = FilteringModes.Pixels;

        [IgnoreMember]
        public FilteringModes FilteringMode
        {
            get => _filteringMode;
            set
            {
                _filteringMode = value;
                _computeKey();
            }
        }

        
        [Key(2)]
        public Vector2 UVOffset = new(0f, 0f);

        [Key(3)]
        public Vector2 UVScale = new (1f, 1f);

        /**
         * We do not need has mipmap in the key, a texture either has a
         * mipmap or it doesn't.
         */
        [Key(4)]
        private bool _hasMipmap = false;

        [IgnoreMember]
        public bool HasMipmap {
            get => _hasMipmap;
            set
            {
                _hasMipmap = value;
                _computeKey();
            }
        }

        [Key(5)]
        public int Width { get; set; }

        [Key(6)]
        public int Height { get; set; }

        [IgnoreMember]
        public Vector2 Size2
        {
            get => new Vector2(Width, Height);
        }


        [IgnoreMember]
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
        
        
        /**
         * Deserialisation constructor; see IMessagePackSerializationCallbackReceiver
         * below for where the key comes back from.
         */
        private Texture()
        {
        }


        public void OnBeforeSerialize()
        {
        }


        public void OnAfterDeserialize()
        {
            _computeKey();
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
