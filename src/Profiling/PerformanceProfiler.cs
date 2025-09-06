using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using CS1Profiler.Core;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// 軽量なメソッド実行時間測定 - 高速配列ベース
    /// </summary>
    public static class PerformanceProfiler
    {
        // 軽量測定システム用の配列ベースストレージ
        private const int MAX_METHODS = 8192; // サイズ削減
        private const int SAMPLE_INTERVAL = 10; // 10回に1回サンプリング
        
        private static long[] _totalNs = new long[MAX_METHODS];
        private static int[] _callCounts = new int[MAX_METHODS];
        private static long[] _maxNs = new long[MAX_METHODS];
        private static string[] _methodNames = new string[MAX_METHODS];
        private static string[] _assemblyNames = new string[MAX_METHODS];
        private static int _nextMethodId = 0;
        private static Dictionary<MethodBase, int> _methodIds = new Dictionary<MethodBase, int>();
        
        // ThreadStatic TSCタイマー
        [ThreadStatic]
        private static long _tscStart;
        [ThreadStatic]
        private static int _currentMethodId;
        [ThreadStatic]
        private static int _sampleCounter;
        
        public static void MethodStart(MethodBase method)
        {
            if (method == null || _nextMethodId >= MAX_METHODS) return;
            
            // サンプリング制御
            if (++_sampleCounter % SAMPLE_INTERVAL != 0) return;
            
            // メソッドID取得または割り当て
            if (!_methodIds.TryGetValue(method, out _currentMethodId))
            {
                _currentMethodId = _nextMethodId++;
                if (_currentMethodId >= MAX_METHODS) return;
                
                _methodIds[method] = _currentMethodId;
                _methodNames[_currentMethodId] = method.DeclaringType?.Name + "." + method.Name;
                _assemblyNames[_currentMethodId] = method.DeclaringType?.Assembly.GetName().Name ?? "Unknown";
            }
            
            _tscStart = Stopwatch.GetTimestamp();
        }
        
        public static void MethodEnd()
        {
            if (_currentMethodId <= 0 || _currentMethodId >= MAX_METHODS) return;
            
            long elapsed = Stopwatch.GetTimestamp() - _tscStart;
            long elapsedNs = elapsed * 1000000000L / Stopwatch.Frequency;
            
            _totalNs[_currentMethodId] += elapsedNs;
            _callCounts[_currentMethodId]++;
            if (elapsedNs > _maxNs[_currentMethodId])
                _maxNs[_currentMethodId] = elapsedNs;
        }

        // Harmonyパッチからの呼び出し用オーバーロード
        public static void MethodEnd(MethodBase method)
        {
            MethodEnd(); // 既存のロジックを使用
        }
        
        public static List<ProfileData> GetTopMethods(int count = 10)
        {
            var results = new List<ProfileData>();
            
            for (int i = 1; i < _nextMethodId && i < MAX_METHODS; i++)
            {
                if (_callCounts[i] > 0)
                {
                    results.Add(new ProfileData
                    {
                        MethodName = _methodNames[i],
                        AssemblyName = _assemblyNames[i],
                        TotalMilliseconds = _totalNs[i] / 1000000.0,
                        AverageMilliseconds = _totalNs[i] / 1000000.0 / _callCounts[i],
                        MaxMilliseconds = _maxNs[i] / 1000000.0,
                        CallCount = _callCounts[i]
                    });
                }
            }
            
            results.Sort((a, b) => b.TotalMilliseconds.CompareTo(a.TotalMilliseconds));
            return results.GetRange(0, Math.Min(count, results.Count));
        }
        
        public static Dictionary<string, double> GetDetailedStats()
        {
            var stats = new Dictionary<string, double>();
            
            for (int i = 0; i < _nextMethodId; i++)
            {
                if (_callCounts[i] > 0 && _methodNames[i] != null)
                {
                    double avgMs = _totalNs[i] / 1000000.0 / _callCounts[i];
                    stats[_methodNames[i]] = avgMs;
                }
            }
            
            return stats;
        }
        
        public static void Reset()
        {
            for (int i = 0; i < MAX_METHODS; i++)
            {
                _totalNs[i] = 0;
                _callCounts[i] = 0;
                _maxNs[i] = 0;
                _methodNames[i] = null;
                _assemblyNames[i] = null;
            }
            _nextMethodId = 0;
            _methodIds.Clear();
        }

        // 要件対応: HarmonyパッチからDirectで呼び出されるメソッド
        public static void RecordMethodExecution(string methodKey, long elapsedTicks)
        {
            try
            {
                if (_nextMethodId >= MAX_METHODS) return;
                
                // メソッドキーからIDを検索または作成
                int methodId = -1;
                for (int i = 0; i < _nextMethodId; i++)
                {
                    if (_methodNames[i] == methodKey)
                    {
                        methodId = i;
                        break;
                    }
                }
                
                if (methodId == -1)
                {
                    methodId = _nextMethodId++;
                    if (methodId >= MAX_METHODS) return;
                    _methodNames[methodId] = methodKey;
                    _assemblyNames[methodId] = "HarmonyPatched";
                }
                
                // 実行時間を記録
                long ns = elapsedTicks * 1000000000L / Stopwatch.Frequency;
                _totalNs[methodId] += ns;
                _callCounts[methodId]++;
                if (ns > _maxNs[methodId])
                {
                    _maxNs[methodId] = ns;
                }

                // CSVManagerにリアルタイムでデータを送信
                try
                {
                    double executionTimeMs = ns / 1000000.0;
                    if (CS1Profiler.Managers.ProfilerManager.Instance?.CsvManager != null)
                    {
                        CS1Profiler.Managers.ProfilerManager.Instance.CsvManager.LogMethodExecution(methodKey, executionTimeMs, _callCounts[methodId]);
                    }
                }
                catch
                {
                    // CSVエラーは無視してプロファイリングを続行
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} RecordMethodExecution error: " + e.Message);
            }
        }

        // 軽量版: 生データを直接CSV形式で出力
        public static string GetRawDataCSV()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Method,TotalNs,CallCount,MaxNs");
            
            try
            {
                for (int i = 0; i < _nextMethodId; i++)
                {
                    if (_callCounts[i] > 0 && _methodNames[i] != null)
                    {
                        csv.AppendLine(string.Format("{0},{1},{2},{3}",
                            _methodNames[i], _totalNs[i], _callCounts[i], _maxNs[i]));
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} GetRawDataCSV error: " + e.Message);
                csv.AppendLine("ERROR," + e.Message + ",0,0");
            }
            
            return csv.ToString();
        }

        // 全データをTopNと同じ形式で出力（ヘッダ統一）
        public static string GetAllDataCSV()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Rank,Method,AvgMs,MaxMs,TotalMs,Calls");
            
            try
            {
                // 平均時間でソートするためのリスト作成
                var methodList = new List<MethodData>();
                
                for (int i = 0; i < _nextMethodId; i++)
                {
                    if (_callCounts[i] > 0 && _methodNames[i] != null)
                    {
                        double totalMs = _totalNs[i] / 1000000.0;
                        double maxMs = _maxNs[i] / 1000000.0;
                        double avgMs = totalMs / _callCounts[i];
                        
                        methodList.Add(new MethodData
                        {
                            MethodName = _methodNames[i],
                            TotalMs = totalMs,
                            MaxMs = maxMs,
                            AvgMs = avgMs,
                            CallCount = _callCounts[i]
                        });
                    }
                }
                
                // 平均時間でソート（降順）
                for (int i = 0; i < methodList.Count - 1; i++)
                {
                    for (int j = i + 1; j < methodList.Count; j++)
                    {
                        if (methodList[i].AvgMs < methodList[j].AvgMs)
                        {
                            var temp = methodList[i];
                            methodList[i] = methodList[j];
                            methodList[j] = temp;
                        }
                    }
                }
                
                // CSV出力
                for (int i = 0; i < methodList.Count; i++)
                {
                    var method = methodList[i];
                    csv.AppendLine(string.Format("{0},{1},{2:F3},{3:F3},{4:F3},{5}",
                        i + 1, method.MethodName, method.AvgMs, method.MaxMs, method.TotalMs, method.CallCount));
                }
                
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} GetAllDataCSV error: " + e.Message);
                csv.AppendLine("ERROR,ERROR,0,0,0,0");
            }
            
            return csv.ToString();
        }

        // ヘルパークラス
        private struct MethodData
        {
            public string MethodName;
            public double TotalMs;
            public double MaxMs;
            public double AvgMs;
            public int CallCount;
        }
    }
}
