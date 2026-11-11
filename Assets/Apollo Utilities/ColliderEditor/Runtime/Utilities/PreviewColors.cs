using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColliderEditor
{
	public static class PreviewColors
	{
		public static readonly Color BLENDER_WIREFRAME_COLOR = new Color(0.0f, 0.0f, 0.0f, 0.55f);
		public static readonly Color BLENDER_SELECTED_EDGE_COLOR = new Color(1.0f, 0.55f, 0.0f, 0.45f);
		public static readonly Color BLENDER_SELECTED_FACE_COLOR = new Color(1.0f, 0.55f, 0.0f, 0.18f);

		public static readonly Color FASTEST_PREVIEW_COLOR = new Color(0.0f, 1.0f, 0.25f, 0.1f); // green
		public static readonly Color FAST_PREVIEW_COLOR = new Color(0.45f, 1.0f, 0.0f, 0.1f); // yellow-green
		public static readonly Color MEDIUM_PREVIEW_COLOR = new Color(1.0f, 0.78f, 0.0f, 0.1f); // amber
		public static readonly Color SLOWEST_PREVIEW_COLOR = new Color(1.0f, 0.0f, 0.0f, 0.1f); // red
	}
}
