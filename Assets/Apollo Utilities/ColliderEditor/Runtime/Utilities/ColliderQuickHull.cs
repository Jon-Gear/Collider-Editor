using System.Collections.Generic;
using UnityEngine;
using System.Linq;
namespace ColliderEditor
{
  // TODO: re-implement coroutine for runtime quickhull? improve find initial points?
  // Implemented with the help of the explanation of the algorithm found here: http://algolist.ru/maths/geom/convhull/qhull3d.php
  public class ColliderQuickHull
  {

    /// <summary>
    /// Calculates a convex hull for a list of local-space points.
    /// </summary>
    /// <param name="points">Local-Space points to generate a hull on.</param>
    /// <returns>the class with the result already calculated</returns>
    public static ColliderQuickHull CalculateHull(List<Vector3> points)
    {
      ColliderQuickHull qh = new ColliderQuickHull();
      // Calculate and return the quickhull.
      qh.GenerateHull(points);
      return qh;
    }

    /// <summary>
    /// Calculates a convex hull for a list of world-space points.
    /// </summary>
    /// <param name="points">World-Space points to generate a hull on.</param>
    /// <param name="attachTo">Transform the result will be attached to.</param>
    /// <returns>class with result calculated</returns>
    public static ColliderQuickHull CalculateHullWorld(List<Vector3> points, Transform attachTo)
    {
      List<Vector3> localPoints = new List<Vector3>();
      foreach (Vector3 point in points)
      {
        localPoints.Add(attachTo.InverseTransformPoint(point));
      }
      ColliderQuickHull qh = new ColliderQuickHull();
      qh.GenerateHull(localPoints);
      return qh;
    }

    /// <summary>
    /// class representing a triangle / face
    /// </summary>
    private class Face
    {
      /// <summary>
      /// v0 to v1 face
      /// </summary>
      public int f0;

      /// <summary>
      /// v1 to v2 face
      /// </summary>
      public int f1;

      /// <summary>
      /// v2 to v0 face
      /// </summary>
      public int f2;

      /// <summary>
      /// Normal of the face
      /// </summary>
      public Vector3 normal;

      /// <summary>
      /// is the face on the convex hull?
      /// </summary>
      public bool onConvexHull;

      /// <summary>
      /// List of vertices on the outside of the triangle (signed distance from plane is positive)
      /// </summary>
      public List<int> outsideVertices;

      // vertex index on points list.
      public int v0;
      public int v1;
      public int v2;

      /// <summary>
      /// Creates a face
      /// </summary>
      /// <param name="v0">vertex 0</param>
      /// <param name="v1">vertex 1</param>
      /// <param name="v2">vertex 2</param>
      /// <param name="normal">normal of the face</param>
      /// <param name="f0">face connected to v0-v1 edge</param>
      /// <param name="f1">face connected to v1-v2edge</param>
      /// <param name="f2">face connected to v2-v0 edge</param>
      public Face(int v0, int v1, int v2, Vector3 normal, int f0, int f1, int f2)
      {
        this.v0 = v0;
        this.v1 = v1;
        this.v2 = v2;
        this.normal = normal;
        outsideVertices = new List<int>();
        this.f0 = f0;
        this.f1 = f1;
        this.f2 = f2;
        onConvexHull = true;
      }
    }

    /// <summary>
    /// Class to hold vertex and face data of the current horizon edge
    /// </summary>
    private class Horizon
    {
      /// <summary>
      /// Index of Face crossed over to
      /// </summary>
      public int face;

      /// <summary>
      /// Index of Face crossed over from
      /// </summary>
      public int from;

      /// <summary>
      /// Is the edge on the convex hull?
      /// </summary>
      public bool onConvexHull;

      /// <summary>
      /// Index of vertex 0 of edge
      /// </summary>
      public int v0;

      /// <summary>
      /// Index of vertex 1 of edge
      /// </summary>
      public int v1;

      /// <summary>
      /// Create a new horizon edge, automatically marked on convex hull
      /// </summary>
      /// <param name="v0">Index of vertex 0 of edge</param>
      /// <param name="v1">Index of vertex 1 of edge</param>
      /// <param name="face">Face we cross edge to</param>
      /// <param name="from">Face we cross edge from</param>
      public Horizon(int v0, int v1, int face, int from)
      {
        this.v0 = v0;
        this.v1 = v1;
        this.face = face;
        this.from = from;
        onConvexHull = true;
      }
    }

    //Debug variables left in for future use.
    protected bool debugHorizon;
    protected Color debugHorizonColor = new Color(1, 0.5f, 0, 1);
    protected int debugLoopNumber = 0;
    protected int debugMaxLoopNumber;
    protected bool debugNewFaces;
    protected bool debugNormals;
    protected bool debugOutsideSet;
    protected Color debugNormalColor = new Color(0.5f, 0, 0.5f, 1);
    protected float drawTime = 2f;

