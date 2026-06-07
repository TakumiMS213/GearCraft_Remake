public class ArrivalMessageModel
{
    private readonly ArrivalMessageSequenceData sequence;
    private int currentIndex = -1;

    public ArrivalMessageModel(ArrivalMessageSequenceData sequence)
    {
        this.sequence = sequence;
    }

    public bool TryMoveNext(out ArrivalMessagePageData page)
    {
        currentIndex++;
        page = sequence == null ? null : sequence.GetPage(currentIndex);
        return page != null;
    }
}
