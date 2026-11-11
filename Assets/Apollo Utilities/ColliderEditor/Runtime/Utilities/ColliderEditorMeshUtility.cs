using System.Collections.Generic;
using UnityEngine;

namespace ColliderEditor
{
    /// <summary>
    /// Utility methods for turning the current vertex selection into meshes that can be
    /// consumed by the different automatic collider generation back ends.
    /// </summary>
    public static class ColliderEditorMeshUtility
    {
        public static List<Vector3> GetSelectedWorldPoints(ColliderEditorState state)
        {
            List<Vector3> points = new List<Vector3>(state.SelectedVertices.Count);
            int count = state.SelectedVertices.Count;

            for (int i = 0; i < count; i++)
            {
                points.Add(state.SelectedVertices[i].WorldPosition);
            }

            return points;
        }

        /// <summary>
        /// Builds a single mesh from all fully selected source triangles.
        /// This path is still useful for generators that work on one combined mesh.
        /// </summary>
        public static Mesh BuildSelectedRegionMesh(ColliderEditorState state)
        {
            List<Mesh> selectedRegionMeshes = BuildSelectedRegionMeshes(state);
            if (selectedRegionMeshes.Count == 0)
            {
                return BuildConvexFallbackMesh(state);
            }

            CombineInstance[] combineInstances = new CombineInstance[selectedRegionMeshes.Count];
            for (int i = 0; i < selectedRegionMeshes.Count; i++)
            {
                combineInstances[i].mesh = selectedRegionMeshes[i];
                combineInstances[i].transform = Matrix4x4.identity;
            }

            Mesh outputMesh = new Mesh();
            outputMesh.name = state.SelectedObject.name + "_SelectedRegion";
            outputMesh.CombineMeshes(combineInstances, true, false);
            outputMesh.RecalculateBounds();
            outputMesh.RecalculateNormals();
            return outputMesh;
        }

        /// <summary>
        /// Builds a list of source meshes that preserve the original selected triangles.
        /// Unlike the convex fallback path this keeps the real triangle topology intact,
        /// which is what CoACD expects as input.
        /// </summary>
        public static List<Mesh> BuildSelectedRegionMeshes(ColliderEditorState state)
        {
            List<Mesh> meshes = new List<Mesh>();

            if (state == null || state.SelectedObject == null)
            {
                return meshes;
            }

            HashSet<VertexKey> selectedKeys = state.GetSelectedVertexKeys();
            List<MeshFilter> filters = ColliderEditorSelectionUtility.GetMeshFilters(state.SelectedObject, state.IncludeChildren);
            int filterCount = filters.Count;

            for (int filterIndex = 0; filterIndex < filterCount; filterIndex++)
            {
                MeshFilter filter = filters[filterIndex];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Mesh sourceMesh = filter.sharedMesh;
                Matrix4x4 toRoot = state.SelectedObject.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);

                for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                {
                    Mesh extractedMesh = BuildSelectedSubMesh(filter, sourceMesh, subMeshIndex, selectedKeys, toRoot);
                    if (extractedMesh != null)
                    {
                        meshes.Add(extractedMesh);
                    }
                }
            }

            return meshes;
        }

        public static Mesh BuildConvexFallbackMesh(ColliderEditorState state)
        {
            List<Vector3> points = GetSelectedWorldPoints(state);
            if (points.Count < 4 || state.SelectedObject == null)
            {
                return null;
            }

            ColliderQuickHull quickHull = ColliderQuickHull.CalculateHullWorld(points, state.SelectedObject.transform);
            return quickHull.result;
        }

        private static Mesh BuildSelectedSubMesh(
            MeshFilter filter,
            Mesh sourceMesh,
            int subMeshIndex,
            HashSet<VertexKey> selectedKeys,
            Matrix4x4 toRoot)
        {
            int[] sourceTriangles = sourceMesh.GetTriangles(subMeshIndex);
            if (sourceTriangles == null || sourceTriangles.Length < 3)
            {
                return null;
            }

            Vector3[] sourceVertices = sourceMesh.vertices;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            Dictionary<int, int> indexMap = new Dictionary<int, int>();

            int triangleCount = sourceTriangles.Length;
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex += 3)
            {
                int index0 = sourceTriangles[triangleIndex + 0];
                int index1 = sourceTriangles[triangleIndex + 1];
                int index2 = sourceTriangles[triangleIndex + 2];

                VertexKey key0 = new VertexKey(filter.transform, sourceVertices[index0]);
                VertexKey key1 = new VertexKey(filter.transform, sourceVertices[index1]);
                VertexKey key2 = new VertexKey(filter.transform, sourceVertices[index2]);

                if (!selectedKeys.Contains(key0) || !selectedKeys.Contains(key1) || !selectedKeys.Contains(key2))
                {
                    continue;
                }

                triangles.Add(GetOrAddIndex(index0, sourceVertices, toRoot, vertices, indexMap));
                triangles.Add(GetOrAddIndex(index1, sourceVertices, toRoot, vertices, indexMap));
                triangles.Add(GetOrAddIndex(index2, sourceVertices, toRoot, vertices, indexMap));
            }

            if (triangles.Count == 0)
            {
                return null;
            }

            Mesh extractedMesh = new Mesh();
            extractedMesh.name = sourceMesh.name + "_Selected_SubMesh_" + subMeshIndex;
            extractedMesh.SetVertices(vertices);
            extractedMesh.SetTriangles(triangles, 0);
            extractedMesh.RecalculateBounds();
            extractedMesh.RecalculateNormals();
            return extractedMesh;
        }

        private static int GetOrAddIndex(
            int sourceIndex,
            Vector3[] sourceVertices,
            Matrix4x4 toRoot,
            List<Vector3> vertices,
            Dictionary<int, int> indexMap)
        {
            if (indexMap.TryGetValue(sourceIndex, out int existingIndex))
            {
                return existingIndex;
            }

            Vector3 localPoint = toRoot.MultiplyPoint3x4(sourceVertices[sourceIndex]);
            int index = vertices.Count;
            vertices.Add(localPoint);
            indexMap.Add(sourceIndex, index);
            return index;
        }
    }
}
