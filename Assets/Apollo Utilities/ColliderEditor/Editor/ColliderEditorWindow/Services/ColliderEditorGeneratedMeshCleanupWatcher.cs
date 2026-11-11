#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColliderEditor
{
	/// <summary>
	/// Removes generated mesh assets when their generated hierarchy objects are deleted.
	/// </summary>
	[InitializeOnLoad]
	internal static class ColliderEditorGeneratedMeshCleanupWatcher
	{
		private static readonly Dictionary<int, List<string>> MESH_ASSET_PATHS_BY_HIERARCHY_INSTANCE_ID = new Dictionary<int, List<string>>();
		private static readonly Dictionary<string, HashSet<string>> PENDING_MESH_ASSET_PATHS_BY_PREFAB_ASSET_PATH = new Dictionary<string, HashSet<string>>();
		private static readonly HashSet<string> PENDING_MESH_ASSET_PATHS = new HashSet<string>();

		private static bool cleanupScheduled;
		private static bool cacheRebuildScheduled;
		private static bool prefabStageIsClosing;

		static ColliderEditorGeneratedMeshCleanupWatcher()
		{
			ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
			PrefabStage.prefabStageOpened += OnPrefabStageOpened;
			PrefabStage.prefabSaved += OnPrefabSaved;
			PrefabStage.prefabStageClosing += OnPrefabStageClosing;

			ScheduleCacheRebuild();
		}

		internal static bool QueueMeshAssetCleanupAfterPrefabSave(Collider collider)
		{
			PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

			if(collider == null || prefabStage == null || prefabStage.prefabContentsRoot == null || !prefabStage.IsPartOfPrefabContents(collider.gameObject))
			{
				return false;
			}

			Mesh generatedMesh = GetGeneratedMesh(collider);
			string assetPath = generatedMesh == null ? string.Empty : AssetDatabase.GetAssetPath(generatedMesh);

			if(!string.IsNullOrEmpty(assetPath))
			{
				QueuePrefabMeshAssetPath(prefabStage.assetPath, assetPath);
			}

			return true;
		}

		private static void OnPrefabStageOpened(PrefabStage prefabStage)
		{
			prefabStageIsClosing = false;
			ScheduleCacheRebuild();
		}

		private static void OnPrefabSaved(GameObject prefabContentsRoot)
		{
			PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(prefabContentsRoot);

			if(prefabStage == null)
			{
				return;
			}

			MovePrefabMeshAssetPathsToPendingCleanup(prefabStage.assetPath);

			if(PENDING_MESH_ASSET_PATHS.Count > 0)
			{
				ScheduleCleanup();
			}

			ScheduleCacheRebuild();
		}

		private static void OnPrefabStageClosing(PrefabStage prefabStage)
		{
			prefabStageIsClosing = true;

			MESH_ASSET_PATHS_BY_HIERARCHY_INSTANCE_ID.Clear();

			if(prefabStage != null)
			{
				PENDING_MESH_ASSET_PATHS_BY_PREFAB_ASSET_PATH.Remove(prefabStage.assetPath);
			}

			EditorApplication.delayCall += FinishPrefabStageClose;
		}

		private static void FinishPrefabStageClose()
		{
			prefabStageIsClosing = false;
			ScheduleCacheRebuild();
		}

		private static void OnObjectChangesPublished(ref ObjectChangeEventStream changeStream)
		{
			if(prefabStageIsClosing)
			{
				return;
			}

			if(Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
			{
				ScheduleCacheRebuild();
				return;
			}

			int eventCount = changeStream.length;

			for(int eventIndex = 0; eventIndex < eventCount; eventIndex++)
			{
				ProcessObjectChange(ref changeStream, eventIndex);
			}

			if(PENDING_MESH_ASSET_PATHS.Count > 0)
			{
				ScheduleCleanup();
			}

			ScheduleCacheRebuild();
		}

		private static void ProcessObjectChange(ref ObjectChangeEventStream changeStream, int eventIndex)
		{
			ObjectChangeKind changeKind = changeStream.GetEventType(eventIndex);

			if(changeKind != ObjectChangeKind.DestroyGameObjectHierarchy)
			{
				return;
			}

			changeStream.GetDestroyGameObjectHierarchyEvent(eventIndex, out DestroyGameObjectHierarchyEventArgs destroyEvent);

			if(!destroyEvent.scene.IsValid() || !destroyEvent.scene.isLoaded)
			{
				return;
			}

			string prefabAssetPath = GetPrefabAssetPathForScene(destroyEvent.scene);

			QueueMeshAssetsForDestroyedHierarchy(destroyEvent.instanceId, prefabAssetPath);

			if(!string.IsNullOrEmpty(prefabAssetPath) && !destroyEvent.scene.isDirty)
			{
				MovePrefabMeshAssetPathsToPendingCleanup(prefabAssetPath);
			}
		}

		private static string GetPrefabAssetPathForScene(Scene scene)
		{
			PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

			if(prefabStage == null || prefabStage.scene != scene)
			{
				return string.Empty;
			}

			return prefabStage.assetPath;
		}

		private static void QueueMeshAssetsForDestroyedHierarchy(int destroyedInstanceId, string prefabAssetPath)
		{
			if(!MESH_ASSET_PATHS_BY_HIERARCHY_INSTANCE_ID.TryGetValue(destroyedInstanceId, out List<string> assetPaths))
			{
				return;
			}

			int pathCount = assetPaths.Count;

			for(int index = 0; index < pathCount; index++)
			{
				QueueMeshAssetPath(assetPaths[index], prefabAssetPath);
			}
		}

		private static void QueueMeshAssetPath(string assetPath, string prefabAssetPath)
		{
			if(string.IsNullOrEmpty(prefabAssetPath))
			{
				PENDING_MESH_ASSET_PATHS.Add(assetPath);
				return;
			}

			QueuePrefabMeshAssetPath(prefabAssetPath, assetPath);
		}

		private static void QueuePrefabMeshAssetPath(string prefabAssetPath, string assetPath)
		{
			if(string.IsNullOrEmpty(prefabAssetPath) || string.IsNullOrEmpty(assetPath))
			{
				return;
			}

			if(!PENDING_MESH_ASSET_PATHS_BY_PREFAB_ASSET_PATH.TryGetValue(prefabAssetPath, out HashSet<string> assetPaths))
			{
				assetPaths = new HashSet<string>();
				PENDING_MESH_ASSET_PATHS_BY_PREFAB_ASSET_PATH.Add(prefabAssetPath, assetPaths);
			}

			assetPaths.Add(assetPath);
		}

		private static void MovePrefabMeshAssetPathsToPendingCleanup(string prefabAssetPath)
		{
			if(string.IsNullOrEmpty(prefabAssetPath) || !PENDING_MESH_ASSET_PATHS_BY_PREFAB_ASSET_PATH.TryGetValue(prefabAssetPath, out HashSet<string> assetPaths))
			{
				return;
			}

			PENDING_MESH_ASSET_PATHS.UnionWith(assetPaths);
			PENDING_MESH_ASSET_PATHS_BY_PREFAB_ASSET_PATH.Remove(prefabAssetPath);
		}

		private static Mesh GetGeneratedMesh(Collider collider)
		{
			ColliderEditorGeneratedCollider generatedCollider = collider.GetComponent<ColliderEditorGeneratedCollider>();

			if(generatedCollider != null && generatedCollider.Mesh != null)
			{
				return generatedCollider.Mesh;
			}

			MeshCollider meshCollider = collider as MeshCollider;
			return meshCollider == null ? null : meshCollider.sharedMesh;
		}

		private static void ScheduleCleanup()
		{
			if(cleanupScheduled)
			{
				return;
			}

			cleanupScheduled = true;
			EditorApplication.delayCall += DeletePendingMeshAssets;
		}

		private static void DeletePendingMeshAssets()
		{
			cleanupScheduled = false;

			List<string> assetPaths = new List<string>(PENDING_MESH_ASSET_PATHS);
			PENDING_MESH_ASSET_PATHS.Clear();

			int pathCount = assetPaths.Count;

			for(int index = 0; index < pathCount; index++)
			{
				AssetPersistence.DeleteGeneratedMeshAssetIfUnused(assetPaths[index]);
			}
		}

		private static void ScheduleCacheRebuild()
		{
			if(cacheRebuildScheduled)
			{
				return;
			}

			cacheRebuildScheduled = true;
			EditorApplication.delayCall += RebuildCache;
		}

		private static void RebuildCache()
		{
			cacheRebuildScheduled = false;
			MESH_ASSET_PATHS_BY_HIERARCHY_INSTANCE_ID.Clear();

			CacheMainStageGeneratedColliders();
			CacheCurrentPrefabStageGeneratedColliders();
		}

		private static void CacheMainStageGeneratedColliders()
		{
			ColliderEditorGeneratedCollider[] generatedColliders = Object.FindObjectsByType<ColliderEditorGeneratedCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			CacheGeneratedColliders(generatedColliders);
		}

		private static void CacheCurrentPrefabStageGeneratedColliders()
		{
			PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

			if(prefabStage == null || prefabStage.prefabContentsRoot == null)
			{
				return;
			}

			ColliderEditorGeneratedCollider[] generatedColliders = prefabStage.prefabContentsRoot.GetComponentsInChildren<ColliderEditorGeneratedCollider>(true);
			CacheGeneratedColliders(generatedColliders);
		}

		private static void CacheGeneratedColliders(ColliderEditorGeneratedCollider[] generatedColliders)
		{
			int markerCount = generatedColliders.Length;

			for(int index = 0; index < markerCount; index++)
			{
				CacheGeneratedCollider(generatedColliders[index]);
			}
		}

		private static void CacheGeneratedCollider(ColliderEditorGeneratedCollider generatedCollider)
		{
			if(generatedCollider == null || generatedCollider.Mesh == null)
			{
				return;
			}

			string assetPath = AssetDatabase.GetAssetPath(generatedCollider.Mesh);

			if(string.IsNullOrEmpty(assetPath))
			{
				return;
			}

			Transform currentTransform = generatedCollider.transform;

			while(currentTransform != null)
			{
				AddAssetPathToHierarchyObject(currentTransform.gameObject.GetInstanceID(), assetPath);
				currentTransform = currentTransform.parent;
			}
		}

		private static void AddAssetPathToHierarchyObject(int hierarchyInstanceId, string assetPath)
		{
			if(!MESH_ASSET_PATHS_BY_HIERARCHY_INSTANCE_ID.TryGetValue(hierarchyInstanceId, out List<string> assetPaths))
			{
				assetPaths = new List<string>();
				MESH_ASSET_PATHS_BY_HIERARCHY_INSTANCE_ID.Add(hierarchyInstanceId, assetPaths);
			}

			if(!assetPaths.Contains(assetPath))
			{
				assetPaths.Add(assetPath);
			}
		}
	}
}
#endif
