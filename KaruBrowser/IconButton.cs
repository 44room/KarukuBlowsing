using System.Drawing.Drawing2D;

namespace Karu;

/// <summary>ホバーで円形ハイライトが出るフラットなアイコンボタン(戻る・進む・再読み込み用)。</summary>
public class IconButton : Control
{
    private bool _hover;
    private bool _pressed;

    public IconButton(string glyph)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Text = glyph;
        Size = new Size(34, 34);
        Margin = new Padding(2, 0, 2, 0);
        Font = new Font("Yu Gothic UI", 11f);
        BackColor = Theme.BgBar;
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled) { _hover = false; _pressed = false; }
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        if (Enabled && (_hover || _pressed))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(_pressed ? Theme.Border : Theme.Surface);
            g.FillEllipse(b, 1, 1, Width - 3, Height - 3);
        }

        var color = !Enabled ? Theme.TextDim : _hover ? Theme.Text : Theme.TextMute;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
