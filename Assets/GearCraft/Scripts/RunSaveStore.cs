using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

// No Unity calls: all disk operations run through one serialized queue.
public sealed class RunSaveStore
{
    public sealed class Manifest
    {
        public int version = 1;
        public string runId;
        public int seed;
        public bool ended;
    }
    private sealed class Envelope
    {
        public int version = 1;
        public string runId;
        public long revision;
        public string savedUtc;
        public string payload;
        public string checksum;
    }
    public sealed class ReadResult
    {
        public Manifest manifest;
        public string payload;
        public string error;
        public bool HasSave => payload != null;
    }
    private readonly string directory;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private int epoch;
    public RunSaveStore(string directory) { this.directory = directory; }
    private string PathFor(string name) => Path.Combine(directory, name);

    private static string Hash(string text)
    {
        using (var sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }
    private static void AtomicWrite(string path, string content, string backup = null)
    {
        string temporary = path + ".tmp";
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        if (File.Exists(path)) File.Replace(temporary, path, backup);
        else File.Move(temporary, path);
    }
    private Manifest ReadManifest()
    {
        string path = PathFor("run-state.json");
        if (!File.Exists(path)) return null;
        var value = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(path));
        if (value == null || value.version != 1 || string.IsNullOrEmpty(value.runId))
            throw new InvalidDataException("周回管理データを読み込めません。");
        return value;
    }
    private Envelope ReadEnvelope(string path, string runId)
    {
        var value = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(path));
        if (value != null && value.runId != runId) return null; // A committed new run has no checkpoint yet.
        if (value != null && value.version > 1)
            throw new NotSupportedException("新しいバージョンのセーブです。");
        if (value == null || value.version != 1 || value.runId != runId || value.revision < 1 ||
            !DateTime.TryParse(value.savedUtc, out _) || value.payload == null || value.checksum != Hash(value.payload))
            throw new InvalidDataException("セーブデータが破損しています。");
        return value;
    }
    public Task<ReadResult> ReadAsync() => Locked(() =>
    {
        var result = new ReadResult();
        try
        {
            result.manifest = ReadManifest();
            if (result.manifest == null || result.manifest.ended) return result;
            foreach (string name in new[] { "run-save.json", "run-save.bak" })
            {
                string path = PathFor(name);
                if (!File.Exists(path)) continue;
                try
                {
                    var e = ReadEnvelope(path, result.manifest.runId);
                    if (e == null) continue;
                    result.payload = e.payload;
                    result.error = null;
                    return result;
                }
                catch (NotSupportedException) { throw; }
                catch (Exception ex) { result.error = ex.Message; }
            }
        }
        catch (Exception ex) { result.error = ex.Message; }
        return result;
    });

    public Task BeginAsync(string runId, int seed)
    {
        Interlocked.Increment(ref epoch);
        return Locked(() =>
        {
            Directory.CreateDirectory(directory);
            AtomicWrite(PathFor("run-state.json"), JsonConvert.SerializeObject(new Manifest { runId = runId, seed = seed }));
            return true;
        });
    }
    public Task<bool> WriteAsync(string runId, long revision, string payload)
    {
        int requestedEpoch = Volatile.Read(ref epoch);
        return Locked(() =>
        {
            if (requestedEpoch != Volatile.Read(ref epoch)) return false;
            var manifest = ReadManifest();
            if (manifest == null || manifest.ended || manifest.runId != runId) return false;
            string primary = PathFor("run-save.json");
            string backup = null;
            if (File.Exists(primary))
            {
                try { if (ReadEnvelope(primary, runId) != null) backup = PathFor("run-save.bak"); }
                catch (NotSupportedException) { throw; }
                catch (Exception) { /* Preserve the healthy backup used to recover a corrupt primary. */ }
            }
            var record = new Envelope { runId = runId, revision = revision,
                savedUtc = DateTime.UtcNow.ToString("O"), payload = payload, checksum = Hash(payload) };
            AtomicWrite(primary, JsonConvert.SerializeObject(record), backup);
            return true;
        });
    }
    public Task InvalidateAsync()
    {
        Interlocked.Increment(ref epoch);
        return Locked(() =>
        {
            var manifest = ReadManifest();
            if (manifest != null)
            { manifest.ended = true; AtomicWrite(PathFor("run-state.json"), JsonConvert.SerializeObject(manifest)); }
            return true;
        });
    }
    public Task DeleteAllAsync()
    {
        Interlocked.Increment(ref epoch);
        return Locked(() =>
        {
            // Keep the ended marker until every checkpoint and temporary file is gone.
            Directory.CreateDirectory(directory);
            AtomicWrite(PathFor("run-state.json"), JsonConvert.SerializeObject(new Manifest { runId = "reset", ended = true }));
            foreach (string name in new[] { "run-save.json", "run-save.bak", "run-save.json.tmp", "run-state.json.tmp" })
                if (File.Exists(PathFor(name))) File.Delete(PathFor(name));
            File.Delete(PathFor("run-state.json"));
            return true;
        });
    }
    private async Task<T> Locked<T>(Func<T> operation)
    {
        await gate.WaitAsync();
        try { return await Task.Run(operation); }
        finally { gate.Release(); }
    }
}
