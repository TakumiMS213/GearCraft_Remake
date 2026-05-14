using UnityEngine;

public class GameExitManager : MonoBehaviour
{
    // この関数をボタンに紐付ける
    public void QuitGame()
    {
        Debug.Log("ゲームを終了します");

#if UNITY_EDITOR
        // エディター上では再生を停止
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // ビルド実行時にアプリケーションを終了
        Application.Quit();
#endif
    }
}
