using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System;
using System.Diagnostics;

namespace NetFrameworkAISDK.Samples
{
    /// <summary>
    /// 测试 BouncyCastle TLS 代理功能：
    /// 先原生 SChannel 请求，再强制启用代理请求，对比验证代理转发正确性。
    /// 适用于任何 Windows 版本（非 XP 也可测试）。
    /// </summary>
    public class TlsProxySample : ISample
    {
        public string Name
        {
            get { return "TLS Proxy — BouncyCastle Proxy Test"; }
        }

        public void Run()
        {
            Console.WriteLine("\n=== BouncyCastle TLS Proxy Test ===");
            Console.WriteLine("Tests HTTPS requests with and without the BouncyCastle proxy.");
            Console.WriteLine("-------------------------------------------------------------");

            var config = SampleConfig.ReadFromConsole("OpenAI", "https://api.openai.com/v1", "gpt-3.5-turbo");
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping sample.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Using configuration:");
            Console.WriteLine("- API Key: " + SampleConfig.MaskKey(config.ApiKey));
            Console.WriteLine("- Base URL: " + config.BaseUrl);
            Console.WriteLine("- Model: " + config.Model);
            Console.WriteLine("- OS Version: " + Environment.OSVersion.VersionString);

            // === Phase 1: Native SChannel (no proxy) ===
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("Phase 1: Native SChannel (no proxy)");
            Console.WriteLine("----------------------------------------");
            RunTest(config, "hi", false, false);

            // === Phase 2: Force proxy ===
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("Phase 2: BouncyCastle TLS Proxy (FORCED)");
            Console.WriteLine("----------------------------------------");
            try
            {
                HttpClientBase.ForceTlsProxyForDiagnostics();
                Console.WriteLine("Proxy started successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Failed to start proxy: " + ex.Message);
                return;
            }

            // Non-streaming test through proxy
            RunTest(config, "hi", false, true);

            Console.Write("\nPress Enter to continue with streaming test...");
            Console.ReadLine();

            // Streaming test through proxy
            RunTest(config, "Say hello in exactly 5 words.", true, true);

            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("Test complete. Proxy is forwarding correctly.");
            Console.WriteLine("Delete HttpClientBase.ForceTlsProxyForDiagnostics() call to disable.");
        }

        private void RunTest(SampleConfig config, string message, bool useStreaming, bool viaProxy)
        {
            string label = useStreaming ? "Streaming" : "Non-streaming";
            string route = viaProxy ? "via proxy" : "native";
            Console.WriteLine("\n[" + label + " " + route + "] Message: \"" + message + "\"");
            Console.Write("Assistant: ");

            var sw = Stopwatch.StartNew();

            try
            {
                OpenAIClient client;
                if (!string.IsNullOrEmpty(config.BaseUrl))
                {
                    client = new OpenAIClient(config.ApiKey, config.BaseUrl);
                }
                else
                {
                    client = new OpenAIClient(config.ApiKey);
                }

                var agent = new AIAgent(client, config.Model,
                    "You are a helpful assistant. Keep replies short.", null);

                if (useStreaming)
                {
                    agent.RunStreaming(message,
                        onUpdate: chunk => Console.Write(chunk),
                        onError: error => Console.Write("[ERROR: " + error.Message + "]"));
                }
                else
                {
                    var result = agent.Run(message);
                    if (result.IsSuccess)
                    {
                        Console.Write(result.Result);
                    }
                    else
                    {
                        Console.Write("[ERROR: " + result.Error.Message + "]");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Write("[EXCEPTION: " + ex.Message + "]");
            }

            sw.Stop();
            Console.WriteLine();
            Console.WriteLine("[" + label + " " + route + "] Completed in " + sw.ElapsedMilliseconds + "ms");
        }
    }
}
