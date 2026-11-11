#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ColliderEditor.ColliderEditorState;

#if(UNITY_6000_0_OR_NEWER)
using PhysicsMaterialType = UnityEngine.PhysicsMaterial;
#else
using PhysicsMaterialType = UnityEngine.PhysicMaterial;
#endif

namespace ColliderEditor
{
	/// <summary>
	/// Partial implementation for ColliderEditorWindow.
	/// </summary>
	public sealed class SelectionSettingsPanel
	{
		private readonly ColliderEditorState STATE;
		private readonly VertexSelectionController CONTROLLER;

		public SelectionSettingsPanel(ColliderEditorState state, VertexSelectionController controller)
		{
			this.STATE = state;
			this.CONTROLLER = controller;
		}

		public void Draw()
		{
			STATE.VertexSelectionSettingsExpanded = EditorGUILayout.Foldout(
				STATE.VertexSelectionSettingsExpanded,
				new GUIContent("Vertex Selection Settings", "Controls how Scene view vertex picking behaves."), true);

			if(STATE.VertexSelectionSettingsExpanded)
			{
				EditorGUI.indentLevel++;
				STATE.ThroughSelect = EditorGUILayout.Toggle(new GUIContent("Through Select", "Allow click and drag selection to pass through visible surfaces and select back-side vertices."), STATE.ThroughSelect);
				EditorGUILayout.HelpBox(
				STATE.ThroughSelect ?
					"Through Select is enabled: drag and click selection can pick vertices behind the visible surface." :
					"Visible-only selection is enabled: drag and click selection only pick vertices visible from the Scene camera.",
				MessageType.None);

				const float BUTTON_SPACING = 6.0f;

				float buttonWidth = (EditorGUIUtility.currentViewWidth - BUTTON_SPACING) * 0.5f;

				using(new EditorGUILayout.HorizontalScope())
				{
					if(GUILayout.Button(new GUIContent("Clear Vertex Selection", "Clear the current selected vertices. This can be undone with Control/Command+Z."), GUILayout.Width(buttonWidth)))
					{
						CONTROLLER.ClearVertexSelection();
					}

					if(GUILayout.Button(new GUIContent("Select All Visible Vertices", "Select every unhidden vertex on the selected object. Hidden vertices stay masked and cannot be selected."), GUILayout.Width(buttonWidth)))
					{
						CONTROLLER.SelectAllVertices();
					}
				}

				using(new EditorGUILayout.HorizontalScope())
				{
					using(new EditorGUI.DisabledScope(STATE.SelectedVertices.Count == 0))
					{
						if(GUILayout.Button(new GUIContent("Hide Selected Vertices", "Mask the selected vertices. Hidden vertices are not drawn and cannot be selected until the mask is cleared. Shortcut: H."), GUILayout.Width(buttonWidth)))
						{
							CONTROLLER.HideSelectedVertices();
						}
					}

					using(new EditorGUI.DisabledScope(STATE.SelectedVertices.Count == 0))
					{
						if(GUILayout.Button(new GUIContent("Isolate Vertices", "Hide every vertex except the current selection. The mesh asset is not modified. Shortcut: Shift+H."), GUILayout.Width(buttonWidth)))
						{
							CONTROLLER.IsolateSelectedVertices();
						}
					}
				}

				using(new EditorGUI.DisabledScope(STATE.HiddenVertices.Count == 0))
				{
					if(GUILayout.Button(new GUIContent("Unhide All Vertices", "Clear the vertex mask and make every vertex selectable again.")))
					{
						CONTROLLER.UnhideAllVertices();
					}
				}

				EditorGUI.indentLevel--;
			}

			STATE.VertexPreviewSettingsExpanded = EditorGUILayout.Foldout(STATE.VertexPreviewSettingsExpanded, new GUIContent("Vertex Preview Settings", "Controls how visible and selected vertices are drawn in the Scene view."), true);

			if(STATE.VertexPreviewSettingsExpanded)
			{

				EditorGUI.indentLevel++;
				EditorGUILayout.LabelField(new GUIContent("Selected Vertices", "Number of currently selected, unhidden vertices."), new GUIContent(STATE.SelectedVertices.Count.ToString()));
				EditorGUILayout.LabelField(new GUIContent("Hidden Vertices", "Number of currently masked vertices. Hidden vertices cannot be selected."), new GUIContent(STATE.HiddenVertices.Count.ToString()));

				bool displayVertices = EditorGUILayout.Toggle(new GUIContent("Display Vertices", "Draw selectable vertices in the Scene view while vertex selection mode is active."), STATE.DisplayVertices);
				if(displayVertices != STATE.DisplayVertices)
				{
					STATE.DisplayVertices = displayVertices;
					SceneView.RepaintAll();
				}

				float vertexDrawSize = EditorGUILayout.Slider(new GUIContent("Vertex Size", "Scene view size for unselected visible vertices."), STATE.VertexDrawSize, 0.001f, 0.1f);

				if(vertexDrawSize != STATE.VertexDrawSize)
				{
					STATE.VertexDrawSize = vertexDrawSize;
					SceneView.RepaintAll();
				}

				float selectedVertexDrawSize = EditorGUILayout.Slider(new GUIContent("Selected Size", "Scene view size for selected vertices."), STATE.SelectedVertexDrawSize, 0.001f, 0.15f);

				if(selectedVertexDrawSize != STATE.SelectedVertexDrawSize)
				{
					STATE.SelectedVertexDrawSize = selectedVertexDrawSize;
					SceneView.RepaintAll();
				}

				STATE.VertexColor = EditorGUILayout.ColorField(new GUIContent("Vertex Color", "Scene view color for unselected visible vertices."), STATE.VertexColor);
				STATE.SelectedVertexColor = EditorGUILayout.ColorField(new GUIContent("Selected Color", "Scene view color for selected vertices."), STATE.SelectedVertexColor);

				EditorGUI.indentLevel--;
			}

		}
	}
}
#endif
