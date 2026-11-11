#if UNITY_EDITOR
using UnityEngine;

namespace ColliderEditor
{
	/// <summary>
	/// Partial scene preview implementation for ColliderEditor.
	/// Draws colliders that already exist under the selected object so artists can inspect the
	/// generated result with the same performance colors used while authoring new colliders.
	/// </summary>
	public sealed class ExistingColliderPerformancePreview
	{
		private readonly DrawingService DRAW;
		public ExistingColliderPerformancePreview(DrawingService draw)
		{
			this.DRAW = draw;
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

		public void DrawExistingColliderPerformanceView(ColliderEditorState state)
		{
			Collider[] colliders = state.SelectedObject.GetComponentsInChildren<Collider>(true);

			for(int index = 0; index < colliders.Length; index++)
			{
				DrawExistingColliderPerformancePreview(state, colliders[index]);
			}
		}

		private void DrawExistingColliderPerformancePreview(ColliderEditorState state, Collider collider)
		{
			if(collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
			{
				return;
			}

			if(!state.IsExistingColliderVisible(collider))
			{
				return;
			}

			Color color = GetPerformanceColorForCollider(collider);

			BoxCollider boxCollider = collider as BoxCollider;
			if(boxCollider != null)
			{
				DRAW.DrawBox(Geometry.CalculateBoxMatrixFromCollider(boxCollider), color, state.DrawExistingColliderPreviewAsWireframe);
				return;
			}

			SphereCollider sphereCollider = collider as SphereCollider;
			if(sphereCollider != null)
			{
				DRAW.DrawSphere(Geometry.CalculateSphereMatrixFromCollider(sphereCollider), color, state.DrawExistingColliderPreviewAsWireframe);
				return;
			}

			CapsuleCollider capsuleCollider = collider as CapsuleCollider;
			if(capsuleCollider != null)
			{
				(Matrix4x4 matrix, (float height, float radius)) = Geometry.CalculateCapsuleMatrixFromCollider(capsuleCollider);
				DRAW.DrawCapsule(matrix, color, state.DrawExistingColliderPreviewAsWireframe, (height, radius));
				return;
			}

			MeshCollider meshCollider = collider as MeshCollider;
			if(meshCollider != null && meshCollider.sharedMesh != null)
			{
				DRAW.DrawConvexMesh(meshCollider.sharedMesh, meshCollider.transform.localToWorldMatrix, color, state.DrawExistingColliderPreviewAsWireframe);
			}
		}
	}
}
#endif
