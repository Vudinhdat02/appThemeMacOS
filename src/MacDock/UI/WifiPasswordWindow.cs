using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MacDock;

/// <summary>Hộp nhập mật khẩu Wi-Fi (cửa sổ thường để nhận bàn phím).</summary>
public sealed class WifiPasswordWindow : GlassWindow
{
    string? _result;
    readonly PasswordBox _box = new() { Padding = new Thickness(8, 6, 8, 6), FontSize = 14 };

    WifiPasswordWindow(string ssid) : base(360)
    {
        Title = L.T("Kết nối Wi-Fi");
        var sp = new StackPanel { Margin = new Thickness(26, 30, 26, 22) };
        var icon = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(28), Background = ThemeManager.Current.Accent, HorizontalAlignment = HorizontalAlignment.Center, Child = Glyphs.WifiIcon(Brushes.White, 100, 30) };
        sp.Children.Add(icon);
        sp.Children.Add(new TextBlock { Text = L.F("Mạng Wi-Fi \"{0}\" yêu cầu mật khẩu.", ssid), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 14, 0, 14) });
        sp.Children.Add(_box);
        var show = new CheckBox { Content = L.T("Hiện mật khẩu"), Margin = new Thickness(0, 8, 0, 0) };
        var plain = new TextBox { Padding = new Thickness(8, 6, 8, 6), FontSize = 14, Visibility = Visibility.Collapsed };
        sp.Children.Insert(sp.Children.IndexOf(_box) + 1, plain);
        show.Checked += (_, _) => { plain.Text = _box.Password; plain.Visibility = Visibility.Visible; _box.Visibility = Visibility.Collapsed; };
        show.Unchecked += (_, _) => { _box.Password = plain.Text; _box.Visibility = Visibility.Visible; plain.Visibility = Visibility.Collapsed; };
        sp.Children.Add(show);
        var buttons = new UniformGrid { Columns = 2, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = PillButton(L.T("Huỷ"), false); cancel.Click += (_, _) => Close();
        var ok = PillButton(L.T("Kết nối"), true); ok.IsDefault = true;
        ok.Click += (_, _) => { _result = show.IsChecked == true ? plain.Text : _box.Password; Close(); };
        cancel.Margin = new Thickness(0, 0, 5, 0); ok.Margin = new Thickness(5, 0, 0, 0);
        buttons.Children.Add(cancel); buttons.Children.Add(ok);
        sp.Children.Add(buttons);
        Body = sp;
        Loaded += (_, _) => _box.Focus();
    }

    public static string? Ask(string ssid)
    {
        var w = new WifiPasswordWindow(ssid) { WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true };
        w.ShowDialog();
        return w._result;
    }
}
