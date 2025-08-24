using System;
using CS1Profiler.Test;

namespace CS1Profiler
{
    /// <summary>
    /// テスト実行用のプログラムエントリーポイント
    /// </summary>
    class TestProgram
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== CS1 Profiler Test Application ===");
                ProfilerTest.RunTest();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
