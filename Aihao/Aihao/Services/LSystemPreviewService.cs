using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using engine.joyce;
using Splash.Silk;

namespace Aihao.Services;

/// <summary>
/// Manages L-System generation on a background thread.
/// Now that Aihao references Joyce normally, all JoyceCode types
/// (MatMesh, InstanceDesc, etc.) are the same types Splash uses.
/// </summary>
public sealed class LSystemPreviewService
{
    /// <summary>
    /// Generate an L-System on a background thread from a JSON definition.
    /// Returns true if geometry was produced and is ready for upload.
    /// </summary>
    public async Task<bool> GenerateAsync(string definitionJson, int iterations,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var loader = new builtin.tools.Lindenmayer.LSystemLoader();
                var definition = loader.LoadDefinition(definitionJson);
                var system = loader.CreateSystem(definition);
                var generator = new builtin.tools.Lindenmayer.LGenerator(system, "preview");
                var instance = generator.Generate(iterations);

                var interpreter = new builtin.tools.Lindenmayer.AlphaInterpreter(instance);
                var matMesh = new MatMesh();
                interpreter.Run(null, Vector3.Zero, matMesh, null);

                if (matMesh.IsEmpty())
                {
                    PreviewHelper.Instance.ClearInstanceDesc();
                    return false;
                }

                var id = InstanceDesc.CreateFromMatMesh(matMesh, 500f);
                PreviewHelper.Instance.SetInstanceDesc(id);
                return true;
            }
            catch
            {
                PreviewHelper.Instance.ClearInstanceDesc();
                return false;
            }
        }, ct);
    }
}
