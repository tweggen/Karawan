using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CmdLine
{
    public class PackTextures
    {
        public Action<string> Trace = (msg) => System.Diagnostics.Debug.WriteLine(msg);
        private string[] _args;
        private Packer _packer;

        public int AtlasSize { get; set; } = 1024;

        private void _generateTraditional(GameConfig gc)
        {
            foreach (var kvp in gc.MapAtlasSpecs)
            {
                Trace($"packtextures: Generating atlas {kvp.Key}...");
                _packer = new Packer()
                {
                    AtlasSize = AtlasSize, FitHeuristic = BestFitHeuristic.Area
                };
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
            SortedDictionary<string, Packer> dictPackers = new SortedDictionary<string, Packer>();

            /*
             * First create packers for all individual channels.
             */
            foreach (var kvpChannel in ts.Channels)
            {
                Trace($"Creating packer for texture channel \"{kvpChannel.Key}\".");
                var packer = new Packer()
                {
                    AtlasSize = AtlasSize, FitHeuristic = BestFitHeuristic.Area,
                    DestinationTexture = Path.Combine(_args[2], $"{kvpChannel.Key}.json").Replace('\\', '/')
                };
                packer.Prepare();
                dictPackers[kvpChannel.Key] = packer;

            }

            /*
             * The combinations are sorted from the multi-channel to the single channel.
             * Therefore, after each combination, we pack the corresponding channels.
             *
             * Note, that this assumes we do not have overlapping definitions.
             */
            foreach (ChannelCombination comb in ts.SetChannelCombinations)
            {
                Trace($"Collecting textures for channel combination \"{comb.GetCombinationString()}\".");
                foreach (Texture texture in comb.Textures)
                {
                    foreach (var kvpTexChannel in texture.Channels)
                    {
                        Trace($"Adding texture \"{kvpTexChannel.Value.Uri}\" for channel \"{kvpTexChannel.Key}\".");
                        dictPackers[kvpTexChannel.Key].AddTexture(kvpTexChannel.Value, 0);
                    }
                }
                
                Trace($"Packing what we have so for for combination \"{comb.GetCombinationString()}\".");
                foreach (var kvpChannel in comb.Channels)
                {
                    dictPackers[kvpChannel.Key].ProcessTextures(); 
                }
            }
            
            
            /*
             * Finally, write everything out.
             */
            foreach (var kvpPacker in dictPackers)
            {
                Trace($"Writing atlasses for packer \"{kvpPacker.Key}\".");
                kvpPacker.Value.FinishTextures();
                kvpPacker.Value.SaveAtlasses();
            }
        }
        
        
        
        public int Execute()
        {
            Trace("packtextures: Working...");
            GameConfig gc = new GameConfig(_args[1]) { Trace = Trace };
            gc.Load();

            // _generateTraditional(gc);
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