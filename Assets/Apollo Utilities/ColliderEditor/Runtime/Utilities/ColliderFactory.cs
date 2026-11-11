using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
#if(UNITY_6000_0_OR_NEWER)
using PhysicsMaterialType = UnityEngine.PhysicsMaterial;
#else
using PhysicsMaterialType = UnityEngine.PhysicMaterial;
#endif

namespace ColliderEditor
{
	/// <summary>
	/// Utility methods for creating collider GameObjects from the current selected point cloud.
	/// The editor window uses these methods for both one-click creation and preview-confirm workflows.
	/// </summary>
	public static class ColliderFactory
	{
		private const float MINIMUM_COLLIDER_VALUE = 0.001f;
		private const float MINIMUM_CAPSULE_RADIUS = 0.001f;
		private const float MINIMUM_CAPSULE_HEIGHT = 0.001f;
		private const int DEFAULT_CYLINDER_SEGMENTS = 16;

		/// <summary>
		/// Small oriented bounds container shared by the preview and collider generation code.
		/// </summary>
		public struct OrientedBoundsData
		{
			public Vector3 center;
			public Vector3 size;
			public Quaternion rotation;
		}

		/// <summary>
		/// Internal primitive fit data for spheres.
		/// </summary>
		public struct SphereFitData
		{
			public Vector3 center;
			public float radius;
		}

		/// <summary>
		/// Internal primitive fit data for capsules.
		/// </summary>
		public struct CapsuleFitData
		{
			public Vector3 center;
			public int axis;
			public float radius;
			public float height;
		}

		/// <summary>
		/// Creates an axis-aligned box collider in the selected object's local space.
		/// </summary>
		public static BoxCollider CreateBoxCollider(GameObject root, List<Vector3> worldPoints, bool isTrigger, PhysicsMaterialType material)
		{
			if(root == null || worldPoints == null || worldPoints.Count == 0)
			{
				return null;
			}

			GameObject child = CreateColliderChild(root, "BoxCollider");
			BoxCollider collider = child.AddComponent<BoxCollider>();
			ApplySharedSettings(collider, isTrigger, material);

			Bounds bounds = Geometry.CreateLocalBounds(root.transform, worldPoints);
			collider.center = bounds.center;
			collider.size = Geometry.MaxVector(bounds.size, Vector3.one * MINIMUM_COLLIDER_VALUE);
			AddGeneratedColliderMarker(child);
			return collider;
		}

		/// <summary>
		/// Creates a rotated box collider by fitting an oriented basis to the selected points.
		/// </summary>
		public static BoxCollider CreateRotatedBoxCollider(GameObject root, List<Vector3> worldPoints, bool isTrigger, PhysicsMaterialType material)
		{
			if(root == null || worldPoints == null || worldPoints.Count == 0)
			{
				return null;
			}

			Geometry.OrientedBoundsData orientedBounds = Geometry.FitOrientedBounds(worldPoints);

			GameObject child = CreateColliderChild(root, "RotatedBoxCollider");
			AlignChildToWorldPose(child.transform, root.transform, orientedBounds.center, orientedBounds.rotation);

			BoxCollider collider = child.AddComponent<BoxCollider>();
			ApplySharedSettings(collider, isTrigger, material);
			collider.center = Vector3.zero;
			collider.size = Geometry.MaxVector(orientedBounds.size, Vector3.one * MINIMUM_COLLIDER_VALUE);
			AddGeneratedColliderMarker(child);
			return collider;
		}

		/// <summary>
		/// Creates a sphere collider using one of the Collider Editor fitting modes.
		/// </summary>
		public static SphereCollider CreateSphereCollider(
			GameObject root,
			List<Vector3> worldPoints,
			bool isTrigger,
			PhysicsMaterialType material,
			SPHERE_COLLIDER_METHOD method = SPHERE_COLLIDER_METHOD.MinMax)
		{
			if(root == null || worldPoints == null || worldPoints.Count == 0)
			{
				return null;
			}

			List<Vector3> localPoints = Geometry.ToLocalPoints(root.transform, worldPoints);
			Geometry.SphereFitData sphereData = Geometry.CalculateSphereFit(localPoints, method);

			GameObject child = CreateColliderChild(root, "SphereCollider");
			SphereCollider collider = child.AddComponent<SphereCollider>();
			ApplySharedSettings(collider, isTrigger, material);
			collider.center = sphereData.center;
			collider.radius = Mathf.Max(MINIMUM_COLLIDER_VALUE, sphereData.radius);
			AddGeneratedColliderMarker(child);
			return collider;
		}

