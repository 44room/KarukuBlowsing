using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Karu;

public class MainForm : Form
{
    private const string AppName = "かるぶらうじんぐ";
    private const string NewTabTitle = "新しいタブ";

    private readonly IconButton _backBtn;
    private readonly IconButton _fwdBtn;
    private readonly IconButton _reloadBtn;
    private readonly AddressBar _addressBar;
    private readonly TabStrip _tabStrip;
    private readonly Panel _host;
    private readonly List<WebView2> _views = new();
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
        BackColor = Theme.Bg;

        var tip = new ToolTip();
        _backBtn = new IconButton("←") { Anchor = AnchorStyles.None };
        _fwdBtn = new IconButton("→") { Anchor = AnchorStyles.None };
        _reloadBtn = new IconButton("⟳") { Anchor = AnchorStyles.None };
        tip.SetToolTip(_backBtn, "戻る (Alt+←)");
        tip.SetToolTip(_fwdBtn, "進む (Alt+→)");
        tip.SetToolTip(_reloadBtn, "再読み込み (F5)");

        _backBtn.Click += (_, _) => { if (Current?.CoreWebView2?.CanGoBack == true) Current.CoreWebView2.GoBack(); };
        _fwdBtn.Click += (_, _) => { if (Current?.CoreWebView2?.CanGoForward == true) Current.CoreWebView2.GoForward(); };
        _reloadBtn.Click += (_, _) => Current?.CoreWebView2?.Reload();

