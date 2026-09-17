namespace Teammate.Gameplay
{
    /// <summary>Generic, non-branded ingredient categories for the cooperative cooking task.</summary>
    public enum IngredientType : byte
    {
        None = 0,
        Greens = 1,
        Protein = 2,
        Starch = 3,
    }

    /// <summary>What state a single carried/cooking item is in along the prep → cook → plate chain.</summary>
    public enum ItemKind : byte
    {
        None = 0,
        RawIngredient = 1,
        PreppedIngredient = 2,
        CookedIngredient = 3,
        BurnedIngredient = 4,
        PlatedDish = 5,
    }

    /// <summary>
    /// Whatever a player is currently holding, or the contents of a station slot. Plain
    /// unmanaged struct (no reference types) so it can go directly into a NetworkVariable
    /// without a custom serializer.
    /// </summary>
    public struct CarriedItem
    {
        public ItemKind Kind;
        public IngredientType Ingredient;
        public int RecipeId; // meaningful only when Kind == PlatedDish

        public static readonly CarriedItem Empty = new CarriedItem { Kind = ItemKind.None, Ingredient = IngredientType.None, RecipeId = -1 };

        public bool IsEmpty => Kind == ItemKind.None;
    }
}
