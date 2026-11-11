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
	public sealed class SelectObjectInSceneController
	{
		private readonly ColliderEditorState STATE;
		private readonly ToolInteractionState TOOL_INTERACTION_STATE;
		private readonly UndoService UNDO;

		public event Action onRepaintPreview;
		public event Action<GUIContent> onShowNotification;

		public SelectObjectInSceneController(ColliderEditorState state, ToolInteractionState toolInteractionState, UndoService undo)
		{
			this.STATE = state;
			this.TOOL_INTERACTION_STATE = toolInteractionState;
			this.UNDO = undo;
		}

		public void RepaintPreview()
		{
			onRepaintPreview?.Invoke();
		}

		public void ShowNotification(GUIContent notification)
		{
			onShowNotification?.Invoke(notification);
		}

		public void StartColliderEditing(GameObject obj)
		{
			if(obj == null)
			{
				return;
			}

			// Ignore prefab assets because the tool edits scene object STATE.
			if(EditorUtility.IsPersistent(obj))
			{
				return;
			}

			// Only mesh objects expose vertices that can be selected for collider creation.
			if(obj.GetComponentInChildren<MeshFilter>() == null)
			{
				return;
			}

			UNDO.RecordSelectionUndo("Change Collider Editor Object");

			STATE.IncludeChildren = true;

			STATE.SelectedObject = obj;
			STATE.ClearSelection();
			STATE.ClearHiddenVertices();

			Selection.activeGameObject = obj;

			Tools.hidden = true;

			FocusObjectInSceneView(obj);
			RepaintPreview();
		}

		public void FinishColliderEditing()
		{
			CancelSelectionDrag();

			Tools.hidden = false;
			Tools.current = Tool.Move;

			UNDO.RecordSelectionUndo("Finish Collider Editing");

			STATE.SelectedObject = null;
			STATE.ClearSelection();
			STATE.ClearHiddenVertices();

			RepaintPreview();
		}

		void CancelSelectionDrag()
		{
			if(!TOOL_INTERACTION_STATE.isDraggingSelection)
			{
				return;
			}

			TOOL_INTERACTION_STATE.isDraggingSelection = false;
			TOOL_INTERACTION_STATE.selectionDragClickCount = 0;
			GUIUtility.hotControl = 0;
			RepaintPreview();
		}

		private void FocusObjectInSceneView(GameObject obj)
		{
			if(obj == null)
			{
				return;
			}

			// Delay the frame call so Unity has applied the object selection and the
			// Scene view can use the final selected object for framing.
			EditorApplication.delayCall += () =>
			{
				if(obj == null)
				{
					return;
				}

				SceneView sceneView = SceneView.lastActiveSceneView;
				if(sceneView == null)
				{
					SceneView.RepaintAll();
					return;
				}

				Selection.activeGameObject = obj;
				sceneView.Focus();
				sceneView.FrameSelected();
				SceneView.RepaintAll();
			};
		}

	}
}
