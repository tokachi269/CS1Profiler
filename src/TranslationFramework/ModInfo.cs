using System.Diagnostics;
using System.Reflection;
using ICities;

namespace CS1Profiler.TranslationFramework
{
    /// <summary>
    /// CS1Profiler MOD情報クラス - Translation Framework用
    /// </summary>
    public class ModInfo : IUserMod
    {
        public const string
            Name = "CS1 Profiler",
            Description = "Performance Analyzer for Cities: Skylines",
            COMIdentifier = "com.Tokachi269.CS1Profiler"
        ;

        public static System.Version Version
        {
            get => Assembly.GetExecutingAssembly().GetName().Version;
        }

        public static string VersionStr
        {
            get => Version.ToString();
        }

        public static FileVersionInfo VersionInfo
        {
            get => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
        }

        // IUserMod実装
        string IUserMod.Name => Name;
        string IUserMod.Description => Description;
    }
}
