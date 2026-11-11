using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace ColliderEditor
{
    public static unsafe class ColliderEditorCoacd
    {
        [Serializable]
        public struct MeshInterface
        {
            public double* verticesPtr;
            public ulong verticesCount;
            public int* trianglesPtr;
            public ulong trianglesCount;
        }

        [Serializable]
        public struct MeshArrayInterface : IDisposable
        {
            public MeshInterface* meshesPtr;
            public ulong meshesCount;

            public void Dispose()
            {
                if (meshesPtr != null)
                {
                    Free(this);
                    meshesPtr = null;
                }
            }
        }

        [DllImport("lib_coacd", CallingConvention = CallingConvention.Cdecl, EntryPoint = "CoACD_freeMeshArray")]
        private static extern void Free(MeshArrayInterface array);

        [DllImport("lib_coacd", CallingConvention = CallingConvention.Cdecl, EntryPoint = "CoACD_run")]
        private static extern MeshArrayInterface Run(
            ref MeshInterface mesh,
            double threshold,
            int maxConvexHull,
            int preprocessMode,
            int preprocessResolution,
            int sampleResolution,
            int mctsNodes,
            int mctsIteration,
            int mctsMaxDepth,
            bool pca,
            bool merge,
            uint seed);

        public static List<Mesh> Decompose(Mesh mesh, ColliderEditorState.CoacdSettings settings)
        {
            List<Mesh> resultMeshes = new List<Mesh>();
            ColliderEditorMeshData meshData = ColliderEditorMeshData.Create(mesh);
            List<ColliderEditorMeshData> decomposedMeshes = Decompose(meshData, settings);

            int count = decomposedMeshes.Count;
            for (int i = 0; i < count; i++)
            {
                resultMeshes.Add(decomposedMeshes[i].CreateMesh());
            }

            return resultMeshes;
        }

        public static List<ColliderEditorMeshData> Decompose(
            ColliderEditorMeshData meshData,
            ColliderEditorState.CoacdSettings settings,
            System.Action<float, string> progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            List<ColliderEditorMeshData> resultMeshes = new List<ColliderEditorMeshData>();

            if (meshData == null || meshData.vertices == null || meshData.triangles == null)
            {
                return resultMeshes;
            }

            progressCallback?.Invoke(0.1f, "Preparing CoACD input...");
            cancellationToken.ThrowIfCancellationRequested();

            Vector3[] unityVertices = meshData.vertices;
            int[] unityTriangles = meshData.triangles;
            double[] vertices = new double[unityVertices.Length * 3];

            int vertexCount = unityVertices.Length;
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[(3 * i) + 0] = unityVertices[i].x;
                vertices[(3 * i) + 1] = unityVertices[i].y;
                vertices[(3 * i) + 2] = unityVertices[i].z;
            }

            progressCallback?.Invoke(0.25f, "Running CoACD...");
            cancellationToken.ThrowIfCancellationRequested();

            fixed (double* vertexPointer = vertices)
            fixed (int* trianglePointer = unityTriangles)
            {
                MeshInterface meshInterface = new MeshInterface();
                meshInterface.verticesPtr = vertexPointer;
                meshInterface.verticesCount = (ulong)unityVertices.Length;
                meshInterface.trianglesPtr = trianglePointer;
                meshInterface.trianglesCount = (ulong)(unityTriangles.LongLength / 3);

                using MeshArrayInterface meshArray = Run(
                    ref meshInterface,
                    settings.threshold,
                    settings.maxConvexHull,
                    settings.preprocessMode,
                    settings.preprocessResolution,
                    settings.sampleResolution,
                    settings.mctsNodes,
                    settings.mctsIteration,
                    settings.mctsMaxDepth,
                    settings.pca,
                    settings.merge,
                    settings.seed);

                progressCallback?.Invoke(0.9f, "Collecting CoACD hulls...");
                cancellationToken.ThrowIfCancellationRequested();

                for (ulong meshIndex = 0; meshIndex < meshArray.meshesCount; meshIndex++)
                {
                    MeshInterface currentMesh = meshArray.meshesPtr[meshIndex];
                    Vector3[] outputVertices = new Vector3[currentMesh.verticesCount];
                    int[] outputTriangles = new int[currentMesh.trianglesCount * 3];

                    for (ulong vertexIndex = 0; vertexIndex < currentMesh.verticesCount; vertexIndex++)
                    {
                        outputVertices[vertexIndex] = new Vector3(
                            (float)currentMesh.verticesPtr[(vertexIndex * 3) + 0],
                            (float)currentMesh.verticesPtr[(vertexIndex * 3) + 1],
                            (float)currentMesh.verticesPtr[(vertexIndex * 3) + 2]);
                    }

                    ulong triangleValueCount = currentMesh.trianglesCount * 3;
                    for (ulong triangleIndex = 0; triangleIndex < triangleValueCount; triangleIndex++)
                    {
                        outputTriangles[triangleIndex] = currentMesh.trianglesPtr[triangleIndex];
                    }

                    ColliderEditorMeshData colliderMesh = new ColliderEditorMeshData();
                    colliderMesh.vertices = outputVertices;
                    colliderMesh.triangles = outputTriangles;
                    resultMeshes.Add(colliderMesh);
                }
            }

            progressCallback?.Invoke(1.0f, "CoACD finished.");
            return resultMeshes;
        }
    }
}