    /// <summary>
    /// list of assigned vertices from add to outside set.
    /// </summary>
    /// <typeparam name="int"></typeparam>
    /// <returns></returns>
    private HashSet<int> assignedVertices = new HashSet<int>();

    /// <summary>
    /// List of vertices that area already done (in/on the convex hull)
    /// </summary>
    private HashSet<int> closedVertices = new HashSet<int>();

    /// <summary>
    /// List of current horizon edges
    /// </summary>
    /// <typeparam name="Horizon"></typeparam>
    /// <returns></returns>
    private List<Horizon> currentHorizon = new List<Horizon>();

    /// <summary>
    /// Just a small value for float comparisons
    /// </summary>
    private float epsilon = 0.000001f;

    /// <summary>
    /// List of faces in the convex hull.
    /// </summary>
    private List<Face> faces = new List<Face>();

    /// <summary>
    /// List of new faces created after finding the horizon edge.
    /// </summary>
    private List<int> newFaces = new List<int>();

    /// <summary>
    /// result mesh of quick hull calculation
    /// </summary>
    public Mesh result = null;

    /// <summary>
    /// list of unasigned vertices for add to outside set.
    /// </summary>
    private HashSet<int> unAssignedVertices = new HashSet<int>();

    /// <summary>
    /// List of all original vertices.
    /// </summary>
    /// <typeparam name="Vector3"></typeparam>
    /// <returns></returns>
    private List<Vector3> verticesList = new List<Vector3>();

		private void AddToOutsideSet(Face face, HashSet<int> vertices)
		{
			float d = 0;
			foreach(int i in vertices)
			{
				// skip already assigned vertices.
				if(assignedVertices.Contains(i) || closedVertices.Contains(i)) continue;
				// vertex is not assigned
				d = DistanceFromPlane(verticesList[i], face.normal, verticesList[face.v0]);
				if(IsApproxZero(d))
				{
					if(IsVertOnFace(i, face))
					{
						closedVertices.Add(i);
					}
				}
				else if(d > 0)
				{
					// claim vertex by removing it from vertices list and adding to the face's set of vertices.
					assignedVertices.Add(i);
					face.outsideVertices.Add(i);
				}
			}
		}

