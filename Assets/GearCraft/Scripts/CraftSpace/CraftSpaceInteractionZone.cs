using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CraftSpaceInteractionZone : MonoBehaviour
{
    [SerializeField] private CraftSpaceInteractionAction actionType;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private UIRangeDisplay interactionController;

    public CraftSpaceInteractionAction ActionType => actionType;
    public GameObject PromptObject => promptObject;

    private void Awake()
    {
        Collider2D zoneCollider = GetComponent<Collider2D>();
        zoneCollider.isTrigger = true;

        if (interactionController == null)
        {
            interactionController = FindFirstObjectByType<UIRangeDisplay>();
        }

        SetPromptActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        interactionController?.RegisterZone(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        interactionController?.UnregisterZone(this);
    }

    public void SetPromptActive(bool active)
    {
        if (promptObject != null)
        {
            promptObject.SetActive(active);
        }
    }
}
