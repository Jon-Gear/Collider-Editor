#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColliderEditor
{
	/// <summary>
	/// Partial scene preview implementation for ColliderEditor.
	/// This file owns the editable mesh overlay because it is the heaviest SceneView path:
	/// high vertex selections can easily generate thousands of vertices, edges and faces.
	/// The overlay therefore keeps dirty caches and draws batched geometry instead of
	/// recalculating mesh topology and issuing one Handle call per element every repaint.
	/// </summary>
	public sealed class MeshVerticesPreview
	{
		private readonly PreviewState PREVIEW_STATE;
		private readonly DrawingService DRAW;

		public MeshVerticesPreview(PreviewState previewState, DrawingService draw)
		{
			this.PREVIEW_STATE = previewState;
			this.DRAW = draw;
		}

		public void DrawVertices(ColliderEditorState state)
		{
			List<MeshFilter> meshFilters = ColliderEditorSelectionUtility.GetMeshFilters(state.SelectedObject, state.IncludeChildren);
			if(meshFilters.Count == 0)
			{
				return;
			}

			ResetOverlayCachesIfTargetChanged(state);

			HashSet<VertexKey> selectedVertexKeys = state.GetSelectedVertexKeys();
			HashSet<VertexKey> hiddenVertexKeys = state.GetHiddenVertexKeys();
			int selectedSignature = Geometry.CalculateVertexListSignature(state.SelectedVertices);
			int hiddenSignature = Geometry.CalculateVertexListSignature(state.HiddenVertices);

			CompareFunction previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.LessEqual;

			for(int index = 0; index < meshFilters.Count; index++)
			{
				DrawEditableMeshOverlay(
					meshFilters[index],
					selectedVertexKeys,
					hiddenVertexKeys,
					selectedSignature,
					hiddenSignature,
					state);
			}

			Handles.zTest = previousZTest;
		}

		/// <summary>
		/// Draws one mesh filter as an edit-mode overlay.
		/// Mesh topology and selection-dependent overlays are cached because SceneView repaint can happen
		/// many times without the artist changing selection.
		/// </summary>
		private void DrawEditableMeshOverlay(
			MeshFilter meshFilter,
			HashSet<VertexKey> selectedVertexKeys,
			HashSet<VertexKey> hiddenVertexKeys,
			int selectedSignature,
			int hiddenSignature,
			ColliderEditorState state)
		{
			if(meshFilter == null || meshFilter.sharedMesh == null)
			{
				return;
			}

			MeshOverlayCache cache = GetOrCreateOverlayCache(
				meshFilter,
				selectedVertexKeys,
				hiddenVertexKeys,
				selectedSignature,
				hiddenSignature,
				state);

			if(cache == null || cache.meshData == null || cache.meshData.localVertices.Length == 0)
			{
				return;
			}

			DrawCachedWireframe(cache, meshFilter.transform);
			DrawCachedSelectedFaces(cache, meshFilter.transform);
			DrawCachedSelectedEdges(cache, meshFilter.transform);
			DrawCachedVertexPoints(cache, state);
		}

		/// <summary>
		/// Gets the per-filter overlay cache, rebuilding only the parts invalidated by mesh or selection changes.
		/// </summary>
		private MeshOverlayCache GetOrCreateOverlayCache(
			MeshFilter meshFilter,
			HashSet<VertexKey> selectedVertexKeys,
			HashSet<VertexKey> hiddenVertexKeys,
			int selectedSignature,
			int hiddenSignature,
			ColliderEditorState state)
		{
			int instanceId = meshFilter.GetInstanceID();
			MeshOverlayCache cache;
			if(!PREVIEW_STATE.MESH_OVERLAY_CACHES.TryGetValue(instanceId, out cache))
			{
				cache = new MeshOverlayCache();
				cache.meshFilter = meshFilter;
				PREVIEW_STATE.MESH_OVERLAY_CACHES.Add(instanceId, cache);
			}

			Mesh sharedMesh = meshFilter.sharedMesh;
			if(cache.sourceMesh != sharedMesh || cache.meshData == null)
			{
				PREVIEW_STATE.DestroyOverlayCacheMeshes(cache);
				cache.sourceMesh = sharedMesh;
				cache.meshData = CreateMeshOverlayData(sharedMesh);
				cache.selectionSignature = 0;
				cache.hiddenSignature = 0;
				cache.vertexDrawSize = -1.0f;
				cache.selectedVertexDrawSize = -1.0f;
				cache.cachedMatrix = Matrix4x4.zero;
				cache.pointMeshMatrix = Matrix4x4.zero;
			}

			Matrix4x4 currentMatrix = meshFilter.transform.localToWorldMatrix;
			bool selectionChanged =
				cache.selectionSignature != selectedSignature ||
				cache.hiddenSignature != hiddenSignature ||
				!Mathf.Approximately(cache.vertexDrawSize, state.VertexDrawSize) ||
				!Mathf.Approximately(cache.selectedVertexDrawSize, state.SelectedVertexDrawSize) ||
				!cache.pointMeshMatrix.Equals(currentMatrix);

			if(selectionChanged)
			{
				RebuildSelectionOverlayCache(
					cache,
					meshFilter.transform,
					selectedVertexKeys,
					hiddenVertexKeys,
					selectedSignature,
					hiddenSignature,
					state);
			}

			return cache;
		}

		/// <summary>
		/// Captures Unity mesh data once. Accessing mesh.vertices and mesh.triangles allocates arrays,
		/// so doing it every SceneView repaint causes unnecessary GC pressure on dense meshes.
		/// </summary>
		private MeshOverlayData CreateMeshOverlayData(Mesh mesh)
		{
			MeshOverlayData meshData = new MeshOverlayData();
			meshData.localVertices = mesh.vertices;
			meshData.triangles = mesh.triangles;
			meshData.localNormals = GetSafeMeshNormals(mesh, meshData.localVertices, meshData.triangles);
			meshData.localOverlayDepthBias = CalculateLocalOverlayDepthBias(mesh);
			meshData.edges = CreateUniqueEdges(meshData.triangles, meshData.localVertices.Length);
			return meshData;
		}

		private float CalculateLocalOverlayDepthBias(Mesh mesh)
		{
			float meshSize = mesh.bounds.size.magnitude;
			return Mathf.Max(meshSize * PreviewState.EDIT_OVERLAY_DEPTH_BIAS, PreviewState.MINIMUM_VERTEX_WORLD_SIZE);
		}

		private Vector3[] GetSafeMeshNormals(Mesh mesh, Vector3[] localVertices, int[] triangles)
		{
			Vector3[] meshNormals = mesh.normals;
			if(meshNormals != null && meshNormals.Length == localVertices.Length)
			{
				return meshNormals;
			}

			Vector3[] generatedNormals = new Vector3[localVertices.Length];
			if(triangles == null)
			{
				for(int vertexIndex = 0; vertexIndex < generatedNormals.Length; vertexIndex++)
				{
					generatedNormals[vertexIndex] = Vector3.up;
				}

				return generatedNormals;
			}

			for(int triangleIndex = 0; triangleIndex + 2 < triangles.Length; triangleIndex += 3)
			{
				int firstIndex = triangles[triangleIndex];
				int secondIndex = triangles[triangleIndex + 1];
				int thirdIndex = triangles[triangleIndex + 2];

				if(!Geometry.IsValidTriangleIndex(firstIndex, secondIndex, thirdIndex, localVertices.Length))
				{
					continue;
				}

				Vector3 firstVertex = localVertices[firstIndex];
				Vector3 secondVertex = localVertices[secondIndex];
				Vector3 thirdVertex = localVertices[thirdIndex];
				Vector3 faceNormal = Vector3.Cross(secondVertex - firstVertex, thirdVertex - firstVertex);

				generatedNormals[firstIndex] += faceNormal;
				generatedNormals[secondIndex] += faceNormal;
				generatedNormals[thirdIndex] += faceNormal;
			}

			for(int vertexIndex = 0; vertexIndex < generatedNormals.Length; vertexIndex++)
			{
				if(generatedNormals[vertexIndex].sqrMagnitude > Mathf.Epsilon)
				{
					generatedNormals[vertexIndex].Normalize();
					continue;
				}

				generatedNormals[vertexIndex] = Vector3.up;
			}

			return generatedNormals;
		}

		/// <summary>
		/// Creates a de-duplicated list of mesh edges from triangle data once per source mesh.
		/// </summary>
		private EdgeKey[] CreateUniqueEdges(int[] triangles, int vertexCount)
		{
			HashSet<EdgeKey> uniqueEdges = new HashSet<EdgeKey>();

			for(int triangleIndex = 0; triangleIndex + 2 < triangles.Length; triangleIndex += 3)
			{
				int firstIndex = triangles[triangleIndex];
				int secondIndex = triangles[triangleIndex + 1];
				int thirdIndex = triangles[triangleIndex + 2];

				AddUniqueEdge(uniqueEdges, firstIndex, secondIndex, vertexCount);
				AddUniqueEdge(uniqueEdges, secondIndex, thirdIndex, vertexCount);
				AddUniqueEdge(uniqueEdges, thirdIndex, firstIndex, vertexCount);
			}

			EdgeKey[] edges = new EdgeKey[uniqueEdges.Count];
			uniqueEdges.CopyTo(edges);
			return edges;
		}

		private void AddUniqueEdge(HashSet<EdgeKey> uniqueEdges, int firstIndex, int secondIndex, int vertexCount)
		{
			if(!Geometry.IsValidVertexIndex(firstIndex, vertexCount) || !Geometry.IsValidVertexIndex(secondIndex, vertexCount))
			{
				return;
			}

			uniqueEdges.Add(new EdgeKey(firstIndex, secondIndex));
		}

		/// <summary>
		/// Rebuilds only the data that depends on the current selected and hidden vertex sets.
		/// This is the expensive pass that used to run every repaint; now it only runs when the selection changes.
		/// </summary>
		private void RebuildSelectionOverlayCache(
			MeshOverlayCache cache,
			Transform meshTransform,
			HashSet<VertexKey> selectedVertexKeys,
			HashSet<VertexKey> hiddenVertexKeys,
			int selectedSignature,
			int hiddenSignature,
			ColliderEditorState state)
		{
			PREVIEW_STATE.DestroyMesh(ref cache.unselectedPointMesh);
			PREVIEW_STATE.DestroyMesh(ref cache.selectedPointMesh);
			PREVIEW_STATE.DestroyMesh(ref cache.selectedFaceMesh);

			cache.selectionSignature = selectedSignature;
			cache.hiddenSignature = hiddenSignature;
			cache.vertexDrawSize = state.VertexDrawSize;
			cache.selectedVertexDrawSize = state.SelectedVertexDrawSize;
			cache.pointMeshMatrix = meshTransform.localToWorldMatrix;
			cache.cachedMatrix = Matrix4x4.zero;
			cache.wireframeWorldLines = null;
			cache.selectedEdgeWorldLines = null;

			MeshOverlayData meshData = cache.meshData;
			Vector3[] localVertices = meshData.localVertices;
			bool[] visibleIndices = new bool[localVertices.Length];
			bool[] selectedIndices = new bool[localVertices.Length];
			List<Vector3> unselectedWorldPoints = new List<Vector3>(localVertices.Length);
			List<Vector3> selectedWorldPoints = new List<Vector3>(Mathf.Min(selectedVertexKeys.Count, localVertices.Length));

			for(int vertexIndex = 0; vertexIndex < localVertices.Length; vertexIndex++)
			{
				Vector3 localPosition = localVertices[vertexIndex];
				VertexKey vertexKey = new VertexKey(meshTransform, localPosition);

				if(hiddenVertexKeys.Contains(vertexKey))
				{
					continue;
				}

				visibleIndices[vertexIndex] = true;

				bool isSelected = selectedVertexKeys.Contains(vertexKey);
				selectedIndices[vertexIndex] = isSelected;

				Vector3 worldPosition = meshTransform.TransformPoint(localPosition);
				if(isSelected)
				{
					selectedWorldPoints.Add(worldPosition);
				}
				else
				{
					unselectedWorldPoints.Add(worldPosition);
				}
			}

			cache.visibleEdgePairs = CreateVisibleTriangleEdgePairs(meshData.triangles, visibleIndices);
			cache.selectedEdgePairs = CreateSelectedEdgePairs(meshData.edges, selectedIndices);
			cache.selectedFaceMesh = CreateSelectedFaceMesh(meshData, selectedIndices);
			cache.unselectedPointMesh = CreatePointMesh(unselectedWorldPoints, CalculateWorldPointSize(unselectedWorldPoints, state.VertexDrawSize), "ColliderEditor_UnselectedVertexBatch");
			cache.selectedPointMesh = CreatePointMesh(selectedWorldPoints, CalculateWorldPointSize(selectedWorldPoints, state.SelectedVertexDrawSize), "ColliderEditor_SelectedVertexBatch");
		}

		private EdgeKey[] CreateVisibleTriangleEdgePairs(int[] sourceTriangles, bool[] visibleIndices)
		{
			HashSet<EdgeKey> visibleEdges = new HashSet<EdgeKey>();

			for(int triangleIndex = 0; triangleIndex + 2 < sourceTriangles.Length; triangleIndex += 3)
			{
				int firstIndex = sourceTriangles[triangleIndex];
				int secondIndex = sourceTriangles[triangleIndex + 1];
				int thirdIndex = sourceTriangles[triangleIndex + 2];

				if(!Geometry.IsValidTriangleIndex(firstIndex, secondIndex, thirdIndex, visibleIndices.Length))
				{
					continue;
				}

				if(!visibleIndices[firstIndex] || !visibleIndices[secondIndex] || !visibleIndices[thirdIndex])
				{
					continue;
				}

				visibleEdges.Add(new EdgeKey(firstIndex, secondIndex));
				visibleEdges.Add(new EdgeKey(secondIndex, thirdIndex));
				visibleEdges.Add(new EdgeKey(thirdIndex, firstIndex));
			}

			EdgeKey[] edges = new EdgeKey[visibleEdges.Count];
			visibleEdges.CopyTo(edges);
			return edges;
		}

		private EdgeKey[] CreateSelectedEdgePairs(EdgeKey[] edges, bool[] selectedIndices)
		{
			List<EdgeKey> selectedEdges = new List<EdgeKey>();

			for(int index = 0; index < edges.Length; index++)
			{
				EdgeKey edge = edges[index];
				if(selectedIndices[edge.StartIndex] || selectedIndices[edge.EndIndex])
				{
					selectedEdges.Add(edge);
				}
			}

			return selectedEdges.ToArray();
		}

		/// <summary>
		/// Builds one transparent mesh for selected faces instead of calling Handles.DrawAAConvexPolygon per triangle.
		/// </summary>
		private Mesh CreateSelectedFaceMesh(MeshOverlayData meshData, bool[] selectedIndices)
		{
			int[] sourceTriangles = meshData.triangles;
			List<int> selectedTriangles = null;

			for(int triangleIndex = 0; triangleIndex + 2 < sourceTriangles.Length; triangleIndex += 3)
			{
				int firstIndex = sourceTriangles[triangleIndex];
				int secondIndex = sourceTriangles[triangleIndex + 1];
				int thirdIndex = sourceTriangles[triangleIndex + 2];

				if(!Geometry.IsValidTriangleIndex(firstIndex, secondIndex, thirdIndex, selectedIndices.Length))
				{
					continue;
				}

				if(!selectedIndices[firstIndex] || !selectedIndices[secondIndex] || !selectedIndices[thirdIndex])
				{
					continue;
				}

				if(selectedTriangles == null)
				{
					selectedTriangles = new List<int>();
				}

				selectedTriangles.Add(firstIndex);
				selectedTriangles.Add(secondIndex);
				selectedTriangles.Add(thirdIndex);
			}

			if(selectedTriangles == null || selectedTriangles.Count == 0)
			{
				return null;
			}

			Mesh mesh = new Mesh();
			mesh.name = "ColliderEditor_SelectedFaceBatch";
			if(meshData.localVertices.Length > 65000)
			{
				mesh.indexFormat = IndexFormat.UInt32;
			}

			mesh.vertices = meshData.localVertices;
			mesh.triangles = selectedTriangles.ToArray();
			mesh.RecalculateBounds();
			return mesh;
		}

		private Mesh CreatePointMesh(List<Vector3> worldPoints, float pointSize, string meshName)
		{
			if(worldPoints == null || worldPoints.Count == 0)
			{
				return null;
			}

			Vector3[] worldPositions = new Vector3[worldPoints.Count];
			for(int index = 0; index < worldPoints.Count; index++)
			{
				worldPositions[index] = worldPoints[index];
			}

			Mesh mesh = CreateCombinedPointMesh(worldPositions, pointSize);
			mesh.name = meshName;
			return mesh;
		}

		private void DrawCachedWireframe(MeshOverlayCache cache, Transform meshTransform)
		{
			EnsureWorldLineCache(cache, meshTransform);

			if(cache.wireframeWorldLines == null || cache.wireframeWorldLines.Length == 0)
			{
				return;
			}

			Handles.color = PreviewColors.BLENDER_WIREFRAME_COLOR;
			Handles.DrawLines(cache.wireframeWorldLines);
		}

		private void DrawCachedSelectedEdges(MeshOverlayCache cache, Transform meshTransform)
		{
			EnsureWorldLineCache(cache, meshTransform);

			if(cache.selectedEdgeWorldLines == null || cache.selectedEdgeWorldLines.Length == 0)
			{
				return;
			}

			Handles.color = PreviewColors.BLENDER_SELECTED_EDGE_COLOR;
			Handles.DrawLines(cache.selectedEdgeWorldLines);
		}

		private void EnsureWorldLineCache(MeshOverlayCache cache, Transform meshTransform)
		{
			Matrix4x4 localToWorld = meshTransform.localToWorldMatrix;
			if(cache.cachedMatrix.Equals(localToWorld) && cache.wireframeWorldLines != null)
			{
				return;
			}

			cache.cachedMatrix = localToWorld;
			cache.wireframeWorldLines = CreateWorldLineSegments(cache.meshData.localVertices, cache.meshData.localNormals, cache.meshData.localOverlayDepthBias, cache.visibleEdgePairs, meshTransform);
			cache.selectedEdgeWorldLines = CreateWorldLineSegments(cache.meshData.localVertices, cache.meshData.localNormals, cache.meshData.localOverlayDepthBias, cache.selectedEdgePairs, meshTransform);
		}

		private Vector3[] CreateWorldLineSegments(Vector3[] localVertices, Vector3[] localNormals, float localOverlayDepthBias, EdgeKey[] edges, Transform meshTransform)
		{
			if(edges == null || edges.Length == 0)
			{
				return null;
			}

			Vector3[] worldLines = new Vector3[edges.Length * 2];

			for(int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
			{
				EdgeKey edge = edges[edgeIndex];
				int lineIndex = edgeIndex * 2;
				worldLines[lineIndex] = GetOverlayPosition(localVertices[edge.StartIndex], localNormals[edge.StartIndex], localOverlayDepthBias, meshTransform);
				worldLines[lineIndex + 1] = GetOverlayPosition(localVertices[edge.EndIndex], localNormals[edge.EndIndex], localOverlayDepthBias, meshTransform);
			}

			return worldLines;
		}

		private void DrawCachedSelectedFaces(MeshOverlayCache cache, Transform meshTransform)
		{
			if(cache.selectedFaceMesh == null)
			{
				return;
			}

			PREVIEW_STATE.meshPreviewMaterial.SetColor("_Color", PreviewColors.BLENDER_SELECTED_FACE_COLOR);
			PREVIEW_STATE.meshPreviewMaterial.SetPass(PreviewState.MESH_OVERLAY_PASS_INDEX);
			Graphics.DrawMeshNow(cache.selectedFaceMesh, meshTransform.localToWorldMatrix);
		}

		private void DrawCachedVertexPoints(MeshOverlayCache cache, ColliderEditorState state)
		{
			if(cache.unselectedPointMesh != null)
			{
				PREVIEW_STATE.meshPreviewMaterial.SetColor("_Color", state.VertexColor);
				PREVIEW_STATE.meshPreviewMaterial.SetPass(PreviewState.MESH_OVERLAY_PASS_INDEX);
				Graphics.DrawMeshNow(cache.unselectedPointMesh, Matrix4x4.identity);
			}

			if(cache.selectedPointMesh != null)
			{
				PREVIEW_STATE.meshPreviewMaterial.SetColor("_Color", state.SelectedVertexColor);
				PREVIEW_STATE.meshPreviewMaterial.SetPass(PreviewState.MESH_OVERLAY_PASS_INDEX);
				Graphics.DrawMeshNow(cache.selectedPointMesh, Matrix4x4.identity);
			}
		}

		private float CalculateWorldPointSize(List<Vector3> worldPoints, float drawSize)
		{
			if(worldPoints == null || worldPoints.Count == 0)
			{
				return PreviewState.MINIMUM_VERTEX_WORLD_SIZE;
			}

			Bounds bounds = new Bounds(worldPoints[0], Vector3.zero);
			for(int index = 1; index < worldPoints.Count; index++)
			{
				bounds.Encapsulate(worldPoints[index]);
			}

			float objectScaledSize = bounds.size.magnitude * Mathf.Max(drawSize, PreviewState.MINIMUM_SCREEN_VERTEX_SIZE) * 0.02f;
			return Mathf.Max(objectScaledSize, PreviewState.MINIMUM_VERTEX_WORLD_SIZE);
		}

		/// <summary>
		/// Clears overlay caches when the target object or child inclusion mode changes.
		/// This prevents old mesh filters from accumulating as the artist switches between assets.
		/// </summary>
		private void ResetOverlayCachesIfTargetChanged(ColliderEditorState state)
		{
			if(PREVIEW_STATE.cachedOverlayOwner == state.SelectedObject && PREVIEW_STATE.cachedOverlayIncludeChildren == state.IncludeChildren)
			{
				return;
			}

			PREVIEW_STATE.ClearMeshOverlayCaches();
			PREVIEW_STATE.cachedOverlayOwner = state.SelectedObject;
			PREVIEW_STATE.cachedOverlayIncludeChildren = state.IncludeChildren;
		}

		/// <summary>
		/// Moves edit-mode wireframe geometry slightly along the mesh surface normal.
		/// This avoids z-fighting with the MeshRenderer without making the cached line positions depend on
		/// the Scene camera direction.
		/// </summary>
		private Vector3 GetOverlayPosition(Vector3 localPosition, Vector3 localNormal, float localOverlayDepthBias, Transform meshTransform)
		{
			Vector3 worldPosition = meshTransform.TransformPoint(localPosition);
			Vector3 worldNormal = meshTransform.TransformDirection(localNormal);

			if(worldNormal.sqrMagnitude <= Mathf.Epsilon)
			{
				worldNormal = meshTransform.TransformDirection(Vector3.up);
			}

			worldNormal.Normalize();

			float largestScale = Mathf.Max(
				Mathf.Abs(meshTransform.lossyScale.x),
				Mathf.Max(Mathf.Abs(meshTransform.lossyScale.y), Mathf.Abs(meshTransform.lossyScale.z)));

			float bias = Mathf.Max(localOverlayDepthBias * largestScale, PreviewState.MINIMUM_VERTEX_WORLD_SIZE);
			return worldPosition + worldNormal * bias;
		}

		/// <summary>
		/// Builds a single mesh containing one octahedron per point.
		/// </summary>
		private Mesh CreateCombinedPointMesh(Vector3[] worldPositions, float pointSize)
		{
			Mesh template = DRAW.GetPreviewVertexDotMesh();
			Vector3[] templateVertices = template.vertices;
			Vector3[] templateNormals = template.normals;
			int[] templateTriangles = template.triangles;

			int templateVertexCount = templateVertices.Length;
			int templateTriangleCount = templateTriangles.Length;
			int totalVertexCount = templateVertexCount * worldPositions.Length;
			int totalTriangleCount = templateTriangleCount * worldPositions.Length;

			Vector3[] vertices = new Vector3[totalVertexCount];
			Vector3[] normals = new Vector3[totalVertexCount];
			int[] triangles = new int[totalTriangleCount];

			for(int pointIndex = 0; pointIndex < worldPositions.Length; pointIndex++)
			{
				int vertexOffset = pointIndex * templateVertexCount;
				int triangleOffset = pointIndex * templateTriangleCount;
				Vector3 worldPosition = worldPositions[pointIndex];

				for(int vertexIndex = 0; vertexIndex < templateVertexCount; vertexIndex++)
				{
					vertices[vertexOffset + vertexIndex] =
						worldPosition + (templateVertices[vertexIndex] * pointSize);
					normals[vertexOffset + vertexIndex] = templateNormals[vertexIndex];
				}

				for(int triangleIndex = 0; triangleIndex < templateTriangleCount; triangleIndex++)
				{
					triangles[triangleOffset + triangleIndex] =
						vertexOffset + templateTriangles[triangleIndex];
				}
			}

			Mesh mesh = new Mesh();
			mesh.name = "ColliderEditor_VertexPreviewMesh";
			if(totalVertexCount > 65000)
			{
				mesh.indexFormat = IndexFormat.UInt32;
			}

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.triangles = triangles;
			mesh.RecalculateBounds();
			return mesh;
		}

	}
}
#endif