        _addressBar = new AddressBar
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(8, 0, 4, 0),
        };
        _addressBar.NavigateRequested += (_, _) =>
        {
            var input = _addressBar.Text;
            if (string.IsNullOrWhiteSpace(input)) return;
            Current?.CoreWebView2?.Navigate(ToUrl(input));
            Current?.Focus();
        };
        _addressBar.EscapePressed += (_, _) =>
        {
            UpdateAddressBar();
            Current?.Focus();
        };

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(8, 7, 12, 7),
            BackColor = Theme.BgBar,
        };
        topBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++)
            topBar.ColumnStyles.Add(i == 3
                ? new ColumnStyle(SizeType.Percent, 100)
                : new ColumnStyle(SizeType.AutoSize));
        topBar.Controls.Add(_backBtn, 0, 0);
        topBar.Controls.Add(_fwdBtn, 1, 0);
        topBar.Controls.Add(_reloadBtn, 2, 0);
        topBar.Controls.Add(_addressBar, 3, 0);
        topBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
        };

        _tabStrip = new TabStrip { Dock = DockStyle.Top, Height = 40 };
        _tabStrip.SelectedChanged += (_, _) =>
        {
            for (int i = 0; i < _views.Count; i++)
                _views[i].Visible = i == _tabStrip.SelectedIndex;
            UpdateAddressBar();
            UpdateWindowTitle();
            UpdateNavButtons();
            Current?.Focus();
        };
        _tabStrip.CloseRequested += (_, index) => CloseTab(index);
        _tabStrip.NewTabRequested += (_, _) => _ = CreateTabAsync(null);

        _host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };

        Controls.Add(_host);
        Controls.Add(topBar);
        Controls.Add(_tabStrip);

        Load += (_, _) => _ = CreateTabAsync(_startupUrl);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyDarkTitleBar(this);
    }

    private WebView2? Current =>
        _tabStrip.SelectedIndex >= 0 && _tabStrip.SelectedIndex < _views.Count
            ? _views[_tabStrip.SelectedIndex]
            : null;

    // ---- タブの生成と破棄 ----

    private async Task CreateTabAsync(string? url)
    {
        var wv = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Theme.Bg,
            Visible = false,
        };
        _host.Controls.Add(wv);
        _views.Add(wv);
        _tabStrip.Add(NewTabTitle);
        _tabStrip.SelectedIndex = _views.Count - 1;

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
            int i = _views.IndexOf(wv);
            if (i < 0) return;
            _tabStrip.SetTitle(i, string.IsNullOrWhiteSpace(core.DocumentTitle) ? NewTabTitle : core.DocumentTitle);
            if (i == _tabStrip.SelectedIndex) UpdateWindowTitle();
        };
        core.SourceChanged += (_, _) => { if (_views.IndexOf(wv) == _tabStrip.SelectedIndex) UpdateAddressBar(); };
        core.HistoryChanged += (_, _) => { if (_views.IndexOf(wv) == _tabStrip.SelectedIndex) UpdateNavButtons(); };
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            _ = CreateTabAsync(e.Uri);
        };
        wv.KeyDown += OnWebViewKeyDown;

        if (url is null)
        {
            core.NavigateToString(StartPageHtml);
            _addressBar.FocusInput();
        }
        else
        {
            core.Navigate(url);
        }
        UpdateNavButtons();
    }

    private void CloseTab(int index)
    {
        if (index < 0 || index >= _views.Count) return;
        var wv = _views[index];
        _views.RemoveAt(index);
        _host.Controls.Remove(wv);
        wv.Dispose();
        _tabStrip.RemoveAt(index);

        if (_views.Count == 0) Close();
    }

    // ---- アドレスバーとショートカット ----

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
                CloseTab(_tabStrip.SelectedIndex);
                return true;
            case Keys.Control | Keys.L:
            case Keys.Alt | Keys.D:
                _addressBar.FocusInput();
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
        int count = _tabStrip.Count;
        if (count < 2) return;
        _tabStrip.SelectedIndex = (_tabStrip.SelectedIndex + delta + count) % count;
    }

    // ---- 表示の更新 ----

    private void UpdateAddressBar()
    {
        var src = Current?.CoreWebView2?.Source;
        _addressBar.Text = (src is null || src == "about:blank") ? "" : src;
    }

    private void UpdateWindowTitle()
    {
        var title = _tabStrip.SelectedTitle;
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
          * { box-sizing: border-box; }
          body {
            margin: 0; height: 100vh; display: grid; place-items: center;
            font-family: "Yu Gothic UI", "Hiragino Sans", sans-serif;
            background:
              radial-gradient(640px 340px at 50% 30%, rgba(156,175,136,.07), transparent 70%),
              #1f2123;
            color: #e8eae4;
          }
          .box { text-align: center; width: min(560px, 86vw); }
          h1 {
            font-size: 1.9rem; font-weight: 600;
            letter-spacing: .3em; text-indent: .3em;
            margin: 0 0 14px;
          }
          h1 .accent { color: #9caf88; }
          .rule {
            width: 44px; height: 3px; border-radius: 2px;
            margin: 0 auto 34px; background: #9caf88; opacity: .55;
          }
          form { display: flex; }
          input {
            flex: 1; font: inherit; font-size: 1.05rem;
            padding: 14px 24px; border-radius: 999px;
            border: 1px solid #3a3f42;
            background: #1a1c1d; color: #e8eae4; outline: none;
            transition: border-color .15s ease, box-shadow .15s ease;
          }
          input::placeholder { color: #757c74; }
          input:focus {
            border-color: #9caf88;
            box-shadow: 0 0 0 3px rgba(156,175,136,.18);
          }
          .hint {
            margin-top: 30px; font-size: .78rem; letter-spacing: .06em;
            color: #5e6460;
          }
          kbd {
            display: inline-block; padding: 1px 7px; margin: 0 1px;
            border: 1px solid #3a3f42; border-bottom-width: 2px; border-radius: 5px;
            background: #26292b; color: #9ba29a;
            font-family: inherit; font-size: .95em;
          }
        </style>
        </head>
        <body>
          <div class="box">
            <h1>かるぶらうじんぐ<span class="accent">。</span></h1>
            <div class="rule"></div>
            <form action="https://www.google.com/search">
              <input name="q" placeholder="Google で検索" autocomplete="off">
            </form>
            <p class="hint"><kbd>Ctrl</kbd>+<kbd>T</kbd> 新しいタブ&emsp;<kbd>Ctrl</kbd>+<kbd>L</kbd> アドレスバー</p>
          </div>
        </body>
        </html>
        """;
}
