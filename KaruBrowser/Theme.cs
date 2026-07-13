using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Karu;

/// <summary>チャコール×セージのカラーパレットと描画ヘルパー。</summary>
public static class Theme
{
    // チャコール(背景系)
    public static readonly Color Bg      = Color.FromArgb(31, 33, 35);   // #1F2123 ウィンドウ・タブストリップ
    public static readonly Color BgBar   = Color.FromArgb(38, 41, 43);   // #26292B ツールバー・アクティブタブ
    public static readonly Color Surface = Color.FromArgb(46, 50, 53);   // #2E3235 ホバー面
    public static readonly Color Field   = Color.FromArgb(26, 28, 29);   // #1A1C1D 入力面
    public static readonly Color Border  = Color.FromArgb(58, 63, 66);   // #3A3F42 境界線

    // セージ(アクセント)
    public static readonly Color Sage       = Color.FromArgb(156, 175, 136); // #9CAF88
    public static readonly Color SageBright = Color.FromArgb(181, 198, 163); // #B5C6A3

    // テキスト
    public static readonly Color Text     = Color.FromArgb(232, 234, 228); // #E8EAE4
    public static readonly Color TextMute = Color.FromArgb(155, 162, 154); // #9BA29A
    public static readonly Color TextDim  = Color.FromArgb(94, 100, 96);   // 無効・控えめ

    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>上の角だけ丸めた矩形(タブ用)。</summary>
    public static GraphicsPath RoundedRectTop(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
        path.CloseFigure();
        return path;
    }

    // ---- ダークタイトルバー(DWM) ----

    private const int DwmwaUseImmersiveDarkMode = 20; // Win10 1809+
    private const int DwmwaCaptionColor = 35;         // Win11

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>タイトルバーをチャコールに合わせる。非対応 OS では単に無視される。</summary>
    public static void ApplyDarkTitleBar(Form form)
    {
        int dark = 1;
        DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        int caption = Bg.R | (Bg.G << 8) | (Bg.B << 16); // COLORREF (0x00BBGGRR)
        DwmSetWindowAttribute(form.Handle, DwmwaCaptionColor, ref caption, sizeof(int));
    }
}
