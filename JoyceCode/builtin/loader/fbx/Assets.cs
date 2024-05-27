using System;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Assimp;

namespace builtin.loader.fbx;

public sealed class Assets
{
    private static Silk.NET.Assimp.FileIO _fileIO;
    
    private unsafe static File* _assimpOpenProc(FileIO* arg0, byte* arg1, byte* arg2)
    {
        var _assimpFile = new AssimpFile();
        return _assimpFile.Open(arg1, arg2);
    }


    private unsafe static void _assimpCloseProc(FileIO* arg0, File* arg1)
    {
        GCHandle gch = GCHandle.FromIntPtr(new IntPtr(arg1->UserData)); 
        AssimpFile assimpFile = gch.Target as AssimpFile;
        assimpFile?.Close();
    }


    public static unsafe Silk.NET.Assimp.FileIO Get()
    {
        _fileIO.OpenProc = new PfnFileOpenProc(_assimpOpenProc);
        _fileIO.CloseProc = new PfnFileCloseProc(_assimpCloseProc);
        _fileIO.UserData = null;
        return _fileIO;
    }

}