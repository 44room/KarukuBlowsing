using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Karu;

/// <summary>
/// 自前描画のタブストリップ。アクティブタブは直下のツールバーと同色で一体化し、
/// 上辺にセージのインジケータが点く。✕(選択中・ホバー中のタブに表示)と中クリックで閉じ、
/// 右端の ＋ で新規タブ。
/// </summary>
public class TabStrip : Control
{
    private sealed class TabItem
    {
        public string Title = "";
    }

    private readonly List<TabItem> _tabs = new();
    private readonly ToolTip _tip = new();
    private int _selected = -1;
    private int _hoverIndex = -1;
    private bool _hoverClose;
    private bool _hoverPlus;

    public event EventHandler? SelectedChanged;
    public event EventHandler<int>? CloseRequested;
    public event EventHandler? NewTabRequested;

    public TabStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
        Height = 40;
    }

    // ---- 公開 API ----

    public int Count => _tabs.Count;

    public string? SelectedTitle => _selected >= 0 && _selected < _tabs.Count ? _tabs[_selected].Title : null;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < 0 || value >= _tabs.Count || value == _selected) return;
            _selected = value;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Add(string title)
    {
        _tabs.Add(new TabItem { Title = title });
        Invalidate();
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _tabs.RemoveAt(index);
        _hoverIndex = -1;
        _hoverClose = false;

        if (_tabs.Count == 0)
        {
            _selected = -1;
            Invalidate();
            return;
        }

        if (index < _selected)
        {
            _selected--;
        }
        else if (index == _selected)
        {
            _selected = Math.Min(index, _tabs.Count - 1);
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
        Invalidate();
    }

    public void SetTitle(int index, string title)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _tabs[index].Title = title;
        Invalidate();
    }

    // ---- レイアウト ----

    private int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    private int TabTop => S(6);
    private int LeftPad => S(8);

    private int TabWidth
    {
        get
        {
            if (_tabs.Count == 0) return S(220);
            int avail = Width - LeftPad - S(28) - S(20);
            return Math.Clamp(avail / _tabs.Count, S(72), S(220));
        }
    }

    private Rectangle TabRect(int i) => new(LeftPad + i * TabWidth, TabTop, TabWidth, Height - TabTop);

    private Rectangle CloseRect(Rectangle tab)
    {
        int s = S(18);
        return new(tab.Right - s - S(8), tab.Y + (tab.Height - s) / 2, s, s);
    }

    private Rectangle PlusRect()
    {
        int size = S(28);
        int x = LeftPad + _tabs.Count * TabWidth + S(6);
        int y = TabTop + (Height - TabTop - size) / 2;
        return new(x, y, size, size);
    }

    // ---- 描画 ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        for (int i = 0; i < _tabs.Count; i++)
        {
            var r = TabRect(i);
            bool sel = i == _selected;
            bool hov = i == _hoverIndex;

            if (sel)
            {
                using var path = Theme.RoundedRectTop(r, S(8));
                using var fill = new SolidBrush(Theme.BgBar);
                g.FillPath(fill, path);

                // セージのインジケータ(タブ形状にクリップして上辺に沿わせる)
                var save = g.Clip;
                g.SetClip(path);
                using var sage = new SolidBrush(Theme.Sage);
                g.FillRectangle(sage, r.X, r.Y, r.Width, S(3));
                g.Clip = save;
            }
            else if (hov)
            {
                var hr = new Rectangle(r.X + S(2), r.Y + S(2), r.Width - S(4), r.Height - S(6));
                using var path = Theme.RoundedRect(hr, S(6));
                using var fill = new SolidBrush(Theme.Surface);
                g.FillPath(fill, path);
            }
            else if (i < _tabs.Count - 1 && i + 1 != _selected && i + 1 != _hoverIndex)
            {
                // 隣接する非アクティブタブの間に控えめな区切り線
                var prev = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.None;
                using var pen = new Pen(Theme.Border);
                int mid = r.Y + r.Height / 2;
                g.DrawLine(pen, r.Right, mid - S(7), r.Right, mid + S(7));
                g.SmoothingMode = prev;
            }

            bool showClose = sel || hov;
            var cr = CloseRect(r);
            int textRight = showClose ? cr.Left - S(4) : r.Right - S(10);
            var textRect = new Rectangle(r.X + S(12), r.Y, Math.Max(0, textRight - r.X - S(12)), r.Height);
            TextRenderer.DrawText(g, _tabs[i].Title, Font, textRect,
                sel ? Theme.Text : Theme.TextMute,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            if (showClose)
            {
                bool hc = hov && _hoverClose;
                if (hc)
                {
                    using var b = new SolidBrush(sel ? Theme.Surface : Theme.Border);
                    g.FillEllipse(b, cr);
                }
                TextRenderer.DrawText(g, "✕", Font, cr,
                    hc ? Theme.Text : Theme.TextMute,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        // ＋ 新規タブボタン
        var pr = PlusRect();
        if (_hoverPlus)
        {
            using var path = Theme.RoundedRect(pr, S(6));
            using var fill = new SolidBrush(Theme.Surface);
            g.FillPath(fill, path);
        }
        TextRenderer.DrawText(g, "＋", Font, pr,
            _hoverPlus ? Theme.Text : Theme.TextMute,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    // ---- マウス ----

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int index = -1;
        bool close = false;
        bool plus = PlusRect().Contains(e.Location);

        if (!plus)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var r = TabRect(i);
                if (!r.Contains(e.Location)) continue;
                index = i;
                close = CloseRect(r).Contains(e.Location);
                break;
            }
        }

        if (index != _hoverIndex || close != _hoverClose || plus != _hoverPlus)
        {
            if (plus != _hoverPlus)
                _tip.SetToolTip(this, plus ? "新しいタブ (Ctrl+T)" : null);
            _hoverIndex = index;
            _hoverClose = close;
            _hoverPlus = plus;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hoverIndex != -1 || _hoverClose || _hoverPlus)
        {
            _hoverIndex = -1;
            _hoverClose = false;
            _hoverPlus = false;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && PlusRect().Contains(e.Location))
        {
            NewTabRequested?.Invoke(this, EventArgs.Empty);
            base.OnMouseDown(e);
            return;
        }

        for (int i = 0; i < _tabs.Count; i++)
        {
            var r = TabRect(i);
            if (!r.Contains(e.Location)) continue;

            if (e.Button == MouseButtons.Middle ||
                (e.Button == MouseButtons.Left && (i == _selected || i == _hoverIndex) && CloseRect(r).Contains(e.Location)))
            {
                CloseRequested?.Invoke(this, i);
            }
            else if (e.Button == MouseButtons.Left)
            {
                SelectedIndex = i;
            }
            break;
        }
        base.OnMouseDown(e);
    }
}
