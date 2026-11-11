#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ColliderEditor
{
	/// <summary>
	/// Main editor window for selecting vertices and generating colliders.
	/// This version focuses on a more guided UX:
	/// - primitive images and preview card
	/// - simple/default actions exposed first
	/// - advanced options hidden behind foldouts
	/// - CoACD available as one manual collider type
	/// </summary>
	public sealed partial class ColliderEditorWindow : EditorWindow
	{
		private const string WINDOW_TITLE = "Collider Editor";
		private const string MENU_PATH = "Tools/Apollo Utilities/Collider Editor";

		// Controllers
		private SelectObjectInSceneController selectObjectInSceneController;
		private VertexSelectionController vertexSelectionController;
		private CreateColliderController createColliderController;
		private CreateConvexDecompositionController createConvexDecompositionController;

		// States
		private ColliderEditorState state;
		private ToolInteractionState interactionState;

		// Panels
		private SelectObjectInScenePanel selectObjectInScenePanel;
		private SolutionPanel solutionPanel;
		private SelectionSettingsPanel selectionSettingsPanel;
		private GeneratedCollidersPanel generatedCollidersPanel;
		private Input inputHandler;
		private SceneGUI sceneGUI;

		// Other
		private UndoService undoService;
		private ColliderPreview colliderPreview;

		[MenuItem(MENU_PATH)]
		public static void ShowWindow()
		{
			GetWindow<ColliderEditorWindow>(WINDOW_TITLE, EditorGUIUtility.IconContent("CompositeCollider2D Icon").image);
		}

		private void OnEnable()
		{
			EnsureState();
			titleContent = new GUIContent(WINDOW_TITLE, EditorGUIUtility.IconContent("CompositeCollider2D Icon").image);
			SceneView.duringSceneGui += OnSceneGui;
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
		}

		private void OnDisable()
		{
			selectObjectInSceneController.FinishColliderEditing();
			SceneView.duringSceneGui -= OnSceneGui;
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			createConvexDecompositionController.CancelBackgroundColliderTask();

			if(colliderPreview != null)
			{
				colliderPreview.Dispose();
				colliderPreview = null;
			}
		}

		private void OnUndoRedoPerformed()
		{
			if(state != null)
			{
				state.RemoveHiddenVerticesFromSelection();
			}

			RepaintPreview();
		}

		private void OnGUI()
		{
			GUIContent titleContent = new GUIContent(WINDOW_TITLE, EditorGUIUtility.IconContent("CompositeCollider2D Icon").image);
			this.titleContent = titleContent;
			EnsureState();

			state.WindowScrollPosition = EditorGUILayout.BeginScrollView(state.WindowScrollPosition);

			EditorGUILayout.Space();
			selectObjectInScenePanel.Draw();

			if(state.SelectedObject == null)
			{
				EditorGUILayout.EndScrollView();
				return;
			}

			EditorGUILayout.Space();
			solutionPanel.Draw();
			EditorGUILayout.Space();
			selectionSettingsPanel.Draw();
			EditorGUILayout.Space();
			generatedCollidersPanel.Draw();

			EditorGUILayout.EndScrollView();
		}

		private void OnSceneGui(SceneView sceneView)
		{
			EnsureState();
			inputHandler.HandleInput(Event.current);
			sceneGUI.Draw();
		}

		private void RepaintPreview()
		{
			Repaint();
			SceneView.RepaintAll();
		}

		private void OnSelectionChange()
		{
			Repaint();
		}

		private void EnsureState()
		{
			if(state == null)
			{
				state = CreateInstance<ColliderEditorState>();
				state.hideFlags = HideFlags.HideAndDontSave;
			}

			if(interactionState == null)
			{
				interactionState = new ToolInteractionState();
			}

			if(undoService == null)
			{
				undoService = new UndoService(state);
				undoService.onRepaintPreview += RepaintPreview;
			}

			if(selectObjectInSceneController == null)
			{
				selectObjectInSceneController = new SelectObjectInSceneController(state, interactionState, undoService);
				selectObjectInSceneController.onRepaintPreview += RepaintPreview;
			}

			if(vertexSelectionController == null)
			{
				vertexSelectionController = new VertexSelectionController(state, undoService);
				vertexSelectionController.onRepaintPreview += RepaintPreview;
			}

			if(createConvexDecompositionController == null)
			{
				createConvexDecompositionController = new CreateConvexDecompositionController(state, undoService);
				createConvexDecompositionController.onRepaintPreview += RepaintPreview;
				createConvexDecompositionController.onShowNotification += ShowNotification;
			}

			if(createColliderController == null)
			{
				createColliderController = new CreateColliderController(state, undoService, createConvexDecompositionController);
			}

			if(colliderPreview == null)
			{
				colliderPreview = new ColliderPreview();
			}

			if(selectObjectInScenePanel == null)
			{
				selectObjectInScenePanel = new SelectObjectInScenePanel(state, selectObjectInSceneController);
			}

			if(solutionPanel == null)
			{
				solutionPanel = new SolutionPanel(state, createColliderController);
				solutionPanel.onRepaintPreview += RepaintPreview;
			}

			if(selectionSettingsPanel == null)
			{
				selectionSettingsPanel = new SelectionSettingsPanel(state, vertexSelectionController);
			}

			if(generatedCollidersPanel == null)
			{
				generatedCollidersPanel = new GeneratedCollidersPanel(state);
				generatedCollidersPanel.onRepaintPreview += RepaintPreview;
			}

			if(inputHandler == null)
			{
				inputHandler = new Input(state, selectObjectInSceneController, createColliderController, vertexSelectionController, interactionState);
				inputHandler.onRepaintPreview += RepaintPreview;
			}

			if(sceneGUI == null)
			{
				sceneGUI = new SceneGUI(state, colliderPreview, interactionState);
			}
		}
	}
}
#endif
