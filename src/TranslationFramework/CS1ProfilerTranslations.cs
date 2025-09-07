using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using ICities;
using ColossalFramework;
using ColossalFramework.Plugins;
using ColossalFramework.Globalization;

namespace CS1Profiler.TranslationFramework
{
    /// <summary>
    /// Static class to provide translation interface for CS1Profiler.
    /// </summary>
    public static class Translations
    {
        private static Translator _translator;

        /// <summary>
        /// Static interface to instance's translate method.
        /// </summary>
        /// <param name="key">Key to translate</param>
        /// <returns>Translation (or key if translation failed)</returns>
        public static string Translate(string key) => Instance.Translate(key);

        /// <summary>
        /// Current language.
        /// </summary>
        public static string Language
        {
            get => Instance.Language;
            set => Instance.SetLanguage(value);
        }

        /// <summary>
        /// On-demand initialisation of translator.
        /// </summary>
        private static Translator Instance
        {
            get
            {
                if (_translator == null)
                {
                    _translator = new Translator();
                }
                return _translator;
            }
        }
    }

    /// <summary>
    /// Handles translations for CS1Profiler.
    /// </summary>
    public class Translator
    {
        private Language systemLanguage = null;
        private SortedList<string, Language> languages;
        private string defaultLanguage = "en";
        private int currentIndex = -1;

        /// <summary>
        /// Constructor.
        /// </summary>
        internal Translator()
        {
            // Initialize languages list.
            languages = new SortedList<string, Language>();

            // Load translations.
            LoadTranslations();

            // Set initial system language.
            SetSystemLanguage();
        }

        /// <summary>
        /// Returns the current language.
        /// </summary>
        internal string Language
        {
            get
            {
                if (currentIndex < 0)
                {
                    return "system";
                }
                return languages.Values[currentIndex].uniqueName;
            }
        }

        /// <summary>
        /// Returns the current language index.
        /// </summary>
        internal int Index => currentIndex;

        /// <summary>
        /// Translates the given text key.
        /// </summary>
        /// <param name="key">Text key to translate</param>
        /// <returns>Translation if available, otherwise the key itself</returns>
        internal string Translate(string key)
        {
            Language currentLanguage = GetLanguage();

            // If no translation available, return the key.
            if (currentLanguage?.translations == null)
            {
                return key;
            }

            // Return translation if key exists.
            if (currentLanguage.translations.TryGetValue(key, out string translation) && !string.IsNullOrEmpty(translation))
            {
                return translation;
            }

            // If not found and not using system language, try system language.
            if (currentIndex >= 0 && systemLanguage?.translations != null)
            {
                if (systemLanguage.translations.TryGetValue(key, out translation) && !string.IsNullOrEmpty(translation))
                {
                    return translation;
                }
            }

            // If still not found, try default language.
            if (languages.TryGetValue(defaultLanguage, out Language defaultLang) && defaultLang.translations != null)
            {
                if (defaultLang.translations.TryGetValue(key, out translation) && !string.IsNullOrEmpty(translation))
                {
                    return translation;
                }
            }

            // If all else fails, return the key.
            return key;
        }

        /// <summary>
        /// Sets the current language.
        /// </summary>
        /// <param name="languageIndex">Language index to set</param>
        internal void SetLanguage(int languageIndex)
        {
            currentIndex = languageIndex;
        }

        /// <summary>
        /// Sets the current language.
        /// </summary>
        /// <param name="languageCode">Language code to set</param>
        internal void SetLanguage(string languageCode)
        {
            if (languageCode == "system")
            {
                currentIndex = -1;
                return;
            }

            for (int i = 0; i < languages.Count; i++)
            {
                if (languages.Values[i].uniqueName == languageCode)
                {
                    currentIndex = i;
                    return;
                }
            }
        }

