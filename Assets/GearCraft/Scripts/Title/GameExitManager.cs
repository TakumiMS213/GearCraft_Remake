using UnityEngine;

public class GameExitManager : MonoBehaviour
{
    // この関数をボタンに紐付ける
    public void QuitGame()
    {
        ApplicationQuitter.QuitGame();
    }
}
