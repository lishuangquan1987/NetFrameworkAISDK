using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System;
using System.Diagnostics;
using System.Text;

namespace AutoProxyTest
{
    class Program
    {
        private const string ApiKey = "111";
        private const string BaseUrl = "https://u701357-b42c-d29bc5d1.westc.seetacloud.com:8443/v1";
        private const string Model = "Qwen3.6-35B-A3B-FP8";

        static int passCount, failCount;

        static void Main(string[] args)
        {
            Console.WriteLine("=== AutoProxyTest — BouncyCastle TLS Proxy ===");
            Console.WriteLine("Target: " + BaseUrl);
            Console.WriteLine("Model: " + Model);
            Console.WriteLine();

            try { HttpClientBase.ForceTlsProxyForDiagnostics(); Console.WriteLine("[OK] Proxy started."); }
            catch (Exception ex) { Console.WriteLine("[FAIL] " + ex.Message); Environment.Exit(1); }

            // Test 1: Non-streaming (3 rounds)
            Console.WriteLine("\n--- Non-streaming (3 rounds) ---");
            for (int i = 1; i <= 3; i++)
            {
                Console.Write("Round " + i + ": ");
                TestNonStreaming();
            }

            // Test 2: Streaming (3 rounds)
            Console.WriteLine("\n--- Streaming (3 rounds) ---");
            for (int i = 1; i <= 3; i++)
            {
                Console.Write("Round " + i + ": ");
                TestStreaming();
            }

            Console.WriteLine("\n========================================");
            Console.WriteLine("PASS=" + passCount + " FAIL=" + failCount);
            Console.WriteLine("========================================");
            if (failCount > 0) Environment.Exit(1);
        }

        static void TestNonStreaming()
        {
            try
            {
                var client = new OpenAIClient(ApiKey, BaseUrl);
                var agent = new AIAgent(client, Model, "Keep replies under 10 words.", null);
                var sw = Stopwatch.StartNew();
                var result = agent.Run("Say hi in 3 words.");
                sw.Stop();

                if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Result))
                {
                    Console.WriteLine("PASS " + sw.ElapsedMilliseconds + "ms: " + result.Result.Trim());
                    passCount++;
                }
                else
                {
                    Console.WriteLine("FAIL " + (result.Error != null ? result.Error.Message : "empty"));
                    failCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message);
                failCount++;
            }
        }

        static void TestStreaming()
        {
            try
            {
                var client = new OpenAIClient(ApiKey, BaseUrl);
                var agent = new AIAgent(client, Model, "Keep replies under 10 words.", null);
                var sb = new StringBuilder();
                var sw = Stopwatch.StartNew();
                bool error = false;
                string errorMsg = "";

                agent.RunStreaming("Say hello in 3 words.",
                    onUpdate: chunk => sb.Append(chunk),
                    onError: err => { error = true; errorMsg = err.Message; });

                sw.Stop();

                if (error)
                {
                    Console.WriteLine("FAIL " + errorMsg);
                    failCount++;
                }
                else if (string.IsNullOrWhiteSpace(sb.ToString()))
                {
                    Console.WriteLine("FAIL empty (" + sw.ElapsedMilliseconds + "ms)");
                    failCount++;
                }
                else
                {
                    Console.WriteLine("PASS " + sw.ElapsedMilliseconds + "ms: " + sb.ToString().Trim());
                    passCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message);
                failCount++;
            }
        }
    }
}
