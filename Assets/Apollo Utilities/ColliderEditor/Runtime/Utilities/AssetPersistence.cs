using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
namespace ColliderEditor
{
	public static class AssetPersistence
	{
		private const string GENERATED_MESH_FOLDER_NAME = "_GeneratedColliderMeshes";
		private const string GENERATED_MESH_ROOT_FOLDER = "Assets/ColliderEditor/GeneratedMeshes";

		public static Mesh MakeGeneratedMeshPersistent(GameObject assetOwner, Mesh mesh, string meshName, string generationAttemptName)
		{
#if UNITY_EDITOR
			if(assetOwner == null || mesh == null || EditorUtility.IsPersistent(mesh))
			{
				return mesh;
			}

			bool useAttemptFolder = !string.IsNullOrEmpty(generationAttemptName);
			string folderPath = GetGeneratedMeshFolderPath(assetOwner, useAttemptFolder);

			if(string.IsNullOrEmpty(folderPath))
			{
				return mesh;
			}

			if(useAttemptFolder)
			{
				folderPath += "/" + CreateSafeAssetName(
					generationAttemptName,
					"GeneratedAttempt");
			}

			EnsureAssetFolderExists(folderPath);

			string safeMeshName = CreateSafeAssetName(
				meshName,
				"GeneratedMeshCollider") + "Mesh";

			string assetPath = AssetDatabase.GenerateUniqueAssetPath(
				folderPath + "/" + safeMeshName + ".asset");

			mesh.name = Path.GetFileNameWithoutExtension(assetPath);

			AssetDatabase.CreateAsset(mesh, assetPath);
			EditorUtility.SetDirty(mesh);
			AssetDatabase.SaveAssets();
#endif

			return mesh;
		}

#if UNITY_EDITOR
		public static string CreateUniqueGeneratedAttemptName(
			GameObject assetOwner,
			string desiredName)
		{
			if(assetOwner == null)
			{
				return CreateSafeAssetName(desiredName, "GeneratedAttempt");
			}

			string safeAttemptName = CreateSafeAssetName(
				desiredName,
				"GeneratedAttempt");

			string ownerFolderPath = GetGeneratedMeshFolderPath(
				assetOwner,
				true);

			int attemptIndex = 1;

			while(true)
			{
				string candidateName =
					safeAttemptName + "_" + attemptIndex.ToString("D3");

				string attemptFolderPath =
					ownerFolderPath + "/" + candidateName;

				string hierarchyRootName =
					candidateName + "_Colliders";

				bool folderExists =
					AssetDatabase.IsValidFolder(attemptFolderPath);

				bool hierarchyRootExists =
					assetOwner.transform.Find(hierarchyRootName) != null;

				if(!folderExists && !hierarchyRootExists)
				{
					return candidateName;
				}

				attemptIndex++;
			}
		}
#endif

		public static string GetUniqueGeneratedMeshName(
	GameObject assetOwner,
	GameObject hierarchyRoot,
	string desiredName,
	string generationAttemptName)
		{
#if UNITY_EDITOR
			if(!string.IsNullOrEmpty(generationAttemptName))
			{
				return desiredName;
			}

			string safeName = CreateSafeAssetName(
				desiredName,
				"GeneratedMeshCollider");

			string folderPath = GetGeneratedMeshFolderPath(
				assetOwner,
				false);

			int attemptIndex = 1;

			while(true)
			{
				string candidateName =
					safeName + "_" + attemptIndex.ToString("D3");

				bool hierarchyNameExists =
					hierarchyRoot != null &&
					hierarchyRoot.transform.Find(candidateName) != null;

				string candidateAssetPath =
					folderPath + "/" + candidateName + "Mesh.asset";

				bool assetExists =
					AssetDatabase.LoadMainAssetAtPath(candidateAssetPath) != null;

				if(!hierarchyNameExists && !assetExists)
				{
					return candidateName;
				}

				attemptIndex++;
			}
#else
	return desiredName;
#endif
		}

#if UNITY_EDITOR
		/// <summary>
		/// Deletes the generated mesh asset used by a collider when no other collider still references it.
		/// </summary>
		public static void DeleteGeneratedMeshAssetIfUnused(
	Collider collider)
		{
			Mesh generatedMesh = GetGeneratedMeshFromCollider(collider);

			if(generatedMesh == null)
			{
				return;
			}

			string assetPath = AssetDatabase.GetAssetPath(generatedMesh);
			GameObject ignoredObject =
				collider == null ? null : collider.gameObject;

			DeleteGeneratedMeshAssetIfUnused(
				assetPath,
				ignoredObject);
		}

