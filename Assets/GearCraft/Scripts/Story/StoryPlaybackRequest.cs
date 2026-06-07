public static class StoryPlaybackRequest
{
    private const string DefaultReturnSceneName = "CraftSpace";

    public static string ReturnSceneName { get; private set; } = DefaultReturnSceneName;
    public static bool HasArrivalMessageRequest { get; private set; }

    public static void SetReturnScene(string sceneName, bool requestArrivalMessage)
    {
        ReturnSceneName = string.IsNullOrWhiteSpace(sceneName) ? DefaultReturnSceneName : sceneName;
        HasArrivalMessageRequest = requestArrivalMessage;
    }

    public static bool ConsumeArrivalMessageRequest()
    {
        bool requested = HasArrivalMessageRequest;
        HasArrivalMessageRequest = false;
        return requested;
    }

    public static void ClearReturnScene()
    {
        ReturnSceneName = DefaultReturnSceneName;
    }
}
