using System;
using System.Collections.Generic;

namespace CmdLine
{

    public class ChannelCombination
    {
        public class ComparerByComplexity : IComparer<ChannelCombination>
        {
            public int Compare(ChannelCombination x, ChannelCombination y)
            {
                if (x == y) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                int nX = x.Channels.Count;
                int nY = y.Channels.Count;

                if (nX < nY)
                {
                    return -1;
                }
                else
                {
                    if (nX > nY)
                    {
                        return 1;
                    }
                }
                
                string strX = x.GetCombinationString();
                string strY = y.GetCombinationString();
                return string.CompareOrdinal(strX, strY);
            }
        }
        
        static public string GetCombinationString(IList<string> keys)
        {
            string str = "";
            foreach (var key in keys)
            {
                if (str.Length > 0)
                {
                    str += $"_{key}";
                }
                else
                {
                    str += key;
                }
            }

            return str;

        }
        public SortedDictionary<string, Channel> Channels = new SortedDictionary<string, Channel>();
        public List<Texture> Textures = new List<Texture>();

        public string GetCombinationString()
        {
            return GetCombinationString(Channels.Keys());
        }
    }
    
    
    /**
     * We keep the information the textures should be generated with.
     *
     * First:
     * For every combination of more than one channel, a new set of texture packers
     * should be generated. The corresponding textures shall be added.
     *
     * Secondly:
     * Now, all remaining single channel textures shall be added.
     */
    public class TextureSection
    {
        
        /**
         * Post processing function.
         */
        private void _computeChannelCombinations()
        {
            foreach (var kvpTexture in Textures)
            {
                Texture tex = kvpTexture.Value;
                List<string> listChannelKeys = new List<string>();
                foreach (var kvpChannels in tex.Channels)
                {
                    // Channel ch = Channels[kvpChannels.Key];
                    listChannelKeys.Add(kvpChannels.Key);
                }

                string combinationString = ChannelCombination.GetCombinationString(listChannelKeys);
                ChannelCombination comb = null; 
                if (!ChannelCombinations.TryGetValue(combinationString, out comb))
                {
                    comb = new ChannelCombination();
                    foreach (var kvpChannels in tex.Channels)
                    {
                        Channel ch = Channels[kvpChannels.Key];
                        comb.Channels[kvpChannels.Key] = ch;
                    }


                    ChannelCombinations[combinationString] = comb;
                    SetChannelCombinations.Add(comb);
                }
                
                comb.Textures.Add(kvpTexture.Value);
            }
        }
        
        public SortedDictionary<string, ChannelCombination> ChannelCombinations = new SortedDictionary<string, ChannelCombination>();
        
        /**
         * Keeps a sorted set of the combinations for later composition of the atlas.
         */
        public SortedSet<ChannelCombination> SetChannelCombinations = new SortedSet<ChannelCombination>(new ChannelCombination.ComparerByComplexity());
        public SortedDictionary<string, Channel> Channels = new SortedDictionary<string, Channel>();
        public SortedDictionary<string, Texture> Textures = new SortedDictionary<string, Texture>();

        
        public void Digest()
        {
            _computeChannelCombinations();
        }
    }
}