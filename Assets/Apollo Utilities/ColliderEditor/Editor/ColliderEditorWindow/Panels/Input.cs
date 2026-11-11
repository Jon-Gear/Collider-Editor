#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using static ColliderEditor.ColliderEditorState;

namespace ColliderEditor
{
	public class ToolInteractionState
	{
		public bool isDraggingSelection;
		public Vector2 dragStartScreen;
		public Vector2 dragCurrentScreen;
		public int selectionDragClickCount;

		public Rect GetSelectionRect()
		{
			Vector2 min = Vector2.Min(dragStartScreen, dragCurrentScreen);
			Vector2 max = Vector2.Max(dragStartScreen, dragCurrentScreen);
			return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
		}
	}

	public sealed class Input
	{
		private readonly ColliderEditorState STATE;
		private readonly SelectObjectInSceneController SELECT_OBJECT_IN_SCENE_CONTROLLER;
		private readonly CreateColliderController CREATE_COLLIDER_CONTROLLER;
		private readonly VertexSelectionController VERTEX_SELECTION_CONTROLLER;
		private readonly ToolInteractionState TOOL_INTERACTION_STATE;

		public event Action onRepaintPreview;

		public Input(ColliderEditorState state, SelectObjectInSceneController selectObjectInSceneController, CreateColliderController createColliderController, VertexSelectionController vertexSelectionController, ToolInteractionState interactionState)
		{
			this.STATE = state;
			this.SELECT_OBJECT_IN_SCENE_CONTROLLER = selectObjectInSceneController;
			this.CREATE_COLLIDER_CONTROLLER = createColliderController;
			this.VERTEX_SELECTION_CONTROLLER = vertexSelectionController;
			this.TOOL_INTERACTION_STATE = interactionState;
		}

		public void RepaintPreview()
		{
			onRepaintPreview?.Invoke();
		}

		public void HandleInput(Event currentEvent)
		{
			if(STATE.SelectedObject == null)
			{
				return;
			}

			if(currentEvent == null)
			{
				return;
			}

			int controlId = GUIUtility.GetControlID(FocusType.Passive);
			Event e = currentEvent;

			ClaimSceneViewInput(e, controlId);

			if(HandleKeyboardInput(e))
			{
				return;
			}

			if(IsBlockedMouseInput(e))
			{
				return;
			}

			HandleMouseInput(e, controlId);
		}

		private void ClaimSceneViewInput(Event e, int controlId)
		{
			if(e.type != EventType.Layout)
			{
				return;
			}

			HandleUtility.AddDefaultControl(controlId);
		}

		private bool IsBlockedMouseInput(Event e)
		{
			return e.alt || e.button != 0;
		}

		private bool HandleKeyboardInput(Event e)
		{
			if(e.type != EventType.KeyDown)
			{
				return false;
			}

			if(e.keyCode == KeyCode.Escape)
			{
				SELECT_OBJECT_IN_SCENE_CONTROLLER.FinishColliderEditing();
				e.Use();
				return true;
			}

			return HandleShortcuts(e);
		}

