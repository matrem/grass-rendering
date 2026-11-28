using UnityEngine;

namespace Scatter.Rendering
{
    /// <summary>
    /// Builds the blade mesh LODs by code (design 6.2, 7): a tapered strip of <c>segmentCount</c> segments,
    /// i.e. pairs of vertices from root to tip plus one tip vertex (3 segments = 7 vertices, 2 = 5, 1 = 3).
    /// Vertices hold a template: x = side (-1 left, 1 right, 0 at the tip), y = height fraction (0 root, 1 tip).
    /// The vertex shader places, scales and curves the blade. The layout matches SpikeBladeInput.hlsl,
    /// which rebuilds the same template from SV_VertexID for procedural draws.
    /// </summary>
    public static class BladeMeshFactory
    {
        public const int MinSegmentCount = 1;

        /// <summary>
        /// Vertex IDs between two blades of a multi-blade index buffer: a power of two, so the vertex shader splits
        /// SV_VertexID with a shift and a mask instead of an integer division (SPIKE_BLADE_VERTEX_STRIDE in
        /// SpikeCommon.hlsl). Holds the largest template, 3 segments = 7 vertices.
        /// </summary>
        public const int MultiBladeVertexStride = 8;

        /// <summary>log2 of <see cref="MultiBladeVertexStride"/> (SPIKE_BLADE_VERTEX_SHIFT in SpikeCommon.hlsl).</summary>
        public const int MultiBladeVertexShift = 3;

        /// <summary>Largest segment count of a multi-blade index buffer: its vertices fit in <see cref="MultiBladeVertexStride"/>.</summary>
        public const int MaxMultiBladeSegmentCount = 3;

        public static int GetVertexCount(int segmentCount)
        {
            return 2 * segmentCount + 1;
        }

        public static int GetTriangleCount(int segmentCount)
        {
            return 2 * segmentCount - 1;
        }

        public static int GetIndexCount(int segmentCount)
        {
            return 3 * GetTriangleCount(segmentCount);
        }

        /// <summary>Template vertex <paramref name="vertex"/>: x = side, y = height fraction.</summary>
        public static Vector2 GetTemplateVertex(int vertex, int segmentCount)
        {
            if (vertex >= 2 * segmentCount)
            {
                return new Vector2(0f, 1f);
            }

            float side = (vertex & 1) != 0 ? 1f : -1f;
            return new Vector2(side, (float)(vertex >> 1) / segmentCount);
        }

        /// <summary>
        /// Writes the triangle list of one blade at <paramref name="destinationOffset"/>, adding
        /// <paramref name="vertexOffset"/> to each index. Odd triangles of the strip swap their first two
        /// corners so that all triangles keep the same winding.
        /// </summary>
        public static void FillTriangleIndices(int segmentCount, int vertexOffset, int[] destination, int destinationOffset)
        {
            int triangleCount = GetTriangleCount(segmentCount);
            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int first = destinationOffset + triangle * 3;
                bool odd = (triangle & 1) != 0;
                destination[first + 0] = vertexOffset + triangle + (odd ? 1 : 0);
                destination[first + 1] = vertexOffset + triangle + (odd ? 0 : 1);
                destination[first + 2] = vertexOffset + triangle + 2;
            }
        }

        /// <summary>Creates the blade mesh of one LOD. Allocates: call at startup.</summary>
        public static Mesh CreateBladeMesh(int segmentCount, string name)
        {
            segmentCount = Mathf.Max(segmentCount, MinSegmentCount);
            int vertexCount = GetVertexCount(segmentCount);
            var vertices = new Vector3[vertexCount];
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                vertices[vertex] = GetTemplateVertex(vertex, segmentCount);
            }

            var indices = new int[GetIndexCount(segmentCount)];
            FillTriangleIndices(segmentCount, 0, indices, 0);

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            // The template is not in world space: bounds are not used (blades are culled by the cull compute).
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>
        /// Indices drawing <paramref name="bladesPerInstance"/> blades per instance with procedural indexed draws:
        /// blade b uses vertex IDs b * <see cref="MultiBladeVertexStride"/> + template vertex. Allocates: startup only.
        /// </summary>
        public static int[] GetMultiBladeIndices(int segmentCount, int bladesPerInstance)
        {
            Debug.Assert(segmentCount <= MaxMultiBladeSegmentCount, "Blade template too large for the multi-blade vertex stride.");
            int indexCount = GetIndexCount(segmentCount);
            var indices = new int[indexCount * bladesPerInstance];
            for (int blade = 0; blade < bladesPerInstance; blade++)
            {
                FillTriangleIndices(segmentCount, blade * MultiBladeVertexStride, indices, blade * indexCount);
            }

            return indices;
        }

        /// <summary>Index buffer of <see cref="GetMultiBladeIndices"/>. Allocates: call at startup.</summary>
        public static GraphicsBuffer CreateMultiBladeIndexBuffer(int segmentCount, int bladesPerInstance, string name)
        {
            int[] indices = GetMultiBladeIndices(segmentCount, bladesPerInstance);
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, indices.Length, sizeof(int)) { name = name };
            buffer.SetData(indices);
            return buffer;
        }
    }
}
