using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>
    /// One deliverable dish: a single cooked ingredient of a given type, plated. Kept to
    /// one ingredient per dish deliberately — the paper never specifies recipe complexity,
    /// and a multi-ingredient crafting graph would be scope well past "connect the cooking
    /// loop and score it." The full pickup → chop → cook → plate → deliver chain is still
    /// exercised end to end per dish.
    /// </summary>
    [CreateAssetMenu(fileName = "DishRecipe", menuName = "Teammate/Dish Recipe")]
    public class DishRecipe : ScriptableObject
    {
        public int recipeId;
        public string displayName = "Dish";
        public IngredientType requiredIngredient = IngredientType.Greens;
        [Tooltip("Seconds a delivered order is allowed to sit before it counts as late.")]
        public float deliveryWindowSeconds = 45f;
    }
}
