using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-700)]
public sealed class RunSaveUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject indicator;
    [SerializeField] private RectTransform gear;
    [SerializeField] private TMP_Text saveText;
    [SerializeField] private GameObject failureNotice;
    [SerializeField] private TMP_Text failureText;
    [SerializeField] private Button retryButton;
    [SerializeField] private GameObject dialog;
    [SerializeField] private RectTransform window;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newButton;
    [SerializeField] private Button backButton;
    public static RunSaveUI Instance { get; private set; }
    private static int consumedFrame = -1;
    public static bool BlocksGameplay => Instance != null && Instance.dialog.activeSelf || consumedFrame == Time.frameCount;
    public bool IsOpen => dialog.activeSelf;
    private float saveStarted = -100f;
    private int request;
    private bool busy;
    private Action onContinue, onNew, onBack;
    private GameObject previousSelection;
    private RunSaveService service;

    private void Awake()
    {
        if (Instance != null && Instance != this) return;
        Instance = this;
        indicator.SetActive(false); failureNotice.SetActive(false); dialog.SetActive(false);
        continueButton.onClick.AddListener(() => { if (!busy) { consumedFrame = Time.frameCount; onContinue?.Invoke(); } });
        newButton.onClick.AddListener(() => { if (!busy) { consumedFrame = Time.frameCount; onNew?.Invoke(); } });
        backButton.onClick.AddListener(CloseDialog);
        retryButton.onClick.AddListener(() => { consumedFrame = Time.frameCount; service.RetrySave(); });
    }
    private void Start()
    {
        if (Instance != this) return;
        service = RunSaveService.Instance;
        service.Changed += Refresh;
        Refresh();
    }
    private void OnDestroy()
    {
        if (service != null) service.Changed -= Refresh;
        if (Instance == this) Instance = null;
    }
    private void Refresh()
    {
        if (service.IsSaving && !indicator.activeSelf) { saveStarted = Time.unscaledTime; indicator.SetActive(true); }
        if (service.RunId == null && !service.IsSaving) indicator.SetActive(false);
        failureNotice.SetActive(!service.IsSaving && !string.IsNullOrEmpty(service.LastError));
        failureText.text = string.IsNullOrEmpty(service.LastError) ? "" : service.LastError;
        retryButton.gameObject.SetActive(service.CanRetry);
    }
    private void Update()
    {
        if (Instance != this || service == null) return;
        if (IsOpen && SceneManager.GetActiveScene().name != "Title" && Input.GetKeyDown(KeyCode.Escape))
        { CloseDialog(); return; }
        if (failureNotice.activeInHierarchy && retryButton.gameObject.activeInHierarchy && Input.GetMouseButtonDown(0) &&
            RectTransformUtility.RectangleContainsScreenPoint(retryButton.transform as RectTransform, Input.mousePosition, null))
            consumedFrame = Time.frameCount;
        if (indicator.activeSelf)
        {
            float elapsed = Time.unscaledTime - saveStarted;
            gear.Rotate(0f, 0f, -90f * Time.unscaledDeltaTime);
            saveText.text = "SAVE" + new string('.', (int)(elapsed / 0.35f) % 3 + 1);
            saveText.alpha = elapsed % 1f < 0.5f ? 1f : 0f;
            if (!service.IsSaving && elapsed >= 1.2f) indicator.SetActive(false);
        }
        if (dialog.activeSelf && SceneManager.GetActiveScene().name != "Title" && onNew != null) CloseDialog();
        if (dialog.activeSelf && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            backButton.Select();
    }
    public async void RequestStart(StartingWeaponSelectionController source)
    {
        if (IsOpen) return;
        int token = ++request;
        OpenDialog("セーブデータを確認しています。", null, null, null);
        busy = true; continueButton.interactable = newButton.interactable = false;
        var info = await service.InspectAsync();
        if (token != request || source == null || SceneManager.GetActiveScene().name != "Title") return;
        busy = false;
        if (!info.CanContinue && info.error == null)
        { CloseDialog(); source.StartNewGame(); return; }
        message.text = info.CanContinue
            ? $"続きから再開しますか？\n次はDAY{service.GetDayNumber(info.data)}・宿舎から再開します。\n出撃を確定すると、現在のセーブは失われます。"
            : info.error;
        continueButton.interactable = info.CanContinue;
        newButton.interactable = true;
        onContinue = async () =>
        {
            busy = true; continueButton.interactable = newButton.interactable = backButton.interactable = false;
            bool loaded = await service.LoadRunAsync();
            busy = false; backButton.interactable = newButton.interactable = true;
            if (loaded) CloseDialog();
            else { message.text = service.LastError; continueButton.interactable = false; ConfigureNavigation(); backButton.Select(); }
        };
        onNew = () => { CloseDialog(); source.StartNewGame(); };
        ConfigureNavigation();
        backButton.Select();
    }
    private void OpenDialog(string text, Action yes, Action create, Action back)
    {
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        message.text = text; onContinue = yes; onNew = create; onBack = back;
        busy = false;
        continueButton.GetComponentInChildren<TMP_Text>().text = "続きから";
        newButton.GetComponentInChildren<TMP_Text>().text = "新しく始める";
        backButton.GetComponentInChildren<TMP_Text>().text = "戻る";
        continueButton.gameObject.SetActive(true); newButton.gameObject.SetActive(true);
        continueButton.interactable = newButton.interactable = backButton.interactable = true;
        dialog.SetActive(true); backButton.Select();
    }
    public void ConfirmUnsavedExit(Action exit, Action cancel)
    {
        OpenDialog("保存できませんでした。\n保存せず終了しますか？\n最後のDAYの進行は保存されません。", () => { CloseDialog(false); exit(); }, null, cancel);
        continueButton.GetComponentInChildren<TMP_Text>().text = "保存せず終了";
        newButton.gameObject.SetActive(false);
        ConfigureNavigation();
    }
    private void ConfigureNavigation()
    {
        var buttons = new System.Collections.Generic.List<Button>();
        foreach (var button in new[] { continueButton, newButton, backButton })
            if (button.gameObject.activeSelf && button.interactable) buttons.Add(button);
        for (int i = 0; i < buttons.Count; i++)
        {
            var previous = buttons[(i + buttons.Count - 1) % buttons.Count];
            var next = buttons[(i + 1) % buttons.Count];
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = previous, selectOnLeft = previous, selectOnDown = next, selectOnRight = next };
        }
    }
    public bool TryHandleEscape()
    {
        if (!IsOpen) return consumedFrame == Time.frameCount;
        CloseDialog(); return true;
    }
    public void CloseDialog() { CloseDialog(true); }
    private void CloseDialog(bool notify)
    {
        if (!IsOpen || busy && !backButton.interactable) return;
        ++request; consumedFrame = Time.frameCount;
        dialog.SetActive(false); busy = false;
        Action back = onBack; onBack = null; onNew = null; onContinue = null;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        if (notify) back?.Invoke();
    }
    public void OnPointerClick(PointerEventData data)
    {
        if (IsOpen && data.button == PointerEventData.InputButton.Left &&
            !RectTransformUtility.RectangleContainsScreenPoint(window, data.position, data.pressEventCamera)) CloseDialog();
    }
}
