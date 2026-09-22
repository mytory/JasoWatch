using System.Collections.Concurrent;

namespace JasoWatch;

public sealed class FileNormalizationService : IDisposable
{
    private readonly Func<AppSettings> _settings;
    private readonly ConcurrentDictionary<string, PendingItem> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _folderLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _failed = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _shutdown = new();

    public event Action<int>? FailureCountChanged;
    public int FailureCount => _failed.Count;

    public FileNormalizationService(Func<AppSettings> settings) => _settings = settings;

    public void Queue(string path)
    {
        var settings = _settings();
        if (!FilenameNormalizer.IsWithinScope(path, settings.WatchFolder, settings.IncludeSubdirectories)) return;
        _failed.TryRemove(path, out _);
        var item = _pending.AddOrUpdate(path,
            _ => new PendingItem(path, _shutdown.Token),
            (_, existing) => { existing.RestartDebounce(); return existing; });
        item.Start(ProcessAsync);
    }

    public async Task<CleanupResult> CleanupAsync(CancellationToken token, IProgress<CleanupProgress>? progress = null)
    {
        var settings = _settings();
        var result = new CleanupResult();
        if (!Directory.Exists(settings.WatchFolder)) return result;
        var entries = EnumerateScope(settings.WatchFolder, settings.IncludeSubdirectories)
            .OrderByDescending(path => path.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar))
            .ThenBy(path => Directory.Exists(path) ? 1 : 0)
            .ToList();
        foreach (var path in entries)
        {
            token.ThrowIfCancellationRequested();
            result.Checked++;
            try { if (await NormalizeAsync(path, token, waitForStability: false)) result.Changed++; }
            catch { result.Failed++; }
            progress?.Report(new CleanupProgress(result.Checked, result.Changed));
        }
        return result;
    }

    private async Task ProcessAsync(PendingItem item)
    {
        try
        {
            while (!_shutdown.IsCancellationRequested && !await item.WaitForDebounceAsync()) { }
            if (_shutdown.IsCancellationRequested) return;
            var token = _shutdown.Token;
            var deadline = DateTimeOffset.UtcNow.AddMinutes(10);
            var retry = TimeSpan.FromMilliseconds(500);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (!FilenameNormalizer.IsWithinScope(item.Path, _settings().WatchFolder, _settings().IncludeSubdirectories) || !File.Exists(item.Path) && !Directory.Exists(item.Path)) return;
                if (await NormalizeAsync(item.Path, token, waitForStability: true)) return;
                await Task.Delay(retry, token);
                retry = TimeSpan.FromMilliseconds(Math.Min(retry.TotalMilliseconds * 2, 15_000));
            }
            if (_failed.TryAdd(item.Path, 0)) FailureCountChanged?.Invoke(FailureCount);
        }
        catch (OperationCanceledException) { }
        finally { _pending.TryRemove(item.Path, out _); }
    }

    private async Task<bool> NormalizeAsync(string path, CancellationToken token, bool waitForStability)
    {
        var settings = _settings();
        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path)) return true;
        if (!isDirectory && FilenameNormalizer.IsExcludedFile(path, settings)) return true;
        if (waitForStability && !await IsStableAndUnlockedAsync(path, token)) return false;
        var currentName = Path.GetFileName(path);
        if (!FilenameNormalizer.NeedsNormalization(currentName)) return true;
        var parent = Path.GetDirectoryName(path)!;
        using var guard = await LockFolderAsync(parent, token);
        var desired = currentName.Normalize(System.Text.NormalizationForm.FormC);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = FilenameNormalizer.FindAvailableName(parent, desired);
            var destination = Path.Combine(parent, candidate);
            try
            {
                if (isDirectory) Directory.Move(path, destination); else File.Move(path, destination);
                return true;
            }
            catch (IOException) when (attempt < 9) { await Task.Delay(100, token); }
            catch (UnauthorizedAccessException) { return false; }
        }
        return false;
    }

    private static async Task<bool> IsStableAndUnlockedAsync(string path, CancellationToken token)
    {
        if (Directory.Exists(path)) return true;
        try
        {
            var before = new FileInfo(path);
            var size = before.Length; var modified = before.LastWriteTimeUtc;
            await Task.Delay(500, token);
            var after = new FileInfo(path);
            if (size != after.Length || modified != after.LastWriteTimeUtc) return false;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return true; }
    }

    private static IEnumerable<string> EnumerateScope(string root, bool includeSubdirectories)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            yield return entry;
            if (includeSubdirectories && Directory.Exists(entry) && !IsReparsePoint(entry))
            {
                foreach (var descendant in EnumerateScope(entry, true)) yield return descendant;
            }
        }
    }

    private async Task<IDisposable> LockFolderAsync(string folder, CancellationToken token)
    {
        var sem = _folderLocks.GetOrAdd(folder, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(token);
        return new Releaser(sem);
    }

    public void Dispose() => _shutdown.Cancel();

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable { public void Dispose() => semaphore.Release(); }
    private sealed class PendingItem
    {
        private readonly CancellationTokenSource _lifetime;
        private CancellationTokenSource _debounce;
        private int _started;
        public string Path { get; }
        public PendingItem(string path, CancellationToken parent) { Path = path; _lifetime = CancellationTokenSource.CreateLinkedTokenSource(parent); _debounce = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); }
        public void RestartDebounce() { var old = Interlocked.Exchange(ref _debounce, CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token)); old.Cancel(); old.Dispose(); }
        public async Task<bool> WaitForDebounceAsync()
        {
            try { await Task.Delay(TimeSpan.FromSeconds(1), _debounce.Token); return true; }
            catch (OperationCanceledException) { return false; }
        }
        public void Start(Func<PendingItem, Task> action) { if (Interlocked.Exchange(ref _started, 1) == 0) _ = Task.Run(() => action(this)); }
    }
}

public sealed class CleanupResult { public int Checked { get; set; } public int Changed { get; set; } public int Failed { get; set; } }
public sealed record CleanupProgress(int Checked, int Changed);
