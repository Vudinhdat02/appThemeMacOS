using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WShapes = System.Windows.Shapes;

namespace MacDock;

/// <summary>Cửa sổ "Giới thiệu máy này" kiểu macOS 26.</summary>
public sealed class AboutPcWindow : GlassWindow
{
    static AboutPcWindow? _open;

    public static void ShowSingle()
    {
        if (_open != null) { _open.Activate(); return; }
        _open = new AboutPcWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
        _open.Closed += (_, _) => _open = null;
        _open.Show();
        _open.Activate();
    }

    readonly TextBlock _model = new() { FontSize = 22, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 0) };
    readonly TextBlock _sub = new() { HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.6, Margin = new Thickness(0, 2, 0, 18), FontSize = 12.5 };
    readonly Grid _grid = new() { HorizontalAlignment = HorizontalAlignment.Center };
    int _row;

    AboutPcWindow() : base(330)
    {
        Title = "Giới thiệu máy này";
        var sp = new StackPanel { Margin = new Thickness(28, 38, 28, 24) };
        sp.Children.Add(Laptop());
        _model.Text = Environment.MachineName;
        sp.Children.Add(_model);
        sp.Children.Add(_sub);
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sp.Children.Add(_grid);

        string product = "Windows", display = "", build = "";
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            product = k?.GetValue("ProductName") as string ?? product;
            display = k?.GetValue("DisplayVersion") as string ?? "";
            string cb = k?.GetValue("CurrentBuild") as string ?? "";
            build = cb + "." + (k?.GetValue("UBR")?.ToString() ?? "0");
            if (int.TryParse(cb, out int b) && b >= 22000) product = product.Replace("Windows 10", "Windows 11");
        }
        catch { }
        _sub.Text = $"{product} {display}".Trim();

        string cpu = "";
        try { using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"); cpu = (k?.GetValue("ProcessorNameString") as string ?? "").Trim(); } catch { }
        cpu = cpu.Replace("(R)", "").Replace("(TM)", "").Replace(" CPU", "");
        AddRow("Chip", cpu);
        var mem = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (Native.GlobalMemoryStatusEx(ref mem)) AddRow("Bộ nhớ", $"{Math.Round(mem.ullTotalPhys / 1073741824.0)} GB");
        var gpu = AddRow("Đồ hoạ", "…");
        try
        {
            var c = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            AddRow("Ổ đĩa khởi động", $"{c.Name.TrimEnd('\\')}  {c.TotalSize / 1e9:0} GB ({c.AvailableFreeSpace / 1e9:0} GB trống)");
        }
        catch { }
        var serial = AddRow("Số sê-ri", "…");
        var m = Monitors.GetAll().FirstOrDefault(x => x.Primary);
        if (m != null) AddRow("Màn hình", $"{m.Bounds.Width}×{m.Bounds.Height} @ {m.Scale * 100:0}%");
        AddRow("Windows", $"{display} (bản dựng {build})");

        var more = PillButton("Thông tin khác…", false);
        more.HorizontalAlignment = HorizontalAlignment.Center;
        more.Margin = new Thickness(0, 22, 0, 0);
        more.Click += (_, _) => Launcher.Open("ms-settings:about");
        sp.Children.Add(more);
        var report = new TextBlock { Text = "Báo cáo hệ thống…", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0), FontSize = 11.5, Cursor = System.Windows.Input.Cursors.Hand };
        report.SetResourceReference(TextBlock.ForegroundProperty, "Md.Accent");
        report.MouseLeftButtonUp += (_, _) => Launcher.Open("msinfo32.exe");
        sp.Children.Add(report);
        Body = sp;

        Task.Run(() =>
        {
            string model = "", gpus = "", sn = "";
            try
            {
                using var s = new System.Management.ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                foreach (System.Management.ManagementObject o in s.Get()) model = $"{o["Manufacturer"]} {o["Model"]}".Trim();
            }
            catch { }
            try
            {
                using var s = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                gpus = string.Join("\n", s.Get().Cast<System.Management.ManagementObject>().Select(o => o["Name"]?.ToString()).Where(n => !string.IsNullOrEmpty(n)));
            }
            catch { }
            try
            {
                using var s = new System.Management.ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
                foreach (System.Management.ManagementObject o in s.Get()) sn = o["SerialNumber"]?.ToString()?.Trim() ?? "";
            }
            catch { }
            return (model, gpus, sn);
        }).ContinueWith(t => Dispatcher.BeginInvoke(() =>
        {
            var (model, gpus, sn) = t.Result;
            if (!string.IsNullOrWhiteSpace(model) && !model.Contains("To Be Filled", StringComparison.OrdinalIgnoreCase)) _model.Text = model;
            gpu.Text = string.IsNullOrEmpty(gpus) ? "Không rõ" : gpus;
            serial.Text = string.IsNullOrEmpty(sn) ? "Không rõ" : sn;
        }));
    }

    TextBlock AddRow(string key, string value)
    {
        _grid.RowDefinitions.Add(new RowDefinition());
        var k = new TextBlock { Text = key, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2.5, 0, 2.5), FontSize = 12, FontWeight = FontWeights.SemiBold };
        var v = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, MaxWidth = 170, Margin = new Thickness(0, 2.5, 0, 2.5), FontSize = 12, Opacity = 0.8 };
        Grid.SetRow(k, _row); Grid.SetRow(v, _row); Grid.SetColumn(v, 2);
        _grid.Children.Add(k); _grid.Children.Add(v);
        _row++;
        return v;
    }

    /// <summary>Hình minh hoạ laptop (vector).</summary>
    static FrameworkElement Laptop()
    {
        var c = new Canvas { Width = 200, Height = 128 };
        var lid = new WShapes.Rectangle { Width = 164, Height = 106, RadiusX = 9, RadiusY = 9, Fill = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)), Stroke = new SolidColorBrush(Color.FromRgb(0xB8, 0xBC, 0xC4)), StrokeThickness = 2 };
        Canvas.SetLeft(lid, 18); c.Children.Add(lid);
        var wall = new LinearGradientBrush(new GradientStopCollection
        {
            new(Color.FromRgb(0x3A, 0x7B, 0xF2), 0), new(Color.FromRgb(0x9B, 0x5C, 0xF0), 0.5), new(Color.FromRgb(0xF4, 0x8C, 0x6E), 1)
        }, new Point(0, 0), new Point(1, 1));
        var screen = new WShapes.Rectangle { Width = 152, Height = 95, RadiusX = 4, RadiusY = 4, Fill = wall };
        Canvas.SetLeft(screen, 24); Canvas.SetTop(screen, 6); c.Children.Add(screen);
        var bar = new WShapes.Rectangle { Width = 152, Height = 5, Fill = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)) };
        Canvas.SetLeft(bar, 24); Canvas.SetTop(bar, 6); c.Children.Add(bar);
        var dock = new WShapes.Rectangle { Width = 60, Height = 8, RadiusX = 3, RadiusY = 3, Fill = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)) };
        Canvas.SetLeft(dock, 70); Canvas.SetTop(dock, 90); c.Children.Add(dock);
        var baseP = new WShapes.Path
        {
            Data = Geometry.Parse("M4,108 L196,108 L196,111 Q196,118 186,118 L14,118 Q4,118 4,111 Z"),
            Fill = new LinearGradientBrush(Color.FromRgb(0xE4, 0xE6, 0xEA), Color.FromRgb(0xA8, 0xAD, 0xB6), 90)
        };
        c.Children.Add(baseP);
        var notch = new WShapes.Rectangle { Width = 30, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = new SolidColorBrush(Color.FromRgb(0x9A, 0x9E, 0xA6)) };
        Canvas.SetLeft(notch, 85); Canvas.SetTop(notch, 108); c.Children.Add(notch);
        return new Viewbox { Width = 200, Height = 128, Child = c, HorizontalAlignment = HorizontalAlignment.Center };
    }
}
