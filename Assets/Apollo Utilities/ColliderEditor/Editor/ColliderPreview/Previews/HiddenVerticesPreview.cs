#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColliderEditor
{
	/// <summary>
	/// Draws a temporary source-mesh preview with hidden vertices removed from the triangle list.
	/// The original MeshRenderer is masked with forceRenderingOff while this preview is active, so the
	/// imported mesh asset and renderer enabled state are not destructively modified by vertex hiding.
	/// </summary>
	public sealed class HiddenVerticesPreview
	{
		private const string HIDDEN_SOURCE_MESH_NAME = "ColliderEditor_HiddenSourceMeshPreview";

		private readonly Dictionary<int, HiddenSourceMeshCache> HIDDEN_SOURCE_MESH_CACHES =
			new Dictionary<int, HiddenSourceMeshCache>();

		private readonly Dictionary<int, RendererVisibilityState> HIDDEN_SOURCE_RENDERERS =
			new Dictionary<int, RendererVisibilityState>();

		private GameObject cachedHiddenSourceOwner;
		private bool cachedHiddenSourceIncludeChildren;

		private sealed class HiddenSourceMeshCache
		{
			public MeshFilter meshFilter;
			public Mesh sourceMesh;
			public Mesh filteredMesh;
			public int hiddenSignature;
			public bool hasHiddenVertices;
		}

		private sealed class RendererVisibilityState
		{
			public Renderer renderer;
			public bool wasEnabled;
			public bool wasForceRenderingOff;

			public RendererVisibilityState(Renderer renderer)
			{
				this.renderer = renderer;
				this.wasEnabled = renderer != null && renderer.enabled;
				this.wasForceRenderingOff = renderer != null && renderer.forceRenderingOff;
			}
		}

		private readonly PreviewState previewState;

		public HiddenVerticesPreview(PreviewState previewState)
		{
			this.previewState = previewState;
		}

		public void DrawHiddenSourceMeshes(ColliderEditorState state)
		{
			if(state == null || state.SelectedObject == null || state.HiddenVertices.Count == 0)
			{
				RestoreHiddenSourceRenderers();
				ClearHiddenSourceMeshCaches();
				cachedHiddenSourceOwner = null;
				return;
			}

			List<MeshFilter> meshFilters = ColliderEditorSelectionUtility.GetMeshFilters(state.SelectedObject, state.IncludeChildren);
			if(meshFilters.Count == 0)
			{
				RestoreHiddenSourceRenderers();
				ClearHiddenSourceMeshCaches();
				cachedHiddenSourceOwner = null;
				return;
			}

			ResetHiddenSourceCachesIfTargetChanged(state);

			HashSet<VertexKey> hiddenVertexKeys = state.GetHiddenVertexKeys();
			int hiddenSignature = Geometry.CalculateVertexListSignature(state.HiddenVertices);
			List<HiddenSourceMeshCache> activeCaches = GetActiveHiddenSourceMeshCaches(meshFilters, hiddenVertexKeys, hiddenSignature);

			UpdateHiddenSourceRendererMask(activeCaches);
			DrawActiveHiddenSourceMeshes(activeCaches);
		}

		private List<HiddenSourceMeshCache> GetActiveHiddenSourceMeshCaches(
			List<MeshFilter> meshFilters,
			HashSet<VertexKey> hiddenVertexKeys,
			int hiddenSignature)
		{
			List<HiddenSourceMeshCache> activeCaches = new List<HiddenSourceMeshCache>();

			int count = meshFilters.Count;
			for(int index = 0; index < count; index++)
			{
				HiddenSourceMeshCache cache = GetOrCreateHiddenSourceMeshCache(
					meshFilters[index],
					hiddenVertexKeys,
					hiddenSignature);

				if(cache != null && cache.hasHiddenVertices)
				{
					activeCaches.Add(cache);
				}
			}

			return activeCaches;
		}

		private HiddenSourceMeshCache GetOrCreateHiddenSourceMeshCache(
			MeshFilter meshFilter,
			HashSet<VertexKey> hiddenVertexKeys,
			int hiddenSignature)
		{
			if(meshFilter == null || meshFilter.sharedMesh == null)
			{
				return null;
			}

			int instanceId = meshFilter.GetInstanceID();
			HiddenSourceMeshCache cache;
			if(!HIDDEN_SOURCE_MESH_CACHES.TryGetValue(instanceId, out cache))
			{
				cache = new HiddenSourceMeshCache();
				cache.meshFilter = meshFilter;
				cache.hiddenSignature = 0;
				HIDDEN_SOURCE_MESH_CACHES.Add(instanceId, cache);
			}

			Mesh sourceMesh = meshFilter.sharedMesh;
			if(cache.sourceMesh == sourceMesh && cache.hiddenSignature == hiddenSignature)
			{
				return cache;
			}

			previewState.DestroyMesh(ref cache.filteredMesh);
			cache.sourceMesh = sourceMesh;
			cache.hiddenSignature = hiddenSignature;

			bool[] hiddenIndices;
			Vector3[] localVertices = sourceMesh.vertices;
			cache.hasHiddenVertices = TryCreateHiddenIndexMap(
				meshFilter.transform,
				localVertices,
				hiddenVertexKeys,
				out hiddenIndices);

			if(!cache.hasHiddenVertices)
			{
				return cache;
			}

			cache.filteredMesh = CreateFilteredSourceMesh(sourceMesh, localVertices, hiddenIndices);
			return cache;
		}

		private bool TryCreateHiddenIndexMap(
			Transform meshTransform,
			Vector3[] localVertices,
			HashSet<VertexKey> hiddenVertexKeys,
			out bool[] hiddenIndices)
		{
			hiddenIndices = new bool[localVertices.Length];
			bool hasHiddenVertices = false;

			for(int vertexIndex = 0; vertexIndex < localVertices.Length; vertexIndex++)
			{
				VertexKey vertexKey = new VertexKey(meshTransform, localVertices[vertexIndex]);
				if(!hiddenVertexKeys.Contains(vertexKey))
				{
					continue;
				}

				hiddenIndices[vertexIndex] = true;
				hasHiddenVertices = true;
			}

			return hasHiddenVertices;
		}

		private Mesh CreateFilteredSourceMesh(Mesh sourceMesh, Vector3[] localVertices, bool[] hiddenIndices)
		{
			int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);
			int[][] visibleTrianglesBySubMesh = new int[subMeshCount][];
			int visibleIndexCount = 0;

			for(int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
			{
				int[] sourceTriangles = sourceMesh.GetTriangles(subMeshIndex);
				List<int> visibleTriangles = CreateVisibleTriangles(sourceTriangles, localVertices.Length, hiddenIndices);
				visibleTrianglesBySubMesh[subMeshIndex] = visibleTriangles.ToArray();
				visibleIndexCount += visibleTriangles.Count;
			}

			if(visibleIndexCount == 0)
			{
				return null;
			}

			Mesh filteredMesh = new Mesh();
			filteredMesh.name = HIDDEN_SOURCE_MESH_NAME;
			filteredMesh.indexFormat = sourceMesh.indexFormat;
			filteredMesh.vertices = localVertices;
			CopySourceMeshChannels(sourceMesh, filteredMesh, localVertices.Length);

			filteredMesh.subMeshCount = subMeshCount;
			for(int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
			{
				filteredMesh.SetTriangles(visibleTrianglesBySubMesh[subMeshIndex], subMeshIndex);
			}

			if(filteredMesh.normals == null || filteredMesh.normals.Length != localVertices.Length)
			{
				filteredMesh.RecalculateNormals();
			}

			filteredMesh.RecalculateBounds();
			return filteredMesh;
		}

		private List<int> CreateVisibleTriangles(int[] sourceTriangles, int vertexCount, bool[] hiddenIndices)
		{
			List<int> visibleTriangles = new List<int>(sourceTriangles.Length);

			for(int triangleIndex = 0; triangleIndex + 2 < sourceTriangles.Length; triangleIndex += 3)
			{
				int firstIndex = sourceTriangles[triangleIndex];
				int secondIndex = sourceTriangles[triangleIndex + 1];
				int thirdIndex = sourceTriangles[triangleIndex + 2];

				if(!Geometry.IsValidTriangleIndex(firstIndex, secondIndex, thirdIndex, vertexCount))
				{
					continue;
				}

				if(hiddenIndices[firstIndex] || hiddenIndices[secondIndex] || hiddenIndices[thirdIndex])
				{
					continue;
				}

				visibleTriangles.Add(firstIndex);
				visibleTriangles.Add(secondIndex);
				visibleTriangles.Add(thirdIndex);
			}

			return visibleTriangles;
		}

		private void CopySourceMeshChannels(Mesh sourceMesh, Mesh filteredMesh, int vertexCount)
		{
			Vector3[] normals = sourceMesh.normals;
			if(normals != null && normals.Length == vertexCount)
			{
				filteredMesh.normals = normals;
			}

			Vector4[] tangents = sourceMesh.tangents;
			if(tangents != null && tangents.Length == vertexCount)
			{
				filteredMesh.tangents = tangents;
			}

			Color32[] colors32 = sourceMesh.colors32;
			if(colors32 != null && colors32.Length == vertexCount)
			{
				filteredMesh.colors32 = colors32;
			}

			CopyUvChannels(sourceMesh, filteredMesh, vertexCount);
		}

		private void CopyUvChannels(Mesh sourceMesh, Mesh filteredMesh, int vertexCount)
		{
			Vector2[] uv = sourceMesh.uv;
			if(uv != null && uv.Length == vertexCount)
			{
				filteredMesh.uv = uv;
			}

			Vector2[] uv2 = sourceMesh.uv2;
			if(uv2 != null && uv2.Length == vertexCount)
			{
				filteredMesh.uv2 = uv2;
			}

			Vector2[] uv3 = sourceMesh.uv3;
			if(uv3 != null && uv3.Length == vertexCount)
			{
				filteredMesh.uv3 = uv3;
			}

			Vector2[] uv4 = sourceMesh.uv4;
			if(uv4 != null && uv4.Length == vertexCount)
			{
				filteredMesh.uv4 = uv4;
			}
		}

		private void UpdateHiddenSourceRendererMask(List<HiddenSourceMeshCache> activeCaches)
		{
			HashSet<int> activeRendererIds = new HashSet<int>();

			int count = activeCaches.Count;
			for(int index = 0; index < count; index++)
			{
				Renderer renderer = GetCacheRenderer(activeCaches[index]);
				if(renderer == null)
				{
					continue;
				}

				int rendererId = renderer.GetInstanceID();
				activeRendererIds.Add(rendererId);

				if(!HIDDEN_SOURCE_RENDERERS.ContainsKey(rendererId))
				{
					HIDDEN_SOURCE_RENDERERS.Add(rendererId, new RendererVisibilityState(renderer));
				}

				renderer.forceRenderingOff = true;
			}

			RestoreInactiveHiddenSourceRenderers(activeRendererIds);
		}

		private Renderer GetCacheRenderer(HiddenSourceMeshCache cache)
		{
			if(cache == null || cache.meshFilter == null)
			{
				return null;
			}

			return cache.meshFilter.GetComponent<Renderer>();
		}

		private void RestoreInactiveHiddenSourceRenderers(HashSet<int> activeRendererIds)
		{
			List<int> rendererIdsToRestore = null;

			foreach(KeyValuePair<int, RendererVisibilityState> rendererPair in HIDDEN_SOURCE_RENDERERS)
			{
				if(rendererPair.Value.renderer == null || !activeRendererIds.Contains(rendererPair.Key))
				{
					if(rendererIdsToRestore == null)
					{
						rendererIdsToRestore = new List<int>();
					}

					rendererIdsToRestore.Add(rendererPair.Key);
				}
			}

			if(rendererIdsToRestore == null)
			{
				return;
			}

			int count = rendererIdsToRestore.Count;
			for(int index = 0; index < count; index++)
			{
				RestoreHiddenSourceRenderer(rendererIdsToRestore[index]);
			}
		}

		private void DrawActiveHiddenSourceMeshes(List<HiddenSourceMeshCache> activeCaches)
		{
			int count = activeCaches.Count;
			for(int index = 0; index < count; index++)
			{
				DrawHiddenSourceMesh(activeCaches[index]);
			}
		}

		private void DrawHiddenSourceMesh(HiddenSourceMeshCache cache)
		{
			if(cache == null || cache.filteredMesh == null || cache.meshFilter == null)
			{
				return;
			}

			Renderer renderer = GetCacheRenderer(cache);
			if(!ShouldDrawHiddenSourceRenderer(renderer))
			{
				return;
			}

			Material[] materials = renderer.sharedMaterials;
			int subMeshCount = cache.filteredMesh.subMeshCount;
			for(int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
			{
				Material material = GetSubMeshMaterial(materials, subMeshIndex);
				if(material == null || !material.SetPass(0))
				{
					continue;
				}

				Graphics.DrawMeshNow(cache.filteredMesh, cache.meshFilter.transform.localToWorldMatrix, subMeshIndex);
			}
		}

		private bool ShouldDrawHiddenSourceRenderer(Renderer renderer)
		{
			if(renderer == null)
			{
				return false;
			}

			RendererVisibilityState rendererState;
			if(!HIDDEN_SOURCE_RENDERERS.TryGetValue(renderer.GetInstanceID(), out rendererState))
			{
				return renderer.enabled && !renderer.forceRenderingOff;
			}

			return rendererState.wasEnabled && !rendererState.wasForceRenderingOff;
		}

		private Material GetSubMeshMaterial(Material[] materials, int subMeshIndex)
		{
			if(materials == null || materials.Length == 0)
			{
				return null;
			}

			int materialIndex = Mathf.Min(subMeshIndex, materials.Length - 1);
			return materials[materialIndex];
		}

		private void ResetHiddenSourceCachesIfTargetChanged(ColliderEditorState state)
		{
			if(cachedHiddenSourceOwner == state.SelectedObject &&
				cachedHiddenSourceIncludeChildren == state.IncludeChildren)
			{
				return;
			}

			RestoreHiddenSourceRenderers();
			ClearHiddenSourceMeshCaches();
			cachedHiddenSourceOwner = state.SelectedObject;
			cachedHiddenSourceIncludeChildren = state.IncludeChildren;
		}

		public void RestoreHiddenSourceRenderers()
		{
			List<int> rendererIds = null;

			foreach(KeyValuePair<int, RendererVisibilityState> rendererPair in HIDDEN_SOURCE_RENDERERS)
			{
				if(rendererIds == null)
				{
					rendererIds = new List<int>();
				}

				rendererIds.Add(rendererPair.Key);
			}

			if(rendererIds == null)
			{
				return;
			}

			int count = rendererIds.Count;
			for(int index = 0; index < count; index++)
			{
				RestoreHiddenSourceRenderer(rendererIds[index]);
			}
		}

		private void RestoreHiddenSourceRenderer(int rendererId)
		{
			RendererVisibilityState rendererState;
			if(!HIDDEN_SOURCE_RENDERERS.TryGetValue(rendererId, out rendererState))
			{
				return;
			}

			if(rendererState.renderer != null)
			{
				rendererState.renderer.forceRenderingOff = rendererState.wasForceRenderingOff;
			}

			HIDDEN_SOURCE_RENDERERS.Remove(rendererId);
		}

		public void ClearHiddenSourceMeshCaches()
		{
			foreach(KeyValuePair<int, HiddenSourceMeshCache> cachePair in HIDDEN_SOURCE_MESH_CACHES)
			{
				HiddenSourceMeshCache cache = cachePair.Value;
				if(cache != null)
				{
					previewState.DestroyMesh(ref cache.filteredMesh);
				}
			}

			HIDDEN_SOURCE_MESH_CACHES.Clear();
		}
	}
}
#endif