		/// <summary>
		/// Creates an axis-aligned capsule collider using one of the Collider Editor fitting modes.
		/// </summary>
		public static CapsuleCollider CreateCapsuleCollider(
			GameObject root,
			List<Vector3> worldPoints,
			bool isTrigger,
			PhysicsMaterialType material,
			int axis = -1,
			CAPSULE_COLLIDER_METHOD method = CAPSULE_COLLIDER_METHOD.MinMax)
		{
			if(root == null || worldPoints == null || worldPoints.Count == 0)
			{
				return null;
			}

			List<Vector3> localPoints = Geometry.ToLocalPoints(root.transform, worldPoints);
			Geometry.CapsuleFitData capsuleData = Geometry.CalculateCapsuleFit(localPoints, axis, method);

			GameObject child = CreateColliderChild(root, "CapsuleCollider");
			CapsuleCollider collider = child.AddComponent<CapsuleCollider>();
			ApplySharedSettings(collider, isTrigger, material);
			collider.center = capsuleData.center;
			collider.direction = capsuleData.axis;
			collider.radius = capsuleData.radius;
			collider.height = capsuleData.height;
			AddGeneratedColliderMarker(child);
			return collider;
		}

		/// <summary>
		/// Creates a rotated capsule collider by fitting an oriented basis to the selected points.
		/// </summary>
		public static CapsuleCollider CreateRotatedCapsuleCollider(
			GameObject root,
			List<Vector3> worldPoints,
			bool isTrigger,
			PhysicsMaterialType material,
			int axis = -1,
			CAPSULE_COLLIDER_METHOD method = CAPSULE_COLLIDER_METHOD.MinMax)
		{
			if(root == null || worldPoints == null || worldPoints.Count == 0)
			{
				return null;
			}

			Geometry.OrientedBoundsData orientedBounds = Geometry.FitOrientedBounds(worldPoints);
			Quaternion worldRotation = orientedBounds.rotation;
			List<Vector3> orientedLocalPoints = Geometry.ToLocalPoints(orientedBounds.center, worldRotation, worldPoints);
			Geometry.CapsuleFitData capsuleData = Geometry.CalculateCapsuleFit(orientedLocalPoints, axis, method);

			GameObject child = CreateColliderChild(root, "RotatedCapsuleCollider");
			AlignChildToWorldPose(child.transform, root.transform, orientedBounds.center, worldRotation * GetAxisRotationFromYAxis(capsuleData.axis));

			CapsuleCollider collider = child.AddComponent<CapsuleCollider>();
			ApplySharedSettings(collider, isTrigger, material);
			collider.center = Vector3.zero;
			collider.direction = 1;
			collider.radius = capsuleData.radius;
			collider.height = capsuleData.height;
			AddGeneratedColliderMarker(child);
			return collider;
		}

		public static MeshCollider CreateConvexHullCollider(GameObject root, List<Vector3> worldPoints, bool isTrigger, PhysicsMaterialType material)
		{
			if(root == null || worldPoints == null || worldPoints.Count < 4)
			{
				return null;
			}

			Mesh hull = ColliderQuickHull.CalculateHullWorld(worldPoints, root.transform).result;
			return CreateMeshCollider(root, hull, "MeshFromSelectedVerticesCollider", isTrigger, material);
		}

