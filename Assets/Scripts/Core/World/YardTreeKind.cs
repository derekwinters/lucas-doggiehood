namespace Doggiehood.Core.World
{
    /// <summary>Which imported CC0 kit model a yard landscaping pick renders
    /// as (#170): the two City Kit Suburban tree pieces staged under
    /// Assets/Art/Houses/CityKitSuburban/Resources/. (The planter kind was
    /// removed in #243 — it placed oddly in the yard, so the whole code path
    /// is gone and only the two trees remain.)</summary>
    public enum YardTreeKind
    {
        TreeLarge,
        TreeSmall,
    }
}
