namespace Teammate.Gameplay
{
    /// <summary>One active order. Unmanaged struct so it can live in a NetworkList for client-side order-board UI.</summary>
    public struct OrderTicket
    {
        public int OrderId;
        public int RecipeId;
        public double SpawnServerTime;
        public double DeadlineServerTime;
        public bool Fulfilled;
    }

    /// <summary>Outcome of a delivery attempt, for scoring and logging.</summary>
    public enum DeliveryResult
    {
        OnTime,
        Late,
        NoMatchingOrder,
    }
}
