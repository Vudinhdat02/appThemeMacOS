using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacDock;

/// <summary>Màn hình chào lần đầu chạy.</summary>
public sealed class WelcomeWindow : Window
{
    public WelcomeWindow(bool reviewOnly = false)
    {
        Title = "Chào mừng đến với MacDock";
        Width = 520; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Icon = Glyphs.AppIcon;
        SetResourceReference(BackgroundProperty, "Md.WindowBg");
        SetResourceReference(ForegroundProperty, "Md.PopupFg");
        SetResourceReference(FontFamilyProperty, "Md.Font");
        FontSize = 13;
        Topmost = true;

        var g = Config.Current.General;
        var sp = new StackPanel { Margin = new Thickness(32, 28, 32, 24) };
        sp.Children.Add(new Image { Source = Glyphs.AppIcon, Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = "Chào mừng đến với MacDock", FontSize = 22, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 4) });
        sp.Children.Add(new TextBlock { Text = "Dock, Thanh Menu và theme kiểu macOS cho Windows 11.", HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.7, Margin = new Thickness(0, 0, 0, 18) });

        foreach (var (title, desc) in new[]
        {
            ("Dock", "Phóng to khi rê chuột, chấm ứng dụng đang chạy, kéo thả để sắp xếp và ghim."),
            ("Thanh Menu", "Menu của ứng dụng đang dùng, đồng hồ, pin, Wi-Fi, âm lượng và icon khay hệ thống."),
            ("Theme", "6 chủ đề dựng sẵn, sáng/tối theo Windows hoặc theo giờ, nhập/xuất .macdocktheme."),
        })
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            row.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
            row.Children.Add(new TextBlock { Text = desc, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
            sp.Children.Add(row);
        }

        CheckBox Opt(string text, bool value)
        {
            var cb = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 6, 0, 0) };
            return cb;
        }
        var auto = Opt("Khởi động cùng Windows", g.AutoStart);
        var hide = Opt("Ẩn thanh tác vụ của Windows (có thể bật lại bất cứ lúc nào)", g.HideTaskbar);
        var bar = Opt("Hiện Thanh Menu ở cạnh trên màn hình", Config.Current.MenuBar.Enabled);
        var opts = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
        opts.Children.Add(auto); opts.Children.Add(hide); opts.Children.Add(bar);
        sp.Children.Add(opts);

        var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        themeRow.Children.Add(new TextBlock { Text = "Chủ đề:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var combo = new ComboBox { MinWidth = 200 };
        foreach (var t in ThemeManager.All()) combo.Items.Add(new ComboBoxItem { Content = t.Name, Tag = t.Id, IsSelected = t.Id == Config.Current.Theme.ThemeId });
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem it) { Config.Current.Theme.ThemeId = (string)it.Tag; ThemeManager.Apply(); } };
        themeRow.Children.Add(combo);
        sp.Children.Add(themeRow);

        sp.Children.Add(new TextBlock
        {
            Text = "Nếu taskbar không hiện lại vì lý do nào đó, chạy: MacDock.exe --restore-taskbar",
            TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Opacity = 0.6, Margin = new Thickness(0, 16, 0, 0)
        });

        var start = new Button { Content = reviewOnly ? "Đóng" : "Bắt đầu", Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(36, 7, 36, 7) };
        start.SetResourceReference(StyleProperty, "Md.AccentButton");
        start.IsDefault = true;
        start.Click += (_, _) => Close();
        sp.Children.Add(start);
        Content = sp;

        Closed += (_, _) =>
        {
            if (reviewOnly)
            {
                // chỉ áp dụng lựa chọn khi xem lại
                g.AutoStart = auto.IsChecked == true; g.HideTaskbar = hide.IsChecked == true; Config.Current.MenuBar.Enabled = bar.IsChecked == true;
                Config.Touch("general"); Config.Touch("menubar"); Config.Touch("theme");
                return;
            }
            g.AutoStart = auto.IsChecked == true;
            g.HideTaskbar = hide.IsChecked == true;
            Config.Current.MenuBar.Enabled = bar.IsChecked == true;
            g.FirstRunDone = true;
            Config.Save();
        };
    }
}
