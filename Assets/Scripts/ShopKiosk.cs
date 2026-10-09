using UnityEngine;

// World interactable that opens the shop. Put it on the kiosk object (or its parent),
// on the "Interactable" layer, with a collider for PlayerInteractor's spherecast to hit.
public class ShopKiosk : MonoBehaviour, IInteractable
{
    [SerializeField] private ShopMenu _shopMenu;

    public string GetPrompt() => "Press E to open shop";

    public void Interact(Player player)
    {
        if (_shopMenu != null)
            _shopMenu.Open(player.GetComponent<PlayerCustomisation>());
    }
}
