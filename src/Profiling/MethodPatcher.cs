using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// Harmonyパッチの管理とメソッド検出
    /// </summary>
    public static class MethodPatcher
    {
        private static readonly HashSet<string> _modAssemblyNames = new HashSet<string>();
        private static readonly HashSet<string> _modTypeNames = new HashSet<string>();
        private static int _patchedMethodCount = 0;
        
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] MethodPatcher initializing (detection only)...");
                
                // MODアセンブリ検出のみ実行（パッチは既存のHarmonyPatches.csで行う）
                DetectModAssemblies();
                
                // パッチ数は既存システムから報告
                _patchedMethodCount = 1; // 既存のSimulationStepパッチが動作中
                
                UnityEngine.Debug.Log($"[CS1Profiler] MethodPatcher detection completed - using existing patches");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] MethodPatcher.Initialize error: {e.Message}");
            }
        }
        
        private static void DetectModAssemblies()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    var name = assembly.GetName().Name;
                    
                    // MODアセンブリを検出
                    if (IsModAssembly(name))
                    {
                        _modAssemblyNames.Add(name);
                        
                        // 型情報も収集
                        try
                        {
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                if (IsPerformanceCriticalType(type))
                                {
                                    _modTypeNames.Add(type.FullName);
                                }
                            }
                        }
                        catch { /* 一部のアセンブリは型を取得できない */ }
                    }
                }
                
                UnityEngine.Debug.Log($"[CS1Profiler] Detected {_modAssemblyNames.Count} MOD assemblies, {_modTypeNames.Count} critical types");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] DetectModAssemblies error: {e.Message}");
            }
        }
        
        private static bool IsModAssembly(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            
            // ゲーム本体のアセンブリを除外
            if (name.StartsWith("Assembly-") || name.StartsWith("UnityEngine") || 
                name.StartsWith("System") || name.StartsWith("mscorlib") ||
                name.StartsWith("ColossalManaged") || name.StartsWith("ICities"))
                return false;
            
            // MODアセンブリの特徴
            return name.Contains("Mod") || name.Contains("Plugin") || 
                   name.Length > 3; // 短すぎない独自アセンブリ
        }
        
        private static bool IsPerformanceCriticalType(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface) return false;
            
            var typeName = type.Name.ToLower();
            return typeName.Contains("manager") || typeName.Contains("controller") ||
                   typeName.Contains("update") || typeName.Contains("simulation") ||
                   typeName.Contains("render") || typeName.Contains("thread");
        }
        
        private static void PatchCriticalAssemblies(HarmonyLib.Harmony harmony)
        {
            var criticalAssemblies = new[]
            {
                "ColossalManaged",  // ゲーム本体の重要部分
                "Assembly-CSharp", // ゲーム本体
                "CitiesHarmony.API" // Harmony統合
            };
            
            foreach (var assemblyName in criticalAssemblies)
            {
                try
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == assemblyName);
                    
                    if (assembly != null)
                    {
                        PatchAssemblyMethods(harmony, assembly);
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[CS1Profiler] Failed to patch {assemblyName}: {e.Message}");
                }
            }
            
            // 検出されたMODアセンブリもパッチ（制限付き）
            foreach (var modAssemblyName in _modAssemblyNames.Take(5)) // 最大5個まで
            {
                try
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == modAssemblyName);
                    
                    if (assembly != null)
                    {
                        PatchAssemblyMethods(harmony, assembly);
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[CS1Profiler] Failed to patch MOD {modAssemblyName}: {e.Message}");
                }
            }
        }
        
        private static void PatchAssemblyMethods(HarmonyLib.Harmony harmony, Assembly assembly)
        {
            try
            {
                var types = assembly.GetTypes();
                var patchedInAssembly = 0;
                
                foreach (var type in types.Take(50)) // 型数制限
                {
                    if (IsPerformanceCriticalType(type))
                    {
                        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(IsPerformanceCriticalMethod)
                            .Take(10); // メソッド数制限
                        
                        foreach (var method in methods)
                        {
                            try
                            {
                                harmony.Patch(method,
                                    prefix: new HarmonyMethod(typeof(PerformanceProfiler), nameof(PerformanceProfiler.MethodStart)),
                                    postfix: new HarmonyMethod(typeof(PerformanceProfiler), nameof(PerformanceProfiler.MethodEnd)));
                                
                                patchedInAssembly++;
                                _patchedMethodCount++;
                            }
                            catch { /* 一部のメソッドはパッチできない */ }
                        }
                    }
                }
                
                if (patchedInAssembly > 0)
                {
                    UnityEngine.Debug.Log($"[CS1Profiler] Patched {patchedInAssembly} methods in {assembly.GetName().Name}");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] PatchAssemblyMethods error: {e.Message}");
            }
        }
        
        private static bool IsPerformanceCriticalMethod(MethodInfo method)
        {
            if (method == null || method.IsAbstract || method.IsConstructor) return false;
            
            var methodName = method.Name.ToLower();
            return methodName.Contains("update") || methodName.Contains("simulate") ||
                   methodName.Contains("render") || methodName.Contains("calculate") ||
                   methodName.StartsWith("on") || method.Name == "LateUpdate";
        }
        
        public static bool IsFromDetectedMod(string methodKey)
        {
            return _modTypeNames.Any(typeName => methodKey.Contains(typeName));
        }
        
        public static int GetPatchedMethodCount()
        {
            return _patchedMethodCount;
        }
    }
}
