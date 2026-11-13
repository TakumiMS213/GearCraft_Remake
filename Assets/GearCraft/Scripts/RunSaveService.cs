using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

[DefaultExecutionOrder(-1200)]
public sealed class RunSaveService : MonoBehaviour
{
    public const string EndedRunKey = "GearCraft.RunSave.EndedRunId";
    [SerializeField] private RunSaveAsset[] assets;
    [SerializeField] private StageGeneratorSO stageConfig;
    public static RunSaveService Instance { get; private set; }
    public event Action Changed;
    public bool IsSaving { get; private set; }
    public string LastError { get; private set; }
    public string RunId { get; private set; }
    public int Seed { get; private set; }
    public int ShopReroll { get; private set; }
    private int shopDay;
    private long revision;
    private int epoch;
    private string failedPayload;
    private RunSaveCodec codec;
    private RunSaveStore store;
    private Task pendingSave = Task.CompletedTask;
    public Task PendingSave => pendingSave;
    public int GetDayNumber(RunSaveData data) => (data.stage - 1) / stageConfig.stagesPerRest + 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        codec = new RunSaveCodec(assets);
        store = new RunSaveStore(Path.Combine(Application.persistentDataPath, "AutoSave"));
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Notify() { Changed?.Invoke(); }
    public static string DescribeError(Exception error) => error is IOException || error is UnauthorizedAccessException
        ? "保存先に書き込めません。空き容量とアクセス権を確認してください。" : error.Message;

    public async Task<bool> BeginRunAsync()
    {
        int operation = ++epoch;
        try
        {
            string id = Guid.NewGuid().ToString("N");
            int seed = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
            await store.BeginAsync(id, seed);
            if (operation != epoch) return false;
            RunId = id; Seed = seed; revision = 0; ShopReroll = 0; shopDay = 0;
            PlayerPrefs.DeleteKey(EndedRunKey); PlayerPrefs.Save();
            failedPayload = null; LastError = null; IsSaving = false; Notify();
            pendingSave = Task.CompletedTask;
            return true;
        }
        catch (Exception ex) { LastError = "新しい周回を開始できません。\n" + DescribeError(ex); Notify(); return false; }
    }

    public void EnterShopDay(int day)
    {
        if (shopDay == day) return;
        shopDay = day; ShopReroll = 0;
    }
    public void AdvanceShopReroll() { ShopReroll++; }

    public sealed class LoadInfo
    {
        public RunSaveData data;
        public string error;
        public bool CanContinue => data != null && error == null;
    }
    public async Task<LoadInfo> InspectAsync()
    {
        await pendingSave;
        var disk = await store.ReadAsync();
        if (!disk.HasSave) return new LoadInfo { error = disk.error == null ? null :
            "セーブデータを読み込めません。\n保存先の状態、データの破損、対応するバージョンを確認してください。" };
        if (disk.manifest.runId == PlayerPrefs.GetString(EndedRunKey, "")) return new LoadInfo();
        try
        {
            var data = JsonConvert.DeserializeObject<RunSaveData>(disk.payload);
            if (data == null || data.runId != disk.manifest.runId || data.seed != disk.manifest.seed)
                throw new InvalidDataException("周回情報が一致しません。");
            codec.Validate(data, stageConfig);
            return new LoadInfo { data = data };
        }
        catch (Exception ex) { return new LoadInfo { error = "続きから再開できません。\n" + ex.Message }; }
    }
    public async Task<bool> LoadRunAsync()
    {
        var info = await InspectAsync();
        if (!info.CanContinue) { LastError = info.error ?? "再開できるセーブがありません。"; Notify(); return false; }
        try
        {
            var values = codec.Validate(info.data, stageConfig);
            if (StatusManager.Instance == null || MaterialManager.Instance == null || StageCounter.Instance == null)
                throw new InvalidOperationException("再開に必要な管理データがありません。");
            FindFirstObjectByType<PlayerController>()?.CancelTemporaryCombatEffects();
            // Discard a retained Craft grid before it can overwrite the restored placement list.
            UpgradeGridManager.Instance?.DiscardRuntimeState();
            codec.Restore(info.data, values, StatusManager.Instance, MaterialManager.Instance);
            RunId = info.data.runId; revision = info.data.revision; Seed = info.data.seed;
            shopDay = StatusManager.Instance.upgradeShopDay; ShopReroll = info.data.shopReroll;
            failedPayload = null; LastError = null; ++epoch;
            StoryPlaybackRequest.ClearRequest();
            Time.timeScale = 1f;
            Notify();
            SceneTransitionManager.LoadSceneWithTransition("CraftSpace");
            return true;
        }
        catch (Exception ex) { LastError = "再開できませんでした。\n" + DescribeError(ex); Notify(); return false; }
    }

    public void SaveDayEnd()
    {
        if (string.IsNullOrEmpty(RunId)) return;
        try
        {
            FindFirstObjectByType<PlayerController>()?.CancelTemporaryCombatEffects();
            var data = codec.Capture(StatusManager.Instance, MaterialManager.Instance, RunId, ++revision, Seed, ShopReroll);
            codec.Validate(data, stageConfig);
            // Serialization completes on the main thread, before the player can change state.
            string payload = JsonConvert.SerializeObject(data);
            pendingSave = SavePayloadAsync(payload, data.runId, data.revision, epoch);
        }
        catch (Exception ex) { LastError = "保存できませんでした。\n" + DescribeError(ex); Notify(); }
    }
    private async Task SavePayloadAsync(string payload, string runId, long number, int operation)
    {
        IsSaving = true; LastError = null; Notify();
        try
        {
            bool written = await store.WriteAsync(runId, number, payload);
            if (!written && operation == epoch) throw new InvalidOperationException("この周回は保存できません。");
            if (operation == epoch) { failedPayload = null; LastError = null; }
        }
        catch (Exception ex)
        {
            if (operation == epoch)
            { failedPayload = payload; LastError = "保存できませんでした。\n" + DescribeError(ex); }
        }
        finally { if (operation == epoch) { IsSaving = false; Notify(); } }
    }
    public void RetrySave()
    {
        if (IsSaving || failedPayload == null || string.IsNullOrEmpty(RunId)) return;
        var data = JsonConvert.DeserializeObject<RunSaveData>(failedPayload);
        pendingSave = SavePayloadAsync(failedPayload, data.runId, data.revision, epoch);
    }
    public bool CanRetry => failedPayload != null && !IsSaving;

    public async Task InvalidateRunAsync()
    {
        // A second tombstone prevents a failed file replacement from reviving a finished run.
        string error = null;
        try
        {
            if (!string.IsNullOrEmpty(RunId))
            { PlayerPrefs.SetString(EndedRunKey, RunId); PlayerPrefs.Save(); }
        }
        catch (Exception ex) { error = DescribeError(ex); }
        ++epoch; RunId = null; failedPayload = null; IsSaving = false;
        try { await store.InvalidateAsync(); }
        catch (Exception ex) { error = DescribeError(ex); }
        LastError = error == null ? null : "終了した周回の保存情報を無効化できませんでした。\n" + error;
        pendingSave = Task.CompletedTask;
        Notify();
    }
    public async Task DeleteAllAsync()
    {
        ++epoch; RunId = null; failedPayload = null; IsSaving = false; Notify();
        await store.DeleteAllAsync();
        PlayerPrefs.DeleteKey(EndedRunKey); PlayerPrefs.Save();
        revision = 0; Seed = 0; ShopReroll = 0; shopDay = 0;
        pendingSave = Task.CompletedTask;
        LastError = null; Notify();
    }
}
