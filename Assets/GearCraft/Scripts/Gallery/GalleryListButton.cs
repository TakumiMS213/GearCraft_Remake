using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GearCraft.Gallery
{
    /// <summary>Preview and scroll to an entry when keyboard/controller focus changes.</summary>
    public sealed class GalleryListButton : Button
    {
        public Action Focused;

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (IsInteractable()) Focused?.Invoke();
        }
    }
}
