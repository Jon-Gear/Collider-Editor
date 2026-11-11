using System;
using System.Collections.Generic;
using UnityEngine;
#if (UNITY_6000_0_OR_NEWER)
using PhysicsMaterialType = UnityEngine.PhysicsMaterial;
#else
using PhysicsMaterialType = UnityEngine.PhysicMaterial;
#endif

namespace ColliderEditor
{
	/// <summary>
	/// Stores the editor window state for the collider creation workflow.
	/// This is kept in a ScriptableObject so the window can safely preserve UI state
	/// while the user iterates on vertex selection and collider creation.
	/// </summary>
	[Serializable]
	public sealed partial class ColliderEditorState : ScriptableObject
	{

		public const float MIN_SELECTION_RECT_SIZE = 2.0f;
		public const float CLICK_SELECTION_RADIUS = 18.0f;
		public const float VISIBLE_SELECTION_OCCLUSION_EPSILON = 0.003f;
		public const float MIN_VISIBLE_SELECTION_EPSILON = 0.0005f;

		public static readonly ColliderEditorState.PrimitiveType[] PRIMITIVE_ORDER =
		{
			ColliderEditorState.PrimitiveType.Sphere,
			ColliderEditorState.PrimitiveType.Capsule,
			ColliderEditorState.PrimitiveType.Box,
			ColliderEditorState.PrimitiveType.Cylinder,
			ColliderEditorState.PrimitiveType.ConvexHull,
			ColliderEditorState.PrimitiveType.ConvexDecomposition,
		};

		public static readonly GUIContent[] SPHERE_METHOD_LABELS =
		{
			new GUIContent("Tightest Radius", "Finds the smallest practical radius around the selected vertices. Use this when you want the sphere to hug the selection as closely as possible."),
			new GUIContent("Center Average + Farthest Point", "Places the center at the average selected position, then expands to the farthest vertex. Useful for organic selections where the midpoint should follow the point cloud."),
			new GUIContent("Bounds Center", "Uses the center of the selection bounds and expands to cover the minimum and maximum selected positions. Predictable and stable for box-like selections."),
		};

		public static readonly SPHERE_COLLIDER_METHOD[] SPHERE_METHOD_VALUES =
		{
			SPHERE_COLLIDER_METHOD.BestFit,
			SPHERE_COLLIDER_METHOD.Distance,
			SPHERE_COLLIDER_METHOD.MinMax,
		};

		public static readonly GUIContent[] CAPSULE_METHOD_LABELS =
		{
			new GUIContent("Tightest Radius", "Fits the capsule around the selected vertices with the tightest practical radius. This is usually the best visual match."),
			new GUIContent("Bounds Endpoints", "Uses the minimum and maximum selected positions along the capsule axis. Stable and predictable for straight selections."),
			new GUIContent("Bounds + Radius", "Extends the height by one radius. Use this when end caps should include a little extra coverage."),
			new GUIContent("Bounds + Diameter", "Extends the height by a full diameter. Use this when the selection needs more forgiving end coverage."),
		};

		public static readonly CAPSULE_COLLIDER_METHOD[] CAPSULE_METHOD_VALUES =
		{
			CAPSULE_COLLIDER_METHOD.BestFit,
			CAPSULE_COLLIDER_METHOD.MinMax,
			CAPSULE_COLLIDER_METHOD.MinMaxPlusRadius,
			CAPSULE_COLLIDER_METHOD.MinMaxPlusDiameter,
		};

		/// ===========

		///====

		public const float DEFAULT_VERTEX_DRAW_SIZE = 0.001f;
		public const float DEFAULT_SELECTED_VERTEX_DRAW_SIZE = 0.001f;

		[SerializeField]
		private GameObject selectedObject;

		[SerializeField]
		private bool isPickingObject = false;

		[SerializeField]
		private bool isInVertexSelectionMode = false;

		[SerializeField]
		private int objectPickerControlID;

		[SerializeField]
		private List<SelectedVertex> selectedVertices = new List<SelectedVertex>();

		[SerializeField]
		private List<SelectedVertex> hiddenVertices = new List<SelectedVertex>();

		[SerializeField]
		private bool displayVertices = true;

		[SerializeField]
		private bool includeChildren = true;

		[SerializeField]
		private bool throughSelect = true;

		[SerializeField]
		private bool isTrigger = false;

		[SerializeField]
		private float vertexDrawSize = DEFAULT_VERTEX_DRAW_SIZE;

		[SerializeField]
		private float selectedVertexDrawSize = DEFAULT_SELECTED_VERTEX_DRAW_SIZE;

		[SerializeField]
		private Color vertexColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);

		[SerializeField]
		private Color selectedVertexColor = new Color(1.0f, 0.55f, 0.0f, 1.0f);

