#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ColliderEditor
{
	/// <summary>
	/// Provides editor-only mesh ray intersection helpers for Scene view selection.
	/// Unity keeps HandleUtility.IntersectRayMesh internal, so this wrapper accesses it through reflection
	/// and keeps that unsupported API usage isolated from the rest of the Collider Editor code.
	///
	/// Dependencies:
	/// - UnityEditor.HandleUtility for the internal IntersectRayMesh method
	/// - MeshFilter, Mesh and Matrix4x4 data supplied by ColliderEditorWindow visible-selection checks
	/// </summary>
	internal static class ColliderEditorHandleUtility
	{
		private static readonly MethodInfo intersectRayMeshMethod = FindIntersectRayMeshMethod();
		private static readonly object[] intersectRayMeshParameters = new object[4];

		/// <summary>
		/// Uses Unity's internal Scene view mesh raycast to find the first hit against a MeshFilter.
		/// </summary>
		public static bool IntersectRayMesh(Ray ray, MeshFilter meshFilter, out RaycastHit hit)
		{
			if(meshFilter == null)
			{
				hit = default;
				return false;
			}

			return IntersectRayMesh(ray, meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, out hit);
		}

		/// <summary>
		/// Uses Unity's internal Scene view mesh raycast to find the first hit against a mesh and transform matrix.
		/// </summary>
		public static bool IntersectRayMesh(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit)
		{
			if(intersectRayMeshMethod == null || mesh == null)
			{
				hit = default;
				return false;
			}

			intersectRayMeshParameters[0] = ray;
			intersectRayMeshParameters[1] = mesh;
			intersectRayMeshParameters[2] = matrix;
			intersectRayMeshParameters[3] = null;

			bool hasHit = (bool)intersectRayMeshMethod.Invoke(null, intersectRayMeshParameters);
			hit = hasHit ? (RaycastHit)intersectRayMeshParameters[3] : default;

			return hasHit;
		}

		private static MethodInfo FindIntersectRayMeshMethod()
		{
			Type handleUtilityType = typeof(HandleUtility);
			Type[] parameterTypes =
			{
				typeof(Ray),
				typeof(Mesh),
				typeof(Matrix4x4),
				typeof(RaycastHit).MakeByRefType()
			};

			return handleUtilityType.GetMethod(
				"IntersectRayMesh",
				BindingFlags.Static | BindingFlags.NonPublic,
				null,
				parameterTypes,
				null);
		}
	}
}
#endif
