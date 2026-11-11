using System.Collections.Generic;
using UnityEngine;

namespace ColliderEditor
{
    public static class ColliderEditorSelectionUtility
    {
        public struct MeshVertex
        {
            public MeshFilter meshFilter;
            public Transform transform;
            public Vector3 localPosition;
            public Vector3 normal;
            public int vertexIndex;

            public MeshVertex(Transform transform, Vector3 localPosition, Vector3 normal)
            {
                this.meshFilter = null;
                this.transform = transform;
                this.localPosition = localPosition;
                this.normal = normal;
                this.vertexIndex = -1;
            }

            public MeshVertex(MeshFilter meshFilter, int vertexIndex, Vector3 localPosition, Vector3 normal)
            {
                this.meshFilter = meshFilter;
                this.transform = meshFilter != null ? meshFilter.transform : null;
                this.localPosition = localPosition;
                this.normal = normal;
                this.vertexIndex = vertexIndex;
            }

            public Vector3 WorldPosition
            {
                get
                {
                    if (transform == null)
                    {
                        return localPosition;
                    }

                    return transform.TransformPoint(localPosition);
                }
            }

            public ColliderEditorState.SelectedVertex ToSelectedVertex()
            {
                return new ColliderEditorState.SelectedVertex(transform, localPosition, normal);
            }
        }

        public static List<MeshFilter> GetMeshFilters(GameObject selectedObject, bool includeChildren)
        {
            List<MeshFilter> filters = new List<MeshFilter>();

            if (selectedObject == null)
            {
                return filters;
            }

            if (includeChildren)
            {
                MeshFilter[] childFilters = selectedObject.GetComponentsInChildren<MeshFilter>();

                int childCount = childFilters.Length;
                for (int i = 0; i < childCount; i++)
                {
                    if (childFilters[i] != null && childFilters[i].sharedMesh != null)
                    {
                        filters.Add(childFilters[i]);
                    }
                }
            }
            else
            {
                MeshFilter filter = selectedObject.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    filters.Add(filter);
                }
            }

            return filters;
        }

        public static List<MeshVertex> GetMeshVertices(GameObject selectedObject, bool includeChildren)
        {
            List<MeshVertex> vertices = new List<MeshVertex>();
            List<MeshFilter> filters = GetMeshFilters(selectedObject, includeChildren);

            int filterCount = filters.Count;
            for (int i = 0; i < filterCount; i++)
            {
                MeshFilter filter = filters[i];
                Mesh mesh = filter.sharedMesh;
                Vector3[] meshVertices = mesh.vertices;
                Vector3[] meshNormals = mesh.normals;
                bool hasNormals = meshNormals != null && meshNormals.Length == meshVertices.Length;

                int vertexCount = meshVertices.Length;
                for (int j = 0; j < vertexCount; j++)
                {
                    Vector3 normal = hasNormals ? meshNormals[j] : Vector3.up;
                    vertices.Add(new MeshVertex(filter, j, meshVertices[j], normal));
                }
            }

            return vertices;
        }
    }
}
