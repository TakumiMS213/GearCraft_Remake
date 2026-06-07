public class StoryPlaybackModel
{
    private readonly StorySequenceData sequence;
    private int currentIndex = -1;

    public StoryPlaybackModel(StorySequenceData sequence)
    {
        this.sequence = sequence;
    }

    public bool TryMoveNext(out StoryPageData page)
    {
        currentIndex++;
        page = sequence == null ? null : sequence.GetPage(currentIndex);
        return page != null;
    }
}