		/// <summary>
		/// Checks if vertices a and b are approximately coincident (x, y, and z differences are all < epsilon)
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns>true if coincident, false otherwise.</returns>
		private bool AreVertsCoincident(int a, int b)
		{
			if(Mathf.Abs(verticesList[a].x - verticesList[b].x) > epsilon
			|| Mathf.Abs(verticesList[a].y - verticesList[b].y) > epsilon
			|| Mathf.Abs(verticesList[a].z - verticesList[b].z) > epsilon)
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Updates closed vertices list by checking if unassigned vertices lie on a face on the convex hull.
		/// Updates the unassigned list by removing the newly closed vertices.
		/// </summary>
		private void CloseUnAssignedVertsOnFaces()
		{
			HashSet<int> newClosedVertices = new HashSet<int>();
			foreach(Face f in faces)
			{
				if(!f.onConvexHull) { continue; }
				foreach(int i in unAssignedVertices)
				{
					if(closedVertices.Contains(i)) { continue; }
					if(IsVertOnFace(i, f))
					{
						newClosedVertices.Add(i);
						closedVertices.Add(i);
					}
				}
			}
			unAssignedVertices.ExceptWith(newClosedVertices);
		}

		/// <summary>
		/// Checks to see if vertex at index i is on a face.
		/// </summary>
		/// <param name="i">index</param>
		/// <param name="face">face</param>
		/// <returns>true if vertex at index i is on the face</returns>
		private bool IsVertOnFace(int i, Face face)
		{
			// same approximate position as one of the corners
			if(AreVertsCoincident(i, face.v0) || AreVertsCoincident(i, face.v1) || AreVertsCoincident(i, face.v2))
			{
				return true;
			}
			// areas of full triangle, and point and edge.
			float a = CalcTriangleArea(face.v0, face.v1, face.v2);
			float a1 = CalcTriangleArea(i, face.v0, face.v1);
			float a2 = CalcTriangleArea(i, face.v1, face.v2);
			float a3 = CalcTriangleArea(i, face.v2, face.v0);
			if(IsApproxEqual(a, (a1 + a2 + a3)))
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Calculates the normal of a face with points a, b, and c.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="c"></param>
		/// <returns>Normal of the face formed by points a, b, and c.</returns>
		private Vector3 CalcNormal(Vector3 a, Vector3 b, Vector3 c)
		{
			return Vector3.Cross(b - a, c - a).normalized;
		}

		/// <summary>
		/// Calculates a normal given vertex index's a, b, and c.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="c"></param>
		/// <returns>Normal of the face formed by the vertices at indexs a, b, and c</returns>
		private Vector3 CalcNormal(int a, int b, int c)
		{
			return Vector3.Cross(verticesList[b] - verticesList[a], verticesList[c] - verticesList[a]).normalized;
		}

		/// <summary>
		/// Calculates the area of a tringle with points v0, v1, and v1.
		/// </summary>
		/// <param name="v0"></param>
		/// <param name="v1"></param>
		/// <param name="v2"></param>
		/// <returns>Area of the triangle</returns>
		private float CalcTriangleArea(int v0, int v1, int v2)
		{
			return (0.5f) * Vector3.Cross(verticesList[v1] - verticesList[v0], verticesList[v2] - verticesList[v1]).magnitude;
		}

		/// <summary>
		/// Calculate the horizon edge recursively
		/// </summary>
		/// <param name="eyePoint">index in vertices list of the current eyepoint</param>
		/// <param name="crossedEdge">last edge that was crossed to get to currFace</param>
		/// <param name="currFace">index in list of faces to check horizon on</param>
		/// <param name="firstFace">is this the first face?</param>
		private void CalculateHorizon(int eyePoint, Horizon crossedEdge, int currFace, bool firstFace = true)
		{
			// if curr face is not on the convex hull (negative distance)
			float d = DistanceFromPlane(verticesList[eyePoint], faces[currFace].normal, verticesList[faces[currFace].v0]);
			// if the currFace is not on the convex hull
			if(!faces[currFace].onConvexHull)
			{
				// mark the crossed edge as not on the convex hull and return
				crossedEdge.onConvexHull = false;
				return;
			}
			// if the curr face is visible from the eyepoint (signed distance from plane will be positive)
			else if(d > 0)
			{
				// 1. mark current face as not on the convex hull.
				faces[currFace].onConvexHull = false;
				// 2. remove all vertices from the currFace's outside set and add them to the list unclaimed vertices.
				unAssignedVertices.UnionWith(faces[currFace].outsideVertices);
				faces[currFace].outsideVertices.Clear();
				// if the crossed edge != null (only null for the first face) then mark the crossed edge as not on the convex hull
				if(!firstFace)
				{
					crossedEdge.onConvexHull = false;
				}
				// cross each of the edges of currface which are still on the convex hull. in counterclockwise order
				// starting from the edge after the crossed edge (in the case of the first face, pick any edge to start with. for each curr edge recurse with the call.)
				if(firstFace)
				{
					// first face -> we can start with any edge.
					// add v0 - v1 edge
					currentHorizon.Add(new Horizon(faces[currFace].v0, faces[currFace].v1, faces[currFace].f0, currFace));
					// recursive call from that edge.
					CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f0, false);
					// add v1 - v2 edge
					currentHorizon.Add(new Horizon(faces[currFace].v1, faces[currFace].v2, faces[currFace].f1, currFace));
					CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f1, false);
					// add v2 - v0 edge.
					currentHorizon.Add(new Horizon(faces[currFace].v2, faces[currFace].v0, faces[currFace].f2, currFace));
					CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f2, false);
				}
				else
				{
					// not the first face, but still visible.
					if(faces[currFace].f0 == crossedEdge.from) // crossed edge was v0-v1 edge.
					{
						// add v1-v2 edge to horizon
						currentHorizon.Add(new Horizon(faces[currFace].v1, faces[currFace].v2, faces[currFace].f1, currFace));
						CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f1, false);
						// add v2-v0 edge to horizon
						currentHorizon.Add(new Horizon(faces[currFace].v2, faces[currFace].v0, faces[currFace].f2, currFace));
						CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f2, false);
					}
					else if(faces[currFace].f1 == crossedEdge.from) // crossed edge was v1-v2 edge
					{
						// So much time spent looking for a bug in this method, and it was simply just forgetting
						// that we need to go in a certain order.
						// we NEED to go v2-v0 then v0-v1....................... Oops.
						// add v2-v0 edge to horizon
						currentHorizon.Add(new Horizon(faces[currFace].v2, faces[currFace].v0, faces[currFace].f2, currFace));
						CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f2, false);
						// add v0-v1 edge to horizon
						currentHorizon.Add(new Horizon(faces[currFace].v0, faces[currFace].v1, faces[currFace].f0, currFace));
						CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f0, false);
					}
					else if(faces[currFace].f2 == crossedEdge.from) // crossed edge was v2-v0 edge.
					{
						// add v0-v1 edge to horizon
						currentHorizon.Add(new Horizon(faces[currFace].v0, faces[currFace].v1, faces[currFace].f0, currFace));
						CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f0, false);
						// add v1-v2 edge to horizon
						currentHorizon.Add(new Horizon(faces[currFace].v1, faces[currFace].v2, faces[currFace].f1, currFace));
						CalculateHorizon(eyePoint, currentHorizon[currentHorizon.Count - 1], faces[currFace].f1, false);
					}
				}
			}
		}

		/// <summary>
		/// Creates a mesh from the face data of the convex hull.
		/// </summary>
		/// <param name="allFaces"></param>
		/// <returns>A mesh of the convex hull</returns>
		private Mesh CreateMesh(List<Face> allFaces)
		{
			Mesh m = new Mesh();
			List<Vector3> vertices = new List<Vector3>();
			// filter faces based on which are still on the convex hull.
			List<Face> faces = allFaces.Where(face => face.onConvexHull).ToList();
			List<Vector3> normals = new List<Vector3>();
			int[] triangles = new int[faces.Count * 3];
			int t0, t1, t2 = t1 = t0 = 0;
			for(int i = 0; i < faces.Count; i++)
			{
				// QH-SMOOTHED
				// shared vertices smooth
				// since we just add normals together and normalize them, and verts can be shared by n faces
				// it's not really proper smoothing.
				t0 = vertices.IndexOf(verticesList[faces[i].v0]);
				t1 = vertices.IndexOf(verticesList[faces[i].v1]);
				t2 = vertices.IndexOf(verticesList[faces[i].v2]);
				if(t0 < 0)
				{
					normals.Add(faces[i].normal);
					vertices.Add(verticesList[faces[i].v0]);
					t0 = vertices.Count - 1;
				}
				else
				{
					normals[t0] = (normals[t0] + faces[i].normal).normalized;
				}
				if(t1 < 0)
				{
					normals.Add(faces[i].normal);
					vertices.Add(verticesList[faces[i].v1]);
					t1 = vertices.Count - 1;
				}
				else
				{
					normals[t1] = (normals[t1] + faces[i].normal).normalized;
				}
				if(t2 < 0)
				{
					normals.Add(faces[i].normal);
					vertices.Add(verticesList[faces[i].v2]);
					t2 = vertices.Count - 1;
				}
				else
				{
					normals[t2] = (normals[t2] + faces[i].normal).normalized;
				}
				//QH_SMOOTHED
				// all vertices are added and arent shared so they aren't smoothed at all.
				//QH-UNSMOOTHED
				// vertices.Add(VerticesList[faces[i].V0]);
				// t0 = vertices.Count - 1;
				// vertices.Add(VerticesList[faces[i].V1]);
				// t1 = vertices.Count - 1;
				// vertices.Add(VerticesList[faces[i].V2]);
				// t2 = vertices.Count - 1;
				// normals.Add(faces[i].Normal);
				// normals.Add(faces[i].Normal);
				// normals.Add(faces[i].Normal);
				//QH_UNSMOOTHED
				triangles[i * 3] = t0;
				triangles[i * 3 + 1] = t1;
				triangles[i * 3 + 2] = t2;
			}
			m.SetVertices(vertices);
			m.SetTriangles(triangles, 0);
			m.SetNormals(normals);
			// m.RecalculateNormals(0);
			return m;
		}

		/// <summary>
		/// Calculates the distance a point is from a line.
		/// </summary>
		/// <param name="point">how far away is this point</param>
		/// <param name="line">direction of line</param>
		/// <param name="pointOnLine">a point on the line</param>
		/// <returns></returns>
		float DistanceFromLine(Vector3 point, Vector3 line, Vector3 pointOnLine)
		{
			Vector3 v = point - pointOnLine;
			float dV = Vector3.Dot(v, line);
			v = pointOnLine + dV * line;
			return Vector3.Distance(v, point);
		}

		/// <summary>
		/// Calculates the signed distance of a point from a plane
		/// </summary>
		/// <param name="point">how far is this point</param>
		/// <param name="p">from this plane</param>
		/// <returns>Distance point is from plane.</returns>
		float DistanceFromPlane(Vector3 point, Plane p)
		{
			return p.GetDistanceToPoint(point);
		}

		/// <summary>
		/// Calculate the signed distance of a point from a plane
		/// </summary>
		/// <param name="point">point</param>
		/// <param name="normal">normal of the plane</param>
		/// <param name="pointOnPlane">a point on the plane.</param>
		/// <returns></returns>
		float DistanceFromPlane(Vector3 point, Vector3 normal, Vector3 pointOnPlane)
		{
			return Vector3.Dot(normal, point - pointOnPlane);
		}

		/// <summary>
		/// Finds the initial max points from which to build a hull from. Creates faces from these points.
		/// </summary>
		/// <param name="points">list of points</param>
		/// <returns>true if it found and made the faces, false otherwise.</returns>
		///

		bool FindInitialHull(List<Vector3> points)
		{
			List<int> initialPoints;
			bool initialPointsFound = false;
			// Brain isn't working great right now, so two methods of finding initial points.
			if(FindInitialPoints(points, out initialPoints))
			{
				initialPointsFound = true;
			}
			else if(FindInitialPointsFallBack(points, out initialPoints))
			{
				initialPointsFound = true;
			}
			if(initialPointsFound)
			{
				// we've found 6 valid points xMin,xMax & same for y, z. that are in a 3d point cloud.
				// find the point which is furthest distance from the line defined by the first two points.
				float maxDistance = -Mathf.Infinity;
				int furthestLinePoint = 0;
				Vector3 line = (points[initialPoints[1]] - points[initialPoints[0]]).normalized;
				int furthestIndex = 0;
				for(int i = 2; i < 6; i++)
				{
					float d = DistanceFromLine(points[initialPoints[i]], line, points[initialPoints[0]]);
					if(IsAGreaterThanB(d, maxDistance))
					{
						maxDistance = d;
						furthestLinePoint = initialPoints[i];
						furthestIndex = i;
					}
				}
				// swap the points at the furthest index and the 3rd point.
				initialPoints[furthestIndex] = initialPoints[2];
				initialPoints[2] = furthestLinePoint;

				// find the point which has the largest absolute distance from the plane defined by the first three points.
				maxDistance = -Mathf.Infinity;
				Plane p = new Plane(points[initialPoints[0]], points[initialPoints[1]], points[furthestLinePoint]);
				int furthestPlanePoint = -1;
				for(int i = 2; i < 6; i++)
				{
					if(initialPoints[i] == furthestLinePoint) continue;
					float d = DistanceFromPlane(points[initialPoints[i]], p);
					if(!IsApproxZero(d) && IsAGreaterThanB(Mathf.Abs(d), maxDistance))
					{
						furthestPlanePoint = initialPoints[i];
						maxDistance = d;
						furthestIndex = i;
					}
				}
				// if the furest plane point is still -1, all points are coplanar.
				if(furthestPlanePoint == -1)
				{
					return false;
				}
				// swap the points
				initialPoints[furthestIndex] = initialPoints[3];
				initialPoints[3] = furthestPlanePoint;

				// remember that if the distance from the fourth point was negative, the order of the first three vertices must be reversed.
				if(DistanceFromPlane(points[furthestPlanePoint], p) < 0.0f)
				{
					int i1 = initialPoints[2];
					initialPoints[2] = initialPoints[0];
					initialPoints[0] = i1;
				}

				// add the faces. (Creating a tetrahedron to start.)
				faces.Add(new Face(initialPoints[0], initialPoints[2], initialPoints[1], CalcNormal(points[initialPoints[0]], points[initialPoints[2]], points[initialPoints[1]]), 2, 3, 1));
				faces.Add(new Face(initialPoints[0], initialPoints[1], initialPoints[3], CalcNormal(points[initialPoints[0]], points[initialPoints[1]], points[initialPoints[3]]), 0, 3, 2));
				faces.Add(new Face(initialPoints[0], initialPoints[3], initialPoints[2], CalcNormal(points[initialPoints[0]], points[initialPoints[3]], points[initialPoints[2]]), 1, 3, 0));
				faces.Add(new Face(initialPoints[1], initialPoints[2], initialPoints[3], CalcNormal(points[initialPoints[1]], points[initialPoints[2]], points[initialPoints[3]]), 0, 2, 1));

				unAssignedVertices.UnionWith(Enumerable.Range(0, points.Count));
				// keep track of all vertices that were assigned.
				assignedVertices = new HashSet<int>();
				foreach(Face f in faces)
				{
					AddToOutsideSet(f, unAssignedVertices);
				}
				// remove all vertices that weren't assigned at all, as they are inside or merged, so not part of the convex hull
				// ClosedVertices = new HashSet<int>();
				closedVertices.UnionWith(unAssignedVertices);
				closedVertices.ExceptWith(assignedVertices);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Fallback old method of finding initial points.
		/// </summary>
		/// <param name="points"></param>
		/// <param name="initialPoints"></param>
		/// <returns></returns>
		bool FindInitialPointsFallBack(List<Vector3> points, out List<int> initialPoints)
		{
			List<int> ips = new List<int>(6) { -1, -1, -1, -1, -1, -1 };
			initialPoints = new List<int>(6) { -1, -1, -1, -1, -1, -1 };
			// keep track of points of x,y,z min and max.
			// could just be floats since we're only using them in comparisons and tracking the actual indexs.
			Vector3 xMin, yMin, zMin = yMin = xMin = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
			Vector3 xMax, yMax, zMax = yMax = xMax = new Vector3(-Mathf.Infinity, -Mathf.Infinity, -Mathf.Infinity);
			for(int i = 0; i < points.Count; i++)
			{
				// using epislon make sure the new point is less than the min
				// if they are the same as the current, and they are used in multiple places, replace it with the new one.
				// (otherwise the same point can be (example) both xMin and zMin, and result in the initial faces being coplanar) when there are other points to use.
				if(IsALessThanB(points[i].x, xMin.x) || (IsApproxEqual(points[i].x, xMin.x) && initialPoints.FindAll(element => element == ips[0]).Count > 1))
				{
					// keep track of the index of the point
					initialPoints[0] = i;
					ips[0] = i;
					// set the minimum point.
					xMin = points[i];
				}
				if(IsAGreaterThanB(points[i].x, xMax.x) || (IsApproxEqual(points[i].x, xMax.x) && initialPoints.FindAll(element => element == ips[1]).Count > 1))
				{
					initialPoints[1] = i;
					ips[1] = i;
					xMax = points[i];
				}
				if(IsALessThanB(points[i].y, yMin.y) || (IsApproxEqual(points[i].y, yMin.y) && initialPoints.FindAll(element => element == ips[2]).Count > 1))
				{
					initialPoints[2] = i;
					ips[2] = i;
					yMin = points[i];
				}
				if(IsAGreaterThanB(points[i].y, yMax.y) || (IsApproxEqual(points[i].y, yMax.y) && initialPoints.FindAll(element => element == ips[3]).Count > 1))
				{
					initialPoints[3] = i;
					ips[3] = i;
					yMax = points[i];
				}
				if(IsALessThanB(points[i].z, zMin.z) || (IsApproxEqual(points[i].z, zMin.z) && initialPoints.FindAll(element => element == ips[4]).Count > 1))
				{
					initialPoints[4] = i;
					ips[4] = i;
					zMin = points[i];
				}
				if(IsAGreaterThanB(points[i].z, zMax.z) || (IsApproxEqual(points[i].z, zMax.z) && initialPoints.FindAll(element => element == ips[5]).Count > 1))
				{
					initialPoints[5] = i;
					ips[5] = i;
					zMax = points[i];
				}
			}
			if(!IsApproxEqual(xMin.x, xMax.x) && !IsApproxEqual(yMin.y, yMax.y) && !IsApproxEqual(zMin.z, zMax.z))
			{
				return true;
				// we're good.
			}
			return false;
		}

		/// <summary>
		/// Finds initial 6 points of min/max in x, y, z dimensions.
		/// Now finds the first 4 non-coplanar points + 2 extra points.
		/// </summary>
		/// <param name="points">list of points</param>
		/// <param name="initialPoints">initial pointss list of length 6.</param>
		/// <returns>true if it finds the initial points [xMin, xMax, yMin, yMax, zMin, zMax], false otherwise</returns>
		bool FindInitialPoints(List<Vector3> points, out List<int> initialPoints)
		{
			// just find the first 4 non-coplanar points.
			initialPoints = new List<int>(6) { -1, -1, -1, -1, -1, -1 };
			Vector3 a, b, c, d = a = b = c = Vector3.zero;
			// search 4 consecutive points for a polyhedron with a volume.
			for(int i = 0; i < points.Count; i++)
			{
				if(i + 3 >= points.Count || i + 2 >= points.Count || i + 1 >= points.Count) continue;
				a = points[i];
				b = points[i + 1];
				c = points[i + 2];
				d = points[i + 3];
				// volume = | (a-d) dot ((b-d) cross (c - d)) | / 6.
				float v = Mathf.Abs(Vector3.Dot((a - d), Vector3.Cross((b - d), (c - d)))) / 6;
				// non zero volume = 4 points are not coplanar.
				if(!IsApproxZero(v))
				{
					initialPoints[0] = i;
					initialPoints[1] = i + 1;
					initialPoints[2] = i + 2;
					initialPoints[3] = i + 3;
					if(i + 4 < points.Count)
					{
						initialPoints[4] = i + 4;
					}
					else { initialPoints[4] = i; }
					if(i + 5 < points.Count)
					{
						initialPoints[5] = i + 5;
					}
					else { initialPoints[5] = i; }
					return true;
				}
				else
				{
					// just swap the last point to find a non-coplanar point.
					for(int j = i + 4; j < points.Count; j++)
					{
						d = points[j];
						v = Mathf.Abs(Vector3.Dot((a - d), Vector3.Cross((b - d), (c - d)))) / 6;
						if(!IsApproxZero(v))
						{
							initialPoints[0] = i;
							initialPoints[1] = i + 1;
							initialPoints[2] = i + 2;
							initialPoints[3] = j;
							if(i + 4 < points.Count)
							{
								initialPoints[4] = i + 4;
							}
							else { initialPoints[4] = i; }
							if(i + 5 < points.Count)
							{
								initialPoints[5] = i + 5;
							}
							else { initialPoints[5] = i; }
							return true;
						}
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Given a set of points, calculate an appropriate epislon value for quickhull-ing
		/// </summary>
		/// <param name="points">local space points</param>
		private void CalculateEpsilon(List<Vector3> points)
		{
			// given a set of points determine an appropriate epislon value to use for quickhull.
			// epislon is relative to maximum abs values of x,y,and z.
			// float maxX, maxY, maxZ = maxY = maxX = -Mathf.Infinity;
			Vector3 min = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
			Vector3 max = new Vector3(-Mathf.Infinity, -Mathf.Infinity, -Mathf.Infinity);
			foreach(Vector3 v in points)
			{
				if(v.x < min.x)
				{
					min.x = v.x;
				}
				if(v.y < min.y)
				{
					min.y = v.y;
				}
				if(v.z < min.z)
				{
					min.z = v.z;
				}
				if(v.x > max.x)
				{
					max.x = v.x;
				}
				if(v.y > max.y)
				{
					max.y = v.y;
				}
				if(v.z > max.z)
				{
					max.z = v.z;
				}
			}
			epsilon = Vector3.Distance(min, max) * 0.000001f;
		}

		/// <summary>
		/// Generates a convex hull from a list of local space points.
		/// The resulting mesh is placed in the result variable.
		/// </summary>
		/// <param name="points">List of local space points.</param>
		///
		public void GenerateHull(List<Vector3> points)
		{
			CalculateEpsilon(points);
			verticesList = points;
			if(FindInitialHull(points))
			{
				// while there is a current face on the current hull which has a non-empty outside set of vertices.
				while(HaveNonEmptyFaceSet())// && whileLoopedCount < DebugMaxLoopNumber)
				{
					// clear unassigned vertices.
					unAssignedVertices = new HashSet<int>();
					// clear the current horizon.
					currentHorizon = new List<Horizon>();
					// get the non empty face
					int currFace = GetNonEmptyFaceIndex();
					// find the point on the currFaces' surface which is farthest away from the plane of currFace. this is the eyePoint
					int eyePoint = GetFurthestPointFromFace(currFace);
					// compute horizon of current poly as seen from eyePoint (CALCULATE_HORIZON)
					// this will mark all visible faces as not on the convex hull and place all of their outside set points
					// on the list listUnclaimedVertices. it creates a list HorizonEdges which is
					// a counter clockwise ordered list of edges around the horizon contour of the polyhedron as viewed from the eyepoint.
					CalculateHorizon(eyePoint, null, currFace, true);
					// calculate horizon updates the unassigned vertices, so we need to update the assigned vertices
					assignedVertices.ExceptWith(unAssignedVertices);
					// construct a cone from the eye point to all of the edges of the horizon.
					// start face (used for last valid face that is added.)
					int startFace = faces.Count;
					// end face (used for first valid face that is added.)
					int endFace = faces.Count + currentHorizon.Where(item => item.onConvexHull).ToList().Count - 1;
					// total number of valid valids (used to see if we're currently adding the last valid face.)
					int totalValidHorizons = currentHorizon.Where(item => item.onConvexHull).ToList().Count;
					// reset the new faces list
					newFaces = new List<int>();
					// count the number of valid faces we've added.
					int validHorizonsDone = 0;
					for(int i = 0; i < currentHorizon.Count; i++)
					{
						// makes it easier than always typing currenthorizon[i].
						Horizon h = currentHorizon[i];
						if(!h.onConvexHull) { continue; }

						if(validHorizonsDone == 0)
						{
							// add a new face that has edge v0, v1 and eye point, calculate the normal, the shared edge (v0,v1) face is the the horizon's face.
							// the next face is the next shared edge, the end face is the last face.
							faces.Add(new Face(h.v0, h.v1, eyePoint, CalcNormal(h.v0, h.v1, eyePoint), h.face, faces.Count + 1, endFace));
						}
						else if(validHorizonsDone == totalValidHorizons - 1)
						{
							// this is the last face, the number of horizon faces we've added is the total count of valid faces.
							faces.Add(new Face(h.v0, h.v1, eyePoint, CalcNormal(h.v0, h.v1, eyePoint), h.face, startFace, faces.Count - 1));
						}
						else
						{
							// add previous face, and next face as it's other faces.
							faces.Add(new Face(h.v0, h.v1, eyePoint, CalcNormal(h.v0, h.v1, eyePoint), h.face, faces.Count + 1, faces.Count - 1));
						}
						// keep track of the added face.
						newFaces.Add(faces.Count - 1);
						// update the face of the horizon to share the new edge with the new face.
						UpdateFace(h, faces.Count - 1);

						validHorizonsDone++;
					}
					// We had an error somewhere on the connected face not correctly being set to the correct face but I can't find the source of the bug,
					// and so we are just going to force verification of all of the new faces that were just added.
					// The error was in the recursive find horizon method the whole time.
					// FIX_FACE_VERIFY (So if someone has an issue in the future, they can just uncomment the foreach loop below and it should all work but slower)
					// foreach (int i in NewFaces)
					// {
					//   ForceUpdateFace(i);
					// }

					// All the vertices of removed faces were added to the unassigned vertices list.
					// update the closed vertices list before assigning an unassigned vertex to any face's outside set.
					CloseUnAssignedVertsOnFaces();
					// the remaining unassigned vertices can all be added to the new faces that were created.
					for(int i = 0; i < newFaces.Count; i++)
					{
						// "randomly" assign the points to the new faces outside sets.
						AddToOutsideSet(faces[newFaces[i]], unAssignedVertices);
					}
					// mark vertices as closed that are still unassigned as they are etiher on or in the convex hull
					unAssignedVertices.ExceptWith(assignedVertices);
					closedVertices.UnionWith(unAssignedVertices);
				}
				// create the mesh from the list of faces.
				result = CreateMesh(faces);
			}
			else
			{
				// Removed warning, as it can happen too often when selecting a face.
				// Debug.LogWarning("ColliderEditor: Unable to find initial points, likely because all points lie on the same plane.");
			}
		}

		/// <summary>
		/// gets the verticeslist index of the furthest point in a face's outside set (furthest signed distance)
		/// </summary>
		/// <param name="face">face we want the furthest point from</param>
		/// <returns>index of vertex at a positive signed distance furthest from the face</returns>
		private int GetFurthestPointFromFace(int faceIndex)
		{
			Face face = faces[faceIndex];
			float maxDistance = -Mathf.Infinity;
			int furthestIndex = -1;
			foreach(int i in face.outsideVertices)
			{
				float d = DistanceFromPlane(verticesList[i], face.normal, verticesList[face.v0]);
				if(d > maxDistance)
				{
					furthestIndex = i;
					maxDistance = d;
				}
			}
			return furthestIndex;
		}

		/// <summary>
		/// Gets the index of the first face that has a non-empty outside set.
		/// </summary>
		/// <returns>index of the first face that has a non-empty outside set, -1 if none are found</returns>
		private int GetNonEmptyFaceIndex()
		{
			for(int i = 0; i < faces.Count; i++)
			{
				if(faces[i].outsideVertices.Count > 0)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// Do we have a face with a non-empty outside set?
		/// </summary>
		/// <returns>true if we have a non-empty outside set</returns>
		private bool HaveNonEmptyFaceSet()
		{
			foreach(Face f in faces)
			{
				if(f.outsideVertices.Count > 0)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Checks if a > b by at least epsilon.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns>true a > b</returns>
		private bool IsAGreaterThanB(float a, float b)
		{
			if(a - b > epsilon)
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Checks if a < b by at least epsilon
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns>true if a < b</returns>
		private bool IsALessThanB(float a, float b)
		{
			if(b - a > epsilon)
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Checks if a and b are approximately equal (the difference between them is < epsilon)
		/// </summary>
		/// <param name="a">a</param>
		/// <param name="b">b</param>
		/// <returns>true if they are approximately equal, false otherwise</returns>
		private bool IsApproxEqual(float a, float b)
		{
			return Mathf.Abs(a - b) < epsilon;
		}

		/// <summary>
		/// Checks if value is approximately zero by comparing abs(value) < epsilon.
		/// </summary>
		/// <param name="a">a</param>
		/// <returns>true is a is approximately 0</returns>
		private bool IsApproxZero(float a)
		{
			return Mathf.Abs(a) < epsilon;
		}

		/// <summary>
		/// Updates the faces of a horizon based on the new face created.
		/// The face that was crossed from is no longer on the convex hull and is replaced with the new face in the correct
		/// spot on horizon.Face's F0, F1, or F2.
		/// </summary>
		/// <param name="horizon">horizon edge</param>
		/// <param name="newFace">new face index</param>
		private void UpdateFace(Horizon horizon, int newFace)
		{
			// if the face is on the convex hull
			if(faces[horizon.face].onConvexHull)
			{
				// and f0's was the face we crossed the edge from
				// the edge we crossed from is no longer on the convex hull and is replaced with the new face.
				if(faces[horizon.face].f0 == horizon.from)
				{
					// set that face to the new face
					faces[horizon.face].f0 = newFace;
				}
				else if(faces[horizon.face].f1 == horizon.from)
				{
					faces[horizon.face].f1 = newFace;
				}
				else if(faces[horizon.face].f2 == horizon.from)
				{
					faces[horizon.face].f2 = newFace;
				}
			}
		}
	}

}

