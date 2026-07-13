using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Karu;

public class MainForm : Form
{
    private const string AppName = "かるぶらうじんぐ";
    private const string NewTabTitle = "新しいタブ";

    private readonly Button _backBtn;
    private readonly Button _fwdBtn;
    private readonly Button _reloadBtn;
    private readonly Button _newTabBtn;
    private readonly TextBox _addressBar;
    private readonly TabControl _tabs;
    private readonly string? _startupUrl;
    private CoreWebView2Environment? _env;

    public MainForm(string? startupUrl = null)
    {
        _startupUrl = startupUrl;

        Text = AppName;
        ClientSize = new Size(1200, 780);
        MinimumSize = new Size(480, 360);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Yu Gothic UI", 9.5f);
        KeyPreview = true;
        BackColor = Color.FromArgb(243, 243, 243);

        _backBtn = MakeNavButton("←", "戻る (Alt+←)");
        _fwdBtn = MakeNavButton("→", "進む (Alt+→)");
        _reloadBtn = MakeNavButton("⟳", "再読み込み (F5)");
        _newTabBtn = MakeNavButton("＋", "新しいタブ (Ctrl+T)");

        _backBtn.Click += (_, _) => { if (Current?.CoreWebView2?.CanGoBack == true) Current.CoreWebView2.GoBack(); };
        _fwdBtn.Click += (_, _) => { if (Current?.CoreWebView2?.CanGoForward == true) Current.CoreWebView2.GoForward(); };
        _reloadBtn.Click += (_, _) => Current?.CoreWebView2?.Reload();
        _newTabBtn.Click += (_, _) => _ = CreateTabAsync(null);

        _addressBar = new TextBox
        {
            Font = new Font("Yu Gothic UI", 10.5f),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "検索または URL を入力",
            Margin = new Padding(6, 0, 6, 0),
        };
        _addressBar.KeyDown += OnAddressKeyDown;
        _addressBar.Enter += (_, _) => BeginInvoke(_addressBar.SelectAll);

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(6, 6, 6, 6),
            BackColor = Color.FromArgb(243, 243, 243),
        };
        topBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            topBar.ColumnStyles.Add(i == 3
                ? new ColumnStyle(SizeType.Percent, 100)
                : new ColumnStyle(SizeType.AutoSize));
        topBar.Controls.Add(_backBtn, 0, 0);
        topBar.Controls.Add(_fwdBtn, 1, 0);
        topBar.Controls.Add(_reloadBtn, 2, 0);
        topBar.Controls.Add(_addressBar, 3, 0);
        topBar.Controls.Add(_newTabBtn, 4, 0);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(200, 30),
        };
        _tabs.DrawItem += OnDrawTab;
        _tabs.MouseDown += OnTabMouseDown;
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            UpdateAddressBar();
            UpdateWindowTitle();
            UpdateNavButtons();
            Current?.Focus();
        };

        Controls.Add(_tabs);
        Controls.Add(topBar);

        Load += (_, _) => _ = CreateTabAsync(_startupUrl);
    }

    private WebView2? Current => _tabs.SelectedTab?.Controls.OfType<WebView2>().FirstOrDefault();

    private static Button MakeNavButton(string text, string tooltip)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(34, 30),
            Anchor = AnchorStyles.None,
            Margin = new Padding(2, 0, 2, 0),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Yu Gothic UI", 11f),
            ForeColor = Color.FromArgb(60, 60, 60),
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 227, 231);
        new ToolTip().SetToolTip(btn, tooltip);
        return btn;
    }

    // ---- タブの生成と破棄 ----

    private async Task CreateTabAsync(string? url)
    {
        var page = new TabPage(NewTabTitle);
        var wv = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White };
        page.Controls.Add(wv);
        _tabs.TabPages.Add(page);
        _tabs.SelectedTab = page;

        try
        {
            _env ??= await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Karu", "Profile"));
            await wv.EnsureCoreWebView2Async(_env);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ブラウザエンジンの初期化に失敗しました。\n" + ex.Message,
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var core = wv.CoreWebView2;

        core.DocumentTitleChanged += (_, _) =>
        {
            page.Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? NewTabTitle : core.DocumentTitle;
            _tabs.Invalidate();
            if (_tabs.SelectedTab == page) UpdateWindowTitle();
        };
        core.SourceChanged += (_, _) => { if (_tabs.SelectedTab == page) UpdateAddressBar(); };
        core.HistoryChanged += (_, _) => { if (_tabs.SelectedTab == page) UpdateNavButtons(); };
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            _ = CreateTabAsync(e.Uri);
        };
        wv.KeyDown += OnWebViewKeyDown;

        if (url is null)
        {
            core.NavigateToString(StartPageHtml);
            _addressBar.Focus();
        }
        else
        {
            core.Navigate(url);
        }
        UpdateNavButtons();
    }

    private void CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.TabCount) return;
        var page = _tabs.TabPages[index];
        var wv = page.Controls.OfType<WebView2>().FirstOrDefault();
        _tabs.TabPages.RemoveAt(index);
        wv?.Dispose();
        page.Dispose();

        if (_tabs.TabCount == 0) Close();
    }

    // ---- タブの描画(閉じるボタン付き) ----

    private void OnDrawTab(object? sender, DrawItemEventArgs e)
    {
        var rect = _tabs.GetTabRect(e.Index);
        bool selected = e.Index == _tabs.SelectedIndex;

        using (var back = new SolidBrush(selected ? Color.White : Color.FromArgb(226, 229, 233)))
            e.Graphics.FillRectangle(back, rect);

        var textRect = new Rectangle(rect.X + 10, rect.Y, rect.Width - 36, rect.Height);
        TextRenderer.DrawText(e.Graphics, _tabs.TabPages[e.Index].Text, Font, textRect,
            Color.FromArgb(32, 32, 32),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(e.Graphics, "✕", Font, GetCloseRect(rect),
            Color.FromArgb(130, 130, 130),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static Rectangle GetCloseRect(Rectangle tabRect) =>
        new(tabRect.Right - 26, tabRect.Y + (tabRect.Height - 18) / 2, 18, 18);

    private void OnTabMouseDown(object? sender, MouseEventArgs e)
    {
        for (int i = 0; i < _tabs.TabCount; i++)
        {
            var rect = _tabs.GetTabRect(i);
            if (!rect.Contains(e.Location)) continue;

            if (e.Button == MouseButtons.Middle ||
                (e.Button == MouseButtons.Left && GetCloseRect(rect).Contains(e.Location)))
            {
                CloseTab(i);
            }
            return;
        }
    }

    // ---- アドレスバーとショートカット ----

    private void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            var input = _addressBar.Text;
            if (string.IsNullOrWhiteSpace(input)) return;
            Current?.CoreWebView2?.Navigate(ToUrl(input));
            Current?.Focus();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            UpdateAddressBar();
            Current?.Focus();
        }
    }

    private void OnWebViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleShortcut(e.KeyData))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (HandleShortcut(keyData)) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool HandleShortcut(Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.T:
                _ = CreateTabAsync(null);
                return true;
            case Keys.Control | Keys.W:
                CloseTab(_tabs.SelectedIndex);
                return true;
            case Keys.Control | Keys.L:
            case Keys.Alt | Keys.D:
                _addressBar.Focus();
                _addressBar.SelectAll();
                return true;
            case Keys.Control | Keys.Tab:
                CycleTab(1);
                return true;
            case Keys.Control | Keys.Shift | Keys.Tab:
                CycleTab(-1);
                return true;
            default:
                return false;
        }
    }

    private void CycleTab(int delta)
    {
        int count = _tabs.TabCount;
        if (count < 2) return;
        _tabs.SelectedIndex = (_tabs.SelectedIndex + delta + count) % count;
    }

    // ---- 表示の更新 ----

    private void UpdateAddressBar()
    {
        var src = Current?.CoreWebView2?.Source;
        _addressBar.Text = (src is null || src == "about:blank") ? "" : src;
    }

    private void UpdateWindowTitle()
    {
        var title = _tabs.SelectedTab?.Text;
        Text = string.IsNullOrWhiteSpace(title) || title == NewTabTitle
            ? AppName
            : $"{title} - {AppName}";
    }

    private void UpdateNavButtons()
    {
        var core = Current?.CoreWebView2;
        _backBtn.Enabled = core?.CanGoBack == true;
        _fwdBtn.Enabled = core?.CanGoForward == true;
        _reloadBtn.Enabled = core is not null;
    }

    // ---- URL 判定 ----

    private static string ToUrl(string input)
    {
        input = input.Trim();

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" or "file" or "about" or "data")
            return input;

        if (!input.Contains(' ') &&
            (input.Contains('.') || input.StartsWith("localhost")))
        {
            var guess = (input.StartsWith("localhost") ? "http://" : "https://") + input;
            if (Uri.TryCreate(guess, UriKind.Absolute, out _)) return guess;
        }

        return "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
    }

    // ---- スタートページ ----

    private const string StartPageHtml = """
        <!doctype html>
        <html lang="ja">
        <head>
        <meta charset="utf-8">
        <title>新しいタブ</title>
        <style>
          :root { color-scheme: light dark; }
          body {
            margin: 0; height: 100vh; display: grid; place-items: center;
            font-family: "Yu Gothic UI", "Hiragino Sans", sans-serif;
            background: light-dark(#f6f7f9, #1e1f22);
            color: light-dark(#333, #ddd);
          }
          .box { text-align: center; width: min(560px, 86vw); }
          h1 { font-size: 2rem; font-weight: 600; letter-spacing: .18em; margin: 0 0 28px; }
          form { display: flex; }
          input {
            flex: 1; font-size: 1.05rem; padding: 13px 20px; border-radius: 999px;
            border: 1px solid light-dark(#ccc, #444);
            background: light-dark(#fff, #2a2b2e); color: inherit; outline: none;
          }
          input:focus { border-color: #7aa2ff; box-shadow: 0 0 0 3px rgba(122,162,255,.25); }
        </style>
        </head>
        <body>
          <div class="box">
            <h1>かるぶらうじんぐ</h1>
            <form action="https://www.google.com/search">
              <input name="q" placeholder="Google で検索" autocomplete="off">
            </form>
          </div>
        </body>
        </html>
        """;
}
