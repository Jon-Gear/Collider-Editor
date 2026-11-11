using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;

#endif
#if(UNITY_6000_0_OR_NEWER)
#else
using PhysicsMaterialType = UnityEngine.PhysicMaterial;
#endif

namespace ColliderEditor
{
	/// <summary>
	/// Partial utility implementation for collider geometry creation.
	/// </summary>
	public static class Geometry
	{
		private const float MINIMUM_COLLIDER_VALUE = 0.001f;
		private const float MINIMUM_CAPSULE_RADIUS = 0.001f;
		private const float MINIMUM_CAPSULE_HEIGHT = 0.001f;
		private const int DEFAULT_CYLINDER_SEGMENTS = 16;

		public struct Matrix3x3
		{
			public float m00;
			public float m01;
			public float m02;
			public float m10;
			public float m11;
			public float m12;
			public float m20;
			public float m21;
			public float m22;

			public Vector3 Multiply(Vector3 vector)
			{
				return new Vector3(
					(m00 * vector.x) + (m01 * vector.y) + (m02 * vector.z),
					(m10 * vector.x) + (m11 * vector.y) + (m12 * vector.z),
					(m20 * vector.x) + (m21 * vector.y) + (m22 * vector.z));
			}

			public static Matrix3x3 operator *(Matrix3x3 matrix, float value)
			{
				matrix.m00 *= value;
				matrix.m01 *= value;
				matrix.m02 *= value;
				matrix.m10 *= value;
				matrix.m11 *= value;
				matrix.m12 *= value;
				matrix.m20 *= value;
				matrix.m21 *= value;
				matrix.m22 *= value;
				return matrix;
			}

		}

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

		public static Matrix4x4 CalculateBoxMatrixFromCollider(BoxCollider collider)
		{
			Matrix4x4 matrix = collider.transform.localToWorldMatrix *
				Matrix4x4.TRS(collider.center, Quaternion.identity, MaxVector(collider.size, Vector3.one * PreviewState.MINIMUM_PREVIEW_VALUE));

			return matrix;
		}

		public static Matrix4x4 CalculateSphereMatrixFromCollider(SphereCollider collider)
		{
			Vector3 worldCenter = collider.transform.TransformPoint(collider.center);
			float radius = Mathf.Max(PreviewState.MINIMUM_PREVIEW_VALUE, collider.radius * GetMaximumAbsComponent(collider.transform.lossyScale));
			Matrix4x4 matrix = Matrix4x4.TRS(worldCenter, collider.transform.rotation, Vector3.one * radius * 2.0f);

			return matrix;
		}

		public static Matrix4x4 CalculateAxisAlignedBoxMatrix(ColliderEditorState state, List<Vector3> worldPoints)
		{
			Bounds bounds = CreatePreviewLocalBounds(state, worldPoints);
			Vector3 clampedSize = MaxVector(bounds.size, Vector3.one * PreviewState.MINIMUM_PREVIEW_VALUE);
			Matrix4x4 matrix = state.SelectedObject.transform.localToWorldMatrix * Matrix4x4.TRS(bounds.center, Quaternion.identity, clampedSize);

			return matrix;
		}

		public static Matrix4x4 CalculateRotatedBoxMatrix(ColliderEditorState state, List<Vector3> worldPoints)
		{
			OrientedBoundsData orientedBounds = FitOrientedBounds(worldPoints);
			Vector3 clampedSize = MaxVector(orientedBounds.size, Vector3.one * PreviewState.MINIMUM_PREVIEW_VALUE);
			Matrix4x4 matrix = Matrix4x4.TRS(orientedBounds.center, orientedBounds.rotation, clampedSize);
			return matrix;
		}

		public static Matrix4x4 CalculateSphereMatrix(ColliderEditorState state, List<Vector3> worldPoints)
		{
			List<Vector3> localPoints = ToLocalPoints(state.SelectedObject.transform, worldPoints);
			SphereFitData sphereData = CalculateSphereFit(localPoints, state.SphereMethod);
			Matrix4x4 matrix = state.SelectedObject.transform.localToWorldMatrix * Matrix4x4.TRS(sphereData.center, Quaternion.identity, Vector3.one * Mathf.Max(sphereData.radius, PreviewState.MINIMUM_PREVIEW_VALUE) * 2.0f);
			return matrix;
		}

		public static (Matrix4x4 matrix, (float height, float radius)) CalculateCapsuleMatrixAndRadiusAndHeightAxisAligned(ColliderEditorState state, List<Vector3> worldPoints, int axis)
		{
			Bounds localBounds = CreatePreviewLocalBounds(state, worldPoints);
			int resolvedAxis = ResolvePreviewAxis(localBounds.size, axis);
			List<Vector3> localPoints = ToLocalPoints(state.SelectedObject.transform, worldPoints);
			CapsuleFitData capsuleData = CalculateCapsuleFit(localPoints, resolvedAxis, state.CapsuleMethod);

			Matrix4x4 matrix = state.SelectedObject.transform.localToWorldMatrix *
				Matrix4x4.TRS(capsuleData.center, GetAxisRotationFromYAxis(capsuleData.axis), Vector3.one);

			return (matrix, (capsuleData.height, capsuleData.radius));
		}