        /// <summary>
        /// Sets the current language to system language.
        /// </summary>
        private void SetSystemLanguage()
        {
            string systemLanguageCode = LocaleManager.instance.language;
            
            // Convert system language to our format.
            switch (systemLanguageCode)
            {
                case "ja":
                    if (languages.ContainsKey("jp"))
                    {
                        systemLanguage = languages["jp"];
                    }
                    break;
                default:
                    if (languages.ContainsKey("en"))
                    {
                        systemLanguage = languages["en"];
                    }
                    break;
            }

            // Default to English if system language not found.
            if (systemLanguage == null && languages.ContainsKey("en"))
            {
                systemLanguage = languages["en"];
            }
        }

        /// <summary>
        /// Gets the current active language.
        /// </summary>
        /// <returns>Current language</returns>
        private Language GetLanguage()
        {
            if (currentIndex < 0)
            {
                return systemLanguage;
            }

            if (currentIndex < languages.Count)
            {
                return languages.Values[currentIndex];
            }

            return systemLanguage;
        }

        /// <summary>
        /// Loads translation files.
        /// </summary>
        private void LoadTranslations()
        {
            // Load built-in translations.
            LoadBuiltinTranslations();
        }

        /// <summary>
        /// Loads built-in translations.
        /// </summary>
        private void LoadBuiltinTranslations()
        {
            // English translations.
            var englishTranslations = new Dictionary<string, string>
            {
                { "RENDERIT_OPTIMIZATION_TOOLTIP", "Fixes performance issues in RenderIt MOD. Optimizes heavy frame processing and caches mod enablement checks." },
                { "PLOPPABLEASPHALTFIX_OPTIMIZATION_TOOLTIP", "Fixes performance issues in PloppableAsphaltFix MOD. Optimizes ApplyProperties processing that causes 838ms spikes." },
                { "LOG_SUPPRESSION_TOOLTIP", "Suppresses PackageDeserializer warning logs. Reduces log file size and provides slight performance improvement." },
                { "FIVE_MINUTE_ANALYSIS_TOOLTIP", "Runs performance and simulation analysis for 5 minutes. Generates detailed CSV reports. CPU usage will temporarily increase." },
                { "STOP_ANALYSIS_TOOLTIP", "Manually stops running analysis. Normally auto-stops after 5 minutes, but can be stopped early if needed." },
                { "COPY_MOD_LIST_TOOLTIP", "Copies enabled MOD list in \"WorkshopID ModName\" format to clipboard. Useful for support requests." }
            };

            var englishLanguage = new Language
            {
                uniqueName = "en",
                readableName = "English",
                translations = englishTranslations
            };

            languages.Add("en", englishLanguage);

            // Japanese translations.
            var japaneseTranslations = new Dictionary<string, string>
            {
                { "RENDERIT_OPTIMIZATION_TOOLTIP", "RenderIt MODの性能問題を修正します。重いフレーム処理を最適化し、MOD有効性チェックをキャッシュ化します。" },
                { "PLOPPABLEASPHALTFIX_OPTIMIZATION_TOOLTIP", "PloppableAsphaltFix MODの性能問題を修正します。838msのスパイクを引き起こすApplyProperties処理を最適化します。" },
                { "LOG_SUPPRESSION_TOOLTIP", "PackageDeserializerの警告ログを抑制します。ログファイルサイズの削減とわずかな性能向上が期待できます。" },
                { "FIVE_MINUTE_ANALYSIS_TOOLTIP", "パフォーマンスとシミュレーション分析を5分間実行します。詳細なCSVレポートが生成されます。CPU使用率が一時的に上昇します。" },
                { "STOP_ANALYSIS_TOOLTIP", "実行中の分析を手動で停止します。通常は5分後に自動停止しますが、必要に応じて早期停止できます。" },
                { "COPY_MOD_LIST_TOOLTIP", "有効なMOD一覧を「WorkshopID MOD名」形式でクリップボードにコピーします。サポート依頼時に便利です。" }
            };

            var japaneseLanguage = new Language
            {
                uniqueName = "jp",
                readableName = "日本語",
                translations = japaneseTranslations
            };

            languages.Add("jp", japaneseLanguage);
        }
    }

    /// <summary>
    /// Language data structure.
    /// </summary>
    public class Language
    {
        public string uniqueName;
        public string readableName;
        public Dictionary<string, string> translations;
    }
}
