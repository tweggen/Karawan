using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Aihao.Models;

namespace Aihao.Services;

/// <summary>
/// Service for running and debugging the game
/// </summary>
public class ProcessService
{
    private Process? _gameProcess;
    
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public event EventHandler<int>? ProcessExited;
    
    public bool IsRunning => _gameProcess != null && !_gameProcess.HasExited;
    
    /// <summary>
    /// Run the game, optionally with a debugger attached
    /// </summary>
    public async Task<bool> RunGameAsync(AihaoProject project, bool debug)
    {
        // Stop any existing process
        StopGame();
        
        // Find the executable
        var exePath = project.GameExecutablePath;
        
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            // Try to build first
            ErrorReceived?.Invoke(this, "Game executable not found. Please build the project first.");
            return false;
        }
        
        if (debug)
        {
            return await StartWithDebuggerAsync(project, exePath);
        }
        else
        {
            return StartDirectly(exePath, project.ProjectDirectory);
        }
    }
    
    private bool StartDirectly(string exePath, string workingDirectory)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            };
            
            _gameProcess = new Process { StartInfo = startInfo };
            
            _gameProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OutputReceived?.Invoke(this, e.Data);
            };
            
            _gameProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    ErrorReceived?.Invoke(this, e.Data);
            };
            
            _gameProcess.EnableRaisingEvents = true;
            _gameProcess.Exited += (s, e) =>
            {
                ProcessExited?.Invoke(this, _gameProcess?.ExitCode ?? -1);
                _gameProcess = null;
            };
            
            if (_gameProcess.Start())
            {
                _gameProcess.BeginOutputReadLine();
                _gameProcess.BeginErrorReadLine();
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, $"Failed to start game: {ex.Message}");
            return false;
        }
    }
    
    private async Task<bool> StartWithDebuggerAsync(AihaoProject project, string exePath)
    {
        // Detect available IDE
        var idePath = DetectIDE();
        
        if (string.IsNullOrEmpty(idePath))
        {
            ErrorReceived?.Invoke(this, "No supported IDE found. Please install JetBrains Rider or Visual Studio.");
            return false;
        }
        
        try
        {
            var slnPath = project.SolutionPath;
            
            if (idePath.Contains("rider", StringComparison.OrdinalIgnoreCase))
            {
                // JetBrains Rider
                // rider64.exe --temp-project <path> --debug <exe>
                var args = !string.IsNullOrEmpty(slnPath) && File.Exists(slnPath)
                    ? $"\"{slnPath}\""
                    : $"--temp-project \"{project.ProjectDirectory}\"";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = idePath,
                    Arguments = args,
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
                OutputReceived?.Invoke(this, $"Launched Rider. Please start debugging from the IDE.");
                return true;
            }
            else if (idePath.Contains("devenv", StringComparison.OrdinalIgnoreCase))
            {
                // Visual Studio
                var args = !string.IsNullOrEmpty(slnPath) && File.Exists(slnPath)
                    ? $"\"{slnPath}\" /DebugExe \"{exePath}\""
                    : $"/DebugExe \"{exePath}\"";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = idePath,
                    Arguments = args,
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
                OutputReceived?.Invoke(this, $"Launched Visual Studio with debugger attached.");
                return true;
            }
            else if (idePath.Contains("code", StringComparison.OrdinalIgnoreCase))
            {
                // VS Code (with C# extension)
                var args = $"\"{project.ProjectDirectory}\"";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = idePath,
                    Arguments = args,
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
                OutputReceived?.Invoke(this, $"Launched VS Code. Please configure launch.json for debugging.");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, $"Failed to start debugger: {ex.Message}");
            return false;
        }
    }
    
    private string? DetectIDE()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Check for Rider
            var riderPaths = new[]
            {
                @"C:\Program Files\JetBrains\JetBrains Rider\bin\rider64.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"JetBrains\Toolbox\apps\Rider\ch-0\*\bin\rider64.exe")
            };
            
            foreach (var path in riderPaths)
            {
                if (path.Contains('*'))
                {
                    var dir = Path.GetDirectoryName(path);
                    var pattern = Path.GetFileName(path);
                    if (dir != null && Directory.Exists(Path.GetDirectoryName(dir)))
                    {
                        try
                        {
                            foreach (var found in Directory.EnumerateFiles(Path.GetDirectoryName(dir)!, pattern, SearchOption.AllDirectories))
                            {
                                return found;
                            }
                        }
                        catch { }
                    }
                }
                else if (File.Exists(path))
                {
                    return path;
                }
            }
            
            // Check for Visual Studio
            var vsPaths = new[]
            {
                @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
            };
            
            foreach (var path in vsPaths)
            {
                if (File.Exists(path))
                    return path;
            }
            
            // Check for VS Code
            var codePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe"),
                @"C:\Program Files\Microsoft VS Code\Code.exe",
            };
            
            foreach (var path in codePaths)
            {
                if (File.Exists(path))
                    return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS paths
            if (File.Exists("/Applications/Rider.app/Contents/MacOS/rider"))
                return "/Applications/Rider.app/Contents/MacOS/rider";
            if (File.Exists("/Applications/Visual Studio Code.app/Contents/MacOS/Electron"))
                return "/Applications/Visual Studio Code.app/Contents/MacOS/Electron";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux - check PATH
            var result = FindInPath("rider");
            if (result != null) return result;
            
            result = FindInPath("code");
            if (result != null) return result;
        }
        
        return null;
    }
    
    private string? FindInPath(string executable)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = executable,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                
                if (!string.IsNullOrEmpty(output) && File.Exists(output))
                    return output;
            }
        }
        catch { }
        
        return null;
    }
    
    /// <summary>
    /// Stop the currently running game
    /// </summary>
    public void StopGame()
    {
        if (_gameProcess != null && !_gameProcess.HasExited)
        {
            try
            {
                _gameProcess.Kill(entireProcessTree: true);
            }
            catch { }
            
            _gameProcess = null;
        }
    }
    
    /// <summary>
    /// Build the project
    /// </summary>
    public async Task<bool> BuildProjectAsync(AihaoProject project, string configuration = "Debug")
    {
        var slnPath = project.SolutionPath;
        
        if (string.IsNullOrEmpty(slnPath) || !File.Exists(slnPath))
        {
            ErrorReceived?.Invoke(this, "Solution file not found. Cannot build.");
            return false;
        }
        
        try
        {
            OutputReceived?.Invoke(this, $"Building {Path.GetFileName(slnPath)} ({configuration})...");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{slnPath}\" -c {configuration}",
                WorkingDirectory = Path.GetDirectoryName(slnPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = new Process { StartInfo = startInfo };
            
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OutputReceived?.Invoke(this, e.Data);
            };
            
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    ErrorReceived?.Invoke(this, e.Data);
            };
            
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await process.WaitForExitAsync();
            
            var success = process.ExitCode == 0;
            OutputReceived?.Invoke(this, success ? "Build succeeded." : "Build failed.");
            
            return success;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, $"Build failed: {ex.Message}");
            return false;
        }
    }
}
