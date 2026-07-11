namespace Doggiehood.Core.Art
{
    /// <summary>
    /// The low-poly art budget (#6). CI's EditMode sweep asserts every mesh
    /// under the art asset folders stays within this budget.
    /// </summary>
    public static class ArtBudget
    {
        public const int MaxTrianglesPerMesh = 2000;

        public static bool IsWithinBudget(int triangleCount)
        {
            return triangleCount <= MaxTrianglesPerMesh;
        }
    }
}