		public static void DeleteGeneratedMeshAssetIfUnused(
			string assetPath)
		{
			DeleteGeneratedMeshAssetIfUnused(assetPath, null);
		}

		private static void DeleteGeneratedMeshAssetIfUnused(
			string assetPath,
			GameObject ignoredObject)
		{
			if(!CanDeleteGeneratedMeshAsset(assetPath))
			{
				return;
			}

			Mesh generatedMesh =
				AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

			if(generatedMesh != null &&
				IsMeshUsedByAnotherLoadedObject(
					ignoredObject,
					generatedMesh))
			{
				return;
			}

			if(!AssetDatabase.DeleteAsset(assetPath))
			{
				return;
			}

			DeleteEmptyGeneratedMeshFolders(assetPath);
			AssetDatabase.SaveAssets();
		}

		/// <summary>
		/// Saves the owning prefab or marks the owning scene dirty after generated collider data changes.
		/// </summary>
		public static void SaveGeneratedColliderOwnerIfNeeded(GameObject root)
		{
			PrefabStage prefabStage = GetContainingPrefabStage(root);
			if(prefabStage != null && prefabStage.prefabContentsRoot != null)
			{
				EditorUtility.SetDirty(prefabStage.prefabContentsRoot);
				EditorSceneManager.MarkSceneDirty(prefabStage.scene);
				return;
			}

			if(root != null && root.scene.IsValid())
			{
				EditorSceneManager.MarkSceneDirty(root.scene);
			}
		}

		/// <summary>
		/// Finds the prefab stage that owns the target root. This avoids using an unrelated open prefab stage.
		/// </summary>
		private static PrefabStage GetContainingPrefabStage(GameObject root)
		{
			PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
			if(prefabStage == null || prefabStage.prefabContentsRoot == null || root == null)
			{
				return null;
			}

			if(root == prefabStage.prefabContentsRoot || root.transform.IsChildOf(prefabStage.prefabContentsRoot.transform))
			{
				return prefabStage;
			}

			return null;
		}

		/// <summary>
		/// Chooses the place a human would expect generated collider meshes to live.
		/// </summary>
		private static string GetGeneratedMeshFolderPath(GameObject assetOwner, bool includePrefabOwnerFolder)
		{
			PrefabStage prefabStage = GetContainingPrefabStage(assetOwner);

			if(prefabStage != null &&
				!string.IsNullOrEmpty(prefabStage.assetPath))
			{
				string prefabFolderPath =
					GetSiblingGeneratedMeshFolder(prefabStage.assetPath);

				if(includePrefabOwnerFolder)
				{
					prefabFolderPath += "/" + CreateSafeAssetName(
						assetOwner.name,
						"Object");
				}

				return prefabFolderPath;
			}

			if(assetOwner != null &&
				assetOwner.scene.IsValid() &&
				!string.IsNullOrEmpty(assetOwner.scene.path))
			{
				string sceneFolder = Path.GetDirectoryName(
					assetOwner.scene.path).Replace("\\", "/");

				string sceneName = Path.GetFileNameWithoutExtension(
					assetOwner.scene.path);

				return sceneFolder + "/" +
					CreateSafeAssetName(sceneName, "Scene") +
					GENERATED_MESH_FOLDER_NAME + "/" +
					CreateSafeAssetName(assetOwner.name, "Object");
			}

			string ownerName =
				assetOwner == null ? "Object" : assetOwner.name;

			return GENERATED_MESH_ROOT_FOLDER +
				"/UnsavedScene/" +
				CreateSafeAssetName(ownerName, "Object");
		}

