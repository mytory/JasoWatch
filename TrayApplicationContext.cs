using Microsoft.Win32;

namespace JasoWatch;

public sealed class TrayApplicationContext : ApplicationContext
{
    private AppSettings _settings;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _status = new() { Enabled = false };
    private readonly ToolStripMenuItem _pauseResume = new();
    private readonly ToolStripMenuItem _openFolder = new();
    private readonly ToolStripMenuItem _cleanup = new("기존 파일명 정리");
    private readonly FileNormalizationService _normalizer;
    private FileSystemWatcher? _watcher;
    private bool _paused;
    private bool _folderUnavailable;
    private CancellationTokenSource? _cleanupCancellation;
    private readonly SynchronizationContext _ui;

    public TrayApplicationContext(bool autoStarted, EventWaitHandle existingInstanceSignal)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settings = SettingsStore.Load();
        _normalizer = new FileNormalizationService(() => _settings);
        _normalizer.FailureCountChanged += _ => UpdateUi();
        _tray = new NotifyIcon { Visible = true, Text = "JasoWatch" };
        _tray.DoubleClick += (_, _) => OpenWatchFolder();
        _pauseResume.Click += (_, _) => TogglePause();
        _openFolder.Click += (_, _) => OpenWatchFolder();
        _cleanup.Click += async (_, _) => await StartCleanupAsync();
        var settings = new ToolStripMenuItem("설정", null, (_, _) => ShowSettings());
        var exit = new ToolStripMenuItem("종료", null, (_, _) => Exit());
        _tray.ContextMenuStrip = new ContextMenuStrip();
        _tray.ContextMenuStrip.Items.AddRange([_status, new ToolStripSeparator(), _pauseResume, _openFolder, _cleanup, settings, new ToolStripSeparator(), exit]);
        ApplyAutostart();
        StartWatcher();
        UpdateUi();
        _ = Task.Run(() => { while (existingInstanceSignal.WaitOne()) _ui.Post(_ => ShowBalloon("JasoWatch", "JasoWatch가 이미 실행 중입니다."), null); });
    }

    private void StartWatcher()
    {
        StopWatcher();
        if (_paused) return;
        try
        {
            if (!Directory.Exists(_settings.WatchFolder)) throw new DirectoryNotFoundException();
            _watcher = new FileSystemWatcher(_settings.WatchFolder)
            {
                IncludeSubdirectories = _settings.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                InternalBufferSize = 64 * 1024,
                EnableRaisingEvents = true
            };
            _watcher.Created += (_, e) => _normalizer.Queue(e.FullPath);
            _watcher.Renamed += (_, e) => _normalizer.Queue(e.FullPath);
            _watcher.Changed += (_, e) => _normalizer.Queue(e.FullPath);
            _watcher.Error += async (_, _) => await RecoverFromWatcherErrorAsync();
            _folderUnavailable = false;
        }
        catch
        {
            _folderUnavailable = true;
            var retry = new System.Windows.Forms.Timer { Interval = 5_000 };
            retry.Tick += (_, _) => { if (Directory.Exists(_settings.WatchFolder)) { retry.Stop(); retry.Dispose(); StartWatcher(); UpdateUi(); } };
            retry.Start();
            ShowBalloon("감시 폴더 접근 불가", "감시 폴더에 접근할 수 없어 감시를 일시 중단했습니다.");
        }
    }

    private async Task RecoverFromWatcherErrorAsync()
    {
        StartWatcher();
        if (!_folderUnavailable)
        {
            try { await _normalizer.CleanupAsync(CancellationToken.None); ShowBalloon("이벤트 복구 검사 완료", "감시 범위를 다시 검사했습니다."); }
            catch { }
        }
    }

    private void StopWatcher() { _watcher?.Dispose(); _watcher = null; }

    private void TogglePause()
    {
        _paused = !_paused;
        if (_paused) { StopWatcher(); _cleanupCancellation?.Cancel(); }
        else StartWatcher();
        UpdateUi();
    }

    private async Task StartCleanupAsync()
    {
        if (_cleanupCancellation is not null) return;
        _cleanupCancellation = new CancellationTokenSource();
        UpdateUi();
        try
        {
            var progress = new Progress<CleanupProgress>(p => { _status.Text = $"기존 파일명 정리 진행: 검사 {p.Checked}개, 변경 {p.Changed}개"; });
            var result = await _normalizer.CleanupAsync(_cleanupCancellation.Token, progress);
            ShowBalloon("기존 파일명 정리 완료", $"검사 {result.Checked}개 · 변경 {result.Changed}개 · 실패 {result.Failed}개");
        }
        catch (OperationCanceledException) { }
        finally { _cleanupCancellation.Dispose(); _cleanupCancellation = null; UpdateUi(); }
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK) return;
        var next = form.Settings;
        try
        {
            if (!Directory.Exists(next.WatchFolder)) throw new DirectoryNotFoundException("선택한 감시 폴더가 없거나 접근할 수 없습니다.");
            SettingsStore.Save(next);
            _settings = next;
            ApplyAutostart();
            StartWatcher();
            UpdateUi();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "설정을 저장할 수 없음", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ApplyAutostart()
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath)!;
            if (_settings.StartWithWindows)
            {
                var exe = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue("JasoWatch", $"\"{exe}\" --autostart");
            }
            else key.DeleteValue("JasoWatch", false);
        }
        catch
        {
            _settings.StartWithWindows = false;
            SettingsStore.Save(_settings);
            ShowBalloon("자동 시작 설정 실패", "Windows 자동 시작 설정을 변경하지 못했습니다.");
        }
    }

    private void UpdateUi()
    {
        _pauseResume.Text = _paused ? "감시 재개" : "감시 일시 중지";
        _openFolder.Text = string.Equals(_settings.WatchFolder, KnownFolders.Downloads, StringComparison.OrdinalIgnoreCase) ? "다운로드 폴더 열기" : "감시 폴더 열기";
        _cleanup.Enabled = !_paused && !_folderUnavailable && _cleanupCancellation is null;
        _cleanup.Text = _cleanupCancellation is null ? "기존 파일명 정리" : "기존 파일명 정리 중…";
        _status.Text = _paused ? "상태: 감시 일시 중단" : _folderUnavailable ? "상태: 감시 폴더 접근 불가" : _cleanupCancellation is not null ? "상태: 기존 파일명 정리 진행" : _normalizer.FailureCount > 0 ? $"상태: 정상화 실패 {_normalizer.FailureCount}개" : "상태: 실행 중";
        var iconName = _paused ? "jasowatch-paused.ico" : _folderUnavailable || _normalizer.FailureCount > 0 ? "jasowatch-warning.ico" : "jasowatch-active.ico";
        using var stream = typeof(TrayApplicationContext).Assembly.GetManifestResourceStream($"JasoWatch.assets.icons.{iconName}")!;
        _tray.Icon = (Icon)new Icon(stream).Clone();
    }

    private void OpenWatchFolder()
    {
        if (Directory.Exists(_settings.WatchFolder)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{_settings.WatchFolder}\"") { UseShellExecute = true });
    }
    private void ShowBalloon(string title, string text) => _tray.ShowBalloonTip(5_000, title, text, ToolTipIcon.Info);
    private void Exit() { _cleanupCancellation?.Cancel(); StopWatcher(); _normalizer.Dispose(); _tray.Visible = false; _tray.Dispose(); ExitThread(); }
}
