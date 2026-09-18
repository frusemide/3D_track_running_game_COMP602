// Contract for anything the player can interact with in the world (practice start,
// shop, race setup, etc.). Each interactable defines its own prompt text and what
// happens when interacted with. The PlayerInteractor handles detection and input;
// implementers only define behaviour.
public interface IInteractable
{
    // Shown to the player when this interactable is in focus (e.g. "Press E to practice").
    string GetPrompt();

    // Called when the player triggers interaction while this is in focus.
    void Interact(Player player);
}
