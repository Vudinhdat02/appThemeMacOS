using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MacDock;

public sealed record InstalledApp(string Name, string AppsFolderPath, string? ExePath, ImageSource? Icon);

/// <summary>Chọn ứng dụng đã cài (thư mục shell:AppsFolder) để ghim lên Dock.</summary>
public sealed class AppPickerWindow : GlassWindow
{
    static List<InstalledApp>? _cache;
    readonly TextBox _search = new() { Padding = new Thickness(10, 6, 10, 6), FontSize = 13 };
    readonly ListBox _list = new() { Height = 380, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    readonly TextBlock _status = new() { Opacity = 0.6, Margin = new Thickness(2, 6, 0, 0) };
    List<InstalledApp> _all = new();

    public AppPickerWindow() : base(460)
    {
        Title = "Thêm ứng dụng vào Dock";
        var sp = new StackPanel { Margin = new Thickness(22, 40, 22, 20) };
        sp.Children.Add(new TextBlock { Text = "Thêm ứng dụng vào Dock", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        var sb = new Grid();
        var hint = new TextBlock { Text = "Tìm ứng dụng…", Opacity = 0.45, Margin = new Thickness(12, 7, 0, 0), IsHitTestVisible = false };
        sb.Children.Add(_search); sb.Children.Add(hint);
        _search.TextChanged += (_, _) => { hint.Visibility = _search.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible; Filter(); };
        sp.Children.Add(sb);
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        _list.Margin = new Thickness(0, 10, 0, 0);
        _list.MouseDoubleClick += (_, _) => AddSelected();
        _list.KeyDown += (_, e) => { if (e.Key == Key.Enter) AddSelected(); };
        sp.Children.Add(_list);
        sp.Children.Add(_status);
        var buttons = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
        var browse = PillButton("Chọn file .exe…", false);
        browse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Ứng dụng (*.exe;*.lnk)|*.exe;*.lnk", Title = "Chọn ứng dụng" };
            if (dlg.ShowDialog(this) == true) { DockModel.Instance.HandleDrop(new[] { dlg.FileName }, null, int.MaxValue); _status.Text = "Đã ghim " + System.IO.Path.GetFileNameWithoutExtension(dlg.FileName); }
        };
        var add = PillButton("Thêm vào Dock", true); add.IsDefault = true;
        add.Click += (_, _) => AddSelected();
        DockPanel.SetDock(add, Dock.Right);
        buttons.Children.Add(browse); buttons.Children.Add(add);
        sp.Children.Add(buttons);
        Body = sp;
        Loaded += async (_, _) => { _search.Focus(); await LoadApps(); };
    }

    async Task LoadApps()
    {
        _status.Text = "Đang tải danh sách ứng dụng…";
        if (_cache == null)
        {
            var tcs = new TaskCompletionSource<List<InstalledApp>>();
            var th = new Thread(() =>
            {
                var res = new List<InstalledApp>();
                try
                {
                    var t = Type.GetTypeFromProgID("Shell.Application")!;
                    dynamic sh = Activator.CreateInstance(t)!;
                    dynamic folder = sh.NameSpace("shell:AppsFolder");
                    foreach (dynamic item in folder.Items())
                    {
                        try
                        {
                            string name = item.Name; string path = item.Path;
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path)) continue;
                            string? exe = null;
                            try { exe = item.ExtendedProperty("System.Link.TargetParsingPath") as string; } catch { }
                            if (exe != null && !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) exe = null;
                            if (name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Gỡ cài đặt", StringComparison.OrdinalIgnoreCase)) continue;
                            var icon = ShellIcons.GetIcon(@"shell:AppsFolder\" + path, 32);
                            res.Add(new InstalledApp(name, path, exe, icon));
                        }
                        catch { }
                    }
                }
                catch (Exception ex) { Log.Warn("Liệt kê ứng dụng lỗi: " + ex.Message); }
                tcs.SetResult(res.GroupBy(a => a.Name).Select(g => g.First()).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
            }) { IsBackground = true };
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
            _cache = await tcs.Task;
        }
        _all = _cache;
        Filter();
        _status.Text = $"{_all.Count} ứng dụng — bấm đúp để thêm";
    }

    void Filter()
    {
        string q = _search.Text.Trim();
        _list.Items.Clear();
        foreach (var a in _all.Where(a => q.Length == 0 || a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).Take(400))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 3, 2, 3), Tag = a };
            row.Children.Add(new Image { Source = a.Icon, Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0) });
            row.Children.Add(new TextBlock { Text = a.Name, VerticalAlignment = VerticalAlignment.Center });
            _list.Items.Add(new ListBoxItem { Content = row, Tag = a });
        }
    }

    void AddSelected()
    {
        if (_list.SelectedItem is not ListBoxItem { Tag: InstalledApp a }) return;
        DockModel.Instance.PinInstalled(a);
        _status.Text = "Đã thêm " + a.Name + " vào Dock";
    }
}
