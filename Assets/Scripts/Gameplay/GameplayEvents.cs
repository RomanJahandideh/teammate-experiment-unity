using System;

namespace Teammate.Gameplay
{
    /// <summary>
    /// Server-side-only event bus: every station raises this once it has validated and
    /// applied a player's action (pickup, chop/cook place-or-collect, plate, deliver,
    /// discard). PressureEventScheduler and IdleTracker both listen here rather than to
    /// each station individually, so adding a new station type doesn't require touching
    /// them.
    /// </summary>
    public static class GameplayEvents
    {
        public static event Action<ulong> OnPlayerAction;

        public static void RaisePlayerAction(ulong clientId) => OnPlayerAction?.Invoke(clientId);
    }
}
