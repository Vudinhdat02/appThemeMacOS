using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacDock;

/// <summary>Cửa sổ "Giới thiệu máy này".</summary>
public sealed class AboutPcWindow : Window
{
    static AboutPcWindow? _open;

    public static void ShowSingle()
    {
        if (_open != null) { _open.Activate(); return; }
        _open = new AboutPcWindow();
        _open.Closed += (_, _) => _open = null;
        _open.Show();
        _open.Activate();
    }

    AboutPcWindow()
    {
        Title = "Giới thiệu máy này";
        Width = 480; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = Glyphs.AppIcon;
        SetResourceReference(BackgroundProperty, "Md.WindowBg");
        SetResourceReference(ForegroundProperty, "Md.PopupFg");
        SetResourceReference(FontFamilyProperty, "Md.Font");
        FontSize = 13;

        var sp = new StackPanel { Margin = new Thickness(30, 26, 30, 22) };
        var logo = Glyphs.Icon(Glyphs.Logo, ThemeManager.Current.Accent, 64);
        logo.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(logo);

        string product = "Windows", display = "", build = "";
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            product = k?.GetValue("ProductName") as string ?? product;
            display = k?.GetValue("DisplayVersion") as string ?? "";
            build = (k?.GetValue("CurrentBuild") as string ?? "") + "." + (k?.GetValue("UBR")?.ToString() ?? "0");
            if (int.TryParse((k?.GetValue("CurrentBuild") as string) ?? "0", out int b) && b >= 22000) product = product.Replace("Windows 10", "Windows 11");
        }
        catch { }
        sp.Children.Add(new TextBlock { Text = product, FontSize = 22, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) });
        sp.Children.Add(new TextBlock { Text = $"Phiên bản {display} (bản dựng {build})", HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.65, Margin = new Thickness(0, 0, 0, 18) });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int row = 0;
        void Add(string k, string v)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            var a = new TextBlock { Text = k, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 10, 3), HorizontalAlignment = HorizontalAlignment.Right };
            var b = new TextBlock { Text = v, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3) };
            Grid.SetRow(a, row); Grid.SetRow(b, row); Grid.SetColumn(b, 1);
            grid.Children.Add(a); grid.Children.Add(b);
            row++;
        }
        Add("Tên máy", Environment.MachineName);
        string cpu = "";
        try { using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"); cpu = (k?.GetValue("ProcessorNameString") as string ?? "").Trim(); } catch { }
        Add("Bộ xử lý", $"{cpu} ({Environment.ProcessorCount} luồng)");
        var mem = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (Native.GlobalMemoryStatusEx(ref mem)) Add("Bộ nhớ", $"{mem.ullTotalPhys / 1073741824.0:0.0} GB");
        var gpuText = new TextBlock();
        Add("Đồ hoạ", "…");
        var gpuCell = (TextBlock)grid.Children[^1];
        var monitors = Monitors.GetAll();
        Add("Màn hình", string.Join("\n", monitors.Select(m => $"{m.Bounds.Width}×{m.Bounds.Height} @ {m.Scale * 100:0}%{(m.Primary ? " (chính)" : "")}")));
        Add("Người dùng", Environment.UserName);
        sp.Children.Add(grid);

        Task.Run(() =>
        {
            try
            {
                using var s = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                return string.Join("\n", s.Get().Cast<System.Management.ManagementObject>().Select(o => o["Name"]?.ToString()).Where(n => n != null));
            }
            catch { return "Không rõ"; }
        }).ContinueWith(t => Dispatcher.BeginInvoke(() => gpuCell.Text = t.Result));

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) };
        foreach (var (text, target) in new[] { ("Thông tin hệ thống…", "ms-settings:about"), ("Báo cáo chi tiết…", "msinfo32.exe") })
        {
            var b = new Button { Content = text, Margin = new Thickness(4) };
            b.SetResourceReference(StyleProperty, "Md.Button");
            var tg = target;
            b.Click += (_, _) => Launcher.Open(tg);
            buttons.Children.Add(b);
        }
        sp.Children.Add(buttons);
        Content = sp;
    }
}
