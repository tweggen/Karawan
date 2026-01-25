using System.IO;
using engine.casette;

namespace Aihao.Services;

/// <summary>
/// File provider for the editor that uses direct filesystem access.
/// </summary>
public class EditorFileProvider : IMixFileProvider
{
    private readonly string _projectDirectory;
    
    public EditorFileProvider(string projectDirectory)
    {
        _projectDirectory = projectDirectory;
    }
    
    public bool Exists(string path)
    {
        // Mix passes just the filename, so we need to combine with project dir
        var fullPath = Path.Combine(_projectDirectory, path);
        return File.Exists(fullPath);
    }
    
    public Stream Open(string path)
    {
        var fullPath = Path.Combine(_projectDirectory, path);
        return File.OpenRead(fullPath);
    }
    
    public void AddAssociation(string key, string path)
    {
        // Editor doesn't need to track associations like the runtime does
    }
    
    public string ResolvePath(string relativePath)
    {
        return Path.Combine(_projectDirectory, relativePath);
    }
}
