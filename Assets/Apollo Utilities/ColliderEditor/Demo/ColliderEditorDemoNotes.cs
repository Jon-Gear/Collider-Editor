using UnityEngine;

namespace ColliderEditor.Demo
{
	/// <summary>
	/// Holds the walkthrough text for the demo scene so it shows up in the Inspector.
	/// Has no runtime behaviour.
	/// </summary>
	[AddComponentMenu("")]
	public sealed class ColliderEditorDemoNotes : MonoBehaviour
	{
		[TextArea(14, 40)]
		public string notes;
	}
}
