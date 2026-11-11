#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ColliderEditor
{
	/// <summary>
	/// Partial implementation for ColliderEditorWindow.
	/// </summary>
	public sealed class SceneGUI
	{
		private static readonly Color SELECTION_FILL_COLOR = new Color(0.63f, 0.49f, 0.40f, 0.5f);
		private static readonly Color SELECTION_OUTLINE_COLOR = new Color(0.63f, 0.49f, 0.40f, 1.00f);

		private readonly ColliderEditorState STATE;
		private readonly ColliderPreview COLLIDER_PREVIEW;
		private readonly ToolInteractionState TOOL_INTERACTION_STATE;

		public SceneGUI(ColliderEditorState state, ColliderPreview colliderPreview, ToolInteractionState interactionState)
		{
			this.STATE = state;
			this.COLLIDER_PREVIEW = colliderPreview;
			this.TOOL_INTERACTION_STATE = interactionState;
		}

		public void Draw()
		{
			if(Event.current.type != EventType.Repaint)
			{
				return;
			}

			COLLIDER_PREVIEW.Draw(STATE);

			if(STATE.SelectedObject == null)
			{
				return;
			}

			DrawVertexSelectionModeSceneBanner();

			if(TOOL_INTERACTION_STATE.isDraggingSelection)
			{
				DrawSelectionRectangle();
			}
		}

		private void DrawVertexSelectionModeSceneBanner()
		{
			Handles.BeginGUI();
			Rect bannerRect = new Rect(12.0f, 12.0f, 760.0f, 58.0f);
			EditorGUI.DrawRect(bannerRect, new Color(0.12f, 0.12f, 0.12f, 0.82f));

			GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
			style.normal.textColor = Color.white;
			style.wordWrap = true;

			GUI.Label(
				new Rect(bannerRect.x + 12.0f, bannerRect.y + 6.0f, bannerRect.width - 24.0f, bannerRect.height - 12.0f),
				"You are using The Collider Editor. Drag/select vertices, double-click selects a hull, H hides selected, Shift+H isolates selected, Ctrl/Cmd+Z undoes selection or masks. " +
				"Press 1-5 to pick a collider, Ctrl/Cmd+1-5 to create it, " +
				"Esc - Exit",
				style);

			Handles.EndGUI();
		}

		private void DrawSelectionRectangle()
		{
			Handles.BeginGUI();
			Rect selectionRect = TOOL_INTERACTION_STATE.GetSelectionRect();
			EditorGUI.DrawRect(selectionRect, SELECTION_FILL_COLOR);
			Handles.DrawSolidRectangleWithOutline(selectionRect, Color.clear, SELECTION_OUTLINE_COLOR);
			Handles.EndGUI();
		}
	}
}
#endif
