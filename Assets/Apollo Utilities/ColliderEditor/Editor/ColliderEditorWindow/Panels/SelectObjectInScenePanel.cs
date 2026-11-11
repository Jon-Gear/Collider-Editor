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
	public sealed class SelectObjectInScenePanel
	{
		private readonly ColliderEditorState STATE;
		private readonly SelectObjectInSceneController CONTROLLER;

		public SelectObjectInScenePanel(ColliderEditorState state, SelectObjectInSceneController controller)
		{
			this.STATE = state;
			this.CONTROLLER = controller;
		}

		private enum SceneSelectionWorkflowAction
		{
			NoObjectSelected,
			CreateOrEditCollider,
			FinishCollider,
			FinishColliderAndCreateOrEditNewCollider
		}

		public void Draw()
		{
			GameObject sceneObject = Selection.activeGameObject;
			SceneSelectionWorkflowAction action = GetSceneSelectionWorkflowAction(sceneObject);

			string buttonText = GetSceneSelectionWorkflowButtonText(action, sceneObject);
			Texture2D selectIcon = GetSceneSelectionWorkflowButtonIcon(action, sceneObject);

			DrawSceneSelectionWorkflowHelp(action, sceneObject);

			using(new EditorGUI.DisabledScope(action == SceneSelectionWorkflowAction.NoObjectSelected))
			{
				if(GUILayout.Button(new GUIContent(" " + buttonText, selectIcon, "Use the current Scene selection for collider editing.")))
				{
					ExecuteSceneSelectionWorkflowAction(action, sceneObject);
				}
			}
		}

		private void ExecuteSceneSelectionWorkflowAction(SceneSelectionWorkflowAction action, GameObject sceneObject)
		{
			switch(action)
			{
				case SceneSelectionWorkflowAction.CreateOrEditCollider:
					CONTROLLER.StartColliderEditing(sceneObject);
					return;

				case SceneSelectionWorkflowAction.FinishCollider:
					CONTROLLER.FinishColliderEditing();
					return;

				case SceneSelectionWorkflowAction.FinishColliderAndCreateOrEditNewCollider:
					CONTROLLER.FinishColliderEditing();
					CONTROLLER.StartColliderEditing(sceneObject);
					return;
			}
		}

		private SceneSelectionWorkflowAction GetSceneSelectionWorkflowAction(GameObject sceneObject)
		{
			bool hasEditableSceneObject = CanEditSceneSelection(sceneObject);
			bool hasSelectedObject = STATE.SelectedObject != null;

			// Treat the current tool object as not being a new Scene selection.
			// This prevents the focus helper from making the button say
			// "Finish Collider And Create/Edit New Collider" for the same object.
			bool hasNewSceneObject = hasEditableSceneObject &&
				sceneObject != STATE.SelectedObject;

			if(!hasNewSceneObject && !hasSelectedObject)
			{
				return SceneSelectionWorkflowAction.NoObjectSelected;
			}

			if(hasNewSceneObject && !hasSelectedObject)
			{
				return SceneSelectionWorkflowAction.CreateOrEditCollider;
			}

			if(!hasNewSceneObject && hasSelectedObject)
			{
				return SceneSelectionWorkflowAction.FinishCollider;
			}

			return SceneSelectionWorkflowAction.FinishColliderAndCreateOrEditNewCollider;
		}

		private string GetSceneSelectionWorkflowButtonText(SceneSelectionWorkflowAction action, GameObject sceneObject)
		{
			switch(action)
			{
				case SceneSelectionWorkflowAction.CreateOrEditCollider:
					return GetCreateOrEditColliderButtonText(sceneObject);

				case SceneSelectionWorkflowAction.FinishCollider:
					return "Stop Editing Collider";

				case SceneSelectionWorkflowAction.FinishColliderAndCreateOrEditNewCollider:
					return "Stop Editing Current Collider And " + GetCreateOrEditColliderButtonText(sceneObject);

				default:
					return "No Object Selected";
			}
		}

		private Texture2D GetSceneSelectionWorkflowButtonIcon(SceneSelectionWorkflowAction action, GameObject sceneObject)
		{
			switch(action)
			{
				case SceneSelectionWorkflowAction.CreateOrEditCollider:
					return EditorGUIUtility.IconContent("d_editicon.sml").image as Texture2D;

				case SceneSelectionWorkflowAction.FinishCollider:
					return EditorGUIUtility.IconContent("Valid").image as Texture2D;

				case SceneSelectionWorkflowAction.FinishColliderAndCreateOrEditNewCollider:
					return EditorGUIUtility.IconContent("Valid").image as Texture2D;

				default:
					return null;
			}
		}

		private string GetCreateOrEditColliderButtonText(GameObject sceneObject)
		{
			if(HasEditableCollider(sceneObject))
			{
				return "Edit Object Collider";
			}

			return "Create Object Collider";
		}

		private void DrawSceneSelectionWorkflowHelp(SceneSelectionWorkflowAction action, GameObject sceneObject)
		{
			switch(action)
			{
				case SceneSelectionWorkflowAction.NoObjectSelected:
					DrawNoObjectSelectedHelp(sceneObject);
					return;

				case SceneSelectionWorkflowAction.CreateOrEditCollider:
					EditorGUILayout.HelpBox("Ready to edit: " + sceneObject.name, MessageType.Info);
					return;

				case SceneSelectionWorkflowAction.FinishCollider:
					EditorGUILayout.HelpBox("Currently editing: " + STATE.SelectedObject.name, MessageType.Info);
					return;

				case SceneSelectionWorkflowAction.FinishColliderAndCreateOrEditNewCollider:
					EditorGUILayout.HelpBox(
						"Currently editing: " + STATE.SelectedObject.name +
						"\nNext object: " + sceneObject.name,
						MessageType.Info);
					return;
			}
		}

		private bool CanEditSceneSelection(GameObject sceneObject)
		{
			return sceneObject != null &&
				!EditorUtility.IsPersistent(sceneObject) &&
				sceneObject.GetComponentInChildren<MeshFilter>() != null;
		}

		private bool HasEditableCollider(GameObject sceneObject)
		{
			return sceneObject != null &&
				sceneObject.GetComponentInChildren<Collider>(true) != null;
		}

		private void DrawNoObjectSelectedHelp(GameObject sceneObject)
		{
			if(sceneObject == null)
			{
				EditorGUILayout.HelpBox("No object is selected.", MessageType.None);
				return;
			}

			if(EditorUtility.IsPersistent(sceneObject))
			{
				EditorGUILayout.HelpBox("Prefab assets cannot be edited directly. Select a scene instance instead.", MessageType.Warning);
				return;
			}

			EditorGUILayout.HelpBox("Selected scene object has no MeshFilter. Select a mesh object to create or edit colliders.", MessageType.Warning);
		}
	}
}

#endif
