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
	public sealed class UndoService
	{
		private readonly ColliderEditorState STATE;

		public event Action onRepaintPreview;

		public UndoService(ColliderEditorState state)
		{
			this.STATE = state;
		}
		private void RepaintPreview()
		{
			onRepaintPreview?.Invoke();
		}

		public void ExecuteUndoableCreate(string undoName, System.Func<UnityEngine.Object> createAction)
		{
			ExecuteUndoableCreate(STATE.SelectedObject, undoName, createAction);
		}

		public void ExecuteUndoableCreate(GameObject selectedObject, string undoName, System.Func<UnityEngine.Object> createAction)
		{
			if(selectedObject == null || createAction == null)
			{
				return;
			}

			Undo.IncrementCurrentGroup();
			Undo.SetCurrentGroupName(undoName);

			int undoGroup = Undo.GetCurrentGroup();

			Undo.RegisterFullObjectHierarchyUndo(selectedObject, undoName);
			Undo.RecordObject(STATE, undoName);

			UnityEngine.Object createdObject = createAction.Invoke();

			if(createdObject != null)
			{
				EditorUtility.SetDirty(STATE);

				RecordSelectionUndo("Create Collider From Selection");
				STATE.ClearSelection();

				Selection.activeObject = createdObject;
				EditorGUIUtility.PingObject(createdObject);
			}

			Undo.CollapseUndoOperations(undoGroup);

			RepaintPreview();
		}

		public void RecordSelectionUndo(string undoName)
		{
			if(STATE == null)
			{
				return;
			}

			Undo.RecordObject(STATE, undoName);
			EditorUtility.SetDirty(STATE);
		}
	}
}
