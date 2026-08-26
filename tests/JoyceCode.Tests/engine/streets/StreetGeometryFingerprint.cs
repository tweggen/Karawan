using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using engine.joyce;

namespace JoyceCode.Tests.engine.streets;


/**
 * A stable summary of emitted street geometry.
 *
 * The generator has had a fingerprint gate since WP-0, which is what made a 1200 line
 * rewrite safe. The geometry had none: every elevation change so far leaned on
 * StreetLevels.ElevationOf(0) being exactly zero, which makes the ground-only path a
 * provable no-op without anyone having to look at a vertex. Ramp geometry breaks that
 * argument, because it changes vertex emission itself - so it needs this first.
 *
 * Vertices are hashed in emission ORDER, unlike the network fingerprint which sorts.
 * A mesh is an ordered thing: triangles are built from consecutive vertices, so two
 * meshes with the same vertices in a different order are different meshes.
 */
internal static class StreetGeometryFingerprint
{
    internal static string[] CanonicalLines(Mesh m)
    {
        var lines = new string[m.Vertices.Count];

        for (int i = 0; i < m.Vertices.Count; ++i)
        {
            Vector3 v = m.Vertices[i];
            Vector3 n = (m.Normals != null && i < m.Normals.Count) ? m.Normals[i] : Vector3.Zero;
            Vector2 uv = (m.UVs != null && i < m.UVs.Count) ? m.UVs[i] : Vector2.Zero;

            lines[i] = string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3}|{3:F3},{4:F3},{5:F3}|{6:F4},{7:F4}",
                v.X, v.Y, v.Z, n.X, n.Y, n.Z, uv.X, uv.Y);
        }

        return lines;
    }


    internal static string Of(Mesh m)
    {
        var lines = CanonicalLines(m);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

        return string.Format(CultureInfo.InvariantCulture,
            "v={0},i={1},h={2}",
            m.Vertices.Count,
            m.Indices?.Count ?? 0,
            Convert.ToHexString(hash).Substring(0, 16));
    }


    /**
     * Highest vertex in the mesh: how a raised deck makes itself visible to a test.
     */
    internal static float MaxY(Mesh m) => m.Vertices.Count == 0 ? 0f : m.Vertices.Max(v => v.Y);

    internal static float MinY(Mesh m) => m.Vertices.Count == 0 ? 0f : m.Vertices.Min(v => v.Y);
}
