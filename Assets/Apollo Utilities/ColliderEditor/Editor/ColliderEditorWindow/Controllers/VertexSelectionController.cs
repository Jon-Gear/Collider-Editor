using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

using System.Threading.Tasks;

#if(UNITY_6000_0_OR_NEWER)
using PhysicsMaterialType = UnityEngine.PhysicsMaterial;
#else
using PhysicsMaterialType = UnityEngine.PhysicMaterial;
#endif

namespace ColliderEditor
{
	public sealed class VertexSelectionController
	{
		private readonly ColliderEditorState STATE;

		private readonly UndoService UNDO;

		public event Action onRepaintPreview;

		public VertexSelectionController(ColliderEditorState state, UndoService undo)
		{
			this.STATE = state;
			this.UNDO = undo;
		}

		public void RepaintPreview()
		{
			onRepaintPreview?.Invoke();
		}

		public void HideSelectedVertices()
		{
			UNDO.RecordSelectionUndo("Hide Selected Vertices");
			STATE.HideSelectedVertices();
			RepaintPreview();
		}

		public void IsolateSelectedVertices()
		{
			UNDO.RecordSelectionUndo("Isolate Vertices");

			List<ColliderEditorSelectionUtility.MeshVertex> meshVertices =
				ColliderEditorSelectionUtility.GetMeshVertices(STATE.SelectedObject, STATE.IncludeChildren);
			List<ColliderEditorState.SelectedVertex> availableVertices =
				new List<ColliderEditorState.SelectedVertex>(meshVertices.Count);

			int count = meshVertices.Count;
			for(int i = 0; i < count; i++)
			{
				availableVertices.Add(meshVertices[i].ToSelectedVertex());
			}

			STATE.IsolateSelectedVertices(availableVertices);
			RepaintPreview();
		}

		public void UnhideAllVertices()
		{
			UNDO.RecordSelectionUndo("Unhide All Vertices");
			STATE.ClearHiddenVertices();
			RepaintPreview();
		}

		public void ToggleClosestVertex(Vector2 mousePosition, bool addToSelection, bool removeFromSelection)
		{
			ColliderEditorSelectionUtility.MeshVertex chosenMeshVertex;
			if(!TryFindClosestVisibleVertex(mousePosition, out chosenMeshVertex))
			{
				ClearSelectionWhenReplacing(addToSelection, removeFromSelection);
				return;
			}

			List<ColliderEditorState.SelectedVertex> selection = CreateSelectionCopy();
			ColliderEditorState.SelectedVertex chosenVertex = chosenMeshVertex.ToSelectedVertex();
			ToggleSelection(selection, chosenVertex, addToSelection, removeFromSelection);
			UNDO.RecordSelectionUndo("Change Vertex Selection");
			STATE.SetSelectedVertices(selection);
		}

		public void SelectClosestVertexHull(Vector2 mousePosition, bool addToSelection, bool removeFromSelection)
		{
			ColliderEditorSelectionUtility.MeshVertex chosenMeshVertex;
			if(!TryFindClosestVisibleVertex(mousePosition, out chosenMeshVertex))
			{
				ClearSelectionWhenReplacing(addToSelection, removeFromSelection);
				return;
			}

			List<ColliderEditorState.SelectedVertex> hullVertices = GetConnectedHullVertices(chosenMeshVertex);
			List<ColliderEditorState.SelectedVertex> selection = CreateSelectionCopy();

			if(!addToSelection && !removeFromSelection)
			{
				selection.Clear();
			}

			int count = hullVertices.Count;
			for(int i = 0; i < count; i++)
			{
				ToggleSelection(selection, hullVertices[i], true, removeFromSelection);
			}

			UNDO.RecordSelectionUndo("Select Vertex Hull");
			STATE.SetSelectedVertices(selection);
		}

		public void SelectVerticesInRect(Rect selectionRect, bool addToSelection, bool removeFromSelection)
		{
			List<ColliderEditorSelectionUtility.MeshVertex> vertices =
				ColliderEditorSelectionUtility.GetMeshVertices(STATE.SelectedObject, STATE.IncludeChildren);
			List<MeshFilter> meshFilters = ColliderEditorSelectionUtility.GetMeshFilters(STATE.SelectedObject, STATE.IncludeChildren);
			VisibleSelectionContext visibilityContext = VisibleSelectionContext.Create(STATE.ThroughSelect, meshFilters);
			HashSet<VertexKey> hiddenVertexKeys = STATE.GetHiddenVertexKeys();

			List<ColliderEditorState.SelectedVertex> selection = CreateSelectionCopy();
			if(!addToSelection && !removeFromSelection)
			{
				selection.Clear();
			}

			int count = vertices.Count;
			for(int i = 0; i < count; i++)
			{
				if(hiddenVertexKeys.Contains(vertices[i].ToSelectedVertex().Key))
				{
					continue;
				}

				Vector2 screenPoint = HandleUtility.WorldToGUIPoint(vertices[i].WorldPosition);
				if(!selectionRect.Contains(screenPoint))
				{
					continue;
				}

				// Do not raycast vertices that are outside the drag rectangle. This keeps
				// visible-only selection responsive on dense meshes and small selections.
				if(!visibilityContext.IsVertexSelectable(vertices[i].WorldPosition))
				{
					continue;
				}

				ColliderEditorState.SelectedVertex vertex = vertices[i].ToSelectedVertex();
				ToggleSelection(selection, vertex, true, removeFromSelection);
			}

			UNDO.RecordSelectionUndo("Change Vertex Selection");
			STATE.SetSelectedVertices(selection);
		}

