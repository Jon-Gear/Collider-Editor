using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#if(UNITY_6000_0_OR_NEWER)
using PhysicsMaterialType = UnityEngine.PhysicsMaterial;
#else
using PhysicsMaterialType = UnityEngine.PhysicMaterial;
#endif

namespace ColliderEditor
{
	public sealed class CreateConvexDecompositionController
	{
		private readonly ColliderEditorState STATE;
		private readonly UndoService UNDO;

		private BackgroundColliderTaskState backgroundColliderTaskState;

		public event Action<GUIContent> onShowNotification;
		public event Action onRepaintPreview;

		public CreateConvexDecompositionController(ColliderEditorState state, UndoService undo)
		{
			this.STATE = state;
			this.UNDO = undo;
		}

		public void RepaintPreview()
		{
			onRepaintPreview?.Invoke();
		}

		public void ShowNotification(GUIContent notification)
		{
			onShowNotification?.Invoke(notification);
		}

		public void CreateCoacdColliders()
		{
			if(IsBackgroundColliderTaskRunning)
			{
				ShowNotification(new GUIContent("Wait for the current background collider task to finish."));
				return;
			}

			Mesh selectedRegionMesh = ColliderEditorMeshUtility.BuildSelectedRegionMesh(STATE);
			if(selectedRegionMesh == null)
			{
				ShowNotification(new GUIContent("Could not build a valid mesh from the current selection."));
				return;
			}

			ColliderEditorMeshData sourceMeshData = ColliderEditorMeshData.Create(selectedRegionMesh);
			GameObject selectedObject = STATE.SelectedObject;
			bool isTrigger = STATE.IsTrigger;
			PhysicsMaterialType physicMaterial = STATE.PhysicMaterial;
			ColliderEditorState.CoacdSettings settings = STATE.CurrentCoacdSettings;

			StartBackgroundColliderTask(
				"Generate Convex Decomposition Colliders",
				"Starting convex decomposition...",
				taskState =>
				{
					taskState.coacdMeshes = ColliderEditorCoacd.Decompose(
						sourceMeshData,
						settings,
						taskState.ReportProgress,
						taskState.cancellationTokenSource.Token);
				},
				taskState =>
				{
					if(taskState.coacdMeshes == null || taskState.coacdMeshes.Count == 0)
					{
						ShowNotification(new GUIContent("Convex decomposition did not return any hulls."));
						return;
					}

					UNDO.ExecuteUndoableCreate(
						selectedObject,
						"Generate Convex Decomposition Colliders",
						() =>
						{
							string generationAttemptName =
								AssetPersistence.CreateUniqueGeneratedAttemptName(
									selectedObject,
									"Convex_Decomposition");

							GameObject root = CreateChildRoot(
								selectedObject,
								generationAttemptName);

							int count = taskState.coacdMeshes.Count;

							for(int index = 0; index < count; index++)
							{
								ColliderFactory.CreateMeshCollider(
									root,
									selectedObject,
									taskState.coacdMeshes[index].CreateMesh(),
									"Convex_Decomposition_Hull_" +
										index.ToString("D2"),
									isTrigger,
									physicMaterial,
									generationAttemptName);
							}

							return root;
						});
				});
		}

		private bool IsBackgroundColliderTaskRunning
		{
			get
			{
				return backgroundColliderTaskState != null;
			}
		}

		private void StartBackgroundColliderTask(
			string title,
			string initialDescription,
			Action<BackgroundColliderTaskState> workerAction,
			Action<BackgroundColliderTaskState> completeOnMainThread)
		{
			if(backgroundColliderTaskState != null)
			{
				return;
			}

			BackgroundColliderTaskState taskState = new BackgroundColliderTaskState();
			taskState.title = title;
			taskState.description = initialDescription;
			taskState.progressValue = 0.0f;
			taskState.completeOnMainThread = completeOnMainThread;
			taskState.cancellationTokenSource = new CancellationTokenSource();
			taskState.progressId = Progress.Start(title, initialDescription, Progress.Options.Sticky);

			backgroundColliderTaskState = taskState;
			EditorApplication.update += UpdateBackgroundColliderTask;

			taskState.workerTask = Task.Run(() =>
			{
				try
				{
					workerAction.Invoke(taskState);
				}
				catch(OperationCanceledException)
				{
					taskState.description = "Canceled.";
				}
				catch(Exception exception)
				{
					taskState.exception = exception;
					taskState.description = "Failed.";
				}
				finally
				{
					taskState.isCompleted = true;
				}
			}, taskState.cancellationTokenSource.Token);
		}

		private void UpdateBackgroundColliderTask()
		{
			if(backgroundColliderTaskState == null)
			{
				EditorApplication.update -= UpdateBackgroundColliderTask;
				return;
			}

			Progress.Report(
				backgroundColliderTaskState.progressId,
				backgroundColliderTaskState.progressValue,
				backgroundColliderTaskState.description);

			RepaintPreview();

			if(!backgroundColliderTaskState.isCompleted)
			{
				return;
			}

			EditorApplication.update -= UpdateBackgroundColliderTask;

			try
			{
				if(backgroundColliderTaskState.cancellationTokenSource.IsCancellationRequested)
				{
					Progress.Finish(backgroundColliderTaskState.progressId, Progress.Status.Canceled);
					ShowNotification(new GUIContent(backgroundColliderTaskState.title + " canceled."));
					return;
				}

				if(backgroundColliderTaskState.exception != null)
				{
					Progress.Finish(backgroundColliderTaskState.progressId, Progress.Status.Failed);
					Debug.LogException(backgroundColliderTaskState.exception);
					ShowNotification(new GUIContent(backgroundColliderTaskState.title + " failed. See Console."));
					return;
				}

				backgroundColliderTaskState.completeOnMainThread?.Invoke(backgroundColliderTaskState);
				Progress.Finish(backgroundColliderTaskState.progressId, Progress.Status.Succeeded);
				ShowNotification(new GUIContent(backgroundColliderTaskState.title + " finished."));
			}
			finally
			{
				backgroundColliderTaskState.cancellationTokenSource.Dispose();
				backgroundColliderTaskState = null;
			}
		}

		public void CancelBackgroundColliderTask()
		{
			if(backgroundColliderTaskState == null)
			{
				return;
			}

			backgroundColliderTaskState.cancellationTokenSource.Cancel();
		}

		private GameObject CreateChildRoot(GameObject parentObject, string suffix)
		{
			if(parentObject == null)
			{
				return null;
			}

			GameObject root = new GameObject(suffix + "_Colliders");
			Undo.RegisterCreatedObjectUndo(root, "Create Collider Root");
			root.transform.SetParent(parentObject.transform, false);
			return root;
		}
	}
}
