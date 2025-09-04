using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IconList_Animation
{
    public static class GlobalLanguage
    {
        public static string CurrentLanguage { get; private set; } = "en";

        public static void SetLanguage(string cultureCode)
        {
            CurrentLanguage = cultureCode;
            LanguageManager.Load(cultureCode);
            Properties.Settings.Default.Language = cultureCode;
            Properties.Settings.Default.Save();
        }

        public static void LoadSavedLanguage()
        {
            var saved = Properties.Settings.Default.Language;
            if (string.IsNullOrEmpty(saved)) saved = "en";
            SetLanguage(saved);
        }
    }
}
