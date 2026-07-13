using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Karu;

/// <summary>ピル型のアドレスバー。フォーカスでセージのリングが点く。</summary>
public class AddressBar : Panel
{
    private readonly TextBox _box;
    private bool _focused;

    public event EventHandler? NavigateRequested;
    public event EventHandler? EscapePressed;

    public AddressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.BgBar;
        Cursor = Cursors.IBeam;
        Height = 34;

        _box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Field,
            ForeColor = Theme.Text,
            Font = new Font("Yu Gothic UI", 10.5f),
            PlaceholderText = "検索または URL を入力",
        };
        _box.KeyDown += OnBoxKeyDown;
        _box.GotFocus += (_, _) =>
        {
            _focused = true;
            Invalidate();
            BeginInvoke(_box.SelectAll);
        };
        _box.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        Controls.Add(_box);

        Click += (_, _) => FocusInput();
    }

    [AllowNull]
    public override string Text
    {
        get => _box.Text;
        set => _box.Text = value ?? "";
    }

    public void FocusInput()
    {
        _box.Focus();
        _box.SelectAll();
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            NavigateRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            EscapePressed?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_box is null) return; // コンストラクタ内の Height 設定時はまだ未生成
        int pad = 16;
        _box.SetBounds(pad, (Height - _box.Height) / 2 + 1, Math.Max(0, Width - pad * 2), _box.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.RoundedRect(r, r.Height / 2);
        using var fill = new SolidBrush(Theme.Field);
        g.FillPath(fill, path);

        if (_focused)
        {
            using var glow = new Pen(Color.FromArgb(70, Theme.Sage), 3.5f);
            g.DrawPath(glow, path);
            using var pen = new Pen(Theme.Sage, 1.5f);
            g.DrawPath(pen, path);
        }
        else
        {
            using var pen = new Pen(Theme.Border);
            g.DrawPath(pen, path);
        }
    }
}
