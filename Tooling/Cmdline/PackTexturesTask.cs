using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine
{

    public class PackTexturesTask : Microsoft.Build.Utilities.Task
    {
        private string _destinationTexture;
        private string _destinationAtlas;


        [Required]
        public ITaskItem[] TextureFiles { get; set; }

        [Required]
        public string DestinationAtlas
        {
            get
            {
                return _destinationAtlas;
            }
            set
            {
                _destinationAtlas = value;
            }
        }


        [Required]
        public string DestinationTexture
        {
            get
            {
                return _destinationTexture;
            }
            set
            {
                _destinationTexture = value;
            }
        }


        public override bool Execute()
        {
            // Log a high-importance comment
            Log.LogMessage(MessageImportance.High,
                $"Asked to pack textures to atlas {_destinationAtlas}.");

            string[] args = new string[3+TextureFiles.Length];
            args[0] = "packtextures";
            args[1] = _destinationAtlas;
            args[2] = _destinationTexture;

            int i = 3;
            foreach (var item in TextureFiles)
            {
                var textureFile = item.GetMetadata("FullPath");
                args[i] = textureFile;
                i++;
            }

            int result = new CmdLine.PackTextures(args).Execute();
            return 0 == result;
        }
    }
}