		public static (Matrix4x4 matrix, (float height, float radius)) CalculateCapsuleMatrixAndRadiusAndHeightRotated(ColliderEditorState state, List<Vector3> worldPoints, int axis)
		{
			OrientedBoundsData orientedBounds = FitOrientedBounds(worldPoints);
			List<Vector3> orientedLocalPoints = ToLocalPoints(orientedBounds.center, orientedBounds.rotation, worldPoints);
			CapsuleFitData capsuleData = CalculateCapsuleFit(orientedLocalPoints, axis, state.CapsuleMethod);

			Matrix4x4 matrix = Matrix4x4.TRS(
				orientedBounds.center,
				orientedBounds.rotation * GetAxisRotationFromYAxis(capsuleData.axis),
				Vector3.one);

			return (matrix, (capsuleData.height, capsuleData.radius));
		}

		public static (Matrix4x4 matrix, (float height, float radius)) CalculateCapsuleMatrixFromCollider(CapsuleCollider collider)
		{
			float radiusScale = GetCapsuleRadiusScale(collider);
			float heightScale = GetCapsuleHeightScale(collider);
			float radius = Mathf.Max(PreviewState.MINIMUM_PREVIEW_VALUE, collider.radius * radiusScale);
			float height = Mathf.Max(radius * 2.0f, collider.height * heightScale);

			Quaternion axisRotation = GetAxisRotationFromYAxis(collider.direction);

			Matrix4x4 matrix = Matrix4x4.TRS(
				collider.transform.TransformPoint(collider.center),
				collider.transform.rotation * axisRotation,
				Vector3.one
			);

			return (matrix, (height, radius));
		}

		public static float CalculateAxisAlignedCylinderRadius(
			ColliderEditorState state,
			List<Vector3> worldPoints,
			Vector3 localCenter,
			int axis)
		{
			float radius = PreviewState.MINIMUM_PREVIEW_VALUE;

			for(int index = 0; index < worldPoints.Count; index++)
			{
				Vector3 localPoint = state.SelectedObject.transform.InverseTransformPoint(worldPoints[index]) - localCenter;
				Vector2 radialPoint = Geometry.GetRadialPoint(localPoint, axis);
				radius = Mathf.Max(radius, radialPoint.magnitude);
			}

			return radius;
		}

