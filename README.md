# かるぶらうじんぐ

Windows 向けの軽量 Chromium ベースブラウザです。

レンダリングエンジンに **Microsoft Edge WebView2**(Chromium ベース、Windows 11 標準搭載)を使用しているため、Electron のようにブラウザエンジン本体を同梱する必要がなく、配布サイズは **約 1.7MB**、本体プロセスのメモリ使用量は **10MB 前後** と非常に軽量です。

## 必要環境

- Windows 10 / 11
- [.NET 9 ランタイム](https://dotnet.microsoft.com/download/dotnet/9.0)(SDK があれば不要)
- WebView2 ランタイム(Windows 11 には標準搭載。無い場合は起動時に案内が表示されます)

## 実行方法

```powershell
# ビルド済みの実行ファイルをそのまま起動
.\publish\Karu.exe

# 起動時に開く URL を指定することもできます
.\publish\Karu.exe https://example.com

# ソースから実行
dotnet run --project KaruBrowser
```

## 再ビルド

```powershell
dotnet publish KaruBrowser -c Release -o publish
```

単一 EXE にまとめたい場合:

```powershell
dotnet publish KaruBrowser -c Release -o publish-single `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -r win-x64 --self-contained false
```

## 機能

- タブブラウジング(✕ ボタン・中クリックで閉じる)
- アドレスバー(URL でなければ自動的に Google 検索)
- 戻る / 進む / 再読み込み
- `target="_blank"` のリンクや `window.open` を新しいタブで開く
- チャコール×セージを基調としたダークテーマ UI(タイトルバー・タブ・ツールバー・スタートページまで統一)
- 検索ボックス付きスタートページ
- ダウンロード・印刷・ページ内検索(Ctrl+F)・ズーム(Ctrl+ホイール)は WebView2 標準機能で動作

## キーボードショートカット

| キー | 動作 |
|---|---|
| `Ctrl+T` | 新しいタブ |
| `Ctrl+W` | タブを閉じる(最後のタブで終了) |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | タブ切り替え |
| `Ctrl+L` / `Alt+D` | アドレスバーへフォーカス |
| `F5` / `Ctrl+R` | 再読み込み |
| `Alt+←` / `Alt+→` | 戻る / 進む |
| `Ctrl+F` | ページ内検索 |

## プロジェクト構成

```
KaruBrowser/
  KaruBrowser.csproj   … .NET 9 WinForms + Microsoft.Web.WebView2
  Program.cs           … エントリポイント(WebView2 ランタイム検出)
  MainForm.cs          … メインウィンドウ(タブ管理・ツールバー・スタートページ)
  Theme.cs             … カラーパレット(チャコール×セージ)・ダークタイトルバー
  TabStrip.cs          … 自前描画のタブストリップ(✕・＋ ボタン付き)
  IconButton.cs        … 円形ホバーのアイコンボタン(戻る / 進む / 再読み込み)
  AddressBar.cs        … ピル型アドレスバー(フォーカスでセージのリング)
publish/               … 配布用ビルド出力(Karu.exe)
```

閲覧データ(Cookie・キャッシュなど)は `%LOCALAPPDATA%\Karu\Profile` に保存されます。