		private bool HandleShortcuts(Event e)
		{
			if(e.type != EventType.KeyDown)
			{
				return false;
			}

			if(e.control || e.command)
			{
				if(e.keyCode == KeyCode.Z)
				{
					Undo.PerformUndo();
					e.Use();
					return true;
				}

				if(e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
				{
					if(STATE.SelectedPrimitiveType != ColliderEditorState.PrimitiveType.None &&
						STATE.SelectedVertices.Count > 0)
					{
						CREATE_COLLIDER_CONTROLLER.ConfirmAndCreateSelectedPrimitive();
					}

					e.Use();
					return true;
				}
			}

			if(e.keyCode == KeyCode.H)
			{
				if(STATE.SelectedVertices.Count > 0)
				{
					if(e.shift)
					{
						VERTEX_SELECTION_CONTROLLER.IsolateSelectedVertices();
					}
					else
					{
						VERTEX_SELECTION_CONTROLLER.HideSelectedVertices();
					}
				}

				e.Use();
				return true;
			}

			if(e.keyCode == KeyCode.A && (e.control || e.command))
			{
				VERTEX_SELECTION_CONTROLLER.SelectAllVertices();
				e.Use();
				return true;
			}

			ColliderEditorState.PrimitiveType shortcutPrimitive;
			if(!TryGetPrimitiveShortcut(e.keyCode, out shortcutPrimitive))
			{
				return false;
			}

			STATE.SelectedPrimitiveType = shortcutPrimitive;

			if((e.control || e.command) && STATE.SelectedVertices.Count > 0)
			{
				CREATE_COLLIDER_CONTROLLER.CreateSelectedPrimitive();
			}

			RepaintPreview();
			e.Use();
			return true;
		}

		private bool TryGetPrimitiveShortcut(KeyCode keyCode, out ColliderEditorState.PrimitiveType primitiveType)
		{
			int shortcutIndex = -1;

			if(keyCode >= KeyCode.Alpha1 && keyCode <= KeyCode.Alpha9)
			{
				shortcutIndex = keyCode - KeyCode.Alpha1;
			}
			else if(keyCode >= KeyCode.Keypad1 && keyCode <= KeyCode.Keypad9)
			{
				shortcutIndex = keyCode - KeyCode.Keypad1;
			}

			if(shortcutIndex < 0 || shortcutIndex >= PRIMITIVE_ORDER.Length)
			{
				primitiveType = ColliderEditorState.PrimitiveType.None;
				return false;
			}

			primitiveType = PRIMITIVE_ORDER[shortcutIndex];
			return true;
		}

		private void HandleMouseInput(Event e, int controlId)
		{
			if(IsIgnoredMouseUpDuringSelectionDrag(e))
			{
				FinishSelectionDrag(TOOL_INTERACTION_STATE.dragCurrentScreen, e);
				return;
			}

			switch(e.type)
			{
				case EventType.MouseDown:
					StartSelectionDrag(e, controlId);
					break;

				case EventType.MouseDrag:
					UpdateSelectionDrag(e, controlId);
					break;

				case EventType.MouseUp:
					FinishSelectionDrag(e.mousePosition, e);
					break;
			}
		}

		private bool IsIgnoredMouseUpDuringSelectionDrag(Event e)
		{
			return TOOL_INTERACTION_STATE.isDraggingSelection &&
				e.type == EventType.Ignore &&
				e.rawType == EventType.MouseUp;
		}

		private void StartSelectionDrag(Event e, int controlId)
		{
			TOOL_INTERACTION_STATE.isDraggingSelection = true;
			TOOL_INTERACTION_STATE.dragStartScreen = e.mousePosition;
			TOOL_INTERACTION_STATE.dragCurrentScreen = e.mousePosition;
			TOOL_INTERACTION_STATE.selectionDragClickCount = e.clickCount;
			GUIUtility.hotControl = controlId;
			e.Use();
		}

		private void UpdateSelectionDrag(Event e, int controlId)
		{
			if(!TOOL_INTERACTION_STATE.isDraggingSelection)
			{
				return;
			}

			if(GUIUtility.hotControl != controlId)
			{
				CancelSelectionDrag();
				return;
			}

			TOOL_INTERACTION_STATE.dragCurrentScreen = e.mousePosition;
			e.Use();
			RepaintPreview();
		}

		private void FinishSelectionDrag(Vector2 mousePosition, Event e)
		{
			if(!TOOL_INTERACTION_STATE.isDraggingSelection)
			{
				return;
			}

			TOOL_INTERACTION_STATE.dragCurrentScreen = mousePosition;

			bool removeFromSelection = e.control || e.command;
			bool addToSelection = e.shift;

			ApplySelectionDrag(mousePosition, addToSelection, removeFromSelection, TOOL_INTERACTION_STATE.selectionDragClickCount);
			ClearSelectionDragState();

			e.Use();
			RepaintPreview();
			SceneView.RepaintAll();
		}

		private void ApplySelectionDrag(Vector2 mousePosition, bool addToSelection, bool removeFromSelection, int clickCount)
		{
			Rect selectionRect = TOOL_INTERACTION_STATE.GetSelectionRect();

			if(selectionRect.width < MIN_SELECTION_RECT_SIZE &&
				selectionRect.height < MIN_SELECTION_RECT_SIZE)
			{
				if(clickCount > 1)
				{
					VERTEX_SELECTION_CONTROLLER.SelectClosestVertexHull(mousePosition, addToSelection, removeFromSelection);
					return;
				}

				VERTEX_SELECTION_CONTROLLER.ToggleClosestVertex(mousePosition, addToSelection, removeFromSelection);
				return;
			}

			VERTEX_SELECTION_CONTROLLER.SelectVerticesInRect(selectionRect, addToSelection, removeFromSelection);
		}

		private void ClearSelectionDragState()
		{
			TOOL_INTERACTION_STATE.isDraggingSelection = false;
			TOOL_INTERACTION_STATE.selectionDragClickCount = 0;
			GUIUtility.hotControl = 0;
		}

		private void CancelSelectionDrag()
		{
			if(!TOOL_INTERACTION_STATE.isDraggingSelection)
			{
				return;
			}

			TOOL_INTERACTION_STATE.isDraggingSelection = false;
			TOOL_INTERACTION_STATE.selectionDragClickCount = 0;
			GUIUtility.hotControl = 0;
			RepaintPreview();
			SceneView.RepaintAll();
		}
	}
}
#endif
