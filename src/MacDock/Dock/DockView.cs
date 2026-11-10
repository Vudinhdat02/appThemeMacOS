using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MacDock;

/// <summary>
/// Vẽ Dock bằng OnRender. Hệ toạ độ trừu tượng: u = dọc theo Dock, v = khoảng cách từ cạnh màn hình vào trong.
/// </summary>
public sealed class DockView : FrameworkElement
{
    sealed class VState { public double Scale = 1, Target = 1; }

    struct Slot
    {
        public DockItem Item;
        public double Start, Width, Center, Size, Bounce;
        public bool Placeholder;
    }

    readonly DockWindow _win;
    readonly Dictionary<DockItem, VState> _st = new();
    List<Slot> _slots = new();
    double _plateStart, _plateEnd;

    static readonly Brush HitBrush = Frozen(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)));
    static Brush Frozen(Brush b) { b.Freeze(); return b; }

    // trạng thái chuột
    bool _hover;
    double _mouseU, _mouseV;
    bool _rendering;
    double _lastTick, _lastFrame;
    bool _menuOpen;
    DockItem? _hoverItem;

    // tự ẩn
    double _hide;          // 0 = hiện, 1 = ẩn
    double _hideTarget;
    double _edgeEnterTime = -1, _leaveTime = -1;

    // kéo thả
    DockItem? _pressItem;
    Point _pressPos;
    bool _dragging, _dragOut;
    DockItem? _dragItem;
    int _dragInsert;
    bool _extDrag;
    DockItem? _extTarget;

    public DockView(DockWindow win)
    {
        _win = win;
        Focusable = false;
        AllowDrop = true;
        SnapsToDevicePixels = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        MouseMove += (_, _) => Wake();
        MouseEnter += (_, _) => Wake();
        PreviewMouseLeftButtonDown += OnDown;
        PreviewMouseLeftButtonUp += OnUp;
        PreviewMouseMove += OnDragMove;
        MouseRightButtonUp += OnRightUp;
        LostMouseCapture += (_, _) => { if (_dragging) EndDrag(false); _pressItem = null; };
        DragEnter += OnExtDrag; DragOver += OnExtDrag;
        DragLeave += (_, _) => { _extDrag = false; _extTarget = null; Wake(); };
        Drop += OnExtDrop;
        ApplySettings();
    }

    DockSettings Cfg => Config.Current.Dock;
    DockEdge Edge => Cfg.Edge;
    bool Horizontal => Edge == DockEdge.Bottom;
    double S => Cfg.IconSize;
    double G => Cfg.Spacing;
    double Pad => Math.Round(S * 0.14);
    double Margin0 => Cfg.EdgeMargin;
    double PlateThick => S + 2 * Pad;
    double SepW => Math.Max(8, S * 0.28);
    double MaxScale => Cfg.Magnify && !App.EffectsOff ? Cfg.MagnifyScale : 1.0;
    double MainLength => Horizontal ? ActualWidth : ActualHeight;
    double CrossLength => Horizontal ? ActualHeight : ActualWidth;

    /// <summary>Độ dày cần chừa (DIP) khi giữ chỗ.</summary>
    public double ReserveThickness => Margin0 + PlateThick + 2;

    public double ComputeCross()
    {
        double extra = Math.Max(S * (MaxScale - 1), S * 0.95);
        return Horizontal
            ? Margin0 + PlateThick + extra + 44
            : Margin0 + PlateThick + extra + 240;
    }

    public double ComputeMain()
    {
        double baseL = 0;
        foreach (var it in DockModel.Instance.Items) baseL += (it.Kind == DockItemKind.Separator ? SepW : S) + G;
        double extra = (MaxScale - 1) * Cfg.CurveRange * (S + G) * 1.1;
        return baseL + extra + 2 * Pad + 260;
    }

    public void ApplySettings()
    {
        if (Log.Verbose) Diag.Hit("applySettings");
        var t = ThemeManager.Current;
        // Bóng của Dock được vẽ trực tiếp (DrawShadow) thay vì DropShadowEffect: hiệu ứng làm mỗi khung hình
        // phải chạy shader trên cả cửa sổ trong suốt, khiến Dock chỉ đạt ~30 khung/giây khi phóng to.
        Effect = null;
        _shadowLayers = null;
        _hideTarget = EffectiveAutoHide ? 1 : 0;
        if (!Cfg.AutoHide) { _hide = 0; if (_appliedOffset != 0) { _appliedOffset = 0; _win.SetHideOffset(0); } }
        InvalidateVisual();
        Wake();
    }

    // ------------------------------------------------------------------ vòng lặp

    public void Wake()
    {
        _pausedHover = false;
        if (_rendering) return;
        _rendering = true;
        _lastTick = DockModel.Now;
        CompositionTarget.Rendering += OnRendering;
        _win.StopIdle();
    }

    void Sleep()
    {
        if (!_rendering) return;
        _rendering = false;
        CompositionTarget.Rendering -= OnRendering;
        _win.StartIdle();
    }

    /// <summary>Kiểm tra nhẹ (50ms) khi không vẽ: chuột có ở gần Dock không.</summary>
    public void IdleCheck()
    {
        if (!IsVisible) return;
        if (_pausedHover) { IdleCheckHover(); return; }
        if (ReadMouse(out double u, out double v) && WantsHover(u, v, false)) Wake();
        else if (EffectiveAutoHide && _hide < 0.99 && !_hover) Wake();
        else if (!EffectiveAutoHide && _hide > 0.001) Wake();
        else if (_slots.Any(s => s.Item.BounceStart >= 0)) Wake();
    }

    /// <summary>Chỉ dùng khi --test: vị trí chuột giả (pixel màn hình).</summary>
    public static POINT? FakeCursor;

    bool ReadMouse(out double u, out double v)
    {
        u = v = -1;
        POINT pt;
        if (FakeCursor is { } fc) pt = fc;
        else if (!Native.GetCursorPos(out pt)) return false;
        // chạm mép màn hình ở bất kỳ đâu dọc theo cạnh của Dock (giống macOS)
        var mb = _win.Monitor.Bounds;
        _atEdge = Edge switch
        {
            DockEdge.Left => pt.X <= mb.Left + 1 && pt.Y >= mb.Top && pt.Y < mb.Bottom,
            DockEdge.Right => pt.X >= mb.Right - 2 && pt.Y >= mb.Top && pt.Y < mb.Bottom,
            _ => pt.Y >= mb.Bottom - 2 && pt.X >= mb.Left && pt.X < mb.Right
        };
        Point p;
        try { p = _win.PointFromScreen(new Point(pt.X, pt.Y)); } catch { return false; }
        if (p.X < -2 || p.Y < -2 || p.X > ActualWidth + 2 || p.Y > ActualHeight + 2) { u = v = -1000; return true; }
        (u, v) = ToUV(p);
        v -= _appliedOffset; // cửa sổ đã dịch ra ngoài khi tự ẩn: tính v từ mép màn hình
        return true;
    }

    bool WantsHover(double u, double v, bool already)
    {
        if (EffectiveAutoHide && _atEdge) return true;
        if (u < -999) return false;
        double hiddenShift = _appliedOffset;
        if (EffectiveAutoHide && _hide > 0.5)
            return v >= -1 && v <= 3 && u >= _plateStart - 30 && u <= _plateEnd + 30;
        double vmax = already ? Margin0 + Pad + S * CurrentMaxScale() + 6 : Margin0 + PlateThick;
        return v >= -1 && v <= vmax - hiddenShift && u >= _plateStart - 2 && u <= _plateEnd + 2;
    }

    double CurrentMaxScale() => _st.Values.Select(s => s.Scale).DefaultIfEmpty(1).Max();

    void OnRendering(object? sender, EventArgs e)
    {
        double now = DockModel.Now;
        Diag.Frames++;
        double dt = Math.Clamp(now - _lastTick, 0, 0.1);
        int fps = App.EffectiveFps;
        if (fps > 0 && now - _lastFrame < 1.0 / fps - 0.003) return;
        _lastTick = now;
        _lastFrame = now;
        double __t0 = FrameStats.Ms;
        try { Tick(now, dt); } finally { if (_hover || Math.Abs(_hide - _hideTarget) > 0.001) FrameStats.Frame(FrameStats.Ms - __t0); }
    }

    void Tick(double now, double dt)
    {

        bool hadMouse = ReadMouse(out double u, out double v);
        bool busy = _dragging || _extDrag || _menuOpen;
        bool hover = busy || (hadMouse && WantsHover(u, v, _hover));
        bool edgeOnly = hover && _atEdge && (u < -999 || u < _plateStart || u > _plateEnd); // ở mép nhưng ngoài Dock: hiện Dock, không phóng to
        // chuột đang ở ô xem trước cửa sổ (hoặc khoảng giữa icon và ô đó): coi như vẫn ở trên Dock — không ẩn, giữ icon phóng to
        bool previewHold = !busy && PreviewWindow.HoldsDock();
        if (_menuOpen) hover = _hover; // giữ nguyên khi menu mở
        else if (previewHold) { hover = true; edgeOnly = false; }
        else { _mouseU = u; _mouseV = v; }

        // tự ẩn (thông minh: chỉ ẩn khi có cửa sổ che Dock, nếu bật)
        bool autoHide = EffectiveAutoHide;
        bool wasHover = _hover;
        double prevHide = _hide;
        if (autoHide)
        {
            if (hover)
            {
                _leaveTime = -1;
                if (_hide > 0.5 && _hideTarget > 0.5)
                {
                    if (_edgeEnterTime < 0) _edgeEnterTime = now;
                    if ((now - _edgeEnterTime) * 1000 >= Cfg.AutoHideShowDelayMs) _hideTarget = 0;
                    hover = _hideTarget < 0.5;
                }
                else _hideTarget = 0;
            }
            else
            {
                _edgeEnterTime = -1;
                if (_hideTarget < 0.5)
                {
                    if (_leaveTime < 0) _leaveTime = now;
                    if ((now - _leaveTime) * 1000 >= Cfg.AutoHideHideDelayMs) _hideTarget = 1;
                }
            }
        }
        else _hideTarget = 0;
        _hover = hover && !edgeOnly && _hide < 0.6;

        double k = App.EffectsOff ? 1 : 1 - Math.Exp(-dt * Cfg.AnimSpeed);
        bool animating = false;

        if (Math.Abs(_hide - _hideTarget) > 0.001)
        {
            double kh = App.EffectsOff ? 1 : 1 - Math.Exp(-dt * (_hideTarget < 0.5 ? 26 : 14));
            _hide += (_hideTarget - _hide) * kh;
            if (Math.Abs(_hide - _hideTarget) < 0.005) _hide = _hideTarget;
            animating = true;
        }

        // ẩn/hiện bằng cách dịch cửa sổ (không vẽ lại cả Dock mỗi khung hình)
        double offset = _hide * HideDistance;
        if (Math.Abs(offset - _appliedOffset) > 0.01) { _appliedOffset = offset; _win.SetHideOffset(offset); }

        ComputeTargets();
        bool scaling = false;
        foreach (var s in _st.Values)
        {
            double d = s.Target - s.Scale;
            if (Math.Abs(d) > 0.001) { s.Scale += d * k; if (Math.Abs(s.Target - s.Scale) < 0.002) s.Scale = s.Target; animating = true; scaling = true; }
        }

        bool bouncing = false;
        foreach (var it in DockModel.Instance.Items)
        {
            DockModel.BounceOffset(it, now, out bool a);
            if (a) bouncing = true;
        }

        bool moved = _hover && (Math.Abs(u - _stillU) > 0.01 || Math.Abs(v - _stillV) > 0.01);
        if (scaling || bouncing || busy || moved || wasHover != _hover || (prevHide >= 0.999) != (_hide >= 0.999)) InvalidateVisual();

        if (!animating && !bouncing && !_hover && !busy && (!autoHide || _hideTarget > 0.5) && _pressItem == null)
            Sleep();
        else if (!animating && !bouncing && _hover && !busy && _pressItem == null && Math.Abs(u - _stillU) < 0.01 && Math.Abs(v - _stillV) < 0.01)
        {
            // chuột đứng yên: tạm dừng vẽ, kiểm tra nhàn rỗi sẽ đánh thức lại khi chuột di chuyển
            if (++_stillFrames > 6) { _stillFrames = 0; SleepWhileHover(); }
        }
        else _stillFrames = 0;
        _stillU = u; _stillV = v;
        UpdateHitRegion(bouncing || busy);
        CheckPreview();
    }

    DockItem? _previewCandidate;
    double _hoverSince;

    void CheckPreview()
    {
        var it = _hover ? _hoverItem : null;
        if (it != _previewCandidate) { _previewCandidate = it; _hoverSince = DockModel.Now; }
        if (it == null || it.Kind != DockItemKind.App || !it.Running || _dragging || _menuOpen || _pressItem != null || !Cfg.WindowPreviews) return;
        if (PreviewWindow.IsOpenFor(it)) return;
        if (PreviewWindow.AnyOpen || (DockModel.Now - _hoverSince) * 1000 >= Cfg.PreviewDelayMs)
        {
            var s = _slots.FirstOrDefault(x => x.Item == it);
            if (s.Item == null) return;
            var r = R(s.Center - s.Size / 2, Margin0 + Pad + s.Bounce, s.Size, s.Size);
            try
            {
                var a = PointToScreen(r.TopLeft); var b = PointToScreen(r.BottomRight);
                PreviewWindow.ShowFor(_win, it, new Rect(a, b));
                InvalidateVisual();
            }
            catch { }
        }
    }

    double _appliedOffset;
    bool _atEdge;
    bool _covered;
    double HideDistance => Margin0 + PlateThick + 6;
    bool EffectiveAutoHide => Cfg.AutoHide && (_covered || !Cfg.AutoHideOnlyWhenCovered);

    /// <summary>Có cửa sổ nào che vùng Dock không (do DockWindow kiểm tra định kỳ khi bật tự ẩn).</summary>
    public bool IsCoveredFlag => _covered;
    public void SetCovered(bool covered)
    {
        if (covered == _covered) return;
        _covered = covered;
        _leaveTime = -1;
        Wake();
    }

    /// <summary>Vùng tấm nền Dock trên màn hình (pixel), dùng để kiểm tra cửa sổ che.</summary>
    public Rect PlateScreenRect()
    {
        try
        {
            var r = R(_plateStart, 0, _plateEnd - _plateStart, Margin0 + PlateThick + 2);
            var a = PointToScreen(r.TopLeft); var b = PointToScreen(r.BottomRight);
            // trừ phần đã dịch ra ngoài khi đang ẩn
            return new Rect(a, b);
        }
        catch { return Rect.Empty; }
    }

    double _stillU, _stillV;
    int _stillFrames;
    bool _pausedHover;

    void SleepWhileHover()
    {
        _pausedHover = true;
        Sleep();
    }

    /// <summary>Gọi từ bộ hẹn giờ nhàn rỗi khi đang hover mà dừng vẽ.</summary>
    public void IdleCheckHover()
    {
        if (!_pausedHover) return;
        if (PreviewWindow.HoldsDock()) return; // chuột đang ở ô xem trước: giữ nguyên, không vẽ lại
        CheckPreview();
        if (ReadMouse(out double u, out double v) && (Math.Abs(u - _stillU) > 0.01 || Math.Abs(v - _stillV) > 0.01 || !WantsHover(u, v, true)))
        {
            _pausedHover = false;
            Wake();
        }
    }

    // ------------------------------------------------------------------ bố cục

    List<DockItem> OrderedItems()
    {
        var items = DockModel.Instance.Items.ToList();
        if (_dragging && _dragItem != null)
        {
            items.Remove(_dragItem);
            int idx = Math.Clamp(_dragInsert, 0, items.Count);
            if (!_dragOut) items.Insert(idx, _dragItem);
        }
        return items;
    }

    void EnsureStates(List<DockItem> items)
    {
        foreach (var it in items) if (!_st.ContainsKey(it)) _st[it] = new VState();
        if (_st.Count > items.Count + 4)
            foreach (var k in _st.Keys.Where(k => !items.Contains(k)).ToList()) _st.Remove(k);
    }

    void ComputeTargets()
    {
        var items = OrderedItems();
        EnsureStates(items);
        double unit = S + G;
        // bố cục gốc
        double baseL = items.Sum(i => (i.Kind == DockItemKind.Separator ? SepW : S) + G);
        double baseStart = (MainLength - baseL) / 2;

        double mb = double.NaN;
        if (_hover && MaxScale > 1.001)
        {
            // map vị trí chuột trong bố cục đang phóng về bố cục gốc
            double L = 0;
            foreach (var it in items) L += SlotWidth(it);
            double start = (MainLength - L) / 2;
            double pos = start, bpos = baseStart;
            mb = baseStart + (_mouseU - start);
            if (_mouseU >= start)
            {
                bool found = false;
                foreach (var it in items)
                {
                    double w = SlotWidth(it), bw = (it.Kind == DockItemKind.Separator ? SepW : S) + G;
                    if (_mouseU < pos + w) { mb = bpos + (_mouseU - pos) / w * bw; found = true; break; }
                    pos += w; bpos += bw;
                }
                if (!found) mb = bpos + (_mouseU - pos);
            }
        }

        double bp = baseStart;
        foreach (var it in items)
        {
            double bw = (it.Kind == DockItemKind.Separator ? SepW : S) + G;
            double c = bp + bw / 2;
            bp += bw;
            var st = _st[it];
            if (it.Kind == DockItemKind.Separator || double.IsNaN(mb)) { st.Target = 1; continue; }
            double x = Math.Abs(mb - c) / (Cfg.CurveRange * unit);
            double f = x < 1 ? (Math.Cos(Math.PI * x) + 1) / 2 : 0;
            st.Target = 1 + (MaxScale - 1) * f;
        }
    }

    double SlotWidth(DockItem it) => it.Kind == DockItemKind.Separator ? SepW + G : S * (_st.TryGetValue(it, out var s) ? s.Scale : 1) + G;

    void Layout(double now)
    {
        var items = OrderedItems();
        EnsureStates(items);
        var slots = new List<Slot>(items.Count);
        double L = items.Sum(SlotWidth);
        double pos = (MainLength - L) / 2;
        foreach (var it in items)
        {
            double w = SlotWidth(it);
            double size = it.Kind == DockItemKind.Separator ? SepW : w - G;
            double b = DockModel.BounceOffset(it, now, out _);
            slots.Add(new Slot { Item = it, Start = pos, Width = w, Center = pos + w / 2, Size = size, Bounce = b, Placeholder = _dragging && it == _dragItem });
            pos += w;
        }
        _slots = slots;
        if (slots.Count > 0)
        {
            _plateStart = slots[0].Start + G / 2 - Pad;
            _plateEnd = slots[^1].Start + slots[^1].Width - G / 2 + Pad;
        }
        else { _plateStart = MainLength / 2 - 40; _plateEnd = MainLength / 2 + 40; }
    }

    // ------------------------------------------------------------------ toạ độ

    (double u, double v) ToUV(Point p) => Edge switch
    {
        DockEdge.Left => (p.Y, p.X),
        DockEdge.Right => (p.Y, ActualWidth - p.X),
        _ => (p.X, ActualHeight - p.Y)
    };

    Rect R(double u0, double v0, double du, double dv) => Edge switch
    {
        DockEdge.Left => new Rect(v0, u0, Math.Max(0, dv), Math.Max(0, du)),
        DockEdge.Right => new Rect(ActualWidth - v0 - dv, u0, Math.Max(0, dv), Math.Max(0, du)),
        _ => new Rect(u0, ActualHeight - v0 - dv, Math.Max(0, du), Math.Max(0, dv))
    };

    Point Pt(double u, double v) => Edge switch
    {
        DockEdge.Left => new Point(v, u),
        DockEdge.Right => new Point(ActualWidth - v, u),
        _ => new Point(u, ActualHeight - v)
    };

    // ------------------------------------------------------------------ vẽ

    protected override void OnRender(DrawingContext dc)
    {
        var theme = ThemeManager.Current;
        if (theme == null) return;
        Diag.Renders++;
        double __r0 = FrameStats.Ms;
        try { RenderCore(dc, theme); } finally { FrameStats.Render(FrameStats.Ms - __r0); }
    }

    void RenderCore(DrawingContext dc, ResolvedTheme theme)
    {
        double now = DockModel.Now;
        Layout(now);
        double shift = 0;

        // dải kích hoạt ở mép màn hình khi tự ẩn (cửa sổ đã dịch ra ngoài _appliedOffset)
        if (Cfg.AutoHide)
            dc.DrawRectangle(HitBrush, null, R(_plateStart - 30, _appliedOffset, _plateEnd - _plateStart + 60, 3));

        if (_hide >= 0.999) return;

        double v0 = Margin0 - shift;
        // nền Dock
        var plate = R(_plateStart, v0, _plateEnd - _plateStart, PlateThick);
        double radius = Math.Min(theme.CornerRadius, PlateThick / 2);
        Pen? border = theme.BorderThickness > 0 ? new Pen(theme.DockBorder, theme.BorderThickness) : null;
        if (Cfg.Shadow && theme.ShadowOpacity > 0) DrawShadow(dc, plate, radius, theme);
        dc.DrawRoundedRectangle(theme.DockBackground, border, plate, radius, radius);
        if (theme.Def.DockMaterial == "glass" && theme.BorderThickness > 0)
        {
            var inner = plate; inner.Inflate(-theme.BorderThickness - 0.5, -theme.BorderThickness - 0.5);
            if (inner.Width > 0 && inner.Height > 0)
                dc.DrawRoundedRectangle(null, new Pen(theme.DockHighlight, 0.8), inner, Math.Max(0, radius - 1.5), Math.Max(0, radius - 1.5));
        }

        _hoverItem = null;
        double fontSize = 12.5;
        foreach (var s in _slots)
        {
            if (s.Placeholder) continue;
            var it = s.Item;
            if (it.Kind == DockItemKind.Separator)
            {
                var a = Pt(s.Center, v0 + Pad * 0.7);
                var b = Pt(s.Center, v0 + PlateThick - Pad * 0.7);
                dc.DrawLine(new Pen(theme.Separator, 1), a, b);
                continue;
            }
            double iv = v0 + Pad + s.Bounce;
            var rect = R(s.Center - s.Size / 2, iv, s.Size, s.Size);
            // vùng bắt chuột cả cột
            dc.DrawRectangle(HitBrush, null, R(s.Start, 0, s.Width, iv + s.Size));
            DrawIcon(dc, it, rect, theme);

            if (Cfg.ShowRunningDots && it.Running)
            {
                double r = Math.Max(1.7, S * 0.045);
                dc.DrawEllipse(theme.Dot, null, Pt(s.Center, v0 + Math.Max(r + 1, Pad * 0.42)), r, r);
            }
            if (it.Badge != 0) DrawBadge(dc, rect, it.Badge, theme);

            if (_hover && !_dragging && _mouseU >= s.Start && _mouseU < s.Start + s.Width) _hoverItem = it;
            if (_extDrag && _extTarget == it)
            {
                dc.DrawRoundedRectangle(null, new Pen(theme.Accent, 2), Inflate(rect, 2), s.Size * 0.24, s.Size * 0.24);
            }
        }

        // mục đang kéo
        if (_dragging && _dragItem != null)
        {
            double size = S * (_dragOut ? 1.0 : 1.15);
            var rect = R(_mouseU - size / 2, Math.Max(v0 + Pad, _mouseV - size / 2), size, size);
            dc.PushOpacity(_dragOut ? 0.55 : 0.9);
            DrawIcon(dc, _dragItem, rect, theme);
            dc.Pop();
            if (_dragOut && _dragItem.Pinned) DrawLabel(dc, L.T("Gỡ khỏi Dock"), rect, theme, fontSize);
        }

        // nhãn tên
        if (Cfg.ShowLabels && _hoverItem != null && !_menuOpen && _hoverItem.Kind != DockItemKind.Separator && !PreviewWindow.IsOpenFor(_hoverItem))
        {
            var s = _slots.First(x => x.Item == _hoverItem);
            var rect = R(s.Center - s.Size / 2, v0 + Pad + s.Bounce, s.Size, s.Size);
            string name = _hoverItem.Kind == DockItemKind.App && _hoverItem.Windows.Count > 1 ? $"{_hoverItem.Name}  ({_hoverItem.Windows.Count})" : _hoverItem.Name;
            DrawLabel(dc, name, rect, theme, fontSize);
        }
    }

    static Rect Inflate(Rect r, double d) { r.Inflate(d, d); return r; }

    /// <summary>Vùng nhận chuột của cửa sổ Dock: cả cửa sổ khi đang tương tác/hiệu ứng, ngược lại chỉ nền Dock (để bấm xuyên xuống app phía dưới).</summary>
    void UpdateHitRegion(bool active)
    {
        if (!_win.Composited) return;
        if (active || _hover || _pressItem != null || PreviewWindow.AnyOpen || CurrentMaxScale() > 1.001) { _win.SetHitRegion(null); return; }
        double blur = Cfg.Shadow ? Math.Clamp(ThemeManager.Current?.ShadowBlur ?? 0, 4, 40) * 0.55 + 4 : 2;
        var list = new List<Rect>
        {
            R(_plateStart - blur, 0, _plateEnd - _plateStart + 2 * blur, Margin0 + PlateThick + blur),
            R(_plateStart - 30, _appliedOffset, _plateEnd - _plateStart + 60, 3),
        };
        _win.SetHitRegion(list);
    }

    Brush[]? _shadowLayers;
    const int ShadowSteps = 7;

    /// <summary>Bóng mềm quanh nền Dock: vài lớp bo góc mờ dần (rẻ hơn nhiều so với hiệu ứng làm mờ), chừa phần dưới nền.</summary>
    void DrawShadow(DrawingContext dc, Rect plate, double radius, ResolvedTheme theme)
    {
        double blur = Math.Clamp(theme.ShadowBlur, 4, 40) * 0.55;
        if (_shadowLayers == null)
        {
            _shadowLayers = new Brush[ShadowSteps];
            for (int i = 0; i < ShadowSteps; i++)
            {
                double a = theme.ShadowOpacity * 0.55 / ShadowSteps * (1 + 0.6 * (ShadowSteps - i) / ShadowSteps);
                var b = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(a * 255, 1, 255), 0, 0, 0)); b.Freeze();
                _shadowLayers[i] = b;
            }
        }
        var shadow = plate;
        var off = R(0, -2.5, 0, 0).TopLeft - R(0, 0, 0, 0).TopLeft; // lệch nhẹ về phía mép màn hình
        shadow.Offset(off);
        var outer = shadow; outer.Inflate(blur + 2, blur + 2);
        var clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(outer), new RectangleGeometry(plate, radius, radius));
        dc.PushClip(clip);
        for (int i = 0; i < ShadowSteps; i++)
        {
            double d = blur * (i + 1) / ShadowSteps;
            var r = shadow; r.Inflate(d, d);
            dc.DrawRoundedRectangle(_shadowLayers[i], null, r, radius + d, radius + d);
        }
        dc.Pop();
    }

    void DrawIcon(DrawingContext dc, DockItem it, Rect rect, ResolvedTheme theme)
    {
        var img = DockModel.Instance.GetIcon(it);
        bool squircle = theme.IconStyle == IconStyle.Squircle && it.Kind == DockItemKind.App && it.Icon != null && !HasOverride(it);
        if (squircle)
        {
            var g = Glyphs.Squircle(Inflate(rect, -rect.Width * 0.04));
            dc.DrawGeometry(theme.IconPlate, new Pen(theme.Separator, 0.6), g);
            if (img != null) dc.DrawImage(img, Inflate(rect, -rect.Width * 0.2));
        }
        else if (img != null)
        {
            dc.DrawImage(img, it.Kind == DockItemKind.App ? Inflate(rect, -rect.Width * 0.03) : rect);
        }
        else
        {
            // chờ nạp icon
            dc.DrawRoundedRectangle(theme.IconPlate, null, Inflate(rect, -rect.Width * 0.08), rect.Width * 0.2, rect.Width * 0.2);
        }
    }

    static bool HasOverride(DockItem it) => Config.Current.Theme.IconOverrides.ContainsKey(it.Key);

    void DrawBadge(DrawingContext dc, Rect icon, int badge, ResolvedTheme theme)
    {
        double d = Math.Max(10, icon.Width * 0.34);
        var c = new Point(icon.Right - d * 0.32, icon.Top + d * 0.32);
        if (badge < 0)
        {
            dc.DrawEllipse(theme.Badge, new Pen(Brushes.White, 1.2), c, d * 0.3, d * 0.3);
            return;
        }
        string text = badge > 99 ? "99+" : badge.ToString(CultureInfo.InvariantCulture);
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(theme.Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), d * 0.6, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        double w = Math.Max(d, ft.Width + d * 0.5);
        var r = new Rect(c.X - w / 2, c.Y - d / 2, w, d);
        dc.DrawRoundedRectangle(theme.Badge, new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), 0.5), r, d / 2, d / 2);
        dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
    }

    void DrawLabel(DrawingContext dc, string text, Rect icon, ResolvedTheme theme, double size)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(theme.Font, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal), size, theme.LabelForeground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        double w = ft.Width + 20, h = ft.Height + 8;
        Rect r = Edge switch
        {
            DockEdge.Left => new Rect(icon.Right + 12, icon.Top + icon.Height / 2 - h / 2, w, h),
            DockEdge.Right => new Rect(icon.Left - 12 - w, icon.Top + icon.Height / 2 - h / 2, w, h),
            _ => new Rect(icon.Left + icon.Width / 2 - w / 2, icon.Top - 10 - h, w, h)
        };
        if (r.Left < 2) r.X = 2;
        if (r.Right > ActualWidth - 2) r.X = ActualWidth - 2 - w;
        if (r.Top < 1) r.Y = 1;
        dc.DrawRoundedRectangle(theme.LabelBackground, new Pen(theme.Separator, 0.5), r, 6, 6);
        dc.DrawText(ft, new Point(r.X + 10, r.Y + 4));
    }

    // ------------------------------------------------------------------ chuột

    DockItem? ItemAt(double u, double v)
    {
        foreach (var s in _slots)
            if (u >= s.Start && u < s.Start + s.Width) return s.Item;
        return null;
    }

    int InsertIndexAt(double u)
    {
        // vị trí chèn trong danh sách đã bỏ mục đang kéo
        var items = DockModel.Instance.Items.Where(i => i != _dragItem).ToList();
        int firstApp = items.FindIndex(i => i.Kind == DockItemKind.App);
        int lastApp = items.FindLastIndex(i => i.Kind == DockItemKind.App);
        if (firstApp < 0) { firstApp = items.FindIndex(i => i.Kind != DockItemKind.Launchpad); if (firstApp < 0) firstApp = items.Count; lastApp = firstApp - 1; }
        int idx = 0;
        foreach (var s in _slots)
        {
            if (s.Item == _dragItem) continue;
            if (u > s.Center) idx++;
        }
        return Math.Clamp(idx, firstApp, lastApp + 1);
    }

    void OnDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        var (u, v) = ToUV(p);
        _pressItem = ItemAt(u, v);
        _pressPos = p;
        if (_pressItem != null) CaptureMouse();
        Wake();
        e.Handled = true;
    }

    void OnDragMove(object sender, MouseEventArgs e)
    {
        if (_pressItem == null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        if (!_dragging)
        {
            if ((p - _pressPos).Length < 8 || !_pressItem.IsDraggable) return;
            _dragging = true;
            _dragItem = _pressItem;
            Wake();
        }
        var (u, v) = ToUV(p);
        _mouseU = u; _mouseV = v;
        _dragOut = v > Margin0 + PlateThick + S * 1.6;
        _dragInsert = InsertIndexAt(u);
        InvalidateVisual();
    }

    void OnUp(object sender, MouseButtonEventArgs e)
    {
        var item = _pressItem;
        bool wasDrag = _dragging;
        PreviewWindow.HideNow();
        if (_dragging) EndDrag(true);
        _pressItem = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (!wasDrag && item != null)
        {
            var (u, v) = ToUV(e.GetPosition(this));
            if (ItemAt(u, v) == item) DockModel.Instance.Activate(item);
        }
        e.Handled = true;
    }

    void EndDrag(bool commit)
    {
        var item = _dragItem;
        bool outside = _dragOut;
        int insert = _dragInsert;
        _dragging = false; _dragItem = null; _dragOut = false;
        if (!commit || item == null) { InvalidateVisual(); return; }
        // thả lên Thùng rác cũng là gỡ
        var (u, v) = (_mouseU, _mouseV);
        var target = ItemAt(u, v);
        if (outside || target?.Kind == DockItemKind.Trash)
        {
            if (item.Pinned) DockModel.Instance.SetPinned(item, false);
            return;
        }
        var order = DockModel.Instance.Items.Where(i => i != item).ToList();
        order.Insert(Math.Clamp(insert, 0, order.Count), item);
        DockModel.Instance.Reorder(order.Where(i => i.Kind == DockItemKind.App).ToList(), item);
    }

    void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        var (u, v) = ToUV(e.GetPosition(this));
        var item = ItemAt(u, v) ?? DockModel.Instance.Items.FirstOrDefault(i => i.Kind == DockItemKind.Separator) ?? DockModel.Instance.Items.FirstOrDefault();
        if (item == null) return;
        ShowMenu(item);
        e.Handled = true;
    }

    ContextMenu? _openMenu;
    public void CloseMenu() { if (_openMenu != null) { _openMenu.IsOpen = false; _openMenu = null; } }

    public void ShowMenu(DockItem item)
    {
        PreviewWindow.HideNow();
        var menu = DockModel.Instance.BuildMenu(item);
        menu.PlacementTarget = this;
        var s = _slots.FirstOrDefault(x => x.Item == item);
        var rect = R(s.Center - s.Size / 2, Margin0 + Pad, s.Size, s.Size);
        menu.Placement = Edge switch { DockEdge.Left => System.Windows.Controls.Primitives.PlacementMode.Right, DockEdge.Right => System.Windows.Controls.Primitives.PlacementMode.Left, _ => System.Windows.Controls.Primitives.PlacementMode.Top };
        menu.PlacementRectangle = rect;
        menu.HorizontalOffset = Edge == DockEdge.Bottom ? (rect.Width - 220) / 2 : 0;
        _openMenu = menu;
        menu.Opened += (_, _) => { _menuOpen = true; Log.Debug("Dock menu mở"); };
        menu.Closed += (_, _) => { _menuOpen = false; Log.Debug("Dock menu đóng"); Wake(); };
        _menuOpen = true;
        menu.IsOpen = true;
        Wake();
    }

    /// <summary>Toạ độ màn hình (pixel) của tâm icon thứ i — dùng cho kiểm thử.</summary>
    public POINT? ItemScreenCenter(int index)
    {
        if (index < 0 || index >= _slots.Count) return null;
        var s = _slots[index];
        var p = PointToScreen(Pt(s.Center, Margin0 + Pad + S / 2));
        return new POINT((int)p.X, (int)p.Y);
    }

    // ------------------------------------------------------------------ kéo thả từ ngoài

    void OnExtDrag(object sender, DragEventArgs e)
    {
        _extDrag = true;
        Wake();
        e.Effects = DragDropEffects.None;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Handled = true; return; }
        var files = e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
        var (u, v) = ToUV(e.GetPosition(this));
        _mouseU = u; _mouseV = v;
        var target = ItemAt(u, v);
        _extTarget = null;
        if (target?.Kind == DockItemKind.Trash) { e.Effects = DragDropEffects.Move; _extTarget = target; }
        else if (target?.Kind == DockItemKind.App && target.PinKind != "folder" && !files.All(DockModel.IsPinnable)) { e.Effects = DragDropEffects.Copy; _extTarget = target; }
        else if (target?.Kind == DockItemKind.App && files.Any(f => !DockModel.IsPinnable(f))) { e.Effects = DragDropEffects.Copy; _extTarget = target; }
        else if (files.Any(DockModel.IsPinnable)) e.Effects = DragDropEffects.Link;
        e.Handled = true;
    }

    void OnExtDrop(object sender, DragEventArgs e)
    {
        _extDrag = false;
        var target = _extTarget;
        _extTarget = null;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var (u, _) = ToUV(e.GetPosition(this));
            int idx = 0;
            foreach (var s in _slots) if (u > s.Center) idx++;
            DockModel.Instance.HandleDrop(files, target, idx);
        }
        Wake();
        e.Handled = true;
    }
}
