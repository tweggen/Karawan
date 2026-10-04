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
 *
 * ⚠️ **And so are two meshes with the same vertices and different TRIANGLES, which this
 * could not see until 2026-09-05.** It hashed the vertex list and reported the index
 * COUNT beside the hash - so any change that kept every vertex and the number of triangles
 * was invisible to it, including one that reverses a triangle's winding. Found by mutation
 * testing in §7s: swapping two indices of every carriageway row passed the entire suite,
 * and back-face culling would have removed half of every road in the game (§7j, where
 * exactly that happened to the pavements and nothing failed). The indices are hashed with
 * the vertices now, which is what moved every recorded hash in street-geometry.json on that
 * date with no vertex having moved.
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


    /**
     * The triangles, three indices to a line, in emission order.
     *
     * Separate from CanonicalLines because that one is also used to DIFF two meshes vertex
     * by vertex when a baseline fails, and a diff of indices against vertices lines nothing
     * up. Both go into the hash.
     */
    internal static string[] TriangleLines(Mesh m)
    {
        if (null == m.Indices) return Array.Empty<string>();

        var lines = new string[m.Indices.Count / 3];
        for (int i = 0; i + 2 < m.Indices.Count; i += 3)
        {
            lines[i / 3] = string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2}", m.Indices[i], m.Indices[i + 1], m.Indices[i + 2]);
        }

        return lines;
    }


    internal static string Of(Mesh m)
    {
        var lines = CanonicalLines(m).Concat(TriangleLines(m)).ToArray();
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
