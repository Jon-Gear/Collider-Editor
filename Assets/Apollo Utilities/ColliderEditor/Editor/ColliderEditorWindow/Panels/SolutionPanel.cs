#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ColliderEditor.ColliderEditorState;

#if(UNITY_6000_0_OR_NEWER)
using PhysicsMaterialType = UnityEngine.PhysicsMaterial;
#else
using PhysicsMaterialType = UnityEngine.PhysicMaterial;
#endif

namespace ColliderEditor
{
	/// <summary>
	/// Partial implementation for ColliderEditorWindow.
	/// </summary>
	public sealed class SolutionPanel
	{
		private readonly ColliderEditorState STATE;
		private readonly CreateColliderController CREATE_COLLIDER_CONTROLLER;

		public event Action onRepaintPreview;

		public SolutionPanel(ColliderEditorState state, CreateColliderController controller)
		{
			this.STATE = state;
			this.CREATE_COLLIDER_CONTROLLER = controller;
		}

		private void RepaintPreview()
		{
			onRepaintPreview?.Invoke();
		}

		public void Draw()
		{
			EditorGUILayout.LabelField(new GUIContent("Create Colliders", "Select vertices in the Scene view, choose a collider type, then press Create Collider."), EditorStyles.largeLabel);

			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField(new GUIContent("Collider Selection", "Available collider types: Box, Sphere, Capsule, Mesh From Selected Vertices, Cylinder, and Convex Decomposition."), EditorStyles.boldLabel);

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			for(int i = 0; i < PRIMITIVE_ORDER.Length; i++)
			{
				ColliderEditorState.PrimitiveType primitiveType = PRIMITIVE_ORDER[i];
				string label = GetPrimitiveDisplayName(primitiveType);
				string tooltip = GetPrimitiveTooltip(primitiveType);

				if(ColliderEditorUIHelper.DisableableIconButtonShortcutCreation(GetPrimitiveIconIndex(primitiveType), true, STATE.SelectedPrimitiveType == primitiveType, label, tooltip))
				{
					STATE.SelectedPrimitiveType = primitiveType;
					RepaintPreview();
				}
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			DrawPrimitiveSettings();
			ColliderSettings();
			DrawColliderPreviewSettings();
			EditorGUILayout.Space();
			DrawCreateColliderButton();
			EditorGUILayout.Space();
			EditorGUI.indentLevel--;
		}

		private void ColliderSettings()
		{
			STATE.ColliderSettingsExpanded = EditorGUILayout.Foldout(STATE.ColliderSettingsExpanded, new GUIContent("Collider Settings", "Shared settings applied to newly generated colliders."), true);

			if(STATE.ColliderSettingsExpanded)
			{
				EditorGUI.indentLevel++;
				STATE.IsTrigger = EditorGUILayout.Toggle(new GUIContent("Is Trigger", "Create colliders as trigger volumes instead of solid physics colliders."), STATE.IsTrigger);
				STATE.PhysicMaterial = (PhysicsMaterialType)EditorGUILayout.ObjectField(new GUIContent("Physic Material", "Optional physics material assigned to generated colliders."), STATE.PhysicMaterial, typeof(PhysicsMaterialType), false);
				EditorGUI.indentLevel--;
			}
		}

		private void DrawColliderPreviewSettings()
		{
			STATE.PreviewSettingsExpanded = EditorGUILayout.Foldout(STATE.PreviewSettingsExpanded, new GUIContent("Collider Preview Settings", "Controls the Scene view collider preview before creation."), true);

			if(STATE.PreviewSettingsExpanded)
			{
				EditorGUI.indentLevel++;
				EditorGUI.BeginChangeCheck();

				STATE.DrawPrimitivePreview = EditorGUILayout.ToggleLeft(new GUIContent("Draw Primitive Preview", "Show a Scene view preview of the currently selected manual collider type."), STATE.DrawPrimitivePreview);
				STATE.DrawPrimitivePreviewAsWireframe = EditorGUILayout.ToggleLeft(new GUIContent("Wireframe Primitive Preview", "Draw primitive previews as outlines. Box, sphere, and capsule use Unity-style wire outlines."), STATE.DrawPrimitivePreviewAsWireframe);
				STATE.ColorPrimitivePreviewByPerformance = EditorGUILayout.ToggleLeft(new GUIContent("Performance Preview Colors", "Color primitive previews by expected physics cost: blue is fastest, red is slowest."), STATE.ColorPrimitivePreviewByPerformance);

				using(new EditorGUI.DisabledScope(STATE.ColorPrimitivePreviewByPerformance))
				{
					STATE.PrimitivePreviewColor = EditorGUILayout.ColorField(new GUIContent("Primitive Preview Color", "Used when Performance Preview Colors is disabled."), STATE.PrimitivePreviewColor);
				}

				if(EditorGUI.EndChangeCheck())
				{
					SceneView.RepaintAll();
				}

				EditorGUI.indentLevel--;
			}
		}

		private void DrawPrimitiveSettings()
		{
			EditorGUI.BeginChangeCheck();

			switch(STATE.SelectedPrimitiveType)
			{
				case ColliderEditorState.PrimitiveType.Box:
					BoxSettings();
					break;

				case ColliderEditorState.PrimitiveType.Sphere:
					SphereSettings();
					break;

				case ColliderEditorState.PrimitiveType.Capsule:
					CapsuleSettings();
					break;

				case ColliderEditorState.PrimitiveType.ConvexHull:
					MeshFromSelectedVerticesSettings();
					break;

				case ColliderEditorState.PrimitiveType.Cylinder:
					CylinderSettings();
					break;
				case ColliderEditorState.PrimitiveType.ConvexDecomposition:
					ConcaveMeshFromSelectedVerticesSettings();
					break;
			}

			if(EditorGUI.EndChangeCheck())
			{
				SceneView.RepaintAll();
			}
		}

		private void BoxSettings()
		{
			EditorGUILayout.LabelField(new GUIContent("Selected Collider: Box", "Creates a BoxCollider from the selected vertices."), EditorStyles.boldLabel);
			STATE.RotateBox = EditorGUILayout.Toggle(new GUIContent("Rotated Box", "Fit an oriented box to the selected vertices. Disabled keeps the box aligned to the selected object's local axes."), STATE.RotateBox);
		}

		private void SphereSettings()
		{
			EditorGUILayout.LabelField(new GUIContent("Selected Collider: Sphere", "Creates a SphereCollider from the selected vertices."), EditorStyles.boldLabel);
			STATE.SphereMethod = DrawSphereMethodPopup(STATE.SphereMethod);
			EditorGUILayout.HelpBox(GetSphereMethodHint(STATE.SphereMethod), MessageType.None);
		}

		private void CapsuleSettings()
		{
			EditorGUILayout.LabelField(new GUIContent("Selected Collider: Capsule", "Creates a CapsuleCollider from the selected vertices."), EditorStyles.boldLabel);
			STATE.RotateCapsule = EditorGUILayout.Toggle(new GUIContent("Rotated Capsule", "Fit the capsule to an oriented basis from the selected vertices. Disabled keeps it aligned to the selected object's local axes."), STATE.RotateCapsule);
			STATE.CapsuleAxis = (ColliderEditorState.PrimitiveAxis)EditorGUILayout.EnumPopup(new GUIContent("Orientation", "Orientation in object space. Automatic uses the longest selected dimension. Choose X, Y, or Z when you need a specific capsule axis."), STATE.CapsuleAxis);
			STATE.CapsuleMethod = DrawCapsuleMethodPopup(STATE.CapsuleMethod);
			EditorGUILayout.HelpBox(GetCapsuleMethodHint(STATE.CapsuleMethod), MessageType.None);
		}

		private void MeshFromSelectedVerticesSettings()
		{
			EditorGUILayout.LabelField(new GUIContent("Selected Collider: Mesh From Selected Vertices", "Creates a convex MeshCollider from the selected vertices."), EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Creates a convex hull mesh from the selected vertices. This can match complex shapes better, but it is the least performant manual primitive option.", MessageType.None);
		}

		private void CylinderSettings()
		{
			EditorGUILayout.LabelField(new GUIContent("Selected Collider: Cylinder", "Creates a convex MeshCollider shaped like a cylinder."), EditorStyles.boldLabel);

			ColliderEditorState.PrimitiveAxis cylinderAxis =
				(ColliderEditorState.PrimitiveAxis)EditorGUILayout.EnumPopup(new GUIContent("Orientation", "Orientation in object space. Automatic uses the longest selected dimension. Choose X, Y, or Z when you need a specific cylinder axis."), STATE.CylinderAxis);
			int cylinderSides = EditorGUILayout.IntSlider(new GUIContent("Cylinder Sides", "Higher values look rounder but create a more expensive convex mesh. Use the lowest side count that gives the shape you need."), STATE.CylinderSides, 3, 64);

			STATE.CylinderAxis = cylinderAxis;
			STATE.CylinderSides = cylinderSides;
		}

		private void ConcaveMeshFromSelectedVerticesSettings()
		{
			EditorGUILayout.LabelField(new GUIContent("Selected Collider: Convex Decomposition", "Creates a group of convex MeshColliders from the selected vertices using CoACD."), EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Generates multiple convex hulls from your selected shape. This is different from Mesh From Selected Vertices: it can preserve concave silhouettes better by splitting the result into several convex MeshColliders.", MessageType.Info);
			EditorGUILayout.HelpBox("No live preview is shown for this mode. The final hulls are produced by the CoACD library when you press Create Collider, so the exact split is only known after generation. In general, it follows the shape of your selected vertices.", MessageType.None);

			ColliderEditorState.CoacdSettings settings = STATE.CurrentCoacdSettings;

			STATE.CoacdAdvancedExpanded = EditorGUILayout.Foldout(STATE.CoacdAdvancedExpanded, new GUIContent("Advanced Settings", "Detailed convex-decomposition controls for hull count, solver quality, and determinism."), true);
			if(STATE.CoacdAdvancedExpanded)
			{
				EditorGUI.indentLevel++;
				if(GUILayout.Button(new GUIContent("Reset To CoACD Defaults", "Restore the default setting values from the original CoACD tooling.")))
				{
					settings = ColliderEditorState.CoacdSettings.Default();
				}
				settings.threshold = EditorGUILayout.Slider(new GUIContent("Threshold", "Approximation threshold for convex decomposition. Lower values fit more tightly."), (float)settings.threshold, 0.01f, 1.0f);
				settings.maxConvexHull = EditorGUILayout.IntSlider(new GUIContent("Max Hulls", "Maximum number of convex hull MeshColliders to create. -1 lets CoACD decide."), settings.maxConvexHull, -1, 64);
				EditorGUILayout.Space();
				settings.preprocessResolution = EditorGUILayout.IntSlider(new GUIContent("Preprocess Resolution", "Voxel resolution used during preprocessing."), settings.preprocessResolution, 20, 100);
				settings.sampleResolution = EditorGUILayout.IntSlider(new GUIContent("Sample Resolution", "Sampling density used by the decomposition solver."), settings.sampleResolution, 1000, 10000);
				settings.mctsNodes = EditorGUILayout.IntSlider(new GUIContent("MCTS Nodes", "Number of Monte Carlo tree search nodes."), settings.mctsNodes, 10, 40);
				settings.mctsIteration = EditorGUILayout.IntSlider(new GUIContent("MCTS Iteration", "Number of Monte Carlo tree search iterations."), settings.mctsIteration, 60, 2000);
				settings.mctsMaxDepth = EditorGUILayout.IntSlider(new GUIContent("MCTS Max Depth", "Maximum search depth for decomposition cuts."), settings.mctsMaxDepth, 2, 7);
				settings.pca = EditorGUILayout.Toggle(new GUIContent("Use PCA", "Use principal component analysis to align decomposition input."), settings.pca);
				settings.merge = EditorGUILayout.Toggle(new GUIContent("Merge", "Merge compatible hulls after decomposition."), settings.merge);
				settings.seed = (uint)Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Seed", "Deterministic random seed used by the generator."), (int)settings.seed));
				EditorGUI.indentLevel--;
			}

			STATE.CurrentCoacdSettings = settings;
		}

		private void DrawCreateColliderButton()
		{
			using(new EditorGUI.DisabledScope(STATE.SelectedObject == null || STATE.SelectedPrimitiveType == ColliderEditorState.PrimitiveType.None || STATE.SelectedVertices.Count == 0))
			{
				if(GUILayout.Button(new GUIContent("Create Collider", "Confirms the selected primitive and creates the collider from the selected vertices. Shortcut: Control/Command+Enter."), GUILayout.Height(30.0f)))
				{
					CREATE_COLLIDER_CONTROLLER.ConfirmAndCreateSelectedPrimitive();
				}
			}
		}

		private SPHERE_COLLIDER_METHOD DrawSphereMethodPopup(SPHERE_COLLIDER_METHOD currentMethod)
		{
			int selectedIndex = GetSphereMethodIndex(currentMethod);
			selectedIndex = EditorGUILayout.Popup(new GUIContent("Sphere Fit", "Choose how the sphere center and radius are calculated."), selectedIndex, SPHERE_METHOD_LABELS);
			return SPHERE_METHOD_VALUES[Mathf.Clamp(selectedIndex, 0, SPHERE_METHOD_VALUES.Length - 1)];
		}

		private CAPSULE_COLLIDER_METHOD DrawCapsuleMethodPopup(CAPSULE_COLLIDER_METHOD currentMethod)
		{
			int selectedIndex = GetCapsuleMethodIndex(currentMethod);
			selectedIndex = EditorGUILayout.Popup(new GUIContent("Capsule Fit", "Choose how the capsule height and radius are calculated."), selectedIndex, CAPSULE_METHOD_LABELS);
			return CAPSULE_METHOD_VALUES[Mathf.Clamp(selectedIndex, 0, CAPSULE_METHOD_VALUES.Length - 1)];
		}

		private int GetSphereMethodIndex(SPHERE_COLLIDER_METHOD method)
		{
			for(int i = 0; i < SPHERE_METHOD_VALUES.Length; i++)
			{
				if(SPHERE_METHOD_VALUES[i] == method)
				{
					return i;
				}
			}

			return 0;
		}

		private int GetCapsuleMethodIndex(CAPSULE_COLLIDER_METHOD method)
		{
			for(int i = 0; i < CAPSULE_METHOD_VALUES.Length; i++)
			{
				if(CAPSULE_METHOD_VALUES[i] == method)
				{
					return i;
				}
			}

			return 0;
		}

		private string GetSphereMethodHint(SPHERE_COLLIDER_METHOD method)
		{
			switch(method)
			{
				case SPHERE_COLLIDER_METHOD.BestFit:
					return "Tightest Radius: best when you want the sphere to closely cover an uneven selection.";

				case SPHERE_COLLIDER_METHOD.Distance:
					return "Center Average + Farthest Point: uses the average selected position as the center, then expands to the farthest selected vertex.";

				case SPHERE_COLLIDER_METHOD.MinMax:
					return "Bounds Center: uses the selection bounds center and the min/max points for a predictable radius.";

				default:
					return string.Empty;
			}
		}

		private string GetCapsuleMethodHint(CAPSULE_COLLIDER_METHOD method)
		{
			switch(method)
			{
				case CAPSULE_COLLIDER_METHOD.BestFit:
					return "Tightest Radius: best when you want the capsule to closely cover an elongated selection.";

				case CAPSULE_COLLIDER_METHOD.MinMax:
					return "Bounds Endpoints: uses the minimum and maximum selected positions along the capsule axis.";

				case CAPSULE_COLLIDER_METHOD.MinMaxPlusRadius:
					return "Bounds + Radius: adds one radius of extra height so the end caps cover slightly beyond the selected endpoints.";

				case CAPSULE_COLLIDER_METHOD.MinMaxPlusDiameter:
					return "Bounds + Diameter: adds a full diameter of extra height for more forgiving capsule ends.";

				default:
					return string.Empty;
			}
		}

		private int GetPrimitiveIconIndex(ColliderEditorState.PrimitiveType primitiveType)
		{
			switch(primitiveType)
			{
				case ColliderEditorState.PrimitiveType.Sphere:
					return 0;
				case ColliderEditorState.PrimitiveType.Capsule:
					return 1;
				case ColliderEditorState.PrimitiveType.Box:
					return 2;
				case ColliderEditorState.PrimitiveType.ConvexHull:
					return 3;

				case ColliderEditorState.PrimitiveType.Cylinder:
					return 4;
				case ColliderEditorState.PrimitiveType.ConvexDecomposition:
					return 5;
				default:
					return 0;
			}
		}

		private string GetPrimitiveDisplayName(ColliderEditorState.PrimitiveType primitiveType)
		{
			switch(primitiveType)
			{
				case ColliderEditorState.PrimitiveType.Box:
					return "Box";

				case ColliderEditorState.PrimitiveType.Sphere:
					return "Sphere";

				case ColliderEditorState.PrimitiveType.Capsule:
					return "Capsule";

				case ColliderEditorState.PrimitiveType.ConvexHull:
					return "Mesh From Selected Vertices";

				case ColliderEditorState.PrimitiveType.Cylinder:
					return "Cylinder";

				case ColliderEditorState.PrimitiveType.ConvexDecomposition:
					return "Convex Decomposition";
				default:
					return "None";
			}
		}

		private string GetPrimitiveTooltip(ColliderEditorState.PrimitiveType primitiveType)
		{
			int shortcut = GetPrimitiveShortcutNumber(primitiveType);
			string keys = "Press " + shortcut + " to select it, or Control/Command+" + shortcut + " to create it";

			switch(primitiveType)
			{
				case ColliderEditorState.PrimitiveType.Box:
					return "Fast box collider. " + keys + " from the current selection.";

				case ColliderEditorState.PrimitiveType.Sphere:
					return "Fastest primitive collider. " + keys + " from the current selection.";

				case ColliderEditorState.PrimitiveType.Capsule:
					return "Fast rounded collider for limbs, handles, poles, and elongated selections. " + keys + ".";

				case ColliderEditorState.PrimitiveType.ConvexHull:
					return "Convex mesh built from selected vertices. " + keys + ".";

				case ColliderEditorState.PrimitiveType.Cylinder:
					return "Convex mesh cylinder. " + keys + ".";

				case ColliderEditorState.PrimitiveType.ConvexDecomposition:
					return "Automatic compound collider generated by CoACD. No live preview. " + keys + ".";

				default:
					return string.Empty;
			}
		}

		/// <summary>
		/// Keyboard shortcut number (1-based) for a primitive, taken from the toolbar order so tooltips and input handling cannot drift apart.
		/// </summary>
		private static int GetPrimitiveShortcutNumber(ColliderEditorState.PrimitiveType primitiveType)
		{
			for(int i = 0; i < PRIMITIVE_ORDER.Length; i++)
			{
				if(PRIMITIVE_ORDER[i] == primitiveType)
				{
					return i + 1;
				}
			}

			return 0;
		}

	}
}
#endif
