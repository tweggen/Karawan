using System.IO;

namespace engine.casette;

/// <summary>
/// Interface for loading files into the Mix system.
/// Allows different implementations for runtime (Assets) vs editor (filesystem).
/// </summary>
public interface IMixFileProvider
{
    /// <summary>
    /// Check if a file exists at the given path.
    /// </summary>
    bool Exists(string path);
    
    /// <summary>
    /// Open a file for reading.
    /// </summary>
    Stream Open(string path);
    
    /// <summary>
    /// Register a file association (for asset systems that need it).
    /// Default implementation does nothing.
    /// </summary>
    void AddAssociation(string key, string path) { }
    
    /// <summary>
    /// Resolve a relative path to a full path for existence checking.
    /// Used for logging warnings about missing files.
    /// Default implementation returns the path as-is.
    /// </summary>
    string ResolvePath(string relativePath) => relativePath;
}
