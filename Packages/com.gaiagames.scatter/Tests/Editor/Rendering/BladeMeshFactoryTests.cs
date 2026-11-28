using NUnit.Framework;
using Scatter.Rendering;
using UnityEngine;

namespace Scatter.Tests.Rendering
{
    public sealed class BladeMeshFactoryTests
    {
        [TestCase(1, 3, 3)]
        [TestCase(2, 5, 9)]
        [TestCase(3, 7, 15)]
        public void Counts_MatchSegmentCount(int segmentCount, int vertexCount, int indexCount)
        {
            Assert.That(BladeMeshFactory.GetVertexCount(segmentCount), Is.EqualTo(vertexCount));
            Assert.That(BladeMeshFactory.GetIndexCount(segmentCount), Is.EqualTo(indexCount));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Triangles_HaveTheSameWinding_AndValidIndices(int segmentCount)
        {
            var indices = new int[BladeMeshFactory.GetIndexCount(segmentCount)];
            BladeMeshFactory.FillTriangleIndices(segmentCount, 0, indices, 0);

            int vertexCount = BladeMeshFactory.GetVertexCount(segmentCount);
            for (int first = 0; first < indices.Length; first += 3)
            {
                Vector2 a = BladeMeshFactory.GetTemplateVertex(indices[first], segmentCount);
                Vector2 b = BladeMeshFactory.GetTemplateVertex(indices[first + 1], segmentCount);
                Vector2 c = BladeMeshFactory.GetTemplateVertex(indices[first + 2], segmentCount);
                float signedArea = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

                Assert.That(signedArea, Is.GreaterThan(0f), $"Triangle {first / 3} is flipped or degenerate.");
                Assert.That(indices[first], Is.InRange(0, vertexCount - 1));
                Assert.That(indices[first + 1], Is.InRange(0, vertexCount - 1));
                Assert.That(indices[first + 2], Is.InRange(0, vertexCount - 1));
            }
        }

        [Test]
        public void TemplateVertices_TaperFromRootToTip()
        {
            const int segmentCount = 3;

            Assert.That(BladeMeshFactory.GetTemplateVertex(0, segmentCount), Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(BladeMeshFactory.GetTemplateVertex(1, segmentCount), Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(BladeMeshFactory.GetTemplateVertex(5, segmentCount).y, Is.EqualTo(2f / 3f).Within(1e-6f));
            Assert.That(BladeMeshFactory.GetTemplateVertex(6, segmentCount), Is.EqualTo(new Vector2(0f, 1f)));
        }

        [Test]
        public void MultiBladeIndices_PutEachBladeAtAPowerOfTwoStride_WithTheTemplateTriangles()
        {
            const int blades = 16;
            Assert.That(1 << BladeMeshFactory.MultiBladeVertexShift, Is.EqualTo(BladeMeshFactory.MultiBladeVertexStride));
            Assert.That(BladeMeshFactory.GetVertexCount(BladeMeshFactory.MaxMultiBladeSegmentCount), Is.LessThanOrEqualTo(BladeMeshFactory.MultiBladeVertexStride));
            for (int segmentCount = 1; segmentCount <= BladeMeshFactory.MaxMultiBladeSegmentCount; segmentCount++)
            {
                int indexCount = BladeMeshFactory.GetIndexCount(segmentCount);
                var template = new int[indexCount];
                BladeMeshFactory.FillTriangleIndices(segmentCount, 0, template, 0);

                int[] indices = BladeMeshFactory.GetMultiBladeIndices(segmentCount, blades);

                Assert.That(indices.Length, Is.EqualTo(indexCount * blades));
                for (int i = 0; i < indices.Length; i++)
                {
                    // What the vertex shader decodes: blade = id >> shift, template vertex = id & (stride - 1).
                    int blade = indices[i] >> BladeMeshFactory.MultiBladeVertexShift;
                    int templateVertex = indices[i] & (BladeMeshFactory.MultiBladeVertexStride - 1);
                    Assert.That(blade, Is.EqualTo(i / indexCount), $"{segmentCount} segments, index {i}.");
                    Assert.That(templateVertex, Is.EqualTo(template[i % indexCount]), $"{segmentCount} segments, index {i}.");
                }
            }
        }

        [Test]
        public void MultiBladeStride_MatchesTheHlslDefines()
        {
            string hlsl = System.IO.File.ReadAllText("Packages/com.gaiagames.scatter/Shaders/Spike/SpikeCommon.hlsl");

            Assert.That(hlsl, Does.Contain("#define SPIKE_BLADE_VERTEX_STRIDE " + BladeMeshFactory.MultiBladeVertexStride + "u"));
            Assert.That(hlsl, Does.Contain("#define SPIKE_BLADE_VERTEX_SHIFT " + BladeMeshFactory.MultiBladeVertexShift + "u"));
        }

        [Test]
        public void FillTriangleIndices_OffsetsEachBladeOfAMultiBladeBuffer()
        {
            const int segmentCount = 2;
            const int blades = 4;
            int indexCount = BladeMeshFactory.GetIndexCount(segmentCount);
            int vertexCount = BladeMeshFactory.GetVertexCount(segmentCount);
            var indices = new int[indexCount * blades];
            for (int blade = 0; blade < blades; blade++)
            {
                BladeMeshFactory.FillTriangleIndices(segmentCount, blade * vertexCount, indices, blade * indexCount);
            }

            for (int i = 0; i < indices.Length; i++)
            {
                int blade = i / indexCount;
                Assert.That(indices[i], Is.InRange(blade * vertexCount, blade * vertexCount + vertexCount - 1));
            }
        }

        [Test]
        public void CreateBladeMesh_HasTheTemplateVertices()
        {
            Mesh mesh = BladeMeshFactory.CreateBladeMesh(3, "Test Blade");
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(7));
                Assert.That(mesh.GetIndexCount(0), Is.EqualTo(15));
                Assert.That(mesh.GetTopology(0), Is.EqualTo(MeshTopology.Triangles));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
