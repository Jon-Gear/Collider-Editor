#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ColliderEditor
{
	public sealed class DrawingService
	{
		private readonly PreviewState PREVIEW_STATE;

		private Mesh cachedCubeMesh;
		private Mesh cachedSphereMesh;

		private Mesh cachedCapsuleMesh;
		private float cachedCapsuleHeight;
		private float cachedCapsuleRadius;

		private Mesh cachedCylinderMesh;
		private float cachedCylinderHeight;
		private float cachedCylinderRadius;
		private float cachedCylinderSides;

		private Mesh cachedMeshFromVertices;
		private MeshFromVerticesCacheKey cachedMeshFromVerticesKey;

		public struct MeshFromVerticesCacheKey: IEquatable<MeshFromVerticesCacheKey>
		{
			public GameObject selectedObject;
			public int pointSignature;

			public bool Equals(MeshFromVerticesCacheKey other)
			{
				return selectedObject == other.selectedObject && pointSignature == other.pointSignature;
			}

			public static MeshFromVerticesCacheKey CreatePrimitiveKey(ColliderEditorState state, List<Vector3> worldPoints)
			{
				MeshFromVerticesCacheKey key = new MeshFromVerticesCacheKey();
				key.selectedObject = state.SelectedObject;
				key.pointSignature = CalculatePointSignature(worldPoints);
				return key;
			}

			/// <summary>
			/// Calculates a stable signature for the current point list.
			/// </summary>
			public static int CalculatePointSignature(List<Vector3> worldPoints)
			{
				unchecked
				{
					int signature = 17;
					signature = (signature * 31) + worldPoints.Count;

					for(int index = 0; index < worldPoints.Count; index++)
					{
						signature = (signature * 31) + Quantize(worldPoints[index].x);
						signature = (signature * 31) + Quantize(worldPoints[index].y);
						signature = (signature * 31) + Quantize(worldPoints[index].z);
					}

					return signature;
				}
			}
			public static int Quantize(float value)
			{
				return Mathf.RoundToInt(value * 10000.0f);
			}

		}

		private Mesh cachedVertexPointOctahedronMesh;

		public DrawingService(PreviewState previewState)
		{
			this.PREVIEW_STATE = previewState;
		}

		#region Draw Methods
		public void DrawBox(Matrix4x4 matrix, Color color, bool drawWireframeOnly)
		{
			if(drawWireframeOnly)
			{
				DrawBoxWireframe(matrix, color);
				return;
			}

			DrawConvexMesh(GetCachedCubeMesh(), matrix, color);
		}

		public void DrawSphere(Matrix4x4 matrix, Color color, bool drawWireframeOnly)
		{
			if(drawWireframeOnly)
			{
				DrawSphereWireframe(matrix, color);
				return;
			}

			DrawConvexMesh(GetCachedSphereMesh(), matrix, color);
		}
		public void DrawCapsule(Matrix4x4 matrix, Color color, bool drawWireframeOnly, (float height, float radius) capsule)
		{
			if(drawWireframeOnly)
			{
				DrawCapsuleWireframe(matrix, color, capsule.radius, capsule.height);
				return;
			}
			DrawConvexMesh(GetCachedCapsuleMesh(capsule.radius, capsule.height), matrix, color);
		}

		/// <summary>
		/// Draws a cylinder aligned to the selected object's local axes.
		/// </summary>
		public void DrawCylinder(Matrix4x4 matrix, Color color, bool drawWireframeOnly, (float height, float radius, int sides) cylinder)
		{
			DrawConvexMesh(GetCachedCylinderMesh(cylinder.radius, cylinder.height, cylinder.sides), matrix, color, drawWireframeOnly);
		}

		/// <summary>
		/// Draws a convex hull preview using the generated hull mesh.
		/// </summary>
		public void DrawMeshFromVertices(ColliderEditorState state, List<Vector3> worldPoints)
		{
			if(worldPoints.Count < 4)
			{
				return;
			}

			DrawConvexMesh(GetCachedMeshFromVertices(state, worldPoints), state.SelectedObject.transform.localToWorldMatrix, GetPrimitivePreviewColor(state), state.DrawPrimitivePreviewAsWireframe);
		}

		/// <summary>
		/// Draws a mesh with the shared preview material.
		/// </summary>
		public void DrawConvexMesh(Mesh mesh, Matrix4x4 matrix, Color color, bool drawWireframeOnly = false)
		{
			if(mesh == null || mesh.vertexCount == 0 || !PREVIEW_STATE.EnsurePreviewMaterial())
			{
				return;
			}

			PREVIEW_STATE.meshPreviewMaterial.SetColor("_Color", color);
			PREVIEW_STATE.meshPreviewMaterial.SetPass(PreviewState.MESH_PREVIEW_PASS_INDEX);

			GL.wireframe = true;
			Graphics.DrawMeshNow(mesh, matrix);
			GL.wireframe = false;

			if(!drawWireframeOnly)
			{
				Graphics.DrawMeshNow(mesh, matrix);
			}
		}

		#endregion

		#region Wireframe
		private void DrawBoxWireframe(Matrix4x4 matrix, Color color)
		{
			Matrix4x4 previousMatrix = Handles.matrix;
			Color previousColor = Handles.color;

			Handles.matrix = matrix;
			Handles.color = WithSolidAlpha(color);
			Handles.DrawWireCube(Vector3.zero, Vector3.one);

			Handles.color = previousColor;
			Handles.matrix = previousMatrix;
		}

		private void DrawSphereWireframe(Matrix4x4 matrix, Color color)
		{
			Matrix4x4 previousMatrix = Handles.matrix;
			Color previousColor = Handles.color;

			Handles.matrix = matrix;
			Handles.color = WithSolidAlpha(color);
			Handles.DrawWireDisc(Vector3.zero, Vector3.right, 0.5f);
			Handles.DrawWireDisc(Vector3.zero, Vector3.up, 0.5f);
			Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 0.5f);

			Handles.color = previousColor;
			Handles.matrix = previousMatrix;
		}

		private void DrawCapsuleWireframe(Matrix4x4 matrix, Color color, float radius, float height)
		{
			Matrix4x4 previousMatrix = Handles.matrix;
			Color previousColor = Handles.color;

			float safeRadius = Mathf.Max(radius, PreviewState.MINIMUM_PREVIEW_VALUE);
			float safeHeight = Mathf.Max(height, safeRadius * 2.0f);
			float cylinderHalfHeight = Mathf.Max(0.0f, (safeHeight * 0.5f) - safeRadius);
			Vector3 top = Vector3.up * cylinderHalfHeight;
			Vector3 bottom = Vector3.down * cylinderHalfHeight;

			Handles.matrix = matrix;
			Handles.color = WithSolidAlpha(color);

			Handles.DrawWireDisc(top, Vector3.up, safeRadius);
			Handles.DrawWireDisc(bottom, Vector3.up, safeRadius);

			Handles.DrawLine(new Vector3(safeRadius, -cylinderHalfHeight, 0.0f), new Vector3(safeRadius, cylinderHalfHeight, 0.0f));
			Handles.DrawLine(new Vector3(-safeRadius, -cylinderHalfHeight, 0.0f), new Vector3(-safeRadius, cylinderHalfHeight, 0.0f));
			Handles.DrawLine(new Vector3(0.0f, -cylinderHalfHeight, safeRadius), new Vector3(0.0f, cylinderHalfHeight, safeRadius));
			Handles.DrawLine(new Vector3(0.0f, -cylinderHalfHeight, -safeRadius), new Vector3(0.0f, cylinderHalfHeight, -safeRadius));

			Handles.DrawWireArc(top, Vector3.forward, Vector3.right, 180.0f, safeRadius);
			Handles.DrawWireArc(bottom, Vector3.forward, Vector3.left, 180.0f, safeRadius);
			Handles.DrawWireArc(top, Vector3.right, Vector3.back, 180.0f, safeRadius);
			Handles.DrawWireArc(bottom, Vector3.right, Vector3.forward, 180.0f, safeRadius);

			Handles.color = previousColor;
			Handles.matrix = previousMatrix;
		}
		private Color WithSolidAlpha(Color color)
		{
			color.a = 1.0f;
			return color;
		}

		#endregion

		#region Cached Meshes

		/// <summary>
		/// Returns the unit cube mesh used for box previews.
		/// </summary>
		private Mesh GetCachedCubeMesh()
		{
			if(cachedCubeMesh != null)
			{
				return cachedCubeMesh;
			}

			cachedCubeMesh = Geometry.CreateCubeMesh();
			cachedCubeMesh.name = "ColliderEditor_CubePreview";
			return cachedCubeMesh;
		}

		/// <summary>
		/// Returns the unit sphere mesh used for sphere previews.
		/// </summary>
		private Mesh GetCachedSphereMesh()
		{
			if(cachedSphereMesh != null)
			{
				return cachedSphereMesh;
			}

			cachedSphereMesh = Geometry.CreateSphereMesh(0.5f, PreviewState.SPHERE_LONGITUDE_SEGMENTS, PreviewState.SPHERE_LATITUDE_SEGMENTS);
			cachedSphereMesh.name = "ColliderEditor_SpherePreview";
			return cachedSphereMesh;
		}

		/// <summary>
		/// Returns a capsule mesh aligned to the Y axis.
		/// </summary>
		private Mesh GetCachedCapsuleMesh(float radius, float height)
		{
			if(cachedCapsuleMesh != null && cachedCapsuleHeight == height && cachedCapsuleRadius == radius && cachedCapsuleMesh != null)
			{
				return cachedCapsuleMesh;
			}

			PREVIEW_STATE.DestroyMesh(ref cachedCapsuleMesh);
			cachedCapsuleHeight = height;
			cachedCapsuleRadius = radius;
			cachedCapsuleMesh = Geometry.CreateCapsuleMesh(radius, height);
			cachedCapsuleMesh.name = "ColliderEditor_CapsulePreview";
			return cachedCapsuleMesh;
		}

		private Mesh GetCachedCylinderMesh(float radius, float height, int sides)
		{
			if(cachedCylinderMesh != null && cachedCylinderHeight == height && cachedCylinderRadius == radius && cachedCylinderSides == sides)
			{
				return cachedCylinderMesh;
			}
			PREVIEW_STATE.DestroyMesh(ref cachedCylinderMesh);
			cachedCylinderHeight = height;
			cachedCylinderRadius = radius;
			cachedCylinderSides = sides;
			cachedCylinderMesh = Geometry.CreateCylinderMesh(Vector3.zero, radius, height, 1, sides);
			cachedCylinderMesh.name = "ColliderEditor_CylinderPreview";
			return cachedCylinderMesh;
		}

		/// <summary>
		/// Gets the cached primitive mesh when the current key matches.
		/// </summary>
		private Mesh GetCachedMeshFromVertices(ColliderEditorState state, List<Vector3> worldPoints)
		{
			MeshFromVerticesCacheKey key = MeshFromVerticesCacheKey.CreatePrimitiveKey(state, worldPoints);

			if(cachedMeshFromVertices != null && cachedMeshFromVerticesKey.Equals(key))
			{
				return cachedMeshFromVertices;
			}

			PREVIEW_STATE.DestroyMesh(ref cachedMeshFromVertices);
			cachedMeshFromVertices = ColliderQuickHull.CalculateHullWorld(worldPoints, state.SelectedObject.transform).result;
			cachedMeshFromVerticesKey = key;

			return cachedMeshFromVertices;
		}
		#endregion

		public Mesh GetPreviewVertexDotMesh()
		{
			if(cachedVertexPointOctahedronMesh != null)
			{
				return cachedVertexPointOctahedronMesh;
			}

			PREVIEW_STATE.DestroyMesh(ref cachedVertexPointOctahedronMesh);

			cachedVertexPointOctahedronMesh = Geometry.CreateOctahedronMesh();

			cachedVertexPointOctahedronMesh.name = "ColliderEditor_VertexPointPreview";

			return cachedVertexPointOctahedronMesh;
		}

		public Color GetPrimitivePreviewColor(ColliderEditorState state)
		{
			if(!state.ColorPrimitivePreviewByPerformance)
			{
				return state.PrimitivePreviewColor;
			}

			switch(state.SelectedPrimitiveType)
			{
				case ColliderEditorState.PrimitiveType.Sphere:
					return PreviewColors.FASTEST_PREVIEW_COLOR;

				case ColliderEditorState.PrimitiveType.Capsule:
					return PreviewColors.FAST_PREVIEW_COLOR;

				case ColliderEditorState.PrimitiveType.Box:
					return PreviewColors.MEDIUM_PREVIEW_COLOR;

				case ColliderEditorState.PrimitiveType.ConvexHull:
				case ColliderEditorState.PrimitiveType.Cylinder:
					return PreviewColors.SLOWEST_PREVIEW_COLOR;

				default:
					return state.PrimitivePreviewColor;
			}
		}
	}
}
#endif
