using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Reflection;
using UnityEngine;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// 超軽量MPSC Logger (.NET 3.5対応版)
    /// Producer側：Stopwatch.GetTimestamp()のみ（Lock-free Ring Buffer）
    /// Consumer側：専用スレッドでI/O処理
    /// </summary>
    public static class MPSCLogger
    {
        // Lock-free Ring Buffer (.NET 3.5対応)
        private const int RING_BUFFER_SIZE = 65536; // 2^16（高速ビット演算用）
        private static readonly LogEvent[] _ringBuffer = new LogEvent[RING_BUFFER_SIZE];
        private static volatile int _writeIndex = 0;
        private static volatile int _readIndex = 0;
        
        // 専用Writer thread
        private static Thread _writerThread;
        private static volatile bool _running = false;
        private static volatile bool _forceStop = false;  // ★強制停止フラグ追加
        private static readonly object _startStopLock = new object();
        
        // メソッド名キャッシュ（Consumer側のみ使用）
        private static readonly Dictionary<string, string> _methodNameCache = new Dictionary<string, string>();
        
        // 出力先
        private static string _outputPath;
        
        /// <summary>
        /// ログイベント構造体（軽量）
        /// </summary>
        public struct LogEvent
        {
            public long StartTicks;
            public long EndTicks;
            public MethodBase MethodInfo;  // ★文字列ではなくMethodBase（Producer側は文字列化しない）
        }
        
        /// <summary>
        /// Writer threadを開始
        /// </summary>
        public static void StartWriter()
        {
            lock (_startStopLock)
            {
                if (_running) return;
                
                _running = true;
                
                // 日時ベースのファイル名（MPSCではない通常のファイル名）
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = string.Format("CS1Profiler_{0}.csv", timestamp);
                _outputPath = Path.Combine(Path.Combine(UnityEngine.Application.dataPath, ".."), fileName);
                
                _writerThread = new Thread(WriterThreadMain)
                {
                    Name = "CS1Profiler-Writer",
                    IsBackground = true
                };
                _writerThread.Start();
                
                Debug.Log(string.Format("[CS1Profiler] Writer thread started. Output: {0}", fileName));
            }
        }
        
        /// <summary>
        /// Writer threadを停止（即座停止版）
        /// </summary>
        public static void StopWriter()
        {
            lock (_startStopLock)
            {
                if (!_running) return;
                
                // ★即座停止：新規エンキューを停止
                _running = false;
                _forceStop = true;
                
                // ★Ring Bufferクリア（残データ書き込み防止）
                _writeIndex = 0;
                _readIndex = 0;
                
                if (_writerThread != null && _writerThread.IsAlive)
                {
                    _writerThread.Join(2000); // 2秒でタイムアウト（短縮）
                }
                
                // ★状態リセット
                _forceStop = false;
                _methodNameCache.Clear();
                
                Debug.Log("[CS1Profiler] MPSC Writer thread stopped immediately");
            }
        }
        
        /// <summary>
        /// 超軽量：Producer側エンキュー（Lock-free Ring Buffer）
        /// Producer側では文字列化を一切行わない
        /// </summary>
        public static void EnqueueMethod(MethodBase methodInfo, long startTicks, long endTicks)
        {
            // ★即座停止チェック（running + forceStop）
            if (!_running || _forceStop) return;
            
            // Lock-free Ring Buffer書き込み（.NET 3.5対応）
            int currentWrite = System.Threading.Interlocked.Increment(ref _writeIndex) - 1;
            int bufferIndex = currentWrite & (RING_BUFFER_SIZE - 1); // 高速ビット演算
            
            // Ring Bufferに直接書き込み（競合なし）
            _ringBuffer[bufferIndex] = new LogEvent
            {
                MethodInfo = methodInfo,  // ★MethodBaseをそのまま渡す（文字列化なし）
                StartTicks = startTicks,
                EndTicks = endTicks
            };
        }
        
        /// <summary>
        /// 軽量：Producer側エンキュー（Lock-free Ring Buffer）
        /// 旧互換性維持用
        /// </summary>
        public static void Enqueue(string methodName, long startTicks, long endTicks)
        {
            // ★即座停止チェック（running + forceStop）
            if (!_running || _forceStop) return;
            
            // Lock-free Ring Buffer書き込み
            int currentWrite = System.Threading.Interlocked.Increment(ref _writeIndex) - 1;
            int bufferIndex = currentWrite & (RING_BUFFER_SIZE - 1);
            
            _ringBuffer[bufferIndex] = new LogEvent
            {
                MethodInfo = null,  // 文字列版では後でmethodNameを使用
                StartTicks = startTicks,
                EndTicks = endTicks
            };
        }
        
        /// <summary>
        /// Writer thread main loop（Consumer側）
        /// </summary>
        private static void WriterThreadMain()
        {
            try
            {
                using (var writer = new StreamWriter(_outputPath, false))
                {
                    // CSVヘッダー（日時列追加）
                    writer.WriteLine("MethodName,Duration(ms),StartTime,EndTime,Timestamp");
                    
                    // ★即座停止対応：forceStopで即座終了
                    while (_running && !_forceStop)
                    {
                        // Lock-free Ring Buffer読み取り
                        LogEvent logEvent;
                        bool hasEvent = false;
                        
                        if (_readIndex < _writeIndex && !_forceStop)
                        {
                            int bufferIndex = _readIndex & (RING_BUFFER_SIZE - 1);
                            logEvent = _ringBuffer[bufferIndex];
                            _readIndex++;
                            hasEvent = true;
                        }
                        else
                        {
                            logEvent = default(LogEvent);
                        }
                        
                        // ★強制停止チェック
                        if (_forceStop) break;
                        
                        if (hasEvent)
                        {
                            // タイマー精度でミリ秒計算
                            double durationMs = (logEvent.EndTicks - logEvent.StartTicks) / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;
                            
                            // ★Consumer側で文字列化（Producer側では一切文字列化しない）
                            string methodName;
                            if (logEvent.MethodInfo != null)
                            {
                                // MethodBaseから文字列化（Consumer側のみ）
                                string namespaceName = logEvent.MethodInfo.DeclaringType?.Namespace ?? "Unknown";
                                string className = logEvent.MethodInfo.DeclaringType?.Name ?? "Unknown";
                                string methodLocalName = logEvent.MethodInfo.Name ?? "Unknown";
                                methodName = string.Format("{0}.{1}.{2}", namespaceName, className, methodLocalName);
                            }
                            else
                            {
                                // 旧互換性（文字列版）
                                methodName = "LegacyStringMethod";
                            }
                            
                            // メソッド名キャッシュ
                            string cachedMethodName;
                            if (!_methodNameCache.TryGetValue(methodName, out cachedMethodName))
                            {
                                cachedMethodName = methodName;
                                if (_methodNameCache.Count < 10000) // キャッシュサイズ制限
                                {
                                    _methodNameCache[methodName] = cachedMethodName;
                                }
                            }
                            
                            // CSV書き込み（日時詳細を追加）
                            DateTime now = DateTime.Now;
                            double startTimeMs = logEvent.StartTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;
                            double endTimeMs = logEvent.EndTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;
                            
                            writer.WriteLine(string.Format("{0},{1:F3},{2:F3},{3:F3},{4}",
                                cachedMethodName,
                                durationMs,
                                startTimeMs,
                                endTimeMs,
                                now.ToString("yyyy-MM-dd HH:mm:ss.fff")));
                        }
                        else
                        {
                            // CPU使用率軽減
                            Thread.Sleep(10);
                        }
                    }
                    
                    writer.Flush();
                }
                
                Debug.Log(string.Format("[CS1Profiler] MPSC Writer completed. Output: {0}", _outputPath));
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[CS1Profiler] MPSC Writer error: {0}", e.Message));
            }
        }
    }
}
