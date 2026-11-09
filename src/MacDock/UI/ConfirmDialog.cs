using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace MacDock;

/// <summary>
/// Hộp xác nhận luôn nằm trên cùng và được đưa lên trước. (MessageBox thường mở từ Thanh Menu — vốn không nhận focus —
/// sẽ nằm khuất sau các cửa sổ khác nên người dùng không thấy để bấm.)
/// </summary>
public sealed class ConfirmDialog : GlassWindow
{
    bool _ok;

    ConfirmDialog(string title, string message, string okText) : base(360, closeButton: false)
    {
        Title = title;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var sp = new StackPanel { Margin = new Thickness(26, 26, 26, 22) };
        sp.Children.Add(new Image { Source = Glyphs.AppIcon, Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 15, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 6) });
        if (message.Length > 0) sp.Children.Add(new TextBlock { Text = message, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = PillButton(L.T("Huỷ"), false); cancel.IsCancel = true; cancel.Margin = new Thickness(0, 0, 5, 0);
        var ok = PillButton(okText, true); ok.IsDefault = true; ok.Margin = new Thickness(5, 0, 0, 0);

        cancel.Click += (_, _) => Close();
        ok.Click += (_, _) => { _ok = true; Close(); };
        grid.Children.Add(cancel); grid.Children.Add(ok);
        sp.Children.Add(grid);
        Body = sp;
        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            WindowActions.Activate(h);
            ok.Focus();
        };
    }

    public static bool Ask(string title, string message, string okText, bool destructive = false)
    {
        var d = new ConfirmDialog(title, message, okText);
        d.ShowDialog();
        Log.Info($"Hộp xác nhận \"{title}\": {(d._ok ? "đồng ý" : "huỷ")}");
        return d._ok;
    }
}
