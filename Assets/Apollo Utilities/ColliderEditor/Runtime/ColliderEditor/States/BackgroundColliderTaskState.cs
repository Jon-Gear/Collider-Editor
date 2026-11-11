using ColliderEditor;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.Threading.Tasks;

namespace ColliderEditor
{
	public sealed class BackgroundColliderTaskState
	{
		public int progressId;
		public string title;
		public volatile bool isCompleted;
		public volatile float progressValue;
		public string description;
		public Exception exception;
		public CancellationTokenSource cancellationTokenSource;
		public Task workerTask;
		public Action<BackgroundColliderTaskState> completeOnMainThread;
		public List<ColliderEditorMeshData> coacdMeshes;

		public void ReportProgress(float progress, string taskDescription)
		{
			progressValue = Mathf.Clamp01(progress);
			description = taskDescription;
		}

	}
}
