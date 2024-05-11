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


        private void _generateTraditional(GameConfig gc)
        {
            foreach (var kvp in gc.MapAtlasSpecs)
            {
                Trace($"packtextures: Generating atlas {kvp.Key}...");
                _packer = new Packer() { AtlasSize = 1024, FitHeuristic = BestFitHeuristic.Area };
                _packer.DestinationTexture = Path.Combine(_args[2], kvp.Key).Replace('\\', '/');
                foreach (var textureResource in kvp.Value.TextureResources)
                {
                    Trace($"packtextures: Adding texture {textureResource.Uri} to atlas {kvp.Key}...");
                    _packer.AddTexture(textureResource,0);
                }

                _packer.Prepare();
                _packer.ProcessTextures();
                Trace($"packtextures: Saving atlas {kvp.Key} to {_packer.DestinationTexture}...");
                _packer.SaveAtlasses();
            }
        }



        private void _generateFromTextureSection(GameConfig gc)
        {
            var ts = gc.TextureSection;
            foreach (ChannelCombination comb in ts.SetChannelCombinations)
            {
                Trace($"Processing channel combination \"{comb.GetCombinationString()}\".");
            }
        }
        
        
        
        public int Execute()
        {
            Trace("packtextures: Working...");
            GameConfig gc = new GameConfig(_args[1]) { Trace = Trace };
            gc.Load();

            _generateTraditional(gc);
            _generateFromTextureSection(gc);
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