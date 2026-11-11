#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColliderEditor
{
	public sealed class ColliderPreview
	{

		PreviewState previewState;

		DrawingService draw;

		PrimitivesPreview primitivesPreview;
		MeshVerticesPreview meshVerticesPreview;
		ExistingColliderPerformancePreview existingCollidersPerformancePreview;
		HiddenVerticesPreview hiddenVerticesPreview;

		public ColliderPreview()
		{
			previewState = new PreviewState();

			draw = new DrawingService(previewState);

			hiddenVerticesPreview = new HiddenVerticesPreview(previewState);
			meshVerticesPreview = new MeshVerticesPreview(previewState, draw);
			existingCollidersPerformancePreview = new ExistingColliderPerformancePreview(draw);
			primitivesPreview = new PrimitivesPreview(previewState, draw);
		}

		/// <summary>
		/// Draws the active preview content.
		/// </summary>
		public void Draw(ColliderEditorState state)
		{
			hiddenVerticesPreview.DrawHiddenSourceMeshes(state);

			if(state == null || state.SelectedObject == null)
			{
				return;
			}

			if(!previewState.EnsurePreviewMaterial())
			{
				return;
			}

			if(state.DisplayVertices)
			{
				meshVerticesPreview.DrawVertices(state);
			}

			if(state.DrawExistingColliderPerformanceView)
			{
				existingCollidersPerformancePreview.DrawExistingColliderPerformanceView(state);
			}

			if(state.DrawPrimitivePreview)
			{
				primitivesPreview.DrawPrimitivePreview(state);
			}
		}

		/// <summary>
		/// Releases all preview resources owned by this renderer.
		/// </summary>

		public void Dispose()
		{
			hiddenVerticesPreview.RestoreHiddenSourceRenderers();
			hiddenVerticesPreview.ClearHiddenSourceMeshCaches();

			previewState.Dispose();
		}
	}
}
#endif
