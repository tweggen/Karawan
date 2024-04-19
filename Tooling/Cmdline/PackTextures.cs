using System.Collections.Generic;

namespace CmdLine
{
    public class PackTextures
    {
        private Packer _packer;
        List<string> _listFiles = new List<string>();

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
            _addTextureFiles(_listFiles);
            return 0;
        }

        
        public PackTextures(string[] args)
        {
            int l = args.Length;
            for (int i = 3; i < l; ++i)
            {
                _listFiles.Add(args[i]);
            }
        }
    }
}