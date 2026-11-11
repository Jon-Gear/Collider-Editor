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
	/// </summary>
	public sealed class PrimitivesPreview
	{
		private readonly PreviewState PREVIEW_STATE;
		private readonly DrawingService DRAW;

		public PrimitivesPreview(PreviewState previewState, DrawingService draw)
		{
			this.PREVIEW_STATE = previewState;
			this.DRAW = draw;
		}
		public void DrawPrimitivePreview(ColliderEditorState state)
		{
			List<Vector3> worldPoints = ColliderEditorMeshUtility.GetSelectedWorldPoints(state);
			if(worldPoints == null || worldPoints.Count == 0)
			{
				return;
			}

			switch(state.SelectedPrimitiveType)
			{
				case ColliderEditorState.PrimitiveType.Box:
					DrawBoxPreview(state, worldPoints);
					break;

				case ColliderEditorState.PrimitiveType.Sphere:
					DrawSpherePreview(state, worldPoints);
					break;

				case ColliderEditorState.PrimitiveType.Capsule:
					DrawCapsulePreview(state, worldPoints);
					break;

				case ColliderEditorState.PrimitiveType.Cylinder:
					DrawCylinderPreview(state, worldPoints);
					break;

				case ColliderEditorState.PrimitiveType.ConvexHull:
					DrawConvexHullPreview(state, worldPoints);
					break;
			}
		}

		private void DrawBoxPreview(ColliderEditorState state, List<Vector3> worldPoints)
		{
			Matrix4x4 matrix = Matrix4x4.identity;
			if(state.RotateBox)
			{
				matrix = Geometry.CalculateRotatedBoxMatrix(state, worldPoints);
			}
			else
			{
				matrix = Geometry.CalculateAxisAlignedBoxMatrix(state, worldPoints);
			}
			Color color = DRAW.GetPrimitivePreviewColor(state);
			bool isWireframe = state.DrawPrimitivePreviewAsWireframe;

			DRAW.DrawBox(matrix, color, isWireframe);
		}

		private void DrawSpherePreview(ColliderEditorState state, List<Vector3> worldPoints)
		{
			Matrix4x4 matrix = Geometry.CalculateSphereMatrix(state, worldPoints);
			Color color = DRAW.GetPrimitivePreviewColor(state);
			bool isWireframe = state.DrawPrimitivePreviewAsWireframe;

			DRAW.DrawSphere(matrix, color, isWireframe);
		}

		private void DrawCapsulePreview(ColliderEditorState state, List<Vector3> worldPoints)
		{

			Matrix4x4 matrix = Matrix4x4.identity;
			float height, radius;
			if(state.RotateCapsule)
			{
				(matrix, (height, radius)) = Geometry.CalculateCapsuleMatrixAndRadiusAndHeightRotated(state, worldPoints, (int)state.CapsuleAxis);
			}
			else
			{
				(matrix, (height, radius)) = Geometry.CalculateCapsuleMatrixAndRadiusAndHeightAxisAligned(state, worldPoints, (int)state.CapsuleAxis);
			}
			Color color = DRAW.GetPrimitivePreviewColor(state);
			bool isWireframe = state.DrawPrimitivePreviewAsWireframe;

			DRAW.DrawCapsule(matrix, color, isWireframe, (height, radius));
		}

		private void DrawCylinderPreview(ColliderEditorState state, List<Vector3> worldPoints)
		{
			Bounds bounds = Geometry.CreatePreviewLocalBounds(state, worldPoints);
			Vector3 size = bounds.size;
			int resolvedAxis = Geometry.ResolvePreviewAxis(size, (int)state.CylinderAxis);

			float radius = Geometry.CalculateAxisAlignedCylinderRadius(state, worldPoints, bounds.center, resolvedAxis);
			float height = Mathf.Max(PreviewState.MINIMUM_PREVIEW_VALUE, Geometry.GetAxisValue(size, resolvedAxis));

			Matrix4x4 matrix = state.SelectedObject.transform.localToWorldMatrix * Matrix4x4.TRS(bounds.center, Geometry.GetAxisRotationFromYAxis(resolvedAxis), Vector3.one);

			Color color = DRAW.GetPrimitivePreviewColor(state);
			bool isWireframe = state.DrawPrimitivePreviewAsWireframe;

			DRAW.DrawCylinder(matrix, color, isWireframe, (height, radius, state.CylinderSides));
		}

		private void DrawConvexHullPreview(ColliderEditorState state, List<Vector3> worldPoints)
		{
			DRAW.DrawMeshFromVertices(state, worldPoints);
		}
	}
}
#endif