		[SerializeField]
		private PhysicsMaterialType physicMaterial;

		[SerializeField]
		private CoacdSettings coacdSettings = CoacdSettings.Default();

		[SerializeField]
		private PrimitiveType selectedPrimitiveType = PrimitiveType.None;

		[SerializeField]
		private bool rotateBox = false;

		[SerializeField]
		private bool rotateCapsule = false;

		[SerializeField]
		private PrimitiveAxis capsuleAxis = PrimitiveAxis.Automatic;

		[SerializeField]
		private PrimitiveAxis cylinderAxis = PrimitiveAxis.Automatic;

		[SerializeField]
		private SPHERE_COLLIDER_METHOD sphereMethod = SPHERE_COLLIDER_METHOD.MinMax;

		[SerializeField]
		private CAPSULE_COLLIDER_METHOD capsuleMethod = CAPSULE_COLLIDER_METHOD.MinMax;

		[SerializeField]
		private int cylinderSides = 16;

		[SerializeField]
		private bool previewSettingsExpanded;

		[SerializeField]
		private bool colliderSettingsExpanded;

		[SerializeField]
		private bool vertexSelectionSettingsExpanded;

		[SerializeField]
		private bool vertexPreviewSettingsExpanded;

		[SerializeField]
		private bool drawPrimitivePreview = true;

		[SerializeField]
		private bool drawPrimitivePreviewAsWireframe = false;

		[SerializeField]
		private bool colorPrimitivePreviewByPerformance = true;

		[SerializeField]
		private Color primitivePreviewColor = new Color(1.0f, 0.55f, 0.0f, 0.35f);

		[SerializeField]
		private bool coacdAdvancedExpanded;

		[SerializeField]
		private bool existingColliderListExpanded = true;

		[SerializeField]
		private bool drawExistingColliderPerformanceView = true;

		[SerializeField]
		private bool drawExistingColliderPreviewAsWireframe = false;

		[SerializeField]
		private List<int> hiddenExistingColliderInstanceIds = new List<int>();

		[SerializeField]
		private Vector2 generatedCollidersScrollPosition = Vector2.zero;

		[SerializeField]
		private Vector2 windowScrollPosition = Vector2.zero;

		public GameObject SelectedObject
		{
			get { return selectedObject; }
			set { selectedObject = value; }
		}

		public bool IsPickingObject
		{
			get { return isPickingObject; }
			set { isPickingObject = value; }
		}

		public bool IsInVertexSelectionMode
		{
			get { return isInVertexSelectionMode; }
			set { isInVertexSelectionMode = value; }
		}

		public int ObjectPickerControlID
		{
			get { return objectPickerControlID; }
			set { objectPickerControlID = value; }
		}

		public List<SelectedVertex> SelectedVertices
		{
			get { return selectedVertices; }
		}

		public List<SelectedVertex> HiddenVertices
		{
			get { return hiddenVertices; }
		}

		public bool DisplayVertices
		{
			get { return displayVertices; }
			set { displayVertices = value; }
		}

		public bool IncludeChildren
		{
			get { return includeChildren; }
			set { includeChildren = value; }
		}

		public bool ThroughSelect
		{
			get { return throughSelect; }
			set { throughSelect = value; }
		}

		public bool IsTrigger
		{
			get { return isTrigger; }
			set { isTrigger = value; }
		}

		public float VertexDrawSize
		{
			get { return vertexDrawSize; }
			set { vertexDrawSize = Mathf.Max(0.001f, value); }
		}

		public float SelectedVertexDrawSize
		{
			get { return selectedVertexDrawSize; }
			set { selectedVertexDrawSize = Mathf.Max(0.001f, value); }
		}

		public Color VertexColor
		{
			get { return vertexColor; }
			set { vertexColor = value; }
		}

		public Color SelectedVertexColor
		{
			get { return selectedVertexColor; }
			set { selectedVertexColor = value; }
		}

		public PhysicsMaterialType PhysicMaterial
		{
			get { return physicMaterial; }
			set { physicMaterial = value; }
		}

		public CoacdSettings CurrentCoacdSettings
		{
			get { return coacdSettings; }
			set { coacdSettings = value; }
		}

		public PrimitiveType SelectedPrimitiveType
		{
			get { return selectedPrimitiveType; }
			set { selectedPrimitiveType = value; }
		}

		public bool RotateBox
		{
			get { return rotateBox; }
			set { rotateBox = value; }
		}

		public bool RotateCapsule
		{
			get { return rotateCapsule; }
			set { rotateCapsule = value; }
		}

		public PrimitiveAxis CapsuleAxis
		{
			get { return capsuleAxis; }
			set { capsuleAxis = value; }
		}

		public PrimitiveAxis CylinderAxis
		{
			get { return cylinderAxis; }
			set { cylinderAxis = value; }
		}

