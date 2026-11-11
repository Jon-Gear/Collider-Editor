using UnityEngine;

namespace ColliderEditor
{
	/// <summary>
	/// Marks colliders created by the Collider Editor and stores their generated mesh asset when they use one.
	/// </summary>
	[AddComponentMenu("")]
	[DisallowMultipleComponent]
	public sealed class ColliderEditorGeneratedCollider : MonoBehaviour
	{
		[SerializeField]
		private Mesh mesh;

		public Mesh Mesh
		{
			get { return mesh; }
		}

		public void SetMesh(Mesh generatedMesh)
		{
			mesh = generatedMesh;
		}
	}
}
