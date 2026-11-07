using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Notices are serialized TextAssets, so the Player includes them and works offline.
public sealed class TitleLicenseController : MonoBehaviour
{
    [SerializeField] private TextAsset[] notices;
    [SerializeField] private GameObject dialog;
    [SerializeField] private Button mailbox, closeButton, previous, next;
    [SerializeField] private TMP_Text body, pageLabel;
    [SerializeField] private ScrollRect scroll;
    private int page;
    private readonly Dictionary<Selectable, bool> savedInteraction = new Dictionary<Selectable, bool>();
    public bool IsOpen => dialog != null && dialog.activeSelf;

    private void Awake() => dialog.SetActive(false);

    // The scene owns every UI object and persistent Button event.
    public void PreviousPage() => ShowPage(page - 1);
    public void NextPage() => ShowPage(page + 1);

    public void Open()
    {
        if (IsOpen) return;
        // Prevent keyboard navigation and pointer input from reaching the title menu.
        savedInteraction.Clear();
        foreach (var selectable in GetComponentsInChildren<Selectable>(true))
        {
            if (selectable.transform.IsChildOf(dialog.transform)) continue;
            savedInteraction[selectable] = selectable.interactable;
            selectable.interactable = false;
        }
        dialog.SetActive(true);
        ShowPage(0);
        closeButton.Select();
    }

    private void ShowPage(int index)
    {
        int count = notices == null ? 0 : notices.Length;
        page = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        body.text = count > 0 && notices[page] != null ? notices[page].text : "ライセンス文書を読み込めませんでした。";
        pageLabel.text = count > 0 ? $"{page + 1} / {count}   スクロールして全文を表示" : "";
        previous.interactable = page > 0;
        next.interactable = page + 1 < count;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        // Selecting a disabled page button would strand keyboard navigation.
        closeButton.Select();
    }

    public void Close()
    {
        if (dialog != null) dialog.SetActive(false);
        foreach (var entry in savedInteraction)
            if (entry.Key != null) entry.Key.interactable = entry.Value;
        savedInteraction.Clear();
        if (mailbox != null && mailbox.gameObject.activeInHierarchy) mailbox.Select();
    }

    public bool TryCloseDialog()
    {
        if (!IsOpen) return false;
        Close();
        return true;
    }

    private void Update()
    {
        if (!IsOpen) return;
        float height = Mathf.Max(1, scroll.content.rect.height - scroll.viewport.rect.height);
        if (Input.GetKeyDown(KeyCode.PageDown)) scroll.verticalNormalizedPosition -= scroll.viewport.rect.height / height;
        if (Input.GetKeyDown(KeyCode.PageUp)) scroll.verticalNormalizedPosition += scroll.viewport.rect.height / height;
        if (Input.GetKeyDown(KeyCode.Home)) scroll.verticalNormalizedPosition = 1;
        if (Input.GetKeyDown(KeyCode.End)) scroll.verticalNormalizedPosition = 0;
    }

    private void OnDisable() { if (IsOpen) Close(); }
}
