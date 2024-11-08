using System;
using System.Collections.Generic;
using engine;
using static engine.Logger;

namespace Splash
{
    public class TextureManager
    {
        private readonly object _lock = new ();

        public bool TraceDetails { get; set; } = false;
        
        private readonly IThreeD _threeD;
        private Dictionary<string, ATextureEntry> _dictTextures;


        public void PushTexture(in string textureKey, in ATextureEntry aTextureEntry)
        {
            lock (_lock)
            {
                _dictTextures[textureKey] = aTextureEntry;
            }
            Trace( $"Uploaded texture {textureKey} available.");
        }
        
        
        public ATextureEntry? FindATexture(in engine.joyce.Texture jTexture)        
        {
            ATextureEntry aTextureEntry;
            bool needFillEntry = false;
            string textureKey = jTexture.Key;
            ATextureEntry.ResourceState textureState;
            lock (_lock)
            {
                if (_dictTextures.TryGetValue(textureKey, out aTextureEntry))
                {
                    textureState = aTextureEntry.State;
                    if (textureState == ATextureEntry.ResourceState.Outdated)
                    {
                        needFillEntry = true;
                    }
                    else
                    {
                        /*
                         * However, it does not need filling if it already is loading.
                         */
                    }
                } 
                else
                {
                    if (TraceDetails) Trace($"Texture {textureKey} not loaded yet.");
                    aTextureEntry = _threeD.CreateTextureEntry(jTexture);
                    _dictTextures.Add(textureKey, aTextureEntry);
                    needFillEntry = true;
                    textureState = aTextureEntry.State;
                    // TXWTODO: Should be async and not with mutex held.
                }

            }
            if (needFillEntry)
            {
                _threeD.UploadTextureEntry(aTextureEntry);
                if (aTextureEntry.State <= ATextureEntry.ResourceState.Created)
                {
                    if (TraceDetails) Trace($"Texture {textureKey} could not be loaded, removing from dict.");
                    lock (_lock)
                    {
                        _dictTextures.Remove(textureKey);
                    }

                    return null;
                }
            }
            return aTextureEntry;
        }
        
        
        public TextureManager()
        {
            _dictTextures = new();
            _threeD = I.Get<IThreeD>();
        }
    }
}
