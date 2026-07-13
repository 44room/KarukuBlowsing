using Microsoft.Web.WebView2.Core;

namespace Karu;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                "Microsoft Edge WebView2 ランタイムが見つかりません。\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/ からインストールしてください。",
                "かるぶらうじんぐ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }
}
