using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Moves focus into a scene-authored panel and restores its opener on close.</summary>
public sealed class PanelFocus : MonoBehaviour
{
    [SerializeField] private Selectable firstSelection;
    [SerializeField] private Selectable fallbackSelection;
    private GameObject previousSelection;

    private void OnEnable()
    {
        EventSystem events = EventSystem.current;
        previousSelection = events != null ? events.currentSelectedGameObject : null;
        // A parent's OnEnable can run before its children's OnEnable.
        // Select now so each Selectable also sees the correct focus when it enables.
        if (events != null && firstSelection != null && firstSelection.interactable)
            firstSelection.Select();
    }

    private void OnDisable()
    {
        EventSystem events = EventSystem.current;
        if (events != null && events.isActiveAndEnabled && !events.alreadySelecting)
        {
            GameObject selected = events.currentSelectedGameObject;
            // Another panel may already have taken focus (for example, inventory).
            if (selected == null || selected.transform.IsChildOf(transform))
            {
                Selectable previous = previousSelection != null ? previousSelection.GetComponent<Selectable>() : null;
                if (previous == null || !previous.IsActive() || !previous.IsInteractable())
                    previous = fallbackSelection;
                events.SetSelectedGameObject(previous != null && previous.IsActive() && previous.IsInteractable()
                    ? previous.gameObject : null);
            }
        }
        previousSelection = null;
    }
}
