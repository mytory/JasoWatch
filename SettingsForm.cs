namespace JasoWatch;

public sealed class SettingsForm : Form
{
    private readonly TextBox _folder = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _subfolders = new() { Text = "하위 폴더 감시", AutoSize = true };
    private readonly CheckBox _autostart = new() { Text = "Windows 시작 시 자동 실행", AutoSize = true };
    private readonly TextBox _extensions = new() { Dock = DockStyle.Fill };
    private readonly TextBox _prefixes = new() { Dock = DockStyle.Fill };
    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Settings = new AppSettings { WatchFolder = current.WatchFolder, IncludeSubdirectories = current.IncludeSubdirectories, StartWithWindows = current.StartWithWindows, ExcludedExtensions = [.. current.ExcludedExtensions], ExcludedPrefixes = [.. current.ExcludedPrefixes], HasShownFirstRunGuide = current.HasShownFirstRunGuide };
        Text = "JasoWatch 설정"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(530, 260);
        _folder.Text = Settings.WatchFolder; _subfolders.Checked = Settings.IncludeSubdirectories; _autostart.Checked = Settings.StartWithWindows;
        _extensions.Text = string.Join(", ", Settings.ExcludedExtensions); _prefixes.Text = string.Join(", ", Settings.ExcludedPrefixes);
        var browse = new Button { Text = "찾아보기…", AutoSize = true };
        browse.Click += (_, _) => { using var picker = new FolderBrowserDialog { SelectedPath = _folder.Text, Description = "감시 폴더 선택" }; if (picker.ShowDialog() == DialogResult.OK) _folder.Text = picker.SelectedPath; };
        var defaults = new Button { Text = "기본값 복원", AutoSize = true };
        defaults.Click += (_, _) => { _extensions.Text = ".crdownload, .part, .tmp"; _prefixes.Text = "~$"; };
        var ok = new Button { Text = "저장", DialogResult = DialogResult.OK, AutoSize = true };
        ok.Click += (_, _) => Settings = new AppSettings { WatchFolder = _folder.Text.Trim(), IncludeSubdirectories = _subfolders.Checked, StartWithWindows = _autostart.Checked, ExcludedExtensions = ParseList(_extensions.Text), ExcludedPrefixes = ParseList(_prefixes.Text), HasShownFirstRunGuide = Settings.HasShownFirstRunGuide };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = ok; CancelButton = cancel;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 3, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "감시 폴더", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); layout.Controls.Add(_folder, 1, 0); layout.Controls.Add(browse, 2, 0);
        layout.Controls.Add(_subfolders, 1, 1); layout.Controls.Add(_autostart, 1, 2);
        layout.Controls.Add(new Label { Text = "제외 확장자", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3); layout.Controls.Add(_extensions, 1, 3);
        layout.Controls.Add(new Label { Text = "제외 접두사", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4); layout.Controls.Add(_prefixes, 1, 4); layout.Controls.Add(defaults, 2, 4);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill }; buttons.Controls.Add(cancel); buttons.Controls.Add(ok); layout.SetColumnSpan(buttons, 3); layout.Controls.Add(buttons, 0, 6);
        Controls.Add(layout);
    }

    private static List<string> ParseList(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