		private static string GetSiblingGeneratedMeshFolder(string ownerAssetPath)
		{
			string assetFolder = Path.GetDirectoryName(ownerAssetPath).Replace("\\", "/");
			string assetName = Path.GetFileNameWithoutExtension(ownerAssetPath);
			return assetFolder + "/" + CreateSafeAssetName(assetName, "Prefab") + GENERATED_MESH_FOLDER_NAME;
		}

		private static void EnsureAssetFolderExists(string folderPath)
		{
			string[] pathParts = folderPath.Split('/');
			string currentPath = pathParts[0];

			for(int i = 1; i < pathParts.Length; i++)
			{
				string nextPath = currentPath + "/" + pathParts[i];
				if(!AssetDatabase.IsValidFolder(nextPath))
				{
					AssetDatabase.CreateFolder(currentPath, pathParts[i]);
				}

				currentPath = nextPath;
			}
		}

		private static string CreateSafeAssetName(string desiredName, string fallbackName)
		{
			string sourceName = string.IsNullOrEmpty(desiredName) ? fallbackName : desiredName;
			char[] invalidCharacters = Path.GetInvalidFileNameChars();
			string safeName = sourceName;

			for(int i = 0; i < invalidCharacters.Length; i++)
			{
				safeName = safeName.Replace(invalidCharacters[i], '_');
			}

			safeName = safeName.Replace(' ', '_');
			return string.IsNullOrEmpty(safeName) ? fallbackName : safeName;
		}

		private static Mesh GetGeneratedMeshFromCollider(Collider collider)
		{
			MeshCollider meshCollider = collider as MeshCollider;
			if(meshCollider != null && meshCollider.sharedMesh != null)
			{
				return meshCollider.sharedMesh;
			}

			ColliderEditorGeneratedCollider generatedMeshAsset = collider == null ? null : collider.GetComponent<ColliderEditorGeneratedCollider>();
			if(generatedMeshAsset != null)
			{
				return generatedMeshAsset.Mesh;
			}

			return null;
		}

