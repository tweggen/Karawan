using System;
using System.Collections.Generic;
using System.IO;

namespace CmdLine
{
    public class PackTextures
    {
        public Action<string> Trace = (msg) => System.Diagnostics.Debug.WriteLine(msg);
        private string[] _args;
        private Packer _packer;

        public int Execute()
        {
            Trace("packtextures: Working...");
            GameConfig gc = new GameConfig(_args[1]) { Trace = Trace };
            gc.Load();

            foreach (var kvp in gc.MapAtlasSpecs)
            {
                Trace($"packtextures: Generating atlas {kvp.Key}...");
                _packer = new Packer() { AtlasSize = 2048, FitHeuristic = BestFitHeuristic.Area };
                _packer.DestinationTexture = Path.Combine(_args[2], kvp.Key);
                foreach (var textureResource in kvp.Value.TextureResources)
                {
                    Trace($"packtextures: Adding texture {textureResource.Uri} to atlas {kvp.Key}...");
                    _packer.AddTexture(textureResource);
                }
                _packer.Process(null, null, 2048,0, true);
                Trace($"packtextures: Saving atlas {kvp.Key} to {_packer.DestinationTexture}...");
                _packer.SaveAtlasses(_packer.DestinationTexture);
            }
            Trace($"packtextures: Done");
            return 0;
        }

        
        public PackTextures(string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException();
            }

            _args = args;
        }
    }
}