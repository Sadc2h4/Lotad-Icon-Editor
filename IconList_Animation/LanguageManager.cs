using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace IconList_Animation
{
    public static class LanguageManager
    {
        private static Dictionary<string, string> strings = new Dictionary<string, string>();

        public static void Load(string cultureCode)
        {
            string resourceName = $"IconList_Animation.lang.{cultureCode}.json";
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            // フォールバック処理
            if (stream == null)
            {
                resourceName = "IconList_Animation.lang.en.json";
                stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            }

            using (stream)
            using (StreamReader reader = new StreamReader(stream))
            {
                strings = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
            }
        }

        public static string Get(string key)
        {
            return strings.TryGetValue(key, out var value) ? value : $"[{key}]";
        }
    }
}
