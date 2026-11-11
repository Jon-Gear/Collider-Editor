using System;
using UnityEngine;

namespace ColliderEditor
{
    /// <summary>
    /// Stores a plain mesh snapshot that can safely be passed to worker threads.
    /// The editor captures this data on the main thread, then the heavy decomposition
    /// work can operate on the copied arrays without touching UnityEngine.Mesh again.
    /// </summary>
    [Serializable]
    public sealed class ColliderEditorMeshData
    {
        public Vector3[] vertices;
        public int[] triangles;

        public static ColliderEditorMeshData Create(Mesh mesh)
        {
            if (mesh == null)
            {
                return null;
            }

            ColliderEditorMeshData meshData = new ColliderEditorMeshData();
            meshData.vertices = mesh.vertices;
            meshData.triangles = mesh.triangles;
            return meshData;
        }

        public Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