		public void ClearVertexSelection()
		{
			UNDO.RecordSelectionUndo("Clear Vertex Selection");
			STATE.ClearSelection();
			SceneView.RepaintAll();
		}

		public void SelectAllVertices()
		{
			List<ColliderEditorSelectionUtility.MeshVertex> vertices =
				ColliderEditorSelectionUtility.GetMeshVertices(STATE.SelectedObject, STATE.IncludeChildren);

			HashSet<VertexKey> hiddenVertexKeys = STATE.GetHiddenVertexKeys();
			List<ColliderEditorState.SelectedVertex> selectedVertices = new List<ColliderEditorState.SelectedVertex>(vertices.Count);
			int count = vertices.Count;
			for(int i = 0; i < count; i++)
			{
				ColliderEditorState.SelectedVertex vertex = vertices[i].ToSelectedVertex();
				if(hiddenVertexKeys.Contains(vertex.Key))
				{
					continue;
				}

				selectedVertices.Add(vertex);
			}

			UNDO.RecordSelectionUndo("Select All Visible Vertices");
			STATE.SetSelectedVertices(selectedVertices);
			SceneView.RepaintAll();
		}

		private void ClearSelectionWhenReplacing(bool addToSelection, bool removeFromSelection)
		{
			if(addToSelection || removeFromSelection)
			{
				return;
			}

			UNDO.RecordSelectionUndo("Clear Vertex Selection");
			STATE.ClearSelection();
		}

		private bool TryFindClosestVisibleVertex(
			Vector2 mousePosition,
			out ColliderEditorSelectionUtility.MeshVertex closestVertex)
		{
			List<ColliderEditorSelectionUtility.MeshVertex> vertices =
				ColliderEditorSelectionUtility.GetMeshVertices(STATE.SelectedObject, STATE.IncludeChildren);
			List<MeshFilter> meshFilters = ColliderEditorSelectionUtility.GetMeshFilters(STATE.SelectedObject, STATE.IncludeChildren);
			VisibleSelectionContext visibilityContext = VisibleSelectionContext.Create(STATE.ThroughSelect, meshFilters);
			HashSet<VertexKey> hiddenVertexKeys = STATE.GetHiddenVertexKeys();

			closestVertex = new ColliderEditorSelectionUtility.MeshVertex();
			float bestDistance = ColliderEditorState.CLICK_SELECTION_RADIUS;
			int bestIndex = -1;

			int count = vertices.Count;
			for(int i = 0; i < count; i++)
			{
				if(hiddenVertexKeys.Contains(vertices[i].ToSelectedVertex().Key))
				{
					continue;
				}

				Vector2 screenPoint = HandleUtility.WorldToGUIPoint(vertices[i].WorldPosition);
				float distance = Vector2.Distance(mousePosition, screenPoint);

				if(distance >= bestDistance)
				{
					continue;
				}

				// Visibility is the expensive part, so it is only tested after the cheap
				// screen-distance filter says the vertex can actually become the clicked vertex.
				if(!visibilityContext.IsVertexSelectable(vertices[i].WorldPosition))
				{
					continue;
				}

				bestDistance = distance;
				bestIndex = i;
			}

			if(bestIndex < 0)
			{
				return false;
			}

			closestVertex = vertices[bestIndex];
			return true;
		}

