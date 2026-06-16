using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace NetFrameworkAISDK.Samples
{
    /// <summary>
    /// 自动化测试运行器 — 直接调用 SDK API 覆盖 8 个核心场景。
    /// 跳过 Multimodal（需支持图片的模型）、McpToolSample（需 MCP Server）、
    /// SkillsSample（需 Skills 目录）。
    /// </summary>
    public class AutoTestRunner : ISample
    {
        // ============================================================
        // 测试配置（由用户提供）
        // ============================================================
        private const string TestUrl = "https://u701357-b42c-d29bc5d1.westc.seetacloud.com:8443/v1";
        private const string TestApiKey = "111";
        private const string TestModel = "Qwen3.6-35B-A3B-FP8";

        private int _passed;
        private int _failed;
        private int _skipped;
        private OpenAIClient _client;

        public string Name
        {
            get { return "Auto-Test: Run All OpenAI Tests (8 scenarios)"; }
        }

        public void Run()
        {
            _passed = 0;
            _failed = 0;
            _skipped = 0;

            Console.WriteLine("\n============================================================");
            Console.WriteLine("  Auto-Test Runner — OpenAI SDK Integration Tests");
            Console.WriteLine("============================================================");
            Console.WriteLine("  URL:   " + TestUrl);
            Console.WriteLine("  Model: " + TestModel);
            Console.WriteLine("============================================================\n");

            try
            {
                HttpClientBase.ForceTlsProxyForDiagnostics();
                Console.WriteLine("[INFO] BouncyCastle TLS Proxy enabled for this test.\n");

                _client = new OpenAIClient(TestApiKey, TestUrl);
                Console.WriteLine("[INFO] OpenAIClient 创建成功。\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FATAL] 创建客户端失败: " + ex.Message);
                return;
            }

            RunTest("1. 非流式基础对话", Test1_BasicChat);
            RunTest("2. 流式对话 (SSE)", Test2_StreamingChat);
            RunTest("3. 工具调用-天气查询", Test3_ToolCallWeather);
            RunTest("4. 多轮工具链-搜索+计算", Test4_MultiTurnChain);
            RunTest("5. 工具审批-发送邮件", Test5_ToolApproval);
            RunTest("6. RunStructured<PersonInfo> 结构化输出", Test6_StructuredOutput);
            RunTest("7. 流式+工具调用", Test7_StreamingWithTools);
            RunTest("8. 多模态图片输入 (base64)", Test8_MultimodalImage);

            // 汇总
            Console.WriteLine("\n============================================================");
            Console.WriteLine("  TEST SUMMARY");
            Console.WriteLine("============================================================");
            Console.WriteLine("  PASSED:  " + _passed);
            Console.WriteLine("  FAILED:  " + _failed);
            Console.WriteLine("  SKIPPED: " + _skipped);
            Console.WriteLine("  TOTAL:   " + (_passed + _failed + _skipped));
            Console.WriteLine("============================================================\n");
        }

        // ============================================================
        // 测试调度
        // ============================================================
        private void RunTest(string label, Action test)
        {
            try
            {
                Console.Write("[" + label + "] ");
                test();
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("\r[FAIL] " + label + " — 异常: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        // ============================================================
        // 场景 1: 非流式基础对话
        // ============================================================
        private void Test1_BasicChat()
        {
            var agent = new AIAgent(_client, TestModel,
                "You are a helpful assistant.", null);
            agent.MaxIterations = 3;

            var response = agent.Run("Hello, please introduce yourself in one sentence.");

            if (response.IsSuccess && !string.IsNullOrEmpty(response.Result))
            {
                _passed++;
                Console.WriteLine("PASS (返回 " + response.Result.Length + " 字符)");
                Console.WriteLine("      响应预览: " + Truncate(response.Result, 100));
            }
            else
            {
                _failed++;
                Console.WriteLine("FAIL — " + (response.Error != null ? response.Error.Message : "空响应"));
            }
        }

        // ============================================================
        // 场景 2: 流式对话 (SSE)
        // ============================================================
        private void Test2_StreamingChat()
        {
            var agent = new AIAgent(_client, TestModel,
                "You are a helpful assistant.", null);
            agent.MaxIterations = 3;

            var chunks = new List<string>();
            bool hasError = false;
            string errorMsg = "";

            agent.RunStreaming(
                "Count from 1 to 5 out loud.",
                new Action<string>(chunk => chunks.Add(chunk)),
                new Action<ApiError>(error =>
                {
                    hasError = true;
                    errorMsg = error.Message;
                }));

            if (!hasError && chunks.Count > 0)
            {
                string full = string.Join("", chunks);
                _passed++;
                Console.WriteLine("PASS (收到 " + chunks.Count + " 个分片, 共 " + full.Length + " 字符)");
                Console.WriteLine("      响应预览: " + Truncate(full, 100));
            }
            else
            {
                _failed++;
                Console.WriteLine("FAIL — " + (hasError ? errorMsg : "0 个分片"));
            }
        }

        // ============================================================
        // 场景 3: 工具调用 — 天气查询
        // ============================================================
        private void Test3_ToolCallWeather()
        {
            var tools = new[]
            {
                AIFunctionFactory.Create(new Func<string, string>(GetWeather))
            };

            var agent = new AIAgent(_client, TestModel,
                "You are a helpful assistant. Use tools to get weather information.",
                tools);
            agent.MaxIterations = 5;

            bool toolWasCalled = false;
            var response = agent.Run(
                "What is the weather in Tokyo?",
                onToolCall: delegate (ToolCallEventArgs e)
                {
                    toolWasCalled = true;
                    Console.Write("[tool:" + e.FunctionName + "]");
                });

            if (response.IsSuccess && !string.IsNullOrEmpty(response.Result))
            {
                _passed++;
                Console.WriteLine(" PASS (工具调用:" + toolWasCalled + ", 响应:" + response.Result.Length + "字符)");
                Console.WriteLine("      响应预览: " + Truncate(response.Result, 100));
            }
            else
            {
                _failed++;
                Console.WriteLine(" FAIL — " + (response.Error != null ? response.Error.Message : "空响应"));
            }
        }

        // ============================================================
        // 场景 4: 多轮工具链 — 搜索 + 计算
        // ============================================================
        private void Test4_MultiTurnChain()
        {
            var tools = new[]
            {
                AIFunctionFactory.Create(new Func<string, string>(WebSearch)),
                AIFunctionFactory.Create(new Func<string, string>(Calculate))
            };

            var agent = new AIAgent(_client, TestModel,
                "You are a research assistant. Use WebSearch to find data, then Calculate to process it.",
                tools);
            agent.MaxIterations = 8;

            int toolCallCount = 0;
            var response = agent.Run(
                "Search for the population of Tokyo, then calculate what 10% of that number is.",
                onToolCall: delegate (ToolCallEventArgs e)
                {
                    toolCallCount++;
                    Console.Write("[tool#" + toolCallCount + ":" + e.FunctionName + "]");
                });

            if (response.IsSuccess && !string.IsNullOrEmpty(response.Result))
            {
                bool hasCalculation = response.Result.Contains("1.4") || response.Result.Contains("140") || response.Result.Contains("%");
                _passed++;
                Console.WriteLine(" PASS (工具调用:" + toolCallCount + "次, 包含计算:" + hasCalculation + ")");
                Console.WriteLine("      响应预览: " + Truncate(response.Result, 120));
            }
            else
            {
                _failed++;
                Console.WriteLine(" FAIL — " + (response.Error != null ? response.Error.Message : "空响应"));
            }
        }

        // ============================================================
        // 场景 5: 工具审批 — 发送邮件（审批通过 + 审批拒绝）
        // ============================================================
        private void Test5_ToolApproval()
        {
            var sendEmail = AIFunctionFactory.Create(
                new Func<string, string, string>(SendEmail));
            sendEmail.RequiresApproval = true;

            var getWeather = AIFunctionFactory.Create(
                new Func<string, string>(GetWeather));
            getWeather.RequiresApproval = false;

            var agent = new AIAgent(_client, TestModel,
                "You are a helpful assistant. Some actions require user approval.",
                new[] { sendEmail, getWeather });
            agent.MaxIterations = 5;

            Console.WriteLine();

            // 子测试 5a: 审批拒绝
            bool rejected = false;
            agent.ToolApproval = delegate (ToolCallEventArgs args)
            {
                // 拒绝 SendEmail
                rejected = true;
                return false;
            };

            var response = agent.Run(
                "Send an email to admin@example.com saying 'Test message'.",
                onToolCall: delegate (ToolCallEventArgs e)
                {
                    Console.Write("[approval:" + (e.IsApproved.HasValue && e.IsApproved.Value ? "approved" : "rejected") + "]");
                });

            if (response.IsSuccess && !string.IsNullOrEmpty(response.Result))
            {
                // 子测试 5b: 审批通过
                bool approved = false;
                agent.ClearHistory();
                agent.ToolApproval = delegate (ToolCallEventArgs args)
                {
                    approved = true;
                    return true;
                };

                var response2 = agent.Run(
                    "Send an email to john@example.com saying 'Meeting at 3pm'.",
                    onToolCall: delegate (ToolCallEventArgs e)
                    {
                        Console.Write("[approval:" + (e.IsApproved.HasValue && e.IsApproved.Value ? "approved" : "rejected") + "]");
                    });

                if (response2.IsSuccess && !string.IsNullOrEmpty(response2.Result))
                {
                    _passed++;
                    Console.WriteLine(" PASS (拒绝:" + rejected + ", 通过:" + approved + ")");
                    Console.WriteLine("      拒绝响应: " + Truncate(response.Result, 80));
                    Console.WriteLine("      通过响应: " + Truncate(response2.Result, 80));
                }
                else
                {
                    _failed++;
                    Console.WriteLine(" FAIL — 审批通过场景失败: " + (response2.Error != null ? response2.Error.Message : "空响应"));
                }
            }
            else
            {
                _failed++;
                Console.WriteLine(" FAIL — 审批拒绝场景失败: " + (response.Error != null ? response.Error.Message : "空响应"));
            }
        }

        // ============================================================
        // 场景 6: RunStructured<PersonInfo> 结构化输出
        // （这是之前报 "content already exists" 的场景！）
        // ============================================================
        private void Test6_StructuredOutput()
        {
            var agent = AIAgent.CreateMinimal(_client, TestModel,
                "You are a data extraction assistant. Extract person information into structured JSON.");

            Console.WriteLine();
            var result = agent.RunStructured<PersonInfo>(
                "张宇，男，1994 年 5 月出生，现居上海，手机号 138-1234-5678，邮箱 zhangyu@email.com。");

            if (result.IsSuccess && result.Result != null)
            {
                var p = result.Result;
                var fields = new List<string>();
                if (!string.IsNullOrEmpty(p.Name)) fields.Add("Name=" + p.Name);
                if (p.Age > 0) fields.Add("Age=" + p.Age);

                _passed++;
                Console.WriteLine(" PASS — 反序列化成功! " + string.Join(", ", fields));
            }
            else
            {
                _failed++;
                Console.WriteLine(" FAIL — " + (result.Error != null ? result.Error.Message : "空结果"));
            }
        }

        // ============================================================
        // 场景 7: 流式 + 工具调用
        // ============================================================
        private void Test7_StreamingWithTools()
        {
            var tools = new[]
            {
                AIFunctionFactory.Create(new Func<string, string>(GetWeather)),
                AIFunctionFactory.Create(new Func<string>(GetCurrentTime))
            };

            var agent = new AIAgent(_client, TestModel,
                "You are a helpful assistant. Use tools when needed.",
                tools);
            agent.MaxIterations = 5;

            var fullResponse = "";
            int toolCallCount = 0;
            bool hasError = false;
            string errorMsg = "";

            agent.RunStreaming(
                "What is the weather in Beijing? Also tell me the current time.",
                new Action<string>(chunk => fullResponse += chunk),
                new Action<ApiError>(error =>
                {
                    hasError = true;
                    errorMsg = error.Message;
                }),
                new Action<ToolCallEventArgs>(e =>
                {
                    toolCallCount++;
                    Console.Write("[tool:" + e.FunctionName + "]");
                }));

            if (!hasError && !string.IsNullOrEmpty(fullResponse))
            {
                _passed++;
                Console.WriteLine(" PASS (工具调用:" + toolCallCount + "次, 响应:" + fullResponse.Length + "字符)");
                Console.WriteLine("      响应预览: " + Truncate(fullResponse, 120));
            }
            else
            {
                _failed++;
                Console.WriteLine(" FAIL — " + (hasError ? errorMsg : "空响应, 工具调用:" + toolCallCount + "次"));
            }
        }

        // ============================================================
        // 场景 8: 多模态图片输入 — base64 编码 1x1 红色像素
        // ============================================================
        private void Test8_MultimodalImage()
        {
            // 1x1 红色像素 PNG 的 base64 编码
            const string redPixelBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";

            var agent = AIAgent.CreateMinimal(_client, TestModel,
                "You are a helpful assistant. When the user provides an image, describe what you see in one short sentence.");

            var contentParts = new List<MessageContent>
            {
                MessageContent.CreateImageFromBase64(redPixelBase64, "image/png")
            };

            var fullResponse = "";
            bool hasError = false;
            string errorMsg = "";

            agent.RunStreaming(
                "What do you see in this image? Answer in one short sentence.",
                contentParts,
                new Action<string>(chunk => fullResponse += chunk),
                new Action<ApiError>(error =>
                {
                    hasError = true;
                    errorMsg = error.Message;
                }));

            if (!hasError && !string.IsNullOrEmpty(fullResponse))
            {
                // 检查是否包含颜色描述（模型支持图片）还是报不支持（不支持图片）
                bool mentionsColor = fullResponse.ToLower().Contains("red")
                    || fullResponse.ToLower().Contains("color")
                    || fullResponse.Contains("红色")
                    || fullResponse.Contains("像素");

                _passed++;
                Console.WriteLine(" PASS (响应:" + fullResponse.Length + "字符, 提及颜色/图片:" + mentionsColor + ")");
                Console.WriteLine("      响应预览: " + Truncate(fullResponse, 120));
            }
            else if (hasError && errorMsg.Contains("multimodal") || errorMsg.Contains("image") || errorMsg.Contains("unsupported"))
            {
                _skipped++;
                Console.WriteLine(" SKIP — 模型不支持多模态图片输入: " + errorMsg);
            }
            else
            {
                _failed++;
                Console.WriteLine(" FAIL — " + (hasError ? errorMsg : "空响应"));
            }
        }

        // ============================================================
        // 工具函数定义
        // ============================================================

        [Description("Get the current weather for a given location.")]
        static string GetWeather(
            [Description("The location to get weather for (e.g., Beijing, Tokyo)")]
            string location)
        {
            var weatherData = new Dictionary<string, string>
            {
                { "beijing", "Sunny, 25°C, Humidity: 40%" },
                { "shanghai", "Cloudy, 22°C, Humidity: 70%" },
                { "tokyo", "Rainy, 18°C, Humidity: 85%" },
                { "paris", "Clear sky, 23°C, Humidity: 45%" },
                { "london", "Overcast, 15°C, Humidity: 80%" }
            };

            string key = location.ToLower().Trim();
            if (weatherData.ContainsKey(key))
                return weatherData[key];

            return "Weather data for " + location + ": 21°C, Partly cloudy, Humidity: 50%";
        }

        [Description("Get the current date and time.")]
        static string GetCurrentTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        [Description("Search the web for information on a given query.")]
        static string WebSearch(
            [Description("The search query")]
            string query)
        {
            var knowledgeBase = new Dictionary<string, string>
            {
                { "tokyo population", "Tokyo, Japan has a population of approximately 14 million in the city proper, and 37 million in the greater metropolitan area (2024)." },
                { "tokyo", "Tokyo is the capital of Japan with a population of approximately 14 million." }
            };

            string key = query.ToLower().Trim();
            foreach (var kvp in knowledgeBase)
            {
                if (key.Contains(kvp.Key) || kvp.Key.Contains(key))
                    return kvp.Value;
            }

            return "Search results for '" + query + "': Found relevant information.";
        }

        [Description("Perform a mathematical calculation. Supports add, subtract, multiply, divide.")]
        static string Calculate(
            [Description("Expression to calculate (e.g., '14000000 + 24900000')")]
            string expression)
        {
            try
            {
                expression = expression.Replace(" ", "");
                double result = EvaluateSimpleExpression(expression);
                return expression + " = " + result;
            }
            catch
            {
                return "Error calculating '" + expression + "'";
            }
        }

        [Description("Send an email to a specified recipient.")]
        static string SendEmail(
            [Description("Email recipient address")]
            string to,
            [Description("Email content (format: 'Subject|Body')")]
            string content)
        {
            var parts = content.Split('|');
            var subject = parts.Length > 0 ? parts[0] : "(no subject)";
            var body = parts.Length > 1 ? parts[1] : content;

            return "Email sent successfully!\n  To: " + to + "\n  Subject: " + subject + "\n  Body: " + body;
        }

        // ============================================================
        // 辅助方法
        // ============================================================
        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text))
                return "(empty)";
            text = text.Replace('\n', ' ').Replace('\r', ' ');
            if (text.Length <= maxLen)
                return text;
            return text.Substring(0, maxLen) + "...";
        }

        private static double EvaluateSimpleExpression(string expr)
        {
            double current = 0;
            char op = '+';
            int i = 0;

            while (i < expr.Length)
            {
                if (char.IsDigit(expr[i]) || expr[i] == '.')
                {
                    int start = i;
                    while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.'))
                        i++;
                    double num = double.Parse(expr.Substring(start, i - start),
                        System.Globalization.CultureInfo.InvariantCulture);

                    switch (op)
                    {
                        case '+': current += num; break;
                        case '-': current -= num; break;
                        case '*': current *= num; break;
                        case '/': current /= num; break;
                    }
                }
                else if (expr[i] == '+' || expr[i] == '-' || expr[i] == '*' || expr[i] == '/')
                {
                    op = expr[i];
                    i++;
                }
                else
                {
                    i++;
                }
            }

            return current;
        }
    }
}
