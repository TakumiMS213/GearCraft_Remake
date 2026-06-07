using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StorySequenceData", menuName = "GearCraft/Story/Story Sequence")]
public class StorySequenceData : ScriptableObject
{
    [SerializeField] private StoryPageData[] pages = Array.Empty<StoryPageData>();

    public int PageCount => pages == null ? 0 : pages.Length;

    public StoryPageData GetPage(int index)
    {
        if (pages == null || index < 0 || index >= pages.Length)
        {
            return null;
        }

        return pages[index];
    }
}

[Serializable]
public class StoryPageData
{
    [SerializeField] private Sprite image;
    [TextArea(2, 5)]
    [SerializeField] private string text;
    [SerializeField, Min(0.1f)] private float autoAdvanceSeconds = 4f;

    public Sprite Image => image;
    public string Text => text;
    public float AutoAdvanceSeconds => autoAdvanceSeconds;
}
