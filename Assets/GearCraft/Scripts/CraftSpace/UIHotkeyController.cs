using UnityEngine;

public class UIHotkeyController : MonoBehaviour
{
    public UISwitcher uiSwitcher;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (uiSwitcher != null)
            {
                uiSwitcher.ShowPanel();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (uiSwitcher != null)
            {
                uiSwitcher.HidePanel();
            }
        }
    }
}
