using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HoverPriorityButton : Button
{
    private bool pointerInside;

    public override void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        base.OnPointerEnter(eventData);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        base.OnPointerExit(eventData);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // Keyboard focus remains selected; hover changes only its visual state.
        if (state == SelectionState.Selected && pointerInside)
            state = SelectionState.Highlighted;
        base.DoStateTransition(state, instant);
    }

    protected override void OnDisable()
    {
        pointerInside = false;
        base.OnDisable();
    }
}
