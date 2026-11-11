using System.Collections.Generic;
using UnityEngine;

namespace ColliderEditor
{
	public class PreviewState
	{
		public const string MESH_PREVIEW_SHADER_NAME = "Custom/ColliderPreview";
		public const int MESH_PREVIEW_PASS_INDEX = 0;
		public const int MESH_OVERLAY_PASS_INDEX = 1;
		public const float MINIMUM_PREVIEW_VALUE = 0.001f;
		public const float MINIMUM_VERTEX_WORLD_SIZE = 0.0005f;
		public const int SPHERE_LONGITUDE_SEGMENTS = 16;
		public const int SPHERE_LATITUDE_SEGMENTS = 10;
		public const int CAPSULE_HEMISPHERE_SEGMENTS = 8;
		public const float MESH_WIREFRAME_WIDTH = 1.0f;
		public const float SELECTED_EDGE_WIDTH = 3.0f;
		public const float MINIMUM_SCREEN_VERTEX_SIZE = 0.025f;
		public const float EDIT_OVERLAY_DEPTH_BIAS = 0.0025f;

		public Shader meshPreviewShader;
		public Material meshPreviewMaterial;

		public readonly Dictionary<int, MeshOverlayCache> MESH_OVERLAY_CACHES = new Dictionary<int, MeshOverlayCache>();
		public GameObject cachedOverlayOwner;
		public bool cachedOverlayIncludeChildren;

		public bool EnsurePreviewMaterial()
		{
			if(meshPreviewMaterial != null)
			{
				return true;
			}

			if(meshPreviewShader == null)
			{
				meshPreviewShader = Shader.Find(MESH_PREVIEW_SHADER_NAME);
			}

			if(meshPreviewShader == null)
			{
				return false;
			}

			meshPreviewMaterial = new Material(meshPreviewShader);
			meshPreviewMaterial.hideFlags = HideFlags.HideAndDontSave;
			return true;
		}

		public void Dispose()
		{
			ClearMeshOverlayCaches();

			if(meshPreviewMaterial != null)
			{
				UnityEngine.Object.DestroyImmediate(meshPreviewMaterial);
				meshPreviewMaterial = null;
			}

			meshPreviewShader = null;
		}

		/// <summary>
		/// Releases every cached mesh used by the edit-mode overlay.
		/// </summary>
		public void ClearMeshOverlayCaches()
		{
			foreach(KeyValuePair<int, MeshOverlayCache> cachePair in this.MESH_OVERLAY_CACHES)
			{
				DestroyOverlayCacheMeshes(cachePair.Value);
			}

			this.MESH_OVERLAY_CACHES.Clear();
		}

		/// <summary>
		/// Releases meshes owned by a single overlay cache.
		/// </summary>
		public void DestroyOverlayCacheMeshes(MeshOverlayCache cache)
		{
			if(cache == null)
			{
				return;
			}

			DestroyMesh(ref cache.unselectedPointMesh);
			DestroyMesh(ref cache.selectedPointMesh);
			DestroyMesh(ref cache.selectedFaceMesh);
			cache.wireframeWorldLines = null;
			cache.selectedEdgeWorldLines = null;
		}

		/// <summary>
		/// Destroys a preview mesh and clears the reference.
		/// </summary>
		public void DestroyMesh(ref Mesh mesh)
		{
			if(mesh == null)
			{
				return;
			}

			UnityEngine.Object.DestroyImmediate(mesh);
			mesh = null;
		}
	}
}
