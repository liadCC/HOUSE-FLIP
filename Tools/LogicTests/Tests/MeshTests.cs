using HouseFlip.Art;
using NUnit.Framework;
using UnityEngine;

namespace HouseFlip.Tests
{
    /// <summary>
    /// Validates generated geometry.
    ///
    /// Mesh bugs are the worst kind to write blind: an inside-out face or a mis-sized box
    /// looks fine in code and wrong on screen, and there is no screen here. So the mesh is
    /// checked the way it would be looked at — every face pointing outward, the right
    /// silhouette, nothing degenerate.
    /// </summary>
    [TestFixture]
    public class MeshTests
    {
        private const int ExpectedTriangles = 6 * 2 + 12 * 2 + 8; // faces + edge chamfers + corners

        [Test]
        public void ChamferedBox_HasTheExpectedLowPolyBudget()
        {
            Mesh mesh = MeshFactory.ChamferedBox(new Vector3(1f, 1f, 1f));

            Assert.That(mesh.triangles.Length / 3, Is.EqualTo(ExpectedTriangles),
                "44 triangles: 12 for the faces, 24 for the edge chamfers, 8 for the corners.");
            Assert.That(mesh.vertices.Length, Is.EqualTo(ExpectedTriangles * 3),
                "Flat shaded, so no vertices are shared between faces.");
        }

        [Test]
        public void ChamferedBox_HasEveryFacePointingOutwards()
        {
            // The bug this exists to catch: one triangle wound backwards, invisible from
            // outside and showing the room through the object.
            Mesh mesh = MeshFactory.ChamferedBox(new Vector3(2f, 0.85f, 1f));

            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]];
                Vector3 b = verts[tris[i + 1]];
                Vector3 c = verts[tris[i + 2]];

                Vector3 geometric = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;

                Assert.That(Vector3.Dot(geometric, centroid), Is.GreaterThan(0f),
                    $"Triangle {i / 3} is wound inside out.");
            }
        }

        [Test]
        public void ChamferedBox_NormalsAgreeWithWinding()
        {
            Mesh mesh = MeshFactory.ChamferedBox(new Vector3(1.4f, 2.1f, 0.7f));

            Vector3[] verts = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] tris = mesh.triangles;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]];
                Vector3 b = verts[tris[i + 1]];
                Vector3 c = verts[tris[i + 2]];

                Vector3 geometric = Vector3.Cross(b - a, c - a).normalized;
                Vector3 stored = normals[tris[i]];

                Assert.That(Vector3.Dot(geometric, stored), Is.GreaterThan(0.99f),
                    $"Stored normal for triangle {i / 3} disagrees with its winding.");
            }
        }

        [Test]
        public void ChamferedBox_MatchesTheRequestedSize()
        {
            // The silhouette has to be exactly the size asked for, or every placement
            // footprint and collider in the game is subtly wrong.
            var size = new Vector3(2f, 0.6f, 2.4f);
            Mesh mesh = MeshFactory.ChamferedBox(size);

            Assert.That(mesh.bounds.size.x, Is.EqualTo(size.x).Within(0.001f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(size.y).Within(0.001f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(size.z).Within(0.001f));
        }

        [Test]
        public void ChamferedBox_IsCentredOnTheOrigin()
        {
            Mesh mesh = MeshFactory.ChamferedBox(new Vector3(1f, 3f, 0.2f));

            Assert.That(mesh.bounds.center.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void ChamferedBox_ClampsTheChamferOnThinObjects()
        {
            // A mirror is 8cm deep. A 4cm chamfer on each side would meet in the middle
            // and turn the mesh inside out, so the chamfer has to be clamped.
            Mesh mesh = MeshFactory.ChamferedBox(new Vector3(0.8f, 0.9f, 0.08f), chamfer: 0.5f);

            Assert.That(mesh.bounds.size.z, Is.EqualTo(0.08f).Within(0.001f));

            foreach (Vector3 v in mesh.vertices)
            {
                Assert.That(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), Is.False);
            }
        }

        [Test]
        public void ChamferedBox_SurvivesDegenerateInput()
        {
            // Catalog data is authored by hand; a zero somewhere must not produce a mesh
            // full of NaNs that quietly corrupts the whole batch.
            Assert.DoesNotThrow(() => MeshFactory.ChamferedBox(Vector3.zero));
            Assert.DoesNotThrow(() => MeshFactory.ChamferedBox(new Vector3(-1f, 0f, 5f)));
        }

        [Test]
        public void ChamferedBox_HasNoDegenerateTriangles()
        {
            // A zero-area triangle renders as nothing but still costs a draw and breaks
            // normal generation.
            Mesh mesh = MeshFactory.ChamferedBox(new Vector3(1f, 1f, 1f), chamfer: 0.05f);

            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]];
                Vector3 b = verts[tris[i + 1]];
                Vector3 c = verts[tris[i + 2]];

                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                Assert.That(area, Is.GreaterThan(1e-6f), $"Triangle {i / 3} has no area.");
            }
        }

        [Test]
        public void ChamferedBox_ActuallyChamfersTheCorners()
        {
            // The whole point: no vertex should sit on a true cube corner, because that
            // is the sharp edge the chamfer is there to remove.
            const float chamfer = 0.06f;
            Mesh mesh = MeshFactory.ChamferedBox(new Vector3(1f, 1f, 1f), chamfer);

            foreach (Vector3 v in mesh.vertices)
            {
                bool atCubeCorner = Mathf.Abs(Mathf.Abs(v.x) - 0.5f) < 1e-4f
                                    && Mathf.Abs(Mathf.Abs(v.y) - 0.5f) < 1e-4f
                                    && Mathf.Abs(Mathf.Abs(v.z) - 0.5f) < 1e-4f;

                Assert.That(atCubeCorner, Is.False, "Found an un-chamfered cube corner.");
            }
        }
    }
}
