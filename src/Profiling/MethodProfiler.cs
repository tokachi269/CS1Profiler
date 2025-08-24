using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// メソッド実行時間を計測するためのプロファイラー
    /// 包括的にパフォーマンスに影響する可能性のあるメソッドをパッチ
    /// </summary>
    public static class MethodProfiler
    {
        private static readonly Dictionary<string, ProfileData> _methodStats = new Dictionary<string, ProfileData>();
        private static readonly Dictionary<string, Stopwatch> _activeStopwatches = new Dictionary<string, Stopwatch>();
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        
        // スパイク検出設定
        private static readonly double SPIKE_THRESHOLD_RATIO = 3.0; // 平均の3倍以上でスパイクとみなす
        private static readonly int MIN_CALLS_FOR_SPIKE_DETECTION = 10; // スパイク検出に必要な最小呼び出し回数
        private static readonly int MAX_SPIKES_PER_METHOD = 50; // メソッドごとの最大スパイク記録数
        
        // 動的に検出されたMODアセンブリのキャッシュ
        private static HashSet<string> _modAssemblyNames = new HashSet<string>();
        private static HashSet<string> _modTypeNames = new HashSet<string>();

        public class SpikeInfo
        {
            public DateTime Timestamp { get; set; }
            public double ExecutionTimeMs { get; set; }
            public double AverageAtTime { get; set; }
            public double SpikeRatio => AverageAtTime > 0 ? ExecutionTimeMs / AverageAtTime : 0;
            public string CallStackInfo { get; set; }
        }

        public class ProfileData
        {
            public string MethodName { get; set; }
            public long TotalTicks { get; set; }
            public int CallCount { get; set; }
            public long MaxTicks { get; set; }
            public DateTime LastCall { get; set; }
            public List<SpikeInfo> Spikes { get; set; } = new List<SpikeInfo>();
            public int SpikeCount => Spikes.Count;

            public double TotalMs => TotalTicks / 10000.0;
            public double AverageMs => CallCount > 0 ? TotalMs / CallCount : 0;
            public double MaxMs => MaxTicks / 10000.0;
            public double MaxSpikeRatio => Spikes.Count > 0 ? Spikes.Max(s => s.SpikeRatio) : 0;
        }

        public static void Initialize(Harmony harmony)
        {
            try
            {
                _stopwatch.Start();
                UnityEngine.Debug.Log("[CS1Profiler] MethodProfiler.Initialize starting...");
                
                // MODアセンブリを動的に検出
                DetectModAssemblies();
                
                // 全アセンブリから性能に影響するメソッドを包括的にパッチ
                int totalPatches = 0;
                var safeAssemblies = new List<Assembly>();
                
                // 安全なアセンブリのみを選別
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (assembly.FullName.Contains("mscorlib") || 
                            assembly.FullName.Contains("System") ||
                            assembly.FullName.Contains("Unity") ||
                            assembly.FullName.Contains("Mono.") ||
                            assembly.ReflectionOnly)
                            continue;
                            
                        // アセンブリが安全にアクセス可能かテスト
                        _ = assembly.GetName();
                        _ = assembly.GetTypes();
                        safeAssemblies.Add(assembly);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"[CS1Profiler] Skipping unsafe assembly {assembly.FullName}: {e.Message}");
                    }
                }
                
                UnityEngine.Debug.Log($"[CS1Profiler] Processing {safeAssemblies.Count} safe assemblies...");
                
                foreach (var assembly in safeAssemblies)
                {

                    int beforeCount = _methodStats.Count;
                    PatchPerformanceCriticalMethods(harmony, assembly);
                    int addedPatches = _methodStats.Count - beforeCount;
                    totalPatches += addedPatches;
                    
                    if (addedPatches > 0)
                    {
                        UnityEngine.Debug.Log($"[CS1Profiler] Patched {addedPatches} methods in {assembly.GetName().Name}");
                    }
                }

                UnityEngine.Debug.Log($"[CS1Profiler] MethodProfiler initialized with {_methodStats.Count} methods ({totalPatches} total patches applied)");
                
                // パッチされたメソッドの一覧をログ出力（カテゴリ別に分類）
                LogPatchedMethodsSummary();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] MethodProfiler initialization failed: " + e.Message);
                UnityEngine.Debug.LogError("[CS1Profiler] Stack trace: " + e.StackTrace);
            }
        }

        private static void DetectModAssemblies()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Detecting MOD assemblies...");
                
                var pluginManager = ColossalFramework.Plugins.PluginManager.instance;
                if (pluginManager != null)
                {
                    var enabledMods = pluginManager.GetPluginsInfo()
                        .Where(p => p.isEnabled && !p.isBuiltin)
                        .ToList();
                    
                    UnityEngine.Debug.Log($"[CS1Profiler] Found {enabledMods.Count} enabled MODs:");
                    
                    foreach (var plugin in enabledMods)
                    {
                        try
                        {
                            // MOD名とアセンブリ名を記録
                            var modInstances = plugin.GetInstances<ICities.IUserMod>();
                            if (modInstances.Any())
                            {
                                var modName = modInstances.First().Name;
                                var assemblyName = plugin.GetType().Assembly.GetName().Name;
                                
                                _modAssemblyNames.Add(assemblyName);
                                
                                // MODのタイプも記録（より正確な検出のため）
                                foreach (var instance in modInstances)
                                {
                                    _modTypeNames.Add(instance.GetType().Name);
                                }
                                
                                UnityEngine.Debug.Log($"[CS1Profiler] - [{plugin.publishedFileID}] {modName} (Assembly: {assemblyName})");
                            }
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogWarning($"[CS1Profiler] Failed to process plugin: {e.Message}");
                        }
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[CS1Profiler] PluginManager is null, fallback to assembly name detection");
                    // フォールバック：アセンブリ名でMODを推測
                    FallbackModDetection();
                }
                
                UnityEngine.Debug.Log($"[CS1Profiler] Detected {_modAssemblyNames.Count} MOD assemblies, {_modTypeNames.Count} MOD types");
                
                // FpsBooster関連メソッドの詳細調査
                DebugFpsBoosterMethods();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] MOD detection failed: {e.Message}");
                // フォールバックを実行
                FallbackModDetection();
            }
        }

        private static void DebugFpsBoosterMethods()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] === FpsBooster Method Investigation ===");
                
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var assemblyName = assembly.GetName().Name;
                        var types = assembly.GetTypes();
                        
                        foreach (var type in types)
                        {
                            if (type != null)
                            {
                                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                                
                                foreach (var method in methods)
                                {
                                    if (method.Name.Contains("FpsBooster"))
                                    {
                                        UnityEngine.Debug.Log($"[CS1Profiler] Found FpsBooster method: {type.FullName}.{method.Name} in assembly: {assemblyName}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // 一部のアセンブリは読めない場合があるので無視
                    }
                }
                
                UnityEngine.Debug.Log("[CS1Profiler] === FpsBooster Investigation Complete ===");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[CS1Profiler] FpsBooster investigation failed: {e.Message}");
            }
        }

        private static void FallbackModDetection()
        {
            // フォールバック：一般的でないアセンブリ名をMODとして扱う
            var knownGameAssemblies = new HashSet<string>
            {
                "Assembly-CSharp", "ICities", "ColossalManaged", "UnityEngine", "mscorlib", "System"
            };
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (!knownGameAssemblies.Any(known => name.Contains(known)))
                {
                    _modAssemblyNames.Add(name);
                }
            }
        }

        private static void LogPatchedMethodsSummary()
        {
            if (_methodStats.Count > 0)
            {
                var managerMethods = _methodStats.Keys.Where(k => k.Contains("Manager")).ToList();
                var modMethods = _methodStats.Keys.Where(k => IsDetectedModMethod(k)).ToList();
                var aiMethods = _methodStats.Keys.Where(k => k.Contains("AI")).ToList();
                var renderMethods = _methodStats.Keys.Where(k => k.Contains("Render")).ToList();
                var otherMethods = _methodStats.Keys.Except(managerMethods).Except(modMethods).Except(aiMethods).Except(renderMethods).ToList();
                
                UnityEngine.Debug.Log($"[CS1Profiler] Patched methods by category:");
                UnityEngine.Debug.Log($"[CS1Profiler] - Manager methods: {managerMethods.Count}");
                UnityEngine.Debug.Log($"[CS1Profiler] - MOD methods: {modMethods.Count}");
                UnityEngine.Debug.Log($"[CS1Profiler] - AI methods: {aiMethods.Count}");
                UnityEngine.Debug.Log($"[CS1Profiler] - Render methods: {renderMethods.Count}");
                UnityEngine.Debug.Log($"[CS1Profiler] - Other methods: {otherMethods.Count}");
                
                // 各カテゴリの最初の3個を例として表示
                if (managerMethods.Count > 0)
                {
                    UnityEngine.Debug.Log("[CS1Profiler] Manager examples: " + string.Join(", ", managerMethods.Take(3).ToArray()));
                }
                if (modMethods.Count > 0)
                {
                    UnityEngine.Debug.Log("[CS1Profiler] MOD examples: " + string.Join(", ", modMethods.Take(3).ToArray()));
                }
                if (aiMethods.Count > 0)
                {
                    UnityEngine.Debug.Log("[CS1Profiler] AI examples: " + string.Join(", ", aiMethods.Take(3).ToArray()));
                }
            }
        }

        private static bool IsDetectedModMethod(string methodKey)
        {
            // 動的に検出されたMODタイプ名をチェック
            return _modTypeNames.Any(modType => methodKey.StartsWith(modType + "."));
        }

        private static void PatchPerformanceCriticalMethods(Harmony harmony, Assembly assembly)
        {
            try
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                int patchedCount = 0;
                const int MAX_PATCHES = 100; // パッチ数を増やして実際の負荷分布を確認

                foreach (var type in types)
                {
                    if (type == null || patchedCount >= MAX_PATCHES) continue;
                    if (type.IsGenericTypeDefinition) continue; // ジェネリック型定義をスキップ

                    // 性能に影響する可能性のあるメソッドを包括的に取得（DeclaredOnlyで基底クラスの特殊メソッド除外）
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    
                    foreach (var method in methods)
                    {
                        if (patchedCount >= MAX_PATCHES) break;
                        
                        if (IsPerformanceCriticalMethod(type, method) && HasIL(method))
                        {
                            try
                            {
                                var key = $"{type.Name}.{method.Name}";
                                _methodStats[key] = new ProfileData
                                {
                                    MethodName = key,
                                    LastCall = DateTime.Now
                                };

                                harmony.Patch(method,
                                    prefix: new HarmonyMethod(typeof(MethodProfiler), nameof(MethodStart)),
                                    postfix: new HarmonyMethod(typeof(MethodProfiler), nameof(MethodEnd))
                                );
                                
                                patchedCount++;
                                UnityEngine.Debug.Log($"[CS1Profiler] Successfully patched: {key}");
                            }
                            catch (Exception e)
                            {
                                // パッチに失敗したメソッドは無視
                                UnityEngine.Debug.LogWarning($"[CS1Profiler] Failed to patch {type.Name}.{method.Name}: {e.Message}");
                            }
                        }
                    }
                }
                
                UnityEngine.Debug.Log($"[CS1Profiler] Assembly {assembly.GetName().Name}: Applied {patchedCount} patches");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[CS1Profiler] Failed to process assembly {assembly.FullName}: {e.Message}");
            }
        }

        private static bool HasIL(MethodInfo m)
        {
            if (m == null) return false;
            if (m.IsAbstract) return false;
            if (m.DeclaringType == null || m.DeclaringType.IsInterface) return false;
            if (m.IsConstructor) return false;              // .ctor/.cctorは外す
            if (m.IsSpecialName) return false;              // get_/set_/op_ 等は外す
            if (m.ContainsGenericParameters || m.IsGenericMethodDefinition) return false;

            // ネイティブ実装はパッチ不可
            var impl = m.GetMethodImplementationFlags();
            if ((impl & MethodImplAttributes.InternalCall) != 0) return false;
            if ((m.Attributes & MethodAttributes.PinvokeImpl) != 0) return false;

            // Mono で不安定になりやすいものをヒューリスティックに除外
            try { var _ = m.MetadataToken; } catch { return false; }   // Dynamic/不完全なメソッド除外
            var mod = m.Module.Name;
            if (string.IsNullOrEmpty(mod) || mod.IndexOf("In Memory", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return true; // ★ GetMethodBody() は呼ばない
        }

        private static bool IsPerformanceCriticalMethod(Type type, MethodInfo method)
        {
            var t = type.Name;
            var n = method.Name;

            // 共通除外
            if (n.StartsWith("get_") || n.StartsWith("set_") ||
                n.StartsWith("add_") || n.StartsWith("remove_") ||
                n == "Equals" || n == "GetHashCode" || n == "ToString" || n == "Finalize")
                return false;

            // ここを"入口"中心に
            if (t.EndsWith("Manager") && (n == "SimulationStepImpl" || n == "SimulationStep" || n.EndsWith("Update")))
                return true;

            if (t.Contains("AI") && (n == "SimulationStep" || n.EndsWith("Update")))
                return true;

            // 画面系の定番ホットスポット
            if (n == "OnRenderImage") return true;

            // MOD検出分は Update/SimulationStep 系だけ
            if (IsFromDetectedMod(type) &&
                (n.Contains("SimulationStep") || n.EndsWith("Update") || n == "OnBeforeSimulation" || n == "OnAfterSimulation"))
                return true;

            return false;
        }

        private static bool IsFromDetectedMod(Type type)
        {
            // 動的に検出されたMODのタイプかチェック
            if (_modTypeNames.Contains(type.Name))
                return true;
                
            // アセンブリ名でもチェック
            var assemblyName = type.Assembly.GetName().Name;
            return _modAssemblyNames.Contains(assemblyName);
        }

        public static void MethodStart(MethodBase __originalMethod)
        {
            try
            {
                var key = $"{__originalMethod.DeclaringType.Name}.{__originalMethod.Name}";
                var sw = Stopwatch.StartNew();
                _activeStopwatches[key] = sw;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[CS1Profiler] MethodStart error: {e.Message}");
            }
        }

        public static void MethodEnd(MethodBase __originalMethod)
        {
            try
            {
                var key = $"{__originalMethod.DeclaringType.Name}.{__originalMethod.Name}";
                
                if (_activeStopwatches.TryGetValue(key, out var sw))
                {
                    sw.Stop();
                    _activeStopwatches.Remove(key);

                    if (_methodStats.TryGetValue(key, out var stats))
                    {
                        var executionTimeMs = sw.ElapsedTicks / 10000.0;
                        var currentAverage = stats.AverageMs;
                        
                        // 統計を更新
                        stats.TotalTicks += sw.ElapsedTicks;
                        stats.CallCount++;
                        stats.LastCall = DateTime.Now;
                        
                        if (sw.ElapsedTicks > stats.MaxTicks)
                            stats.MaxTicks = sw.ElapsedTicks;

                        // スパイク検出
                        DetectSpike(stats, executionTimeMs, currentAverage, __originalMethod);
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[CS1Profiler] MethodEnd error: {e.Message}");
            }
        }

        private static void DetectSpike(ProfileData stats, double executionTimeMs, double currentAverage, MethodBase method)
        {
            try
            {
                // スパイク検出の条件
                if (stats.CallCount >= MIN_CALLS_FOR_SPIKE_DETECTION && 
                    currentAverage > 0 && 
                    executionTimeMs >= currentAverage * SPIKE_THRESHOLD_RATIO)
                {
                    // スパイク情報を記録
                    var spikeInfo = new SpikeInfo
                    {
                        Timestamp = DateTime.Now,
                        ExecutionTimeMs = executionTimeMs,
                        AverageAtTime = currentAverage,
                        CallStackInfo = GetCallStackInfo()
                    };

                    stats.Spikes.Add(spikeInfo);

                    // 最大記録数を超えた場合、古いスパイクを削除
                    if (stats.Spikes.Count > MAX_SPIKES_PER_METHOD)
                    {
                        stats.Spikes.RemoveAt(0);
                    }

                    // 重要なスパイクの場合はログ出力
                    if (executionTimeMs >= currentAverage * (SPIKE_THRESHOLD_RATIO * 2))
                    {
                        var assemblyName = method.DeclaringType?.Assembly?.GetName()?.Name ?? "Unknown";
                        UnityEngine.Debug.LogWarning(
                            $"[CS1Profiler] SPIKE DETECTED: {stats.MethodName} " +
                            $"executed in {executionTimeMs:F2}ms " +
                            $"(avg: {currentAverage:F2}ms, ratio: {spikeInfo.SpikeRatio:F1}x) " +
                            $"from assembly: {assemblyName}");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[CS1Profiler] Spike detection error: {e.Message}");
            }
        }

        private static string GetCallStackInfo()
        {
            try
            {
                // 簡易的な呼び出し元情報（Cities: Skylinesでは制限あり）
                var stackTrace = new StackTrace(3, false); // スキップフレーム数を調整
                var frame = stackTrace.GetFrame(0);
                if (frame != null && frame.GetMethod() != null)
                {
                    var method = frame.GetMethod();
                    return $"{method.DeclaringType?.Name}.{method.Name}";
                }
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public static Dictionary<string, ProfileData> GetStats()
        {
            return new Dictionary<string, ProfileData>(_methodStats);
        }

        public static List<ProfileData> GetMethodsWithSpikes()
        {
            return _methodStats.Values.Where(s => s.SpikeCount > 0)
                               .OrderByDescending(s => s.MaxSpikeRatio)
                               .ToList();
        }

        public static List<SpikeInfo> GetRecentSpikes(int minutes = 5)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            var recentSpikes = new List<SpikeInfo>();

            foreach (var stats in _methodStats.Values)
            {
                var recent = stats.Spikes.Where(s => s.Timestamp >= cutoff).ToList();
                recentSpikes.AddRange(recent);
            }

            return recentSpikes.OrderByDescending(s => s.Timestamp).ToList();
        }

        public static string GetSpikeReport()
        {
            var spikedMethods = GetMethodsWithSpikes();
            if (!spikedMethods.Any())
            {
                return "No spikes detected.";
            }

            var report = $"=== SPIKE ANALYSIS REPORT ===\n";
            report += $"Total methods with spikes: {spikedMethods.Count}\n\n";

            foreach (var method in spikedMethods.Take(10)) // Top 10
            {
                report += $"Method: {method.MethodName}\n";
                report += $"  Average: {method.AverageMs:F2}ms, Max: {method.MaxMs:F2}ms\n";
                report += $"  Spikes: {method.SpikeCount}, Max ratio: {method.MaxSpikeRatio:F1}x\n";
                
                var recentSpike = method.Spikes.LastOrDefault();
                if (recentSpike != null)
                {
                    report += $"  Last spike: {recentSpike.Timestamp:HH:mm:ss} " +
                             $"({recentSpike.ExecutionTimeMs:F2}ms, {recentSpike.SpikeRatio:F1}x avg)\n";
                }
                report += "\n";
            }

            return report;
        }

        public static string GetTopSlowMethods(int count = 10)
        {
            var allMethods = _methodStats.Values.Where(m => m.CallCount > 0).ToList();
            if (!allMethods.Any())
            {
                return "No method data available.";
            }

            // 平均実行時間でソート
            var slowMethods = allMethods.OrderByDescending(m => m.AverageMs).Take(count).ToList();
            
            var report = $"=== TOP {count} SLOWEST METHODS ===\n";
            for (int i = 0; i < slowMethods.Count; i++)
            {
                var method = slowMethods[i];
                report += $"{i+1}. {method.MethodName}\n";
                report += $"   Average: {method.AverageMs:F2}ms, Max: {method.MaxMs:F2}ms\n";
                report += $"   Calls: {method.CallCount}, Total: {method.TotalMs:F1}ms\n";
                if (method.SpikeCount > 0)
                {
                    report += $"   Spikes: {method.SpikeCount} (Max ratio: {method.MaxSpikeRatio:F1}x)\n";
                }
                report += "\n";
            }

            return report;
        }

        public static void ClearStats()
        {
            foreach (var stat in _methodStats.Values)
            {
                stat.TotalTicks = 0;
                stat.CallCount = 0;
                stat.MaxTicks = 0;
            }
        }

        public static void Reset()
        {
            _methodStats.Clear();
            _activeStopwatches.Clear();
        }
    }
}