		private List<ColliderEditorState.SelectedVertex> GetConnectedHullVertices(
			ColliderEditorSelectionUtility.MeshVertex sourceVertex)
		{
			List<ColliderEditorState.SelectedVertex> hullVertices = new List<ColliderEditorState.SelectedVertex>();
			MeshFilter meshFilter = sourceVertex.meshFilter;

			if(meshFilter == null || meshFilter.sharedMesh == null || sourceVertex.vertexIndex < 0)
			{
				hullVertices.Add(sourceVertex.ToSelectedVertex());
				return hullVertices;
			}

			Mesh mesh = meshFilter.sharedMesh;
			bool[] connectedVertexMask = CreateConnectedVertexMask(mesh, sourceVertex.vertexIndex);
			Vector3[] meshVertices = mesh.vertices;
			Vector3[] meshNormals = mesh.normals;
			bool hasNormals = meshNormals != null && meshNormals.Length == meshVertices.Length;
			HashSet<VertexKey> hiddenVertexKeys = STATE.GetHiddenVertexKeys();
			HashSet<VertexKey> addedVertexKeys = new HashSet<VertexKey>();

			int count = meshVertices.Length;
			for(int i = 0; i < count; i++)
			{
				if(!connectedVertexMask[i])
				{
					continue;
				}

				Vector3 normal = hasNormals ? meshNormals[i] : Vector3.up;
				ColliderEditorState.SelectedVertex vertex =
					new ColliderEditorState.SelectedVertex(meshFilter.transform, meshVertices[i], normal);

				if(hiddenVertexKeys.Contains(vertex.Key) || !addedVertexKeys.Add(vertex.Key))
				{
					continue;
				}

				hullVertices.Add(vertex);
			}

			return hullVertices;
		}

		private bool[] CreateConnectedVertexMask(Mesh mesh, int sourceVertexIndex)
		{
			int vertexCount = mesh != null ? mesh.vertexCount : 0;
			bool[] connectedVertexMask = new bool[vertexCount];

			if(sourceVertexIndex < 0 || sourceVertexIndex >= vertexCount)
			{
				return connectedVertexMask;
			}

			List<int>[] adjacency = BuildVertexAdjacency(mesh, vertexCount);
			Stack<int> verticesToVisit = new Stack<int>();
			connectedVertexMask[sourceVertexIndex] = true;
			verticesToVisit.Push(sourceVertexIndex);

			while(verticesToVisit.Count > 0)
			{
				int currentVertexIndex = verticesToVisit.Pop();
				List<int> neighbours = adjacency[currentVertexIndex];

				if(neighbours == null)
				{
					continue;
				}

				int neighbourCount = neighbours.Count;
				for(int i = 0; i < neighbourCount; i++)
				{
					int neighbourIndex = neighbours[i];
					if(connectedVertexMask[neighbourIndex])
					{
						continue;
					}

					connectedVertexMask[neighbourIndex] = true;
					verticesToVisit.Push(neighbourIndex);
				}
			}

			return connectedVertexMask;
		}

