using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

using System.Threading.Tasks;

#if(UNITY_6000_0_OR_NEWER)
using PhysicsMaterialType = UnityEngine.PhysicsMaterial;
#else
using PhysicsMaterialType = UnityEngine.PhysicMaterial;
#endif

namespace ColliderEditor
{
	public sealed class CreateColliderController
	{
		private readonly ColliderEditorState STATE;
		private readonly UndoService UNDO;
		private readonly CreateConvexDecompositionController CREATE_CONVEX_DECOMPOSITION_CONTROLLER;

		public CreateColliderController(ColliderEditorState state, UndoService undo, CreateConvexDecompositionController createConvexDecompositionController)
		{
			this.STATE = state;
			this.UNDO = undo;
			this.CREATE_CONVEX_DECOMPOSITION_CONTROLLER = createConvexDecompositionController;
		}

		public void ConfirmAndCreateSelectedPrimitive()
		{
			string primitiveName = GetSelectedPrimitiveCreationName();
			bool confirmed = EditorUtility.DisplayDialog(
				"Create Collider",
				"Create " + primitiveName + " from " + STATE.SelectedVertices.Count + " selected vertices?",
				"Create Collider",
				"Cancel");

			if(!confirmed)
			{
				return;
			}

			CreateSelectedPrimitive();
		}

		private string GetSelectedPrimitiveCreationName()
		{
			switch(STATE.SelectedPrimitiveType)
			{
				case ColliderEditorState.PrimitiveType.Box:
					return STATE.RotateBox ? "Rotated Box Collider" : "Box Collider";

				case ColliderEditorState.PrimitiveType.Sphere:
					return "Sphere Collider";

				case ColliderEditorState.PrimitiveType.Capsule:
					return STATE.RotateCapsule ? "Rotated Capsule Collider" : "Capsule Collider";

				case ColliderEditorState.PrimitiveType.Cylinder:
					return "Cylinder Collider";

				case ColliderEditorState.PrimitiveType.ConvexHull:
					return "Mesh From Selected Vertices Collider";

				default:
					return STATE.SelectedPrimitiveType == ColliderEditorState.PrimitiveType.ConvexDecomposition ? "Convex Decomposition Collider" : "Collider";
			}
		}

		public void CreateSelectedPrimitive()
		{
			switch(STATE.SelectedPrimitiveType)
			{
				case ColliderEditorState.PrimitiveType.Box:
					if(STATE.RotateBox)
					{
						CreateRotatedBoxCollider();
					}
					else
					{
						CreateBoxCollider();
					}
					return;

				case ColliderEditorState.PrimitiveType.Sphere:
					CreateSphereCollider();
					return;

				case ColliderEditorState.PrimitiveType.Capsule:
					if(STATE.RotateCapsule)
					{
						CreateRotatedCapsuleCollider();
					}
					else
					{
						CreateCapsuleCollider();
					}
					return;

				case ColliderEditorState.PrimitiveType.Cylinder:
					CreateCylinderCollider();
					return;

				case ColliderEditorState.PrimitiveType.ConvexHull:
					CreateMeshFromSelectedVerticesCollider();
					return;
				case ColliderEditorState.PrimitiveType.ConvexDecomposition:
					CREATE_CONVEX_DECOMPOSITION_CONTROLLER.CreateCoacdColliders();
					return;
			}
			STATE.SelectedPrimitiveType = ColliderEditorState.PrimitiveType.None;
		}

		private void CreateBoxCollider()
		{
			List<Vector3> points = ColliderEditorMeshUtility.GetSelectedWorldPoints(STATE);
			UNDO.ExecuteUndoableCreate("Create Box Collider", () =>
			{
				return ColliderFactory.CreateBoxCollider(STATE.SelectedObject, points, STATE.IsTrigger, STATE.PhysicMaterial);
			});
		}

		private void CreateSphereCollider()
		{
			List<Vector3> points = ColliderEditorMeshUtility.GetSelectedWorldPoints(STATE);
			SPHERE_COLLIDER_METHOD sphereMethod = STATE.SphereMethod;
			UNDO.ExecuteUndoableCreate("Create Sphere Collider", () =>
			{
				return ColliderFactory.CreateSphereCollider(STATE.SelectedObject, points, STATE.IsTrigger, STATE.PhysicMaterial, sphereMethod);
			});
		}

		private void CreateCapsuleCollider()
		{
			List<Vector3> points = ColliderEditorMeshUtility.GetSelectedWorldPoints(STATE);
			int axis = (int)STATE.CapsuleAxis;
			CAPSULE_COLLIDER_METHOD capsuleMethod = STATE.CapsuleMethod;
			UNDO.ExecuteUndoableCreate("Create Capsule Collider", () =>
			{
				return ColliderFactory.CreateCapsuleCollider(STATE.SelectedObject, points, STATE.IsTrigger, STATE.PhysicMaterial, axis, capsuleMethod);
			});
		}

		private void CreateRotatedBoxCollider()
		{
			List<Vector3> points = ColliderEditorMeshUtility.GetSelectedWorldPoints(STATE);
			UNDO.ExecuteUndoableCreate("Create Rotated Box Collider", () =>
			{
				return ColliderFactory.CreateRotatedBoxCollider(STATE.SelectedObject, points, STATE.IsTrigger, STATE.PhysicMaterial);
			});
		}

		private void CreateRotatedCapsuleCollider()
		{
			List<Vector3> points = ColliderEditorMeshUtility.GetSelectedWorldPoints(STATE);
			int axis = (int)STATE.CapsuleAxis;
			CAPSULE_COLLIDER_METHOD capsuleMethod = STATE.CapsuleMethod;
			UNDO.ExecuteUndoableCreate("Create Rotated Capsule Collider", () =>
			{
				return ColliderFactory.CreateRotatedCapsuleCollider(STATE.SelectedObject, points, STATE.IsTrigger, STATE.PhysicMaterial, axis, capsuleMethod);
			});
		}

		private void CreateCylinderCollider()
		{
			List<Vector3> points = ColliderEditorMeshUtility.GetSelectedWorldPoints(STATE);
			int axis = (int)STATE.CylinderAxis;
			int cylinderSides = STATE.CylinderSides;
			UNDO.ExecuteUndoableCreate("Create Cylinder Collider", () =>
			{
				return ColliderFactory.CreateCylinderCollider(STATE.SelectedObject, points, STATE.IsTrigger, STATE.PhysicMaterial, axis, cylinderSides);
			});
		}

		private void CreateMeshFromSelectedVerticesCollider()
		{
			List<Vector3> points = ColliderEditorMeshUtility.GetSelectedWorldPoints(STATE);
			UNDO.ExecuteUndoableCreate("Create Mesh From Selected Vertices Collider", () =>
			{
				return ColliderFactory.CreateConvexHullCollider(STATE.SelectedObject, points, STATE.IsTrigger, STATE.PhysicMaterial);
			});
		}
	}
}