		/// <summary>
		/// Creates a convex mesh cylinder collider with a configurable side count.
		/// </summary>
		public static MeshCollider CreateCylinderCollider(
			GameObject root,
			List<Vector3> worldPoints,
			bool isTrigger,
			PhysicsMaterialType material,
			int axis = -1,
			int cylinderSides = DEFAULT_CYLINDER_SEGMENTS)
		{
			if(root == null || worldPoints == null || worldPoints.Count < 2)
			{
				return null;
			}

			Bounds bounds = Geometry.CreateLocalBounds(root.transform, worldPoints);
			Vector3 size = bounds.size;
			int resolvedAxis = Geometry.ResolveAxis(size, axis);
			float height = Mathf.Max(MINIMUM_COLLIDER_VALUE, Geometry.GetAxisValue(size, resolvedAxis));

			float radius = MINIMUM_COLLIDER_VALUE;
			Vector3 center = bounds.center;

			int count = worldPoints.Count;
			for(int i = 0; i < count; i++)
			{
				Vector3 localPoint = root.transform.InverseTransformPoint(worldPoints[i]) - center;
				Vector2 radialPoint = Geometry.GetRadialPoint(localPoint, resolvedAxis);
				radius = Mathf.Max(radius, radialPoint.magnitude);
			}

			Mesh cylinderMesh = Geometry.CreateCylinderMesh(center, radius, height, resolvedAxis, cylinderSides);
			return CreateMeshCollider(root, cylinderMesh, "CylinderCollider", isTrigger, material);
		}

		public static MeshCollider CreateMeshCollider(
			GameObject root,
			Mesh mesh,
			string childName,
			bool isTrigger,
			PhysicsMaterialType material
		)
		{
			return CreateMeshCollider(
				root,
				root,
				mesh,
				childName,
				isTrigger,
				material,
				null);
		}

		public static Quaternion GetAxisRotationFromYAxis(int axis)
		{
			switch(axis)
			{
				case 0:
					return Quaternion.Euler(0.0f, 0.0f, -90.0f);

				case 2:
					return Quaternion.Euler(90.0f, 0.0f, 0.0f);

				default:
					return Quaternion.identity;
			}
		}

		public static MeshCollider CreateMeshCollider(
			GameObject hierarchyRoot,
			GameObject assetOwner,
			Mesh mesh,
			string childName,
			bool isTrigger,
			PhysicsMaterialType material,
			string generationAttemptName
		)
		{
			if(hierarchyRoot == null || mesh == null)
			{
				return null;
			}

			if(assetOwner == null)
			{
				assetOwner = hierarchyRoot;
			}

			string resolvedChildName = AssetPersistence.GetUniqueGeneratedMeshName(
				assetOwner,
				hierarchyRoot,
				childName,
				generationAttemptName);

			GameObject child = CreateColliderChild(
				hierarchyRoot,
				resolvedChildName);

			MeshCollider collider = child.AddComponent<MeshCollider>();
			ApplySharedSettings(collider, isTrigger, material);

			Mesh persistentMesh = AssetPersistence.MakeGeneratedMeshPersistent(
				assetOwner,
				mesh,
				resolvedChildName,
				generationAttemptName);

			collider.sharedMesh = persistentMesh;
			collider.convex = true;

			AddGeneratedColliderMarker(child, persistentMesh);

#if UNITY_EDITOR
			AssetPersistence.SaveGeneratedColliderOwnerIfNeeded(assetOwner);
#endif

			return collider;
		}

		private static GameObject CreateColliderChild(GameObject root, string childName)
		{
			GameObject child = new GameObject(childName);
#if UNITY_EDITOR
			UnityEditor.Undo.RegisterCreatedObjectUndo(child, "Create Collider Child");
#endif
			child.transform.SetParent(root.transform, false);
			return child;
		}

		private static ColliderEditorGeneratedCollider AddGeneratedColliderMarker(GameObject colliderObject, Mesh generatedMesh = null)
		{
			ColliderEditorGeneratedCollider generatedCollider = colliderObject.AddComponent<ColliderEditorGeneratedCollider>();
			generatedCollider.SetMesh(generatedMesh);
			return generatedCollider;
		}

		private static void AlignChildToWorldPose(Transform child, Transform root, Vector3 worldPosition, Quaternion worldRotation)
		{
			child.SetParent(root, false);
			child.position = worldPosition;
			child.rotation = worldRotation;
			child.localScale = Vector3.one;
		}

		private static void ApplySharedSettings(Collider collider, bool isTrigger, PhysicsMaterialType material)
		{
			collider.isTrigger = isTrigger;
			collider.sharedMaterial = material;
		}

	}
}
