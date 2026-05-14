using UnityEngine;

public class PauseOnPanelActive : MonoBehaviour
{
    public GameObject targetPanel;  // 対象となるUIパネル

    private bool isPaused = false;

    void Update()
    {
        if (targetPanel == null) return;

        if (targetPanel.activeSelf && !isPaused)
        {
            Time.timeScale = 0f;  // ゲームを停止
            isPaused = true;
        }
        else if (!targetPanel.activeSelf && isPaused)
        {
            Time.timeScale = 1f;  // ゲームを再開
            isPaused = false;
        }
    }
}