		public static Mesh CreateCubeMesh()
		{
			Mesh mesh = new Mesh();

			mesh.vertices = new Vector3[]
			{
				new Vector3(-0.5f, -0.5f, -0.5f),
				new Vector3(0.5f, -0.5f, -0.5f),
				new Vector3(0.5f, 0.5f, -0.5f),
				new Vector3(-0.5f, 0.5f, -0.5f),
				new Vector3(-0.5f, -0.5f, 0.5f),
				new Vector3(0.5f, -0.5f, 0.5f),
				new Vector3(0.5f, 0.5f, 0.5f),
				new Vector3(-0.5f, 0.5f, 0.5f),
			};
			mesh.triangles = new int[]
			{
				0, 2, 1, 0, 3, 2,
				1, 2, 6, 1, 6, 5,
				5, 6, 7, 5, 7, 4,
				4, 7, 3, 4, 3, 0,
				3, 7, 6, 3, 6, 2,
				4, 0, 1, 4, 1, 5,
			};
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>
		/// Creates a sphere mesh with a configurable radius and segment count.
		/// </summary>
		public static Mesh CreateSphereMesh(float radius, int longitudeSegments, int latitudeSegments)
		{
			Mesh mesh = new Mesh();
			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();

			for(int latitude = 0; latitude <= latitudeSegments; latitude++)
			{
				float v = latitude / (float)latitudeSegments;
				float phi = Mathf.PI * v;
				float y = Mathf.Cos(phi) * radius;
				float ringRadius = Mathf.Sin(phi) * radius;

				for(int longitude = 0; longitude <= longitudeSegments; longitude++)
				{
					float u = longitude / (float)longitudeSegments;
					float theta = (Mathf.PI * 2.0f) * u;
					float x = Mathf.Cos(theta) * ringRadius;
					float z = Mathf.Sin(theta) * ringRadius;
					vertices.Add(new Vector3(x, y, z));
				}
			}

			int stride = longitudeSegments + 1;
			for(int latitude = 0; latitude < latitudeSegments; latitude++)
			{
				for(int longitude = 0; longitude < longitudeSegments; longitude++)
				{
					int current = (latitude * stride) + longitude;
					int next = current + stride;

					triangles.Add(current);
					triangles.Add(next);
					triangles.Add(current + 1);

					triangles.Add(current + 1);
					triangles.Add(next);
					triangles.Add(next + 1);
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>
		/// Creates a capsule mesh aligned to the Y axis in local space.
		/// </summary>
		public static Mesh CreateCapsuleMesh(float radius, float height)
		{
			float clampedRadius = Mathf.Max(PreviewState.MINIMUM_PREVIEW_VALUE, radius);
			float clampedHeight = Mathf.Max(PreviewState.MINIMUM_PREVIEW_VALUE, height);
			float cylinderHeight = Mathf.Max(0.0f, clampedHeight - (clampedRadius * 2.0f));

			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();

			AppendSphereSection(
				vertices,
				triangles,
				Vector3.up * (cylinderHeight * 0.5f),
				clampedRadius,
				0.0f,
				Mathf.PI * 0.5f,
				PreviewState.SPHERE_LONGITUDE_SEGMENTS,
				PreviewState.CAPSULE_HEMISPHERE_SEGMENTS);

			AppendSphereSection(
				vertices,
				triangles,
				Vector3.down * (cylinderHeight * 0.5f),
				clampedRadius,
				Mathf.PI * 0.5f,
				Mathf.PI,
				PreviewState.SPHERE_LONGITUDE_SEGMENTS,
				PreviewState.CAPSULE_HEMISPHERE_SEGMENTS);

			if(cylinderHeight > PreviewState.MINIMUM_PREVIEW_VALUE)
			{
				AppendCylinderSection(vertices, triangles, clampedRadius, cylinderHeight, PreviewState.SPHERE_LONGITUDE_SEGMENTS);
			}

			Mesh mesh = new Mesh();
			mesh.name = "ColliderEditor_CapsulePreview";
			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		public static Mesh CreateCylinderMesh(Vector3 center, float radius, float height, int axis, int cylinderSegments)
		{
			int segmentCount = Mathf.Max(3, cylinderSegments);
			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();
			float halfHeight = height * 0.5f;

			for(int i = 0; i < segmentCount; i++)
			{
				float angle = (Mathf.PI * 2.0f * i) / segmentCount;
				float x = Mathf.Cos(angle) * radius;
				float y = Mathf.Sin(angle) * radius;

				Vector3 bottom;
				Vector3 top;

				if(axis == 0)
				{
					bottom = center + new Vector3(-halfHeight, x, y);
					top = center + new Vector3(halfHeight, x, y);
				}
				else if(axis == 1)
				{
					bottom = center + new Vector3(x, -halfHeight, y);
					top = center + new Vector3(x, halfHeight, y);
				}
				else
				{
					bottom = center + new Vector3(x, y, -halfHeight);
					top = center + new Vector3(x, y, halfHeight);
				}

				vertices.Add(bottom);
				vertices.Add(top);
			}

			int bottomCenterIndex = vertices.Count;
			int topCenterIndex = vertices.Count + 1;

			if(axis == 0)
			{
				vertices.Add(center + new Vector3(-halfHeight, 0.0f, 0.0f));
				vertices.Add(center + new Vector3(halfHeight, 0.0f, 0.0f));
			}
			else if(axis == 1)
			{
				vertices.Add(center + new Vector3(0.0f, -halfHeight, 0.0f));
				vertices.Add(center + new Vector3(0.0f, halfHeight, 0.0f));
			}
			else
			{
				vertices.Add(center + new Vector3(0.0f, 0.0f, -halfHeight));
				vertices.Add(center + new Vector3(0.0f, 0.0f, halfHeight));
			}

			for(int i = 0; i < segmentCount; i++)
			{
				int next = (i + 1) % segmentCount;
				int bottomA = i * 2;
				int topA = bottomA + 1;
				int bottomB = next * 2;
				int topB = bottomB + 1;

				triangles.Add(bottomA);
				triangles.Add(topA);
				triangles.Add(topB);

				triangles.Add(bottomA);
				triangles.Add(topB);
				triangles.Add(bottomB);

				triangles.Add(bottomCenterIndex);
				triangles.Add(bottomA);
				triangles.Add(bottomB);

				triangles.Add(topCenterIndex);
				triangles.Add(topB);
				triangles.Add(topA);
			}

			Mesh mesh = new Mesh();
			mesh.name = "CylinderColliderMesh";
			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			mesh.RecalculateNormals();
			return mesh;
		}

		public static Mesh CreateOctahedronMesh()
		{

			Mesh mesh = new Mesh();
			mesh.vertices = new Vector3[]
			{
				new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 1.0f),
				new Vector3(-1.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 0.0f, -1.0f),
				new Vector3(0.0f, -1.0f, 0.0f),
			};
			mesh.triangles = new int[]
			{
				0, 1, 2,
				0, 2, 3,
				0, 3, 4,
				0, 4, 1,
				5, 2, 1,
				5, 3, 2,
				5, 4, 3,
				5, 1, 4,
			};
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>
		/// Appends a hemisphere section to the in-progress capsule mesh.
		/// </summary>
		private static void AppendSphereSection(
			List<Vector3> vertices,
			List<int> triangles,
			Vector3 center,
			float radius,
			float startPhi,
			float endPhi,
			int longitudeSegments,
			int latitudeSegments)
		{
			int vertexStart = vertices.Count;

			for(int latitude = 0; latitude <= latitudeSegments; latitude++)
			{
				float t = latitude / (float)latitudeSegments;
				float phi = Mathf.Lerp(startPhi, endPhi, t);
				float y = Mathf.Cos(phi) * radius;
				float ringRadius = Mathf.Sin(phi) * radius;

				for(int longitude = 0; longitude <= longitudeSegments; longitude++)
				{
					float u = longitude / (float)longitudeSegments;
					float theta = (Mathf.PI * 2.0f) * u;
					float x = Mathf.Cos(theta) * ringRadius;
					float z = Mathf.Sin(theta) * ringRadius;
					vertices.Add(center + new Vector3(x, y, z));
				}
			}

			int stride = longitudeSegments + 1;
			for(int latitude = 0; latitude < latitudeSegments; latitude++)
			{
				for(int longitude = 0; longitude < longitudeSegments; longitude++)
				{
					int current = vertexStart + (latitude * stride) + longitude;
					int next = current + stride;

					triangles.Add(current);
					triangles.Add(next);
					triangles.Add(current + 1);

					triangles.Add(current + 1);
					triangles.Add(next);
					triangles.Add(next + 1);
				}
			}
		}

		/// <summary>
		/// Appends the cylindrical middle section to the in-progress capsule mesh.
		/// </summary>
		private static void AppendCylinderSection(
			List<Vector3> vertices,
			List<int> triangles,
			float radius,
			float height,
			int radialSegments)
		{
			int vertexStart = vertices.Count;
			float halfHeight = height * 0.5f;

			for(int yIndex = 0; yIndex <= 1; yIndex++)
			{
				float y = (yIndex == 0) ? -halfHeight : halfHeight;

				for(int radialIndex = 0; radialIndex <= radialSegments; radialIndex++)
				{
					float t = radialIndex / (float)radialSegments;
					float angle = (Mathf.PI * 2.0f) * t;
					float x = Mathf.Cos(angle) * radius;
					float z = Mathf.Sin(angle) * radius;
					vertices.Add(new Vector3(x, y, z));
				}
			}

			int stride = radialSegments + 1;
			for(int radialIndex = 0; radialIndex < radialSegments; radialIndex++)
			{
				int currentBottom = vertexStart + radialIndex;
				int nextBottom = currentBottom + 1;
				int currentTop = vertexStart + stride + radialIndex;
				int nextTop = currentTop + 1;

				triangles.Add(currentBottom);
				triangles.Add(currentTop);
				triangles.Add(nextBottom);

				triangles.Add(nextBottom);
				triangles.Add(currentTop);
				triangles.Add(nextTop);
			}
		}

		/// <summary>
		/// Resolves the active axis. When the axis is automatic, the longest dimension wins.
		/// </summary>
		public static int ResolvePreviewAxis(Vector3 size, int axis)
		{
			if(axis >= 0)
			{
				return Mathf.Clamp(axis, 0, 2);
			}

			int resolvedAxis = 0;
			float longestAxis = size.x;

			if(size.y > longestAxis)
			{
				resolvedAxis = 1;
				longestAxis = size.y;
			}

			if(size.z > longestAxis)
			{
				resolvedAxis = 2;
			}

			return resolvedAxis;
		}

		/// <summary>
		/// Returns the 2D radial point for the chosen axis.
		/// </summary>
		public static Vector2 GetRadialPoint(Vector3 point, int axis)
		{
			if(axis == 0)
			{
				return new Vector2(point.y, point.z);
			}

			if(axis == 1)
			{
				return new Vector2(point.x, point.z);
			}

			return new Vector2(point.x, point.y);
		}

		/// <summary>
		/// Builds the rotation that maps a Y-aligned primitive to the requested axis.
		/// </summary>
		public static Quaternion GetAxisRotationFromYAxis(int axis)
		{
			return axis switch
			{
				0 => Quaternion.Euler(0.0f, 0.0f, -90.0f),
				2 => Quaternion.Euler(90.0f, 0.0f, 0.0f),
				_ => Quaternion.identity,
			};
		}

		public static bool IsValidTriangleIndex(int firstIndex, int secondIndex, int thirdIndex, int vertexCount)
		{
			return IsValidVertexIndex(firstIndex, vertexCount) &&
				IsValidVertexIndex(secondIndex, vertexCount) &&
				IsValidVertexIndex(thirdIndex, vertexCount);
		}

		public static bool IsValidVertexIndex(int vertexIndex, int vertexCount)
		{
			return vertexIndex >= 0 && vertexIndex < vertexCount;
		}

		public static int CalculateVertexListSignature(List<ColliderEditorState.SelectedVertex> vertices)
		{
			unchecked
			{
				int hash = 17;
				hash = (hash * 31) + (vertices != null ? vertices.Count : 0);

				if(vertices == null)
				{
					return hash;
				}

				for(int index = 0; index < vertices.Count; index++)
				{
					hash = (hash * 31) + vertices[index].Key.GetHashCode();
				}

				return hash;
			}
		}

		private static float GetCapsuleRadiusScale(CapsuleCollider collider)
		{
			Vector3 scale = AbsVector(collider.transform.lossyScale);

			switch(collider.direction)
			{
				case 0:
					return Mathf.Max(scale.y, scale.z);

				case 2:
					return Mathf.Max(scale.x, scale.y);

				default:
					return Mathf.Max(scale.x, scale.z);
			}
		}

		private static float GetCapsuleHeightScale(CapsuleCollider collider)
		{
			Vector3 scale = AbsVector(collider.transform.lossyScale);

			switch(collider.direction)
			{
				case 0:
					return scale.x;

				case 2:
					return scale.z;

				default:
					return scale.y;
			}
		}

		public static float GetMaximumAbsComponent(Vector3 vector)
		{
			vector = AbsVector(vector);
			return Mathf.Max(vector.x, Mathf.Max(vector.y, vector.z));
		}

		private static Vector3 AbsVector(Vector3 vector)
		{
			return new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
		}

		public static Bounds CreatePreviewLocalBounds(ColliderEditorState state, List<Vector3> worldPoints)
		{
			Vector3 firstPoint = state.SelectedObject.transform.InverseTransformPoint(worldPoints[0]);
			Bounds bounds = new Bounds(firstPoint, Vector3.zero);

			for(int index = 1; index < worldPoints.Count; index++)
			{
				bounds.Encapsulate(state.SelectedObject.transform.InverseTransformPoint(worldPoints[index]));
			}

			return bounds;
		}

		public static OrientedBoundsData FitOrientedBounds(List<Vector3> worldPoints)
		{
			Vector3 centroid = CalculateAveragePoint(worldPoints);
			Matrix3x3 covariance = CalculateCovariance(worldPoints, centroid);

			Vector3 axisX = SolveDominantEigenVector(covariance, Vector3.right);
			Vector3 axisY = SolveSecondaryEigenVector(covariance, axisX, Vector3.up);
			Vector3 axisZ = Vector3.Cross(axisX, axisY).normalized;
			axisY = Vector3.Cross(axisZ, axisX).normalized;

			if(axisZ.sqrMagnitude <= 0.000001f)
			{
				axisZ = BuildPerpendicular(axisX);
				axisY = Vector3.Cross(axisZ, axisX).normalized;
			}

			Quaternion rotation = Quaternion.LookRotation(axisZ, axisY);
			Quaternion inverseRotation = Quaternion.Inverse(rotation);

			Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

			for(int index = 0; index < worldPoints.Count; index++)
			{
				Vector3 localPoint = inverseRotation * (worldPoints[index] - centroid);
				min = Vector3.Min(min, localPoint);
				max = Vector3.Max(max, localPoint);
			}

			Vector3 localCenter = (min + max) * 0.5f;
			OrientedBoundsData data = new OrientedBoundsData();
			data.center = centroid + (rotation * localCenter);
			data.size = MaxVector(max - min, Vector3.one * MINIMUM_COLLIDER_VALUE);
			data.rotation = rotation;
			return data;
		}

		/// <summary>
		/// Calculates sphere fit data in local space based on the requested method.
		/// </summary>
		public static SphereFitData CalculateSphereFit(List<Vector3> localPoints, SPHERE_COLLIDER_METHOD method)
		{
			switch(method)
			{
				case SPHERE_COLLIDER_METHOD.BestFit:
					return CalculateSphereBestFit(localPoints);

				case SPHERE_COLLIDER_METHOD.Distance:
					return CalculateSphereDistance(localPoints);

				default:
					return CalculateSphereMinMax(localPoints);
			}
		}

		/// <summary>
		/// Calculates capsule fit data in local space based on the requested method.
		/// </summary>
		public static CapsuleFitData CalculateCapsuleFit(List<Vector3> localPoints, int axis, CAPSULE_COLLIDER_METHOD method)
		{
			if(method == CAPSULE_COLLIDER_METHOD.BestFit)
			{
				return CalculateCapsuleBestFit(localPoints, axis);
			}

			return CalculateCapsuleMinMax(localPoints, axis, method);
		}

		public static Bounds CreateLocalBounds(Transform root, List<Vector3> worldPoints)
		{
			Vector3 firstPoint = root.InverseTransformPoint(worldPoints[0]);
			Bounds bounds = new Bounds(firstPoint, Vector3.zero);

			int count = worldPoints.Count;
			for(int i = 1; i < count; i++)
			{
				bounds.Encapsulate(root.InverseTransformPoint(worldPoints[i]));
			}

			return bounds;
		}

		public static List<Vector3> ToLocalPoints(Transform root, List<Vector3> worldPoints)
		{
			List<Vector3> localPoints = new List<Vector3>(worldPoints.Count);

			for(int index = 0; index < worldPoints.Count; index++)
			{
				localPoints.Add(root.InverseTransformPoint(worldPoints[index]));
			}

			return localPoints;
		}

		public static List<Vector3> ToLocalPoints(Vector3 center, Quaternion rotation, List<Vector3> worldPoints)
		{
			List<Vector3> localPoints = new List<Vector3>(worldPoints.Count);
			Quaternion inverseRotation = Quaternion.Inverse(rotation);

			for(int index = 0; index < worldPoints.Count; index++)
			{
				localPoints.Add(inverseRotation * (worldPoints[index] - center));
			}

			return localPoints;
		}

		/// <summary>
		/// Returns the component-wise maximum of two vectors.
		/// </summary>
		public static Vector3 MaxVector(Vector3 source, Vector3 minimum)
		{
			return new Vector3(
				Mathf.Max(source.x, minimum.x),
				Mathf.Max(source.y, minimum.y),
				Mathf.Max(source.z, minimum.z));
		}

		public static int ResolveAxis(Vector3 size, int axis)
		{
			if(axis >= 0)
			{
				return Mathf.Clamp(axis, 0, 2);
			}

			int resolvedAxis = 0;
			float longestAxis = size.x;

			if(size.y > longestAxis)
			{
				resolvedAxis = 1;
				longestAxis = size.y;
			}

			if(size.z > longestAxis)
			{
				resolvedAxis = 2;
			}

			return resolvedAxis;
		}

		public static float GetAxisValue(Vector3 size, int axis)
		{
			if(axis == 0)
			{
				return size.x;
			}

			if(axis == 1)
			{
				return size.y;
			}

			return size.z;
		}

		private static SphereFitData CalculateSphereBestFit(List<Vector3> localPoints)
		{
			BestFitSphere bestFitSphere = CalculateBestFitSphere(localPoints);
			SphereFitData data = new SphereFitData();
			data.center = bestFitSphere.center;
			data.radius = Mathf.Max(MINIMUM_COLLIDER_VALUE, bestFitSphere.radius);
			return data;
		}

		private static SphereFitData CalculateSphereDistance(List<Vector3> localPoints)
		{
			bool switchToFasterAlgorithm = false;
#if UNITY_EDITOR
			double startTime = UnityEditor.EditorApplication.timeSinceStartup;
#else
			float startTime = Time.realtimeSinceStartup;
#endif
			const double MAX_TIME = 0.1;

			Vector3 distanceVertexA = Vector3.zero;
			Vector3 distanceVertexB = Vector3.zero;
			float maxDistance = float.NegativeInfinity;

			for(int i = 0; i < localPoints.Count; i++)
			{
				for(int j = i + 1; j < localPoints.Count; j++)
				{
					float distance = Vector3.Distance(localPoints[i], localPoints[j]);
					if(distance > maxDistance)
					{
						maxDistance = distance;
						distanceVertexA = localPoints[i];
						distanceVertexB = localPoints[j];
					}
				}

#if UNITY_EDITOR
				if(UnityEditor.EditorApplication.timeSinceStartup - startTime > MAX_TIME)
#else
				if (Time.realtimeSinceStartup - startTime > MAX_TIME)
#endif
				{
					switchToFasterAlgorithm = true;
					break;
				}
			}

			if(switchToFasterAlgorithm)
			{
				Vector3 mean = CalculateAveragePoint(localPoints);

				for(int index = 0; index < localPoints.Count; index++)
				{
					float distance = Vector3.Distance(localPoints[index], mean);
					if(distance > maxDistance)
					{
						maxDistance = distance;
						distanceVertexA = localPoints[index];
					}
				}

				maxDistance = float.NegativeInfinity;
				for(int index = 0; index < localPoints.Count; index++)
				{
					float distance = Vector3.Distance(localPoints[index], distanceVertexA);
					if(distance > maxDistance)
					{
						maxDistance = distance;
						distanceVertexB = localPoints[index];
					}
				}
			}

			SphereFitData data = new SphereFitData();
			data.center = (distanceVertexA + distanceVertexB) * 0.5f;
			data.radius = Mathf.Max(MINIMUM_COLLIDER_VALUE, maxDistance * 0.5f);
			return data;
		}

		private static SphereFitData CalculateSphereMinMax(List<Vector3> localPoints)
		{
			Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

			for(int index = 0; index < localPoints.Count; index++)
			{
				min = Vector3.Min(min, localPoints[index]);
				max = Vector3.Max(max, localPoints[index]);
			}

			Vector3 center = (min + max) * 0.5f;
			float maxDistance = 0.0f;

			for(int index = 0; index < localPoints.Count; index++)
			{
				maxDistance = Mathf.Max(maxDistance, Vector3.Distance(localPoints[index], center));
			}

			SphereFitData data = new SphereFitData();
			data.center = center;
			data.radius = Mathf.Max(MINIMUM_COLLIDER_VALUE, maxDistance);
			return data;
		}

		private static CapsuleFitData CalculateCapsuleBestFit(List<Vector3> localPoints, int axis)
		{
			Bounds bounds = CreateBoundsFromLocalPoints(localPoints);
			int resolvedAxis = ResolveAxis(bounds.size, axis);
			Vector3 center = bounds.center;
			float axisMin = float.PositiveInfinity;
			float axisMax = float.NegativeInfinity;
			float maxRadius = 0.0f;

			for(int index = 0; index < localPoints.Count; index++)
			{
				Vector3 point = localPoints[index];
				float axisValue = GetAxisComponent(point, resolvedAxis);
				axisMin = Mathf.Min(axisMin, axisValue);
				axisMax = Mathf.Max(axisMax, axisValue);

				Vector3 projectedPoint = point;
				SetAxisComponent(ref projectedPoint, resolvedAxis, GetAxisComponent(center, resolvedAxis));
				maxRadius = Mathf.Max(maxRadius, Vector3.Distance(projectedPoint, center));
			}

			CapsuleFitData data = new CapsuleFitData();
			data.axis = resolvedAxis;
			data.center = center;
			data.radius = Mathf.Max(MINIMUM_CAPSULE_RADIUS, maxRadius);
			data.height = Mathf.Max(MINIMUM_CAPSULE_HEIGHT, axisMax - axisMin);
			data.height = Mathf.Max(data.height, data.radius * 2.0f);
			return data;
		}

		private static CapsuleFitData CalculateCapsuleMinMax(List<Vector3> localPoints, int axis, CAPSULE_COLLIDER_METHOD method)
		{
			Bounds bounds = CreateBoundsFromLocalPoints(localPoints);
			int resolvedAxis = ResolveAxis(bounds.size, axis);
			Vector3 center = bounds.center;
			float height = GetAxisValue(bounds.size, resolvedAxis);
			float maxRadius = 0.0f;

			for(int index = 0; index < localPoints.Count; index++)
			{
				Vector3 point = localPoints[index];
				Vector3 flattenedPoint = point;
				SetAxisComponent(ref flattenedPoint, resolvedAxis, GetAxisComponent(center, resolvedAxis));
				maxRadius = Mathf.Max(maxRadius, Vector3.Distance(flattenedPoint, center));
			}

			if(method == CAPSULE_COLLIDER_METHOD.MinMaxPlusRadius)
			{
				height += maxRadius;
			}
			else if(method == CAPSULE_COLLIDER_METHOD.MinMaxPlusDiameter)
			{
				height += maxRadius * 2.0f;
			}

			CapsuleFitData data = new CapsuleFitData();
			data.axis = resolvedAxis;
			data.center = center;
			data.radius = Mathf.Max(MINIMUM_CAPSULE_RADIUS, maxRadius);
			data.height = Mathf.Max(MINIMUM_CAPSULE_HEIGHT, height);
			data.height = Mathf.Max(data.height, data.radius * 2.0f);
			return data;
		}

		private static Bounds CreateBoundsFromLocalPoints(List<Vector3> localPoints)
		{
			Bounds bounds = new Bounds(localPoints[0], Vector3.zero);

			for(int index = 1; index < localPoints.Count; index++)
			{
				bounds.Encapsulate(localPoints[index]);
			}

			return bounds;
		}

		private static float GetAxisComponent(Vector3 vector, int axis)
		{
			if(axis == 0)
			{
				return vector.x;
			}

			if(axis == 1)
			{
				return vector.y;
			}

			return vector.z;
		}

		private static void SetAxisComponent(ref Vector3 vector, int axis, float value)
		{
			if(axis == 0)
			{
				vector.x = value;
				return;
			}

			if(axis == 1)
			{
				vector.y = value;
				return;
			}

			vector.z = value;
		}

		public static Vector3 CalculateAveragePoint(List<Vector3> points)
		{
			Vector3 sum = Vector3.zero;

			for(int index = 0; index < points.Count; index++)
			{
				sum += points[index];
			}

			return sum / points.Count;
		}

		private static BestFitSphere CalculateBestFitSphere(List<Vector3> localVertices)
		{
			int count = localVertices.Count;
			float xAverage = 0.0f;
			float yAverage = 0.0f;
			float zAverage = 0.0f;

			for(int index = 0; index < count; index++)
			{
				xAverage += localVertices[index].x;
				yAverage += localVertices[index].y;
				zAverage += localVertices[index].z;
			}

			xAverage /= count;
			yAverage /= count;
			zAverage /= count;

			Vector3 vectorB = Vector3.zero;
			Matrix4x4 matrixA = Matrix4x4.zero;
			matrixA.m33 = 1.0f;

			for(int index = 0; index < count; index++)
			{
				Vector3 vertex = localVertices[index];
				float squaredX = vertex.x * vertex.x;
				float squaredY = vertex.y * vertex.y;
				float squaredZ = vertex.z * vertex.z;

				matrixA[0, 0] += 2.0f * (vertex.x * (vertex.x - xAverage)) / count;
				matrixA[0, 1] += 2.0f * (vertex.x * (vertex.y - yAverage)) / count;
				matrixA[0, 2] += 2.0f * (vertex.x * (vertex.z - zAverage)) / count;
				matrixA[1, 0] += 2.0f * (vertex.y * (vertex.x - xAverage)) / count;
				matrixA[1, 1] += 2.0f * (vertex.y * (vertex.y - yAverage)) / count;
				matrixA[1, 2] += 2.0f * (vertex.y * (vertex.z - zAverage)) / count;
				matrixA[2, 0] += 2.0f * (vertex.z * (vertex.x - xAverage)) / count;
				matrixA[2, 1] += 2.0f * (vertex.z * (vertex.y - yAverage)) / count;
				matrixA[2, 2] += 2.0f * (vertex.z * (vertex.z - zAverage)) / count;

				float squaredLength = squaredX + squaredY + squaredZ;
				vectorB.x += squaredLength * (vertex.x - xAverage) / count;
				vectorB.y += squaredLength * (vertex.y - yAverage) / count;
				vectorB.z += squaredLength * (vertex.z - zAverage) / count;
			}

			Vector3 center = (matrixA.transpose * matrixA).inverse * matrixA.transpose * vectorB;
			float radius = 0.0f;

			for(int index = 0; index < count; index++)
			{
				radius += (localVertices[index] - center).sqrMagnitude;
			}

			BestFitSphere result = new BestFitSphere();
			result.center = center;
			result.radius = Mathf.Sqrt(radius / count);
			return result;
		}

		private struct BestFitSphere
		{
			public Vector3 center;
			public float radius;
		}

		private static Matrix3x3 CalculateCovariance(List<Vector3> points, Vector3 centroid)
		{
			Matrix3x3 matrix = new Matrix3x3();

			for(int index = 0; index < points.Count; index++)
			{
				Vector3 offset = points[index] - centroid;
				matrix.m00 += offset.x * offset.x;
				matrix.m01 += offset.x * offset.y;
				matrix.m02 += offset.x * offset.z;
				matrix.m10 += offset.y * offset.x;
				matrix.m11 += offset.y * offset.y;
				matrix.m12 += offset.y * offset.z;
				matrix.m20 += offset.z * offset.x;
				matrix.m21 += offset.z * offset.y;
				matrix.m22 += offset.z * offset.z;
			}

			float inverseCount = 1.0f / Mathf.Max(1, points.Count);
			matrix *= inverseCount;
			return matrix;
		}

		private static Vector3 SolveDominantEigenVector(Matrix3x3 matrix, Vector3 seed)
		{
			Vector3 vector = seed.normalized;

			for(int iteration = 0; iteration < 12; iteration++)
			{
				vector = matrix.Multiply(vector);
				if(vector.sqrMagnitude <= 0.000001f)
				{
					return Vector3.right;
				}

				vector.Normalize();
			}

			return vector;
		}

		private static Vector3 SolveSecondaryEigenVector(Matrix3x3 matrix, Vector3 primaryAxis, Vector3 seed)
		{
			Vector3 vector = (seed - (Vector3.Dot(seed, primaryAxis) * primaryAxis)).normalized;
			if(vector.sqrMagnitude <= 0.000001f)
			{
				vector = BuildPerpendicular(primaryAxis);
			}

			for(int iteration = 0; iteration < 12; iteration++)
			{
				vector = matrix.Multiply(vector);
				vector -= Vector3.Dot(vector, primaryAxis) * primaryAxis;

				if(vector.sqrMagnitude <= 0.000001f)
				{
					vector = BuildPerpendicular(primaryAxis);
					break;
				}

				vector.Normalize();
			}

			return vector.normalized;
		}

		private static Vector3 BuildPerpendicular(Vector3 axis)
		{
			Vector3 candidate = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
			Vector3 perpendicular = Vector3.Cross(axis, candidate).normalized;

			if(perpendicular.sqrMagnitude <= 0.000001f)
			{
				perpendicular = Vector3.forward;
			}

			return perpendicular;
		}

	}
}
