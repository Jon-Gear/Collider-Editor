namespace ColliderEditor
{

  /// <summary>
  /// Capsule method to use when creating a capsule
  /// </summary>
  public enum CAPSULE_COLLIDER_METHOD
  {
    BestFit,
    MinMax,
    MinMaxPlusRadius,
    MinMaxPlusDiameter,
  }

  /// <summary>
  /// Type of collider to create
  /// </summary>
  public enum CREATE_COLLIDER_TYPE
  {
    BOX,
    ROTATED_BOX,
    SPHERE,
    CAPSULE,
    ROTATED_CAPSULE,
    CONVEX_MESH,
    CYLINDER,
  }

  /// <summary>
  /// Sphere method to use when creating a sphere
  /// </summary>
  public enum SPHERE_COLLIDER_METHOD
  {
    BestFit,
    Distance,
    MinMax,
  }
}
