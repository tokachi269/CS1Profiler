using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// パフォーマンス測定用のHarmonyパッチを管理
    /// </summary>
    public static class PerformancePatches
    {
        private static readonly HashSet<string> _modAssemblyNames = new HashSet<string>();
        private static readonly HashSet<string> _modTypeNames = new HashSet<string>();
        
        // パッチ成功時にメソッドを記録するリスト
        private static readonly List<string> _patchedMethods = new List<string>();
        
        // 検出されたMODを記録するリスト
        private static readonly List<string> _detectedMods = new List<string>();

        /// <summary>
        /// 統一された型名・メソッド名フィルター
        /// </summary>
        private static readonly string[] EXCLUDED_PATTERNS = {
            // 基本的な除外パターン
            "get_", "set_", "add_", "remove_", 
            "tostring", "gethashcode", "equals", "finalize",
            "dispose", "destroy", "unload", "delete",
            "abort", "exit", "quit", "__",
            // Harmonyパッチ関連
            "patchall", "unpatchall", "prefixmethod", "postfixmethod", 
            "transpilemethod", "unpatchmethod",
            // Unity基本メソッド除外
            "broadcastmessage", "sendmessage", "getinstanceid",
            "getcomponent", "sendmessageoptions",
        };

        public static void ApplyPerformancePatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Applying performance measurement patches...");
                
                DetectModAssemblies();
                ApplyBlacklistPatches(harmony);
                
                UnityEngine.Debug.Log("[CS1Profiler] Performance measurement patches applied");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ApplyPerformancePatches failed: " + e.Message);
            }
        }

        private static void DetectModAssemblies()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Detecting MOD assemblies...");

                _modAssemblyNames.Clear();
                _modTypeNames.Clear();
                
                // PluginManagerからのMOD情報を取得して記録
                try
                {
                    var pluginManager = ColossalFramework.Plugins.PluginManager.instance;
                    if (pluginManager != null)
                    {
                        var pluginsInfo = pluginManager.GetPluginsInfo();
                        foreach (var plugin in pluginsInfo)
                        {
                            if (plugin.isEnabled && !plugin.isBuiltin)
                            {
                                try
                                {
                                    var userMods = plugin.GetInstances<ICities.IUserMod>();
                                    if (userMods != null && userMods.Length > 0)
                                    {
                                        var modName = userMods[0].Name;
                                        var publishedFileID = plugin.publishedFileID.ToString();
                                        var modInfo = $"[{publishedFileID}] {modName}";
                                        
                                        lock (_detectedMods)
                                        {
                                            if (!_detectedMods.Contains(modInfo))
                                            {
                                                _detectedMods.Add(modInfo);
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    UnityEngine.Debug.LogWarning($"[CS1Profiler] Failed to get MOD info for {plugin.name}: {e.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[CS1Profiler] PluginManager access failed: {e.Message}");
                }

                // 従来のアセンブリベース検出も継続
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (IsModAssembly(assembly))
                    {
                        _modAssemblyNames.Add(assembly.GetName().Name);
                        
                        int addedTypes = 0;
                        foreach (var type in GetSafeTypes(assembly))
                        {
                            if (IsTargetType(type))
                            {
                                _modTypeNames.Add(type.FullName);
                                addedTypes++;
                            }
                        }
                    }
                }
                
                UnityEngine.Debug.Log($"[CS1Profiler] Detected {_modAssemblyNames.Count} MOD assemblies, {_modTypeNames.Count} target types");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] DetectModAssemblies error: " + e.Message);
            }
        }

        private static void ApplyBlacklistPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                int patchCount = 0;
                
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!_modAssemblyNames.Contains(assembly.GetName().Name)) continue;
                    
                    foreach (var type in GetSafeTypes(assembly))
                    {
                        if (!IsTargetType(type)) continue;
                        
                        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                            .Where(IsTargetMethod)
                            .ToArray();
                        
                        foreach (var method in methods)
                        {
                            try
                            {
                                // **最重要**: パッチ前の最終安全性チェック
                                if (method.IsVirtual || !method.IsStatic || method.GetMethodBody() == null)
                                {
                                    continue; // 危険なメソッドはスキップ
                                }
                                
                                var prefix = new HarmonyLib.HarmonyMethod(typeof(PerformanceHooks), "ProfilerPrefix");
                                var postfix = new HarmonyLib.HarmonyMethod(typeof(PerformanceHooks), "ProfilerPostfix");
                                
                                harmony.Patch(method, prefix: prefix, postfix: postfix);
                                
                                // パッチ成功時にリストに追加
                                string methodName = $"{type.Namespace}.{type.Name}.{method.Name}";
                                lock (_patchedMethods)
                                {
                                    _patchedMethods.Add(methodName);
                                }
                                
                                // パッチ適用後の1件だけログ出力
                                if (patchCount == 0)
                                {
                                    UnityEngine.Debug.Log($"[CS1Profiler] First patch applied successfully: {methodName}");
                                }
                                
                                patchCount++;
                            }
                            catch (Exception e)
                            {
                                // パッチ失敗は詳細ログで記録
                                UnityEngine.Debug.LogError($"[CS1Profiler] FAILED to patch {type.FullName}.{method.Name}: {e.GetType().Name}: {e.Message}");
                            }
                        }
                    }
                }
                
                UnityEngine.Debug.Log($"[CS1Profiler] Patching completed. Total patches applied: {patchCount}");
                
                // ログ出力を削除（不要なため）
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] ApplyBlacklistPatches error: {e.Message}");
            }
        }

        private static bool IsModAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            
            // 基本除外パターン
            if (name.StartsWith("System") || 
                name.StartsWith("UnityEngine") || 
                name.StartsWith("mscorlib") ||
                name.Contains("Harmony") ||
                name.StartsWith("Microsoft") ||
                name.StartsWith("Assembly-CSharp") ||
                name == "ColossalManaged" ||
                name == "ICities")
            {
                return false;
            }
            
            return true;
        }

        private static Type[] GetSafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).ToArray();
            }
            catch
            {
                return new Type[0];
            }
        }

        private static bool IsTargetType(Type type)
        {
            if (type == null || type.IsInterface || type.IsAbstract) return false;
            
            // ジェネリック型・ポインタ型を除外
            if (type.IsGenericType || type.ContainsGenericParameters) return false;
            if (type.IsPointer || type.IsByRef) return false;
            
            // システム系型を除外
            var typeName = type.FullName?.ToLower() ?? "";
            if (typeName.Contains("unity") || 
                typeName.Contains("mono") || 
                typeName.Contains("system.") ||
                typeName.Contains("microsoft") ||
                typeName.Contains("colossalframework.ui") ||
                typeName.Contains("harmony"))
            {
                return false;
            }
            
            // MOD関連のマネージャー・コントローラー・ハンドラー系を優先
            if (typeName.Contains("manager") || 
                typeName.Contains("controller") || 
                typeName.Contains("handler") ||
                typeName.Contains("service") ||
                typeName.Contains("processor"))
            {
                return true;
            }
            
            return true;
        }

        private static bool IsTargetMethod(MethodInfo method)
        {
            if (method == null) return false;
            if (method.IsConstructor || method.IsAbstract || method.IsSpecialName) return false;
            
            // ネイティブ/内部呼び出しは除外
            var impl = method.GetMethodImplementationFlags();
            if ((impl & MethodImplAttributes.InternalCall) != 0) return false;
            if ((impl & MethodImplAttributes.Native) != 0) return false;
            
            // 外部メソッド（IL本体なし）を除外
            try
            {
                var body = method.GetMethodBody();
                if (body == null) return false;
                var il = body.GetILAsByteArray();
                if (il == null || il.Length < 10) return false; // 短すぎるメソッドも除外
            }
            catch
            {
                return false; // IL読み込み失敗は除外
            }
            
            // 基本的にstaticメソッドを優先（安全）
            if (!method.IsStatic) return false;
            
            // ジェネリックメソッドを除外
            if (method.IsGenericMethod || method.ContainsGenericParameters) return false;
            
            // 引数が多すぎるメソッドを除外
            if (method.GetParameters().Length > 5) return false;
            
            // 危険な戻り値型を除外
            var returnType = method.ReturnType;
            if (returnType == typeof(System.IntPtr) || 
                returnType == typeof(System.UIntPtr) ||
                returnType.IsPointer ||
                returnType.IsByRef)
            {
                return false;
            }
            
            var methodName = method.Name.ToLower();
            
            // 基本的な除外パターン
            foreach (var pattern in EXCLUDED_PATTERNS)
            {
                if (methodName.Contains(pattern)) return false;
            }
            
            // さらに危険なパターンを除外
            string[] additionalDangerousPatterns = {
                "native", "extern", "marshal", "pointer", "handle", 
                "alloc", "free", "dispose", "finalize", "destroy", 
                "patch", "hook", "detour", "redirect"
            };
            
            foreach (var pattern in additionalDangerousPatterns)
            {
                if (methodName.Contains(pattern)) return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// パッチが成功したメソッドのリストを取得
        /// </summary>
        public static List<string> GetPatchedMethods()
        {
            lock (_patchedMethods)
            {
                return new List<string>(_patchedMethods);
            }
        }
        
        /// <summary>
        /// 検出されたMODのリストを取得
        /// </summary>
        public static List<string> GetDetectedMods()
        {
            lock (_detectedMods)
            {
                return new List<string>(_detectedMods);
            }
        }
        
        /// <summary>
        /// パッチされたメソッド一覧を文字列で取得
        /// </summary>
        public static string GetPatchedMethodsList()
        {
            try
            {
                var methods = GetPatchedMethods();
                if (methods.Count == 0)
                {
                    return "No patched methods recorded.";
                }
                
                var result = new System.Text.StringBuilder();
                
                methods.Sort();
                foreach (var method in methods)
                {
                    result.AppendLine(method);
                }
                
                return result.ToString();
            }
            catch (Exception e)
            {
                return $"Error getting patched methods list: {e.Message}";
            }
        }
        
        /// <summary>
        /// MOD一覧を文字列で取得
        /// </summary>
        public static string GetModsList()
        {
            try
            {
                var result = new System.Text.StringBuilder();
                var mods = new List<string>();
                
                // ColossalFramework.Plugins.PluginManagerを使用してMOD情報を取得
                var pluginManager = ColossalFramework.Plugins.PluginManager.instance;
                if (pluginManager != null)
                {
                    var pluginsInfo = pluginManager.GetPluginsInfo();
                    
                    foreach (var plugin in pluginsInfo)
                    {
                        if (plugin.isEnabled && !plugin.isBuiltin)
                        {
                            try
                            {
                                var userMods = plugin.GetInstances<ICities.IUserMod>();
                                if (userMods != null && userMods.Length > 0)
                                {
                                    var modName = userMods[0].Name;
                                    var publishedFileID = plugin.publishedFileID.ToString();
                                    var modInfo = $"[{publishedFileID}] {modName}";
                                    mods.Add(modInfo);
                                }
                            }
                            catch (Exception e)
                            {
                                // 個別MODの情報取得に失敗した場合はスキップ
                                UnityEngine.Debug.LogWarning($"[CS1Profiler] Failed to get MOD info for {plugin.name}: {e.Message}");
                            }
                        }
                    }
                }
                else
                {
                    // フォールバック: アセンブリベース検出
                    var detectedMods = GetDetectedMods();
                    foreach (var mod in detectedMods)
                    {
                        mods.Add(mod);
                    }
                }
                
                result.AppendLine($"Total: {mods.Count} MODs detected");
                result.AppendLine();
                
                mods.Sort();
                foreach (var mod in mods)
                {
                    result.AppendLine(mod);
                }
                
                return result.ToString();
            }
            catch (Exception e)
            {
                return $"Error getting MODs list: {e.Message}";
            }
        }
    }

    /// <summary>
    /// 汎用パフォーマンス測定フック
    /// </summary>
    internal static class PerformanceHooks
    {
        public static void ProfilerPrefix(MethodBase __originalMethod, out long __state)
        {
            try
            {
                // プロファイリングが無効な場合は何もしない
                if (!CS1Profiler.Profiling.MethodProfiler.IsEnabled()) 
                {
                    __state = 0;
                    return;
                }
                
                __state = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerPrefix error: " + e.Message);
                __state = 0;
            }
        }

        public static void ProfilerPostfix(MethodBase __originalMethod, long __state)
        {
            try
            {
                if (__state == 0) return;
                
                // プロファイリングが無効な場合は何もしない
                if (!CS1Profiler.Profiling.MethodProfiler.IsEnabled()) return;
                
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - __state;
                
                // Namespace.ClassName.MethodName の形式でメソッド名を構築
                string namespaceName = __originalMethod.DeclaringType?.Namespace ?? "Unknown";
                string className = __originalMethod.DeclaringType?.Name ?? "Unknown";
                string methodName = __originalMethod.Name ?? "Unknown";
                string methodKey = $"{namespaceName}.{className}.{methodName}";
                
                // 既存のPerformanceProfilerシステムを活用
                CS1Profiler.Profiling.PerformanceProfiler.RecordMethodExecution(methodKey, elapsed);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerPostfix error: " + e.Message);
            }
        }
    }
}
