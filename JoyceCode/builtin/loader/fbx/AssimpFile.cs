using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Silk.NET.Assimp;
using File = Silk.NET.Assimp.File;

namespace builtin.loader.fbx;

public unsafe class AssimpFile
{
    private Stream _stream;
    private GCHandle _gch;
    private GCHandle _gchFile;
    private int _pos;
    
    private File* _pFile;

    
    private static UIntPtr _read(File* pFile, byte* arg1, UIntPtr arg2, UIntPtr arg3)
    {
        GCHandle gch = GCHandle.FromIntPtr(new IntPtr(pFile->UserData));
        AssimpFile assimpFile = gch.Target as AssimpFile;
        if (null == assimpFile)
        {
            return 0;
        }
        
        ulong len = arg2*arg3;
        Span<byte> spanMemory = new Span<byte>(arg1, (Int32)len);

        var bytesRead = assimpFile._stream.Read(spanMemory);
        return (nuint) bytesRead;
    }

    
    private static UIntPtr _write(File* pFile, byte* arg1, UIntPtr arg2, UIntPtr arg3)
    {
        // We do not support writing.
        return UIntPtr.Zero;
    }


    private static UIntPtr _tell(File* pFile)
    {
        GCHandle gch = GCHandle.FromIntPtr(new IntPtr(pFile->UserData));
        AssimpFile assimpFile = gch.Target as AssimpFile;
        if (null == assimpFile)
        {
            return 0;
        }
        return (nuint)(assimpFile._pos);
    }

    
    private static UIntPtr _fileSize(File* pFile)
    {
        GCHandle gch = GCHandle.FromIntPtr(new IntPtr(pFile->UserData));
        AssimpFile assimpFile = gch.Target as AssimpFile;
        if (null == assimpFile)
        {
            return 0;
        }

        return (nuint) assimpFile._stream.Length;
    }

    
    private static Return _seek(File* pFile, UIntPtr arg1, Origin arg2)
    {
        GCHandle gch = GCHandle.FromIntPtr(new IntPtr(pFile->UserData));
        AssimpFile assimpFile = gch.Target as AssimpFile;
        if (null == assimpFile)
        {
            return 0;
        }

        SeekOrigin origin = SeekOrigin.Begin;
        switch (arg2)
        {
            case Origin.Cur:
                origin = SeekOrigin.Current;
                break;
            
            case Origin.End:
                origin = SeekOrigin.End;
                break;
            
            case Origin.Set:
                origin = SeekOrigin.Begin;
                break;
        }

        assimpFile._stream.Seek((long)arg1, origin);
        
        return Return.Success;
    }


    private static void _flush(File* pFile)
    {
        
    }
    

    private void _alloc()
    {
        _pFile = (File*)Marshal.AllocHGlobal(sizeof(File));
        _pFile->ReadProc = new PfnFileReadProc(_read);
        _pFile->WriteProc = new PfnFileReadProc(_write);
        _pFile->SeekProc = new PfnFileSeek(_seek);
        _pFile->TellProc = new PfnFileTellProc(_tell);
        _pFile->FileSizeProc = new PfnFileTellProc(_fileSize);
        _pFile->FlushProc = new PfnFileFlushProc(_flush);
        _gch = GCHandle.Alloc(this);
        _pFile->UserData = (byte*)GCHandle.ToIntPtr(_gch);
        
    }

    
    private void _free()
    {
        _gch.Free();
    }


    public unsafe void Close()
    {
        _stream.Dispose();
        _free();
    }
    
    public unsafe File* Open(byte* arg1, byte* arg2)
    {
        string filename = Marshal.PtrToStringAnsi((IntPtr)arg1);

        try
        {
            _stream = engine.Assets.Open(filename);
            if (null == _stream)
            {
                return null;
            }
        }
        catch (Exception e)
        {
            return null;
        }
        _pos = 0;
        
        _alloc();

        return _pFile;
    }
}