		public SPHERE_COLLIDER_METHOD SphereMethod
		{
			get { return sphereMethod; }
			set { sphereMethod = value; }
		}

		public CAPSULE_COLLIDER_METHOD CapsuleMethod
		{
			get { return capsuleMethod; }
			set { capsuleMethod = value; }
		}

		public int CylinderSides
		{
			get { return cylinderSides; }
			set { cylinderSides = Mathf.Clamp(value, 3, 64); }
		}

		public bool PreviewSettingsExpanded
		{
			get { return previewSettingsExpanded; }
			set { previewSettingsExpanded = value; }
		}

		public bool ColliderSettingsExpanded
		{
			get { return colliderSettingsExpanded; }
			set { colliderSettingsExpanded = value; }
		}

		public bool VertexSelectionSettingsExpanded
		{
			get { return vertexSelectionSettingsExpanded; }
			set { vertexSelectionSettingsExpanded = value; }
		}

		public bool VertexPreviewSettingsExpanded
		{
			get { return vertexPreviewSettingsExpanded; }
			set { vertexPreviewSettingsExpanded = value; }
		}

		public bool DrawPrimitivePreview
		{
			get { return drawPrimitivePreview; }
			set { drawPrimitivePreview = value; }
		}

		public bool DrawPrimitivePreviewAsWireframe
		{
			get { return drawPrimitivePreviewAsWireframe; }
			set { drawPrimitivePreviewAsWireframe = value; }
		}

		public bool ColorPrimitivePreviewByPerformance
		{
			get { return colorPrimitivePreviewByPerformance; }
			set { colorPrimitivePreviewByPerformance = value; }
		}

		public Color PrimitivePreviewColor
		{
			get { return primitivePreviewColor; }
			set { primitivePreviewColor = value; }
		}

		public bool CoacdAdvancedExpanded
		{
			get { return coacdAdvancedExpanded; }
			set { coacdAdvancedExpanded = value; }
		}

		public bool ExistingColliderListExpanded
		{
			get { return existingColliderListExpanded; }
			set { existingColliderListExpanded = value; }
		}

		public bool DrawExistingColliderPerformanceView
		{
			get { return drawExistingColliderPerformanceView; }
			set { drawExistingColliderPerformanceView = value; }
		}

		public bool DrawExistingColliderPreviewAsWireframe
		{
			get { return drawExistingColliderPreviewAsWireframe; }
			set { drawExistingColliderPreviewAsWireframe = value; }
		}

		public Vector2 GeneratedCollidersScrollPosition
		{
			get { return generatedCollidersScrollPosition; }
			set { generatedCollidersScrollPosition = value; }
		}

		public Vector2 WindowScrollPosition
		{
			get { return windowScrollPosition; }
			set { windowScrollPosition = value; }
		}

		public bool IsExistingColliderVisible(Collider collider)
		{
			if(collider == null)
			{
				return false;
			}

			return !hiddenExistingColliderInstanceIds.Contains(collider.GetInstanceID());
		}

		public void SetExistingColliderVisible(Collider collider, bool isVisible)
		{
			if(collider == null)
			{
				return;
			}

			int instanceId = collider.GetInstanceID();

			if(isVisible)
			{
				hiddenExistingColliderInstanceIds.Remove(instanceId);
				return;
			}

			if(!hiddenExistingColliderInstanceIds.Contains(instanceId))
			{
				hiddenExistingColliderInstanceIds.Add(instanceId);
			}
		}

		public void ClearExistingColliderVisibilityOverrides()
		{
			hiddenExistingColliderInstanceIds.Clear();
		}

		public void ClearSelection()
		{
			selectedVertices.Clear();
		}

		public void ClearHiddenVertices()
		{
			hiddenVertices.Clear();
		}

		public void HideSelectedVertices()
		{
			HashSet<VertexKey> hiddenKeys = GetHiddenVertexKeys();

			int count = selectedVertices.Count;
			for(int i = 0; i < count; i++)
			{
				if(!hiddenKeys.Contains(selectedVertices[i].Key))
				{
					hiddenVertices.Add(selectedVertices[i]);
					hiddenKeys.Add(selectedVertices[i].Key);
				}
			}

			selectedVertices.Clear();
		}

		public void IsolateSelectedVertices(List<SelectedVertex> availableVertices)
		{
			HashSet<VertexKey> selectedKeys = GetSelectedVertexKeys();
			hiddenVertices.Clear();

			if(availableVertices == null || selectedKeys.Count == 0)
			{
				return;
			}

			HashSet<VertexKey> hiddenKeys = new HashSet<VertexKey>();
			int count = availableVertices.Count;
			for(int i = 0; i < count; i++)
			{
				SelectedVertex vertex = availableVertices[i];

				if(selectedKeys.Contains(vertex.Key) || hiddenKeys.Contains(vertex.Key))
				{
					continue;
				}

				hiddenVertices.Add(vertex);
				hiddenKeys.Add(vertex.Key);
			}

			RemoveHiddenVerticesFromSelection();
		}