		private List<int>[] BuildVertexAdjacency(Mesh mesh, int vertexCount)
		{
			List<int>[] adjacency = new List<int>[vertexCount];
			int subMeshCount = Mathf.Max(1, mesh.subMeshCount);

			for(int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
			{
				int[] triangles = mesh.GetTriangles(subMeshIndex);
				if(triangles == null || triangles.Length < 3)
				{
					continue;
				}

				int triangleCount = triangles.Length;
				for(int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex += 3)
				{
					AddTriangleEdges(adjacency, triangles[triangleIndex], triangles[triangleIndex + 1], triangles[triangleIndex + 2]);
				}
			}

			AddSharedPositionEdges(adjacency, mesh.vertices);
			return adjacency;
		}

		private void AddSharedPositionEdges(List<int>[] adjacency, Vector3[] vertices)
		{
			Dictionary<Vector3, int> firstIndexByPosition = new Dictionary<Vector3, int>();
			int count = vertices != null ? vertices.Length : 0;

			for(int i = 0; i < count; i++)
			{
				int existingIndex;
				if(firstIndexByPosition.TryGetValue(vertices[i], out existingIndex))
				{
					AddVertexEdge(adjacency, existingIndex, i);
					continue;
				}

				firstIndexByPosition.Add(vertices[i], i);
			}
		}

		private void AddTriangleEdges(List<int>[] adjacency, int index0, int index1, int index2)
		{
			AddVertexEdge(adjacency, index0, index1);
			AddVertexEdge(adjacency, index1, index2);
			AddVertexEdge(adjacency, index2, index0);
		}

		private void AddVertexEdge(List<int>[] adjacency, int index0, int index1)
		{
			if(index0 < 0 || index0 >= adjacency.Length || index1 < 0 || index1 >= adjacency.Length)
			{
				return;
			}

			if(adjacency[index0] == null)
			{
				adjacency[index0] = new List<int>();
			}

			if(adjacency[index1] == null)
			{
				adjacency[index1] = new List<int>();
			}

			adjacency[index0].Add(index1);
			adjacency[index1].Add(index0);
		}

		private sealed class VisibleSelectionContext
		{
			private readonly bool THROUGH_SELECT;
			private readonly List<MeshFilter> MESH_FILTERS;
			private readonly Camera SCENE_CAMERA;

			private VisibleSelectionContext(bool throughSelect, List<MeshFilter> meshFilters, Camera sceneCamera)
			{
				this.THROUGH_SELECT = throughSelect;
				this.MESH_FILTERS = meshFilters;
				this.SCENE_CAMERA = sceneCamera;
			}

			public static VisibleSelectionContext Create(bool throughSelect, List<MeshFilter> meshFilters)
			{
				SceneView sceneView = SceneView.currentDrawingSceneView;
				Camera sceneCamera = sceneView != null ? sceneView.camera : null;

				return new VisibleSelectionContext(throughSelect, meshFilters, sceneCamera);
			}

			public bool IsVertexSelectable(Vector3 worldPosition)
			{
				if(THROUGH_SELECT || SCENE_CAMERA == null)
				{
					return true;
				}

				return IsVertexVisibleFromSceneCamera(worldPosition);
			}

			private bool IsVertexVisibleFromSceneCamera(Vector3 worldPosition)
			{
				Ray cameraRay;
				float vertexDistance;

				if(!TryCreateCameraRay(worldPosition, out cameraRay, out vertexDistance))
				{
					return true;
				}

				float occlusionEpsilon = Mathf.Max(
					HandleUtility.GetHandleSize(worldPosition) * ColliderEditorState.VISIBLE_SELECTION_OCCLUSION_EPSILON,
					ColliderEditorState.MIN_VISIBLE_SELECTION_EPSILON);

				int filterCount = MESH_FILTERS != null ? MESH_FILTERS.Count : 0;
				for(int filterIndex = 0; filterIndex < filterCount; filterIndex++)
				{
					if(IsVertexOccludedByMesh(cameraRay, vertexDistance, occlusionEpsilon, MESH_FILTERS[filterIndex]))
					{
						return false;
					}
				}

				return true;
			}

			private bool TryCreateCameraRay(Vector3 worldPosition, out Ray cameraRay, out float vertexDistance)
			{
				cameraRay = new Ray();
				vertexDistance = 0.0f;

				if(SCENE_CAMERA.orthographic)
				{
					Vector3 screenPosition = SCENE_CAMERA.WorldToScreenPoint(worldPosition);
					cameraRay = SCENE_CAMERA.ScreenPointToRay(screenPosition);
					vertexDistance = Vector3.Dot(worldPosition - cameraRay.origin, cameraRay.direction);
					return vertexDistance > Mathf.Epsilon;
				}

				Vector3 rayDirection = worldPosition - SCENE_CAMERA.transform.position;
				vertexDistance = rayDirection.magnitude;
				if(vertexDistance <= Mathf.Epsilon)
				{
					return false;
				}

				cameraRay = new Ray(SCENE_CAMERA.transform.position, rayDirection / vertexDistance);
				return true;
			}

			private bool IsVertexOccludedByMesh(
				Ray cameraRay,
				float vertexDistance,
				float occlusionEpsilon,
				MeshFilter meshFilter)
			{
				if(meshFilter == null || meshFilter.sharedMesh == null)
				{
					return false;
				}

				RaycastHit hit;

				if(!ColliderEditorHandleUtility.IntersectRayMesh(
					cameraRay,
					meshFilter.sharedMesh,
					meshFilter.transform.localToWorldMatrix,
					out hit))
				{
					return false;
				}

				return hit.distance > occlusionEpsilon && hit.distance < vertexDistance - occlusionEpsilon;
			}
		}

		private void ToggleSelection(
			List<ColliderEditorState.SelectedVertex> selection,
			ColliderEditorState.SelectedVertex vertex,
			bool addToSelection,
			bool removeFromSelection)
		{
			int existingIndex = FindSelectedIndex(selection, vertex);

			if(removeFromSelection)
			{
				if(existingIndex >= 0)
				{
					selection.RemoveAt(existingIndex);
				}

				return;
			}

			if(existingIndex >= 0)
			{
				if(!addToSelection)
				{
					selection.RemoveAt(existingIndex);
				}

				return;
			}

			selection.Add(vertex);
		}

		private int FindSelectedIndex(List<ColliderEditorState.SelectedVertex> selection, ColliderEditorState.SelectedVertex vertex)
		{
			int count = selection.Count;
			for(int i = 0; i < count; i++)
			{
				if(selection[i].transform == vertex.transform &&
					selection[i].localPosition == vertex.localPosition)
				{
					return i;
				}
			}

			return -1;
		}

		private List<ColliderEditorState.SelectedVertex> CreateSelectionCopy()
		{
			List<ColliderEditorState.SelectedVertex> selection = new List<ColliderEditorState.SelectedVertex>(STATE.SelectedVertices.Count);

			int count = STATE.SelectedVertices.Count;
			for(int i = 0; i < count; i++)
			{
				selection.Add(STATE.SelectedVertices[i]);
			}

			return selection;
		}

	}
}
