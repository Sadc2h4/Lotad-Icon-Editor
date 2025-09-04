using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace IconList_Animation
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // 言語設定を安全に読み込む
            string savedLang = Properties.Settings.Default.Language;
            GlobalLanguage.LoadSavedLanguage();

            // 値が空・不正な場合はデフォルト（例：ja-JP）に戻す
            if (string.IsNullOrEmpty(savedLang) ||
                (savedLang != "ja-JP" && savedLang != "en"))
            {
                savedLang = "ja-JP"; // fallback
                Properties.Settings.Default.Language = savedLang;
                Properties.Settings.Default.Save();
            }

            // 言語を設定（.resx用：残しても問題なし）
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(savedLang);

            // ✅ JSONリソースを読み込む（追加！）
            LanguageManager.Load(savedLang);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main_Form());
        }
    }
}