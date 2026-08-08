using System.Collections.Generic;
using UnityEngine;

namespace HouseFlip.Art
{
    /// <summary>
    /// Builds the low-poly shapes the game is made of (GDD 1).
    ///
    /// The important one is <see cref="ChamferedBox"/>. A plain cube reads as programmer
    /// art no matter how it is shaded, because real objects have no perfectly sharp
    /// edges — the giveaway is that a cube's silhouette catches no light along its
    /// corners. A small chamfer costs 32 extra triangles and picks up a highlight all the
    /// way round, which is most of what separates "stylised low-poly" from "box".
    ///
    /// Everything is flat shaded: each face gets its own vertices so normals stay hard,
    /// which is what the toon shader is built for.
    /// </summary>
    public static class MeshFactory
    {
        /// <summary>
        /// Axis-aligned box with bevelled edges and corners, centred on the origin.
        /// 44 triangles regardless of size.
        /// </summary>
        public static Mesh ChamferedBox(Vector3 size, float chamfer = 0.04f, string name = "ChamferedBox")
        {
            float hx = Mathf.Max(0.001f, size.x) * 0.5f;
            float hy = Mathf.Max(0.001f, size.y) * 0.5f;
            float hz = Mathf.Max(0.001f, size.z) * 0.5f;

            // A chamfer wider than half the smallest dimension would invert the shape.
            float b = Mathf.Clamp(chamfer, 0.0005f, Mathf.Min(hx, Mathf.Min(hy, hz)) * 0.9f);

            float ix = hx - b, iy = hy - b, iz = hz - b;

            var builder = new MeshBuilder();

            // --- 6 face quads, inset by the chamfer -------------------------
            AddFace(builder, new Vector3(hx, 0, 0), new Vector3(0, iy, 0), new Vector3(0, 0, iz));
            AddFace(builder, new Vector3(-hx, 0, 0), new Vector3(0, iy, 0), new Vector3(0, 0, iz));
            AddFace(builder, new Vector3(0, hy, 0), new Vector3(ix, 0, 0), new Vector3(0, 0, iz));
            AddFace(builder, new Vector3(0, -hy, 0), new Vector3(ix, 0, 0), new Vector3(0, 0, iz));
            AddFace(builder, new Vector3(0, 0, hz), new Vector3(ix, 0, 0), new Vector3(0, iy, 0));
            AddFace(builder, new Vector3(0, 0, -hz), new Vector3(ix, 0, 0), new Vector3(0, iy, 0));

            // --- 12 edge chamfers -------------------------------------------
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    // Edges running along Z, between an X face and a Y face.
                    builder.AddQuad(
                        new Vector3(sx * hx, sy * iy, -iz),
                        new Vector3(sx * hx, sy * iy, iz),
                        new Vector3(sx * ix, sy * hy, iz),
                        new Vector3(sx * ix, sy * hy, -iz));
                }
            }

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    // Edges running along Y, between an X face and a Z face.
                    builder.AddQuad(
                        new Vector3(sx * hx, -iy, sz * iz),
                        new Vector3(sx * hx, iy, sz * iz),
                        new Vector3(sx * ix, iy, sz * hz),
                        new Vector3(sx * ix, -iy, sz * hz));
                }
            }

            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    // Edges running along X, between a Y face and a Z face.
                    builder.AddQuad(
                        new Vector3(-ix, sy * hy, sz * iz),
                        new Vector3(ix, sy * hy, sz * iz),
                        new Vector3(ix, sy * iy, sz * hz),
                        new Vector3(-ix, sy * iy, sz * hz));
                }
            }

            // --- 8 corner triangles -----------------------------------------
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        builder.AddTriangle(
                            new Vector3(sx * hx, sy * iy, sz * iz),
                            new Vector3(sx * ix, sy * hy, sz * iz),
                            new Vector3(sx * ix, sy * iy, sz * hz));
                    }
                }
            }

            return builder.Build(name);
        }

        /// <summary>Adds one inset face quad, given its centre and two half-axis vectors.</summary>
        private static void AddFace(MeshBuilder builder, Vector3 centre, Vector3 u, Vector3 v)
        {
            builder.AddQuad(
                centre - u - v,
                centre + u - v,
                centre + u + v,
                centre - u + v);
        }

        /// <summary>
        /// Accumulates flat-shaded geometry and fixes winding automatically.
        ///
        /// Winding is decided by testing each triangle's geometric normal against the
        /// direction away from the shape's centre, rather than by hand-ordering every
        /// corner. Hand-ordering 44 triangles across three axes is exactly the kind of
        /// thing that produces one inside-out face nobody notices for a month.
        /// </summary>
        private class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                AddTriangle(a, b, c);
                AddTriangle(a, c, d);
            }

            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 centroid = (a + b + c) / 3f;
                Vector3 normal = Vector3.Cross(b - a, c - a);

                // The shape is centred on the origin, so "outward" is simply the centroid
                // direction. Flip the winding when the face would face inward.
                if (Vector3.Dot(normal, centroid) < 0f)
                {
                    (b, c) = (c, b);
                    normal = Vector3.Cross(b - a, c - a);
                }

                normal = normal.normalized;

                int index = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);

                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);

                _triangles.Add(index);
                _triangles.Add(index + 1);
                _triangles.Add(index + 2);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.vertices = _vertices.ToArray();
                mesh.normals = _normals.ToArray();
                mesh.triangles = _triangles.ToArray();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
