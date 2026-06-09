using UnityEngine;

public class StorySceneLoader : MonoBehaviour
{
    [SerializeField] private SceneTransitionManager sceneTransitionManager;
    [SerializeField] private string storySceneName = "Story";
    [SerializeField] private string returnSceneName = "CraftSpace";
    [SerializeField] private bool requestArrivalMessage = true;

    public void LoadStory()
    {
        LoadStory(returnSceneName);
    }

    public void LoadStory(string nextSceneName)
    {
        StoryPlaybackRequest.SetReturnScene(nextSceneName, requestArrivalMessage);

        if (sceneTransitionManager != null)
        {
            sceneTransitionManager.LoadScene(storySceneName);
            return;
        }

        SceneTransitionManager.LoadSceneWithTransition(storySceneName);
    }
}