		private static bool CanDeleteGeneratedMeshAsset(string assetPath)
		{
			if(string.IsNullOrEmpty(assetPath))
			{
				return false;
			}

			string normalizedPath = assetPath.Replace("\\", "/");

			if(normalizedPath.StartsWith(
				GENERATED_MESH_ROOT_FOLDER + "/",
				StringComparison.Ordinal))
			{
				return true;
			}

			string[] pathParts = normalizedPath.Split('/');
			int partCount = pathParts.Length;

			for(int index = 0; index < partCount; index++)
			{
				if(pathParts[index].EndsWith(
					GENERATED_MESH_FOLDER_NAME,
					StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsMeshUsedByAnotherLoadedObject(
	GameObject ignoredObject,
	Mesh generatedMesh)
		{
			MeshCollider[] mainStageMeshColliders =
				UnityEngine.Object.FindObjectsByType<MeshCollider>(
					FindObjectsInactive.Include,
					FindObjectsSortMode.None);

			if(IsMeshUsedByMeshCollider(
				mainStageMeshColliders,
				ignoredObject,
				generatedMesh))
			{
				return true;
			}

			ColliderEditorGeneratedCollider[] mainStageMarkers =
				UnityEngine.Object.FindObjectsByType<
					ColliderEditorGeneratedCollider>(
						FindObjectsInactive.Include,
						FindObjectsSortMode.None);

			if(IsMeshUsedByGeneratedMarker(
				mainStageMarkers,
				ignoredObject,
				generatedMesh))
			{
				return true;
			}

			PrefabStage prefabStage =
				PrefabStageUtility.GetCurrentPrefabStage();

			if(prefabStage == null ||
				prefabStage.prefabContentsRoot == null)
			{
				return false;
			}

			MeshCollider[] prefabStageMeshColliders =
				prefabStage.prefabContentsRoot
					.GetComponentsInChildren<MeshCollider>(true);

			if(IsMeshUsedByMeshCollider(
				prefabStageMeshColliders,
				ignoredObject,
				generatedMesh))
			{
				return true;
			}

			ColliderEditorGeneratedCollider[] prefabStageMarkers =
				prefabStage.prefabContentsRoot
					.GetComponentsInChildren<
						ColliderEditorGeneratedCollider>(true);

			return IsMeshUsedByGeneratedMarker(
				prefabStageMarkers,
				ignoredObject,
				generatedMesh);
		}

		private static bool IsMeshUsedByMeshCollider(
	MeshCollider[] meshColliders,
	GameObject ignoredObject,
	Mesh generatedMesh)
		{
			int colliderCount = meshColliders.Length;

			for(int index = 0;
				index < colliderCount;
				index++)
			{
				MeshCollider meshCollider =
					meshColliders[index];

				if(meshCollider == null ||
					meshCollider.gameObject == ignoredObject)
				{
					continue;
				}

				if(meshCollider.sharedMesh == generatedMesh)
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsMeshUsedByGeneratedMarker(
			ColliderEditorGeneratedCollider[] generatedColliders,
			GameObject ignoredObject,
			Mesh generatedMesh)
		{
			int markerCount = generatedColliders.Length;

			for(int index = 0;
				index < markerCount;
				index++)
			{
				ColliderEditorGeneratedCollider generatedCollider =
					generatedColliders[index];

				if(generatedCollider == null ||
					generatedCollider.gameObject == ignoredObject)
				{
					continue;
				}

				if(generatedCollider.Mesh == generatedMesh)
				{
					return true;
				}
			}

			return false;
		}

		private static void DeleteEmptyGeneratedMeshFolders(
	string deletedAssetPath)
		{
			string currentFolderPath = Path.GetDirectoryName(
				deletedAssetPath).Replace("\\", "/");

			while(IsGeneratedMeshFolderPath(currentFolderPath))
			{
				string absoluteFolderPath =
					Path.GetFullPath(currentFolderPath);

				if(!Directory.Exists(absoluteFolderPath))
				{
					return;
				}

				string[] childDirectories =
					Directory.GetDirectories(absoluteFolderPath);

				if(childDirectories.Length > 0)
				{
					return;
				}

				string[] files = Directory.GetFiles(
					absoluteFolderPath);

				int fileCount = files.Length;

				for(int index = 0; index < fileCount; index++)
				{
					if(!files[index].EndsWith(
						".meta",
						StringComparison.OrdinalIgnoreCase))
					{
						return;
					}
				}

				string parentFolderPath = Path.GetDirectoryName(
					currentFolderPath).Replace("\\", "/");

				if(!AssetDatabase.DeleteAsset(currentFolderPath))
				{
					return;
				}

				currentFolderPath = parentFolderPath;
			}
		}

		private static bool IsGeneratedMeshFolderPath(
			string folderPath)
		{
			if(string.IsNullOrEmpty(folderPath))
			{
				return false;
			}

			string normalizedPath = folderPath.Replace("\\", "/");

			if(normalizedPath == GENERATED_MESH_ROOT_FOLDER ||
				normalizedPath.StartsWith(
					GENERATED_MESH_ROOT_FOLDER + "/",
					StringComparison.Ordinal))
			{
				return true;
			}

			string[] pathParts = normalizedPath.Split('/');
			int partCount = pathParts.Length;

			for(int index = 0; index < partCount; index++)
			{
				if(pathParts[index].EndsWith(
					GENERATED_MESH_FOLDER_NAME,
					StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}
#endif
	}
}
