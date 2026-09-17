namespace Teammate.Gameplay
{
    /// <summary>
    /// Common client-side entry point for every world station (ingredient source,
    /// chopping board, pot, plating counter, delivery counter). PlayerStationInteractor
    /// calls this on whichever station is nearest when the local player presses Interact;
    /// each station's implementation forwards to its own ServerRpc, so the server always
    /// knows both the caller (RPC sender) and the station (the RPC's target object)
    /// without passing player references over the network by hand.
    /// </summary>
    public interface IInteractableStation
    {
        void ClientRequestInteract();

        /// <summary>Short label for on-screen prompts, e.g. "Pick up Greens", "Chop", "Deliver".</summary>
        string InteractPrompt { get; }
    }
}
