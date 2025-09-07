using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;

using CS1Profiler.Core;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// 軽量ログイベント構造体（Producerで使用）
    /// </summary>
    public struct LogEvent
    {
        public MethodBase Method;
        public long ElapsedTicks;
        public int CallCount;
        
        public LogEvent(MethodBase method, long elapsedTicks, int callCount)
        {
            Method = method;
            ElapsedTicks = elapsedTicks;
            CallCount = callCount;
        }
    }

    /// <summary>
    /// MPSC型ロチE��フリー・キュー + 専用書き込みスレチE��
    /// 趁E��量：Producer側は斁E���E化もI/OもしなぁE
    /// </summary>
    public class MPSCLogger
    {
        private static MPSCLogger _instance;
        public static MPSCLogger Instance => _instance ?? (_instance = new MPSCLogger());

        // MPSC Queue�E�ENET 3.5対応！E
        private readonly ConcurrentQueue<LogEvent> _eventQueue = new ConcurrentQueue<LogEvent>();
        private volatile int _queueCount = 0;
        private const int MAX_QUEUE_SIZE = 50000; // 大きすぎたらDrop

        // Consumer専用スレチE��
        private Thread _writerThread;
        private volatile bool _isRunning = false;
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);

        // Consumer側でのみ使用�E�スレチE��セーフ不要E��E
        private StreamWriter _csvWriter;
        private string _csvFilePath;
        private readonly Dictionary<MethodBase, string> _methodNameCache = new Dictionary<MethodBase, string>();
        private bool _headerWritten = false;

        /// <summary>
        /// Writer開始！EerformanceProfilingEnabled=true時！E
        /// </summary>
        public void StartWriter()
        {
            if (_isRunning) return;

            try
            {
                // CSVファイル準備
                string gameDirectory = Application.dataPath;
                if (gameDirectory.EndsWith("_Data"))
                {
                    gameDirectory = Directory.GetParent(gameDirectory).FullName;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _csvFilePath = Path.Combine(gameDirectory, string.Format("CS1Profiler_MPSC_{0}.csv", timestamp));

                _isRunning = true;
                _writerThread = new Thread(WriterThreadMain)
                {
                    Name = "CS1Profiler-Writer",
                    IsBackground = true
                };
                _writerThread.Start();

                Debug.Log(string.Format($"{Constants.LOG_PREFIX} MPSC Writer started: {0}", _csvFilePath));
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format($"{Constants.LOG_PREFIX} Failed to start MPSC writer: {0}", e.Message));
                _isRunning = false;
            }
        }

        /// <summary>
        /// Writer停止�E�Elush�E�EerformanceProfilingEnabled=false時！E
        /// </summary>
        public void StopWriter()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _signal.Set(); // スレチE��起庁E

            if (_writerThread != null && _writerThread.IsAlive)
            {
                _writerThread.Join(5000); // 5秒でタイムアウチE
            }

            Debug.Log($"{Constants.LOG_PREFIX} MPSC Writer stopped");
        }

        /// <summary>
        /// Producer側�E�趁E��量エンキュー�E�Erefix/Postfixから呼び出し！E
        /// </summary>
        public void Enqueue(MethodBase method, long elapsedTicks, int callCount)
        {
            if (!_isRunning) return; // 即座にリターン
            
            // バックログ制限：Dropで絶対に征E��なぁE
            if (_queueCount >= MAX_QUEUE_SIZE) return;

            _eventQueue.Enqueue(new LogEvent(method, elapsedTicks, callCount));
            Interlocked.Increment(ref _queueCount);
            
            // 適度に信号送信�E�毎回ではなぁE��E
            if (_queueCount % 100 == 0)
            {
                _signal.Set();
            }
        }

        /// <summary>
        /// Consumer専用スレチE��のメイン処琁E
        /// </summary>
        private void WriterThreadMain()
        {
            try
            {
                _csvWriter = new StreamWriter(_csvFilePath, false);
                
                while (_isRunning || _queueCount > 0)
                {
                    DrainQueue();
                    
                    if (_isRunning)
                    {
                        _signal.WaitOne(1000); // 1秒タイムアウチE
                    }
                }
                
                // 最終フラチE��ュ
                DrainQueue();
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format($"{Constants.LOG_PREFIX} Writer thread error: {0}", e.Message));
            }
            finally
            {
                if (_csvWriter != null)
                {
                    _csvWriter.Flush();
                    _csvWriter.Close();
                    _csvWriter = null;
                }
            }
        }

        /// <summary>
        /// キューをドレイン�E�Eonsumer専用�E�E
        /// </summary>
        private void DrainQueue()
        {
            if (!_headerWritten)
            {
                _csvWriter.WriteLine("MethodName,Duration(ms),CallCount");
                _headerWritten = true;
            }

            LogEvent logEvent;
            int processedCount = 0;
            
            while (_eventQueue.TryDequeue(out logEvent) && processedCount < 1000)
            {
                try
                {
                    // メソチE��名をキャチE��ュ生�E�E��E回�Eみ�E�E
                    string methodName;
                    if (!_methodNameCache.TryGetValue(logEvent.Method, out methodName))
                    {
                        methodName = GetMethodName(logEvent.Method);
                        _methodNameCache[logEvent.Method] = methodName;
                    }

                    // 実行時間計箁E
                    double durationMs = (logEvent.ElapsedTicks * 1000.0) / Stopwatch.Frequency;

                    // CSV書き込み
                    _csvWriter.WriteLine(string.Format("{0},{1:F3},{2}",
                        methodName, durationMs, logEvent.CallCount));

                    processedCount++;
                    Interlocked.Decrement(ref _queueCount);
                }
                catch (Exception e)
                {
                    Debug.LogError(string.Format($"{Constants.LOG_PREFIX} Error processing log event: {0}", e.Message));
                }
            }

            // 間欠フラチE��ュ
            if (processedCount > 0)
            {
                _csvWriter.Flush();
            }
        }

        /// <summary>
        /// メソチE��名生成！Eonsumer専用�E�E
        /// </summary>
        private string GetMethodName(MethodBase method)
        {
            try
            {
                if (method == null) return "Unknown";
                
                string typeName = method.DeclaringType?.FullName ?? "Unknown";
                return string.Format("{0}.{1}", typeName, method.Name);
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// 統計情報取征E
        /// </summary>
        public int GetQueueCount()
        {
            return _queueCount;
        }

        public bool IsRunning()
        {
            return _isRunning;
        }
    }
}
