using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StartingWeaponSettingsController : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private GameObject warningWindow;

    private void OnEnable()
    {
        RefreshState();
        warningWindow.SetActive(false);
    }

    public void ToggleSelection()
    {
        StartingWeaponProgress.SetSelectionEnabled(!StartingWeaponProgress.IsSelectionEnabled);
        RefreshState();
        warningWindow.SetActive(true);
    }

    private void RefreshState()
    {
        bool enabled = StartingWeaponProgress.IsSelectionEnabled;
        stateLabel.text = enabled ? "ON" : "OFF";
        toggleButton.targetGraphic.color = enabled
            ? new Color(0.5f, 0.36f, 0.13f, 1f)
            : new Color(0.16f, 0.16f, 0.15f, 1f);
    }

    public void RefreshAfterReset()
    {
        RefreshState();
        warningWindow.SetActive(false);
    }
}
