#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColliderEditor
{
	/// <summary>
	/// Partial implementation for ColliderEditorWindow.
	/// Shows colliders that already exist under the selected object and lets users draw them with
	/// the same performance color language used by the creation preview.
	/// </summary>
	public sealed class GeneratedCollidersPanel
	{
		private readonly ColliderEditorState STATE;

		public event Action onRepaintPreview;

		public GeneratedCollidersPanel(ColliderEditorState state)
		{
			this.STATE = state;
		}

		public void RepaintPreview()
		{
			onRepaintPreview?.Invoke();
		}

		public void Draw()
		{
			Collider[] colliders = GetGeneratedCollidersForSelectedObject();

			STATE.ExistingColliderListExpanded = EditorGUILayout.Foldout(
				STATE.ExistingColliderListExpanded,
				new GUIContent("Generated Colliders (" + colliders.Length + ")", "Colliders found on the selected object and its children."),
				true);

			if(!STATE.ExistingColliderListExpanded)
			{
				return;
			}

			EditorGUI.indentLevel++;

			bool drawPerformanceView = EditorGUILayout.Toggle(
				new GUIContent("Performance View", "Draw existing colliders in the Scene view with performance colors."),
				STATE.DrawExistingColliderPerformanceView);

			if(drawPerformanceView != STATE.DrawExistingColliderPerformanceView)
			{
				STATE.DrawExistingColliderPerformanceView = drawPerformanceView;
				SceneView.RepaintAll();
			}

			using(new EditorGUI.DisabledScope(!STATE.DrawExistingColliderPerformanceView))
			{
				bool drawWireframe = EditorGUILayout.Toggle(
					new GUIContent("Wireframe Only", "Draw collider performance previews as outlines only."),
					STATE.DrawExistingColliderPreviewAsWireframe);

				if(drawWireframe != STATE.DrawExistingColliderPreviewAsWireframe)
				{
					STATE.DrawExistingColliderPreviewAsWireframe = drawWireframe;
					SceneView.RepaintAll();
				}
			}

			if(colliders.Length == 0)
			{
				EditorGUILayout.HelpBox("No colliders were found on the selected object or its children.", MessageType.Info);
				EditorGUI.indentLevel--;
				return;
			}

			using(new EditorGUILayout.HorizontalScope())
			{
				if(GUILayout.Button(new GUIContent("Show All", "Show every listed collider in the performance view.")))
				{
					STATE.DrawExistingColliderPerformanceView = true;
					STATE.ClearExistingColliderVisibilityOverrides();
					SceneView.RepaintAll();
				}

				if(GUILayout.Button(new GUIContent("Refresh", "Refresh the collider list and repaint the Scene view.")))
				{
					SceneView.RepaintAll();
					RepaintPreview();
				}
			}

			STATE.GeneratedCollidersScrollPosition = EditorGUILayout.BeginScrollView(
				STATE.GeneratedCollidersScrollPosition,
				GUILayout.Height(250));

			for(int index = 0; index < colliders.Length; index++)
			{
				DrawGeneratedColliderRow(colliders[index], index);
			}

			EditorGUILayout.EndScrollView();
			EditorGUI.indentLevel--;
		}

		private Collider[] GetGeneratedCollidersForSelectedObject()
		{
			if(STATE.SelectedObject == null)
			{
				return new Collider[0];
			}

			return STATE.SelectedObject.GetComponentsInChildren<Collider>(true);
		}

		private void DrawGeneratedColliderRow(Collider collider, int index)
		{
			if(collider == null)
			{
				return;
			}

			using(new EditorGUILayout.HorizontalScope())
			{
				using(new EditorGUI.DisabledGroupScope(!STATE.DrawExistingColliderPerformanceView))
				{
					bool isVisible = STATE.IsExistingColliderVisible(collider);
					bool newVisibility = EditorGUILayout.Toggle(isVisible, GUILayout.Width(28.0f));

					if(newVisibility != isVisible)
					{
						STATE.SetExistingColliderVisible(collider, newVisibility);
						SceneView.RepaintAll();
					}
				}

				string label = (index + 1).ToString("D2") + ". " + GetGeneratedColliderDisplayName(collider);
				EditorGUILayout.LabelField(new GUIContent(label, GetGeneratedColliderTooltip(collider)));

				using(new EditorGUI.DisabledScope(!collider.enabled))
				{
					EditorGUILayout.LabelField(
						new GUIContent(GetPerformanceLabelForCollider(collider), "Estimated physics cost category for this collider type."),
						GUILayout.Width(70.0f));
				}

				if(GUILayout.Button(new GUIContent("Ping", "Select and ping this collider in the Hierarchy."), GUILayout.Width(46.0f)))
				{
					Selection.activeObject = collider.gameObject;
					EditorGUIUtility.PingObject(collider.gameObject);
				}

				using(new EditorGUI.DisabledScope(!CanDeleteGeneratedCollider(collider)))
				{
					if(GUILayout.Button(new GUIContent("Delete", "Delete this generated collider and its generated mesh asset when it is safe to do so."), GUILayout.Width(56.0f)))
					{
						DeleteGeneratedCollider(collider);
					}
				}
			}

			if(collider != null && !collider.enabled)
			{
				EditorGUILayout.HelpBox(GetGeneratedColliderDisplayName(collider) + " is disabled and will not be drawn in the performance view.", MessageType.None);
			}
		}

		private bool CanDeleteGeneratedCollider(Collider collider)
		{
			if(collider == null || STATE.SelectedObject == null)
			{
				return false;
			}

			return collider.GetComponent<ColliderEditorGeneratedCollider>() != null;
		}

		private void DeleteGeneratedCollider(Collider collider)
		{
			if(collider == null)
			{
				return;
			}

			bool confirmed = EditorUtility.DisplayDialog(
				"Delete Generated Collider",
				"Delete " + collider.gameObject.name + "?\n\nIf this collider uses a mesh asset generated by the Collider Editor, the asset will also be deleted when no other collider uses it.",
				"Delete",
				"Cancel");

			if(!confirmed)
			{
				return;
			}

			GameObject colliderObject = collider.gameObject;
			GameObject ownerObject = STATE.SelectedObject;
			bool cleanupDeferred = ColliderEditorGeneratedMeshCleanupWatcher.QueueMeshAssetCleanupAfterPrefabSave(collider);

			if(!cleanupDeferred)
			{
				AssetPersistence.DeleteGeneratedMeshAssetIfUnused(collider);
			}

			Undo.DestroyObjectImmediate(colliderObject);
			MarkGeneratedColliderOwnerDirty(ownerObject);

			Selection.activeObject = ownerObject;
			SceneView.RepaintAll();
			RepaintPreview();
		}

		private void MarkGeneratedColliderOwnerDirty(GameObject ownerObject)
		{
			if(ownerObject == null)
			{
				return;
			}

			PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

			if(prefabStage != null &&
				prefabStage.prefabContentsRoot != null &&
				prefabStage.IsPartOfPrefabContents(ownerObject))
			{
				EditorUtility.SetDirty(prefabStage.prefabContentsRoot);
				EditorSceneManager.MarkSceneDirty(prefabStage.scene);
				return;
			}

			if(ownerObject.scene.IsValid())
			{
				EditorSceneManager.MarkSceneDirty(ownerObject.scene);
			}
		}

		private string GetGeneratedColliderDisplayName(Collider collider)
		{
			string colliderTypeName = ObjectNames.NicifyVariableName(collider.GetType().Name);
			return colliderTypeName + " - " + collider.gameObject.name;
		}

		private string GetGeneratedColliderTooltip(Collider collider)
		{
			string status = collider.enabled ? "Enabled" : "Disabled";
			return status + " " + collider.GetType().Name + " on " + collider.transform.GetHierarchyPath();
		}

		public static Color GetPerformanceColorForCollider(Collider collider)
		{
			if(collider is SphereCollider)
			{
				return PreviewColors.FASTEST_PREVIEW_COLOR;
			}

			if(collider is CapsuleCollider)
			{
				return PreviewColors.FAST_PREVIEW_COLOR;
			}

			if(collider is BoxCollider)
			{
				return PreviewColors.MEDIUM_PREVIEW_COLOR;
			}

			if(collider is MeshCollider)
			{
				return PreviewColors.SLOWEST_PREVIEW_COLOR;
			}

			return PreviewColors.SLOWEST_PREVIEW_COLOR;
		}

		public static string GetPerformanceLabelForCollider(Collider collider)
		{
			if(collider is SphereCollider)
			{
				return "Fastest";
			}

			if(collider is CapsuleCollider)
			{
				return "Fast";
			}

			if(collider is BoxCollider)
			{
				return "Medium";
			}

			if(collider is MeshCollider)
			{
				return "Slowest";
			}

			return "Unknown";
		}
	}

	internal static class ColliderEditorTransformExtensions
	{
		public static string GetHierarchyPath(this Transform transform)
		{
			if(transform == null)
			{
				return string.Empty;
			}

			string path = transform.name;
			Transform currentParent = transform.parent;

			while(currentParent != null)
			{
				path = currentParent.name + "/" + path;
				currentParent = currentParent.parent;
			}

			return path;
		}
	}
}
#endif
