using System.Collections.Generic;

namespace CmdLine
{
    public class PackTextures
    {
        private Packer _packer;
        private string _destinationAtlas;
        private string _destinationTexture;
        
        private List<string> _listFiles = new List<string>();

        private void _addTextureFiles(List<string> listFiles)
        {
            foreach (var fileName in listFiles)
            {
                _packer.AddTexture(fileName);
            }
        }


        public int Execute()
        {
            _packer = new Packer();
            _packer.DestinationAtlas = _destinationAtlas;
            _packer.DestinationTexture = _destinationTexture;
            _addTextureFiles(_listFiles);
            return 0;
        }

        
        public PackTextures(string[] args)
        {
            _destinationAtlas = args[1];
            _destinationTexture = args[2];
            int l = args.Length;
            for (int i = 3; i < l; ++i)
            {
                _listFiles.Add(args[i]);
            }
        }
    }
}