using System;
using System.IO;
using System.Reflection;
using CS1Profiler.Profiling;

namespace CS1Profiler.Test
{
    /// <summary>
    /// プロファイラーとCSV出力のテスト用クラス
    /// </summary>
    public static class ProfilerTest
    {
        public static void RunTest()
        {
            try
            {
                Console.WriteLine("[CS1ProfilerTest] Starting profiler test...");
                
                // テスト用のメソッド情報を作成
                var testMethod = typeof(ProfilerTest).GetMethod("TestMethod", BindingFlags.NonPublic | BindingFlags.Static);
                
                // プロファイラーを初期化
                MethodProfiler.Initialize(null);
                
                // テストデータを生成
                Console.WriteLine("[CS1ProfilerTest] Generating test data...");
                for (int i = 0; i < 10; i++)
                {
                    MethodProfiler.MethodStart(testMethod);
                    System.Threading.Thread.Sleep(10 + (i * 5)); // 実行時間をシミュレート
                    MethodProfiler.MethodEnd(testMethod);
                }
                
                // CSV出力をテスト
                Console.WriteLine("[CS1ProfilerTest] Testing CSV output...");
                var csvData = MethodProfiler.GetCSVReport();
                
                if (!string.IsNullOrEmpty(csvData))
                {
                    var csvFile = "TestProfileData.csv";
                    File.WriteAllText(csvFile, csvData);
                    Console.WriteLine($"[CS1ProfilerTest] CSV data written to {csvFile}");
                    Console.WriteLine($"[CS1ProfilerTest] CSV preview:");
                    Console.WriteLine(csvData);
                }
                else
                {
                    Console.WriteLine("[CS1ProfilerTest] No CSV data generated");
                }
                
                // プロファイル統計をテスト
                var report = MethodProfiler.GetPerformanceReport();
                Console.WriteLine($"[CS1ProfilerTest] Performance report:");
                Console.WriteLine(report);
                
                Console.WriteLine("[CS1ProfilerTest] Test completed successfully");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[CS1ProfilerTest] Test failed: {e.Message}");
                Console.WriteLine($"[CS1ProfilerTest] Stack trace: {e.StackTrace}");
            }
        }
        
        private static void TestMethod()
        {
            // テスト用のダミーメソッド
            System.Threading.Thread.Sleep(10);
        }
    }
}