		public bool IsVertexHidden(Transform transform, Vector3 localPosition)
		{
			return GetHiddenVertexKeys().Contains(new VertexKey(transform, localPosition));
		}

		public HashSet<VertexKey> GetHiddenVertexKeys()
		{
			HashSet<VertexKey> keys = new HashSet<VertexKey>();

			int count = hiddenVertices.Count;
			for(int i = 0; i < count; i++)
			{
				keys.Add(hiddenVertices[i].Key);
			}

			return keys;
		}

		public void RemoveHiddenVerticesFromSelection()
		{
			HashSet<VertexKey> hiddenKeys = GetHiddenVertexKeys();

			for(int i = selectedVertices.Count - 1; i >= 0; i--)
			{
				if(hiddenKeys.Contains(selectedVertices[i].Key))
				{
					selectedVertices.RemoveAt(i);
				}
			}
		}

		public HashSet<VertexKey> GetSelectedVertexKeys()
		{
			HashSet<VertexKey> keys = new HashSet<VertexKey>();

			int count = selectedVertices.Count;
			for(int i = 0; i < count; i++)
			{
				keys.Add(selectedVertices[i].Key);
			}

			return keys;
		}

		public void SetSelectedVertices(List<SelectedVertex> vertices)
		{
			selectedVertices.Clear();

			int count = vertices.Count;
			for(int i = 0; i < count; i++)
			{
				selectedVertices.Add(vertices[i]);
			}
		}

		/// <summary>
		/// Primitive types that can be created from the manual collider picker.
		/// </summary>
		public enum PrimitiveType
		{
			None = 0,
			Sphere = 1,
			Capsule = 2,
			Box = 3,
			ConvexHull = 4,
			Cylinder = 5,
			ConvexDecomposition = 6,
		}

		/// <summary>
		/// User friendly axis selection for elongated primitives.
		/// </summary>
		public enum PrimitiveAxis
		{
			Automatic = -1,
			ObjectX = 0,
			ObjectY = 1,
			ObjectZ = 2,
		}

		[Serializable]
		public struct SelectedVertex
		{
			public Transform transform;
			public Vector3 localPosition;
			public Vector3 normal;

			public SelectedVertex(Transform transform, Vector3 localPosition, Vector3 normal)
			{
				this.transform = transform;
				this.localPosition = localPosition;
				this.normal = normal;
			}

			public Vector3 WorldPosition
			{
				get
				{
					if(transform == null)
					{
						return localPosition;
					}

					return transform.TransformPoint(localPosition);
				}
			}

			public VertexKey Key
			{
				get
				{
					return new VertexKey(transform, localPosition);
				}
			}
		}

		[Serializable]
		public struct CoacdSettings
		{
			public double threshold;
			public int maxConvexHull;
			public int preprocessMode;
			public int preprocessResolution;
			public int sampleResolution;
			public int mctsNodes;
			public int mctsIteration;
			public int mctsMaxDepth;
			public bool pca;
			public bool merge;
			public uint seed;

			/// <summary>
			/// Matches the generally useful defaults from the original CoACD tooling.
			/// Artists can still override these in the advanced foldout when a specific asset needs tuning.
			/// </summary>
			public static CoacdSettings Default()
			{
				CoacdSettings settings = new CoacdSettings();
				settings.threshold = 0.05;
				settings.maxConvexHull = -1;
				settings.preprocessMode = 0;
				settings.preprocessResolution = 50;
				settings.sampleResolution = 2000;
				settings.mctsNodes = 20;
				settings.mctsIteration = 150;
				settings.mctsMaxDepth = 3;
				settings.pca = false;
				settings.merge = true;
				settings.seed = 0;
				return settings;
			}
		}

	}

	public readonly struct VertexKey : IEquatable<VertexKey>
	{
		private readonly Transform TRANSFORM;
		private readonly Vector3 LOCAL_POSITION;

		public VertexKey(Transform transform, Vector3 localPosition)
		{
			this.TRANSFORM = transform;
			this.LOCAL_POSITION = localPosition;
		}

		public bool Equals(VertexKey other)
		{
			return TRANSFORM == other.TRANSFORM && LOCAL_POSITION == other.LOCAL_POSITION;
		}

		public override bool Equals(object obj)
		{
			return obj is VertexKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			int hashCode = 17;
			hashCode = (hashCode * 31) + (TRANSFORM != null ? TRANSFORM.GetHashCode() : 0);
			hashCode = (hashCode * 31) + LOCAL_POSITION.GetHashCode();
			return hashCode;
		}
	}
}
