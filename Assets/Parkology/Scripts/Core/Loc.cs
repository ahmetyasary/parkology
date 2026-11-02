using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Parkology
{
    /// <summary>Key-based localisation. English is the base language; Turkish follows the device or the player's choice.</summary>
    public static class Loc
    {
        [Serializable] class Entry { public string key, value; }
        [Serializable] class Catalog { public Entry[] entries; }

        public static readonly string[] Codes = { "en", "tr" };
        static readonly Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>();
        public static string Code { get; private set; } = "en";
        public static event Action Changed;

        public static string NativeName(string code) => code == "tr" ? "Türkçe" : "English";

        public static void Load()
        {
            if (tables.Count > 0) return;
            foreach (string code in Codes)
            {
                var asset = Resources.Load<TextAsset>("Localization/" + code);
                var table = new Dictionary<string, string>();
                if (asset != null)
                    foreach (var e in JsonUtility.FromJson<Catalog>(asset.text).entries)
                        table[e.key] = e.value;
                tables[code] = table;
            }
            string fallback = Application.systemLanguage == SystemLanguage.Turkish ? "tr" : "en";
            string chosen = PlayerPrefs.GetString("language", fallback);
            Code = tables.ContainsKey(chosen) ? chosen : "en";
        }

        public static void Select(string code)
        {
            Load();
            if (!tables.ContainsKey(code) || code == Code) return;
            Code = code;
            PlayerPrefs.SetString("language", code);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string Get(string key)
        {
            Load();
            if (tables[Code].TryGetValue(key, out var v)) return v;
            if (tables["en"].TryGetValue(key, out v)) return v;
            return key;
        }

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);
    }

    /// <summary>Keeps a Text in sync with the selected language.</summary>
    public sealed class LocText : MonoBehaviour
    {
        Text label;
        string key;
        object[] args;

        public static LocText Bind(Text label, string key, params object[] args)
        {
            if (!label.TryGetComponent(out LocText l)) l = label.gameObject.AddComponent<LocText>();
            l.label = label;
            l.Set(key, args);
            return l;
        }

        public void Set(string newKey, params object[] newArgs)
        {
            key = newKey;
            args = newArgs;
            Refresh();
        }

        void OnEnable() { Loc.Changed += Refresh; Refresh(); }
        void OnDisable() { Loc.Changed -= Refresh; }

        void Refresh()
        {
            if (label == null || key == null) return;
            label.text = args != null && args.Length > 0 ? Loc.Format(key, args) : Loc.Get(key);
        }
    }
}
