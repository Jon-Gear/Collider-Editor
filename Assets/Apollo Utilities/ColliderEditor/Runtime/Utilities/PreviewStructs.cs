using System;
using UnityEngine;

namespace ColliderEditor
{

	/// <summary>
	/// Stores a triangle edge with a stable order so it can be de-duplicated before drawing.
	/// </summary>
	public readonly struct EdgeKey: IEquatable<EdgeKey>
	{
		private readonly int START_INDEX;
		private readonly int END_INDEX;

		public EdgeKey(int firstIndex, int secondIndex)
		{
			if(firstIndex < secondIndex)
			{
				START_INDEX = firstIndex;
				END_INDEX = secondIndex;
			}
			else
			{
				START_INDEX = secondIndex;
				END_INDEX = firstIndex;
			}
		}

		public int StartIndex
		{
			get { return START_INDEX; }
		}

		public int EndIndex
		{
			get { return END_INDEX; }
		}

		public bool Equals(EdgeKey other)
		{
			return START_INDEX == other.START_INDEX && END_INDEX == other.END_INDEX;
		}

		public override bool Equals(object obj)
		{
			return obj is EdgeKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (START_INDEX * 397) ^ END_INDEX;
			}
		}
	}

	/// <summary>
	/// Mesh data copied from Unity once so SceneView repaint does not repeatedly allocate arrays.
	/// </summary>
	public sealed class MeshOverlayData
	{
		public Vector3[] localVertices;
		public Vector3[] localNormals;
		public float localOverlayDepthBias;
		public int[] triangles;
		public EdgeKey[] edges;
	}

	/// <summary>
	/// Per-MeshFilter overlay cache. Selection-dependent meshes are rebuilt only when selection changes,
	/// while world-space line arrays are rebuilt only when the object transform changes.
	/// </summary>
	public sealed class MeshOverlayCache
	{
		public MeshFilter meshFilter;
		public Mesh sourceMesh;
		public MeshOverlayData meshData;
		public int selectionSignature;
		public int hiddenSignature;
		public float vertexDrawSize;
		public float selectedVertexDrawSize;
		public Matrix4x4 cachedMatrix;
		public Matrix4x4 pointMeshMatrix;
		public EdgeKey[] visibleEdgePairs;
		public EdgeKey[] selectedEdgePairs;
		public Vector3[] wireframeWorldLines;
		public Vector3[] selectedEdgeWorldLines;
		public Mesh unselectedPointMesh;
		public Mesh selectedPointMesh;
		public Mesh selectedFaceMesh;
	}
}
