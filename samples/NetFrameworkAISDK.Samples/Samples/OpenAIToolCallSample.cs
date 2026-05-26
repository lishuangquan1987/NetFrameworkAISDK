using NetFrameworkAISDK.OpenAI;
using NetFrameworkAISDK.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace NetFrameworkAISDK.Samples
{
    public class OpenAIToolCallSample : ISample
    {
        private static int _searchCallCount = 0;

        public string Name
        {
            get { return "OpenAI - Tool Calling (Full Demo)"; }
        }

        public void Run()
        {
            Console.WriteLine("\n============================================================");
            Console.WriteLine("  OpenAI Tool Calling - Full Feature Demo");
            Console.WriteLine("============================================================");

            var config = SampleConfig.ReadFromConsole("OpenAI", "https://api.openai.com/v1", "gpt-3.5-turbo");
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping sample.");
                return;
            }

            Console.WriteLine("\nUsing configuration:");
            Console.WriteLine("- API Key: " + SampleConfig.MaskKey(config.ApiKey));
            Console.WriteLine("- Base URL: " + config.BaseUrl);
            Console.WriteLine("- Model: " + config.Model);

            OpenAIClient client;
            if (!string.IsNullOrEmpty(config.BaseUrl))
            {
                client = new OpenAIClient(config.ApiKey, config.BaseUrl);
            }
            else
            {
                client = new OpenAIClient(config.ApiKey);
            }

            while (true)
            {
                Console.WriteLine("\n------------------------------------------------------------");
                Console.WriteLine("Select a demo scenario:");
                Console.WriteLine("1. Basic Tool Call (weather + time)");
                Console.WriteLine("2. Multi-Turn Chain (search → calculate)");
                Console.WriteLine("3. Tool Approval (require user confirmation)");
                Console.WriteLine("4. Structured Output (RunStructured<T>)");
                Console.WriteLine("5. Streaming with Tool Calls");
                Console.WriteLine("6. Interactive Chat (all tools)");
                Console.WriteLine("0. Return to main menu");
                Console.Write("Enter your choice: ");

                var choice = Console.ReadLine();
                Console.WriteLine();

                switch (choice)
                {
                    case "1":
                        DemoBasicToolCall(client, config.Model);
                        break;
                    case "2":
                        DemoMultiTurnChain(client, config.Model);
                        break;
                    case "3":
                        DemoToolApproval(client, config.Model);
                        break;
                    case "4":
                        DemoStructuredOutput(client, config.Model);
                        break;
                    case "5":
                        DemoStreamingWithTools(client, config.Model);
                        break;
                    case "6":
                        DemoInteractiveChat(client, config.Model);
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Invalid choice.");
                        break;
                }
            }
        }

        // ──────────────────────────────────────────────
        // Demo 1: Basic Tool Calling
        // ──────────────────────────────────────────────
        private void DemoBasicToolCall(OpenAIClient client, string model)
        {
            Console.WriteLine("=== Demo 1: Basic Tool Call ===\n");

            var tools = new[]
            {
                AIFunctionFactory.Create(new Func<string, string>(GetWeather)),
                AIFunctionFactory.Create(new Func<string>(GetCurrentTime)),
                AIFunctionFactory.Create(new Func<string, string>(GetStockPrice))
            };

            var agent = new AIAgent(client, model,
                "You are a helpful assistant. Use tools to get weather, time, or stock information.",
                tools);

            Console.WriteLine("Registered tools:");
            foreach (var t in tools)
            {
                Console.WriteLine("  - " + t.Name + ": " + t.Description);
            }
            Console.WriteLine();

            // Test 1: Weather
            Console.WriteLine("--- Test 1: Weather query ---");
            Console.Write("User: What is the weather in Tokyo and Shanghai?\n");
            var response = agent.Run("What is the weather in Tokyo and Shanghai?",
                onToolCall: OnToolCall);
            PrintResponse(response);

            // Test 2: Stock
            Console.WriteLine("\n--- Test 2: Stock price ---");
            Console.Write("User: What's the current price of AAPL?\n");
            response = agent.Run("What's the current price of AAPL?",
                onToolCall: OnToolCall);
            PrintResponse(response);

            // Test 3: Time
            Console.WriteLine("\n--- Test 3: Current time ---");
            Console.Write("User: What time is it now?\n");
            response = agent.Run("What time is it now?",
                onToolCall: OnToolCall);
            PrintResponse(response);

            Console.WriteLine("\n\nConversation history (" + agent.GetHistory().Count + " messages):");
            PrintHistory(agent);
        }

        // ──────────────────────────────────────────────
        // Demo 2: Multi-Turn Tool Chain
        // ──────────────────────────────────────────────
        private void DemoMultiTurnChain(OpenAIClient client, string model)
        {
            Console.WriteLine("=== Demo 2: Multi-Turn Tool Chain ===\n");

            var tools = new[]
            {
                AIFunctionFactory.Create(new Func<string, string>(WebSearch)),
                AIFunctionFactory.Create(new Func<string, string>(Calculate)),
                AIFunctionFactory.Create(new Func<string>(GetCurrentTime))
            };

            var agent = new AIAgent(client, model,
                "You are a research assistant. Use WebSearch to find data, then Calculate to process it.",
                tools);

            Console.WriteLine("Registered tools:");
            foreach (var t in tools)
            {
                Console.WriteLine("  - " + t.Name + ": " + t.Description);
            }
            Console.WriteLine();

            _searchCallCount = 0;

            // Scenario: find population data and calculate
            Console.WriteLine("--- Scenario: Research + Calculate ---");
            Console.Write("User: Search for the population of Tokyo and Shanghai, then calculate the total.\n");
            var response = agent.Run(
                "Search for the population of Tokyo and Shanghai separately, then calculate their total.",
                onToolCall: OnToolCall);
            PrintResponse(response);

            Console.WriteLine("\n\nTool call count: " + _searchCallCount);
            PrintHistory(agent);
        }

        // ──────────────────────────────────────────────
        // Demo 3: Tool Approval
        // ──────────────────────────────────────────────
        private void DemoToolApproval(OpenAIClient client, string model)
        {
            Console.WriteLine("=== Demo 3: Tool Approval ===\n");

            var sendEmail = AIFunctionFactory.Create(
                new Func<string, string, string>(SendEmail));
            sendEmail.RequiresApproval = true;

            var getWeather = AIFunctionFactory.Create(
                new Func<string, string>(GetWeather));
            getWeather.RequiresApproval = false;

            var agent = new AIAgent(client, model,
                "You are a helpful assistant. Some actions require user approval.",
                new[] { sendEmail, getWeather });

            agent.ToolApproval = delegate (ToolCallEventArgs args)
            {
                Console.WriteLine();
                Console.WriteLine("╔══════════════════════════════════════╗");
                Console.WriteLine("║  APPROVAL REQUIRED                  ║");
                Console.WriteLine("║  Tool: " + args.FunctionName.PadRight(30) + "║");
                Console.WriteLine("║  Args: " + (args.FunctionArguments ?? "{}").PadRight(30) + "║");
                Console.WriteLine("╚══════════════════════════════════════╝");
                Console.Write("Approve? (y/n): ");
                var key = Console.ReadKey();
                Console.WriteLine();
                return key.KeyChar == 'y' || key.KeyChar == 'Y';
            };

            Console.WriteLine("Registered tools:");
            Console.WriteLine("  - SendEmail [REQUIRES APPROVAL]: " + sendEmail.Description);
            Console.WriteLine("  - GetWeather: " + getWeather.Description);
            Console.WriteLine();

            // Test 1: Send email (will ask for approval)
            Console.WriteLine("--- Test 1: Send email (approve) ---");
            Console.Write("User: Send an email to john@example.com saying 'Meeting at 3pm'.\n");
            Console.WriteLine("(Approve this one)");
            var response = agent.Run(
                "Send an email to john@example.com with the subject 'Meeting' and body 'Meeting at 3pm'.",
                onToolCall: e =>
                {
                    if (e.IsApproved.HasValue && !e.IsApproved.Value)
                    {
                        Console.WriteLine(">>> [REJECTED] " + e.FunctionName);
                    }
                    else
                    {
                        OnToolCall(e);
                    }
                });
            PrintResponse(response);

            // Test 2: Send email (reject)
            Console.WriteLine("\n--- Test 2: Send email (reject) ---");
            Console.Write("User: Send an email to admin@example.com with password reset.\n");
            Console.WriteLine("(Reject this one)");
            response = agent.Run(
                "Send an email to admin@example.com with the subject 'Reset' and body 'Please reset my password'.",
                onToolCall: e =>
                {
                    if (e.IsApproved.HasValue && !e.IsApproved.Value)
                    {
                        Console.WriteLine(">>> [REJECTED] " + e.FunctionName);
                    }
                    else
                    {
                        OnToolCall(e);
                    }
                });
            PrintResponse(response);

            // Test 3: Weather (no approval)
            Console.WriteLine("\n--- Test 3: Weather (no approval needed) ---");
            Console.Write("User: What is the weather in Paris?\n");
            response = agent.Run("What is the weather in Paris?",
                onToolCall: OnToolCall);
            PrintResponse(response);
        }

        // ──────────────────────────────────────────────
        // Demo 4: Structured Output
        // ──────────────────────────────────────────────
        private void DemoStructuredOutput(OpenAIClient client, string model)
        {
            Console.WriteLine("=== Demo 4: Structured Output (RunStructured<T>) ===\n");

            var agent = AIAgent.CreateMinimal(client, model,
                "You are a data extraction assistant. Extract weather information into structured JSON.");

            Console.WriteLine("Target type: WeatherReport");
            Console.WriteLine("Fields: City (string), Temperature (double), Condition (string), Humidity (int)");
            Console.WriteLine();

            var result = agent.RunStructured<WeatherReport>(
                "The weather in Tokyo is 22 degrees Celsius and sunny with 65% humidity.");

            if (result.IsSuccess)
            {
                Console.WriteLine("Structured output parsed successfully!\n");
                Console.WriteLine("  City:        " + result.Result.City);
                Console.WriteLine("  Temperature: " + result.Result.Temperature + "°C");
                Console.WriteLine("  Condition:   " + result.Result.Condition);
                Console.WriteLine("  Humidity:    " + result.Result.Humidity + "%");

                if (result.Metadata != null)
                {
                    Console.WriteLine("\nResponse metadata:");
                    Console.WriteLine("  Model:        " + (result.Metadata.Model ?? "(unknown)"));
                    Console.WriteLine("  FinishReason: " + (result.Metadata.FinishReason ?? "(unknown)"));
                }
            }
            else
            {
                Console.WriteLine("Error: " + result.Error.Message);
            }
        }

        // ──────────────────────────────────────────────
        // Demo 5: Streaming with Tool Calls
        // ──────────────────────────────────────────────
        private void DemoStreamingWithTools(OpenAIClient client, string model)
        {
            Console.WriteLine("=== Demo 5: Streaming with Tool Calls ===\n");

            var tools = new[]
            {
                AIFunctionFactory.Create(new Func<string, string>(GetWeather)),
                AIFunctionFactory.Create(new Func<string>(GetCurrentTime))
            };

            var agent = new AIAgent(client, model,
                "You are a helpful assistant.",
                tools);

            Console.WriteLine("Streaming response with automatic tool call handling:\n");
            Console.Write("Assistant: ");

            var resetEvent = new ManualResetEvent(false);
            bool hasError = false;

            agent.RunStreaming(
                "What is the weather in Beijing AND what time is it?",
                onUpdate: chunk => Console.Write(chunk),
                onError: error =>
                {
                    hasError = true;
                    Console.WriteLine("\nError: " + error.Message);
                    resetEvent.Set();
                },
                onToolCall: e =>
                {
                    Console.WriteLine();
                    Console.WriteLine(">>> [Tool: " + e.FunctionName + "] " + e.Result);
                    Console.Write("Assistant: ");
                });

            // Wait for streaming to complete
            if (!hasError)
            {
                resetEvent.WaitOne(30000);
            }

            Console.WriteLine("\n\nStreaming complete.");
        }

        // ──────────────────────────────────────────────
        // Demo 6: Interactive Chat
        // ──────────────────────────────────────────────
        private void DemoInteractiveChat(OpenAIClient client, string model)
        {
            Console.WriteLine("=== Demo 6: Interactive Chat (all tools) ===\n");

            var tools = new[]
            {
                AIFunctionFactory.Create(new Func<string, string>(GetWeather)),
                AIFunctionFactory.Create(new Func<string>(GetCurrentTime)),
                AIFunctionFactory.Create(new Func<string, string>(GetStockPrice)),
                AIFunctionFactory.Create(new Func<string, string>(Calculate)),
                AIFunctionFactory.Create(new Func<string, string>(WebSearch))
            };

            var agent = new AIAgent(client, model,
                "You are a versatile assistant with access to weather, time, stock, calculator, and web search tools.",
                tools);

            Console.WriteLine("Available tools:");
            foreach (var t in tools)
            {
                Console.WriteLine("  - " + t.Name + ": " + t.Description);
            }
            Console.WriteLine("\nType 'exit' to quit, 'history' to see conversation, 'clear' to reset.\n");

            while (true)
            {
                Console.Write("You: ");
                var input = Console.ReadLine();

                if (input == "exit") break;
                if (input == "history")
                {
                    PrintHistory(agent);
                    continue;
                }
                if (input == "clear")
                {
                    agent.ClearHistory();
                    Console.WriteLine("Conversation cleared.\n");
                    continue;
                }

                Console.Write("Assistant: ");
                var response = agent.Run(input, onToolCall: OnToolCall);
                if (response.IsSuccess)
                {
                    Console.WriteLine(response.Result);
                }
                else
                {
                    Console.WriteLine("Error: " + response.Error.Message);
                }
                Console.WriteLine();
            }
        }

        // ──────────────────────────────────────────────
        // Helper Methods
        // ──────────────────────────────────────────────
        private static void OnToolCall(ToolCallEventArgs e)
        {
            Console.WriteLine();
            Console.WriteLine(">>> Tool Call:");
            Console.WriteLine("    Name:   " + e.FunctionName);
            Console.WriteLine("    Args:   " + (e.FunctionArguments != null ? e.FunctionArguments : "{}"));
            Console.WriteLine("    Result: " + (e.Result != null ? e.Result : "(null)"));
            Console.WriteLine("    ID:     " + (e.ToolCallId != null ? e.ToolCallId : "(none)"));
        }

        private static void PrintResponse(ApiResponse<string> response)
        {
            if (response.IsSuccess)
            {
                Console.WriteLine("Assistant: " + response.Result);
                if (response.Metadata != null)
                {
                    if (!string.IsNullOrEmpty(response.Metadata.FinishReason))
                        Console.WriteLine("[finish: " + response.Metadata.FinishReason + "]");
                }
            }
            else
            {
                Console.WriteLine("Error: " + response.Error.Message);
            }
        }

        private static void PrintHistory(AIAgent agent)
        {
            var history = agent.GetHistory();
            Console.WriteLine("=== Conversation History (" + history.Count + " messages) ===");
            for (int i = 0; i < history.Count; i++)
            {
                var msg = history[i];
                Console.Write("[" + i + "] " + msg.Role);
                if (!string.IsNullOrEmpty(msg.Name))
                    Console.Write(" (" + msg.Name + ")");
                if (!string.IsNullOrEmpty(msg.ToolCallId))
                    Console.Write(" [id=" + msg.ToolCallId + "]");

                if (msg.Content != null)
                {
                    var preview = msg.Content.Length > 80
                        ? msg.Content.Substring(0, 80) + "..."
                        : msg.Content;
                    Console.Write(": " + preview);
                }

                if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    Console.Write(" → tool_calls: " + msg.ToolCalls.Count);
                    foreach (var tc in msg.ToolCalls)
                        Console.Write(" [" + tc.FunctionName + "]");
                }

                Console.WriteLine();
            }
            Console.WriteLine("===========================================\n");
        }

        // ──────────────────────────────────────────────
        // Tool Functions
        // ──────────────────────────────────────────────

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
                { "new york", "Partly cloudy, 20°C, Humidity: 55%" },
                { "paris", "Clear sky, 23°C, Humidity: 45%" },
                { "london", "Overcast, 15°C, Humidity: 80%" },
                { "sydney", "Sunny, 28°C, Humidity: 35%" }
            };

            string key = location.ToLower().Trim();
            if (weatherData.ContainsKey(key))
                return weatherData[key];

            // Simulate latency for demo realism
            return "Weather data for " + location + ": 21°C, Partly cloudy, Humidity: 50%";
        }

        [Description("Get the current date and time.")]
        static string GetCurrentTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
        }

        [Description("Get the current stock price for a given ticker symbol.")]
        static string GetStockPrice(
            [Description("Stock ticker symbol (e.g., AAPL, GOOGL)")]
            string ticker)
        {
            var prices = new Dictionary<string, string>
            {
                { "aapl", "AAPL: $192.35 (+1.2%)" },
                { "googl", "GOOGL: $175.80 (-0.5%)" },
                { "msft", "MSFT: $425.22 (+2.1%)" },
                { "tsla", "TSLA: $248.50 (-1.8%)" }
            };

            string key = ticker.ToLower().Trim();
            if (prices.ContainsKey(key))
                return prices[key];

            return ticker.ToUpper() + ": $" + new Random().Next(50, 500) + "." + new Random().Next(0, 99).ToString("D2");
        }

        [Description("Search the web for information on a given query. Returns relevant snippets.")]
        static string WebSearch(
            [Description("The search query")]
            string query)
        {
            _searchCallCount++;

            var knowledgeBase = new Dictionary<string, string>
            {
                { "tokyo population", "Tokyo, Japan has a population of approximately 14 million in the city proper, and 37 million in the greater metropolitan area (2024)." },
                { "shanghai population", "Shanghai, China has a population of approximately 24.9 million in the municipality (2024)." },
                { "beijing population", "Beijing, China has a population of approximately 21.5 million in the municipality (2024)." },
                { "earth circumference", "The circumference of Earth at the equator is approximately 40,075 km (24,901 miles)." }
            };

            string key = query.ToLower().Trim();
            foreach (var kvp in knowledgeBase)
            {
                if (key.Contains(kvp.Key) || kvp.Key.Contains(key))
                    return kvp.Value;
            }

            return "Search results for '" + query + "': Found relevant information. (call #" + _searchCallCount + ")";
        }

        [Description("Perform a mathematical calculation. Supports: add, subtract, multiply, divide, power.")]
        static string Calculate(
            [Description("Expression to calculate (e.g., '14000000 + 24900000' or '5 * 3')")]
            string expression)
        {
            try
            {
                expression = expression.Replace(" ", "");
                expression = expression.Replace("x", "*");

                // Simple expression evaluator for + - * /
                double result = EvaluateExpression(expression);
                return expression + " = " + result;
            }
            catch (Exception ex)
            {
                return "Error calculating '" + expression + "': " + ex.Message;
            }
        }

        [Description("Send an email to a specified recipient.")]
        static string SendEmail(
            [Description("Email recipient address")]
            string to,
            [Description("Email subject and body (format: 'Subject|Body')")]
            string content)
        {
            var parts = content.Split('|');
            var subject = parts.Length > 0 ? parts[0] : "(no subject)";
            var body = parts.Length > 1 ? parts[1] : content;

            return "Email sent successfully!\n  To: " + to + "\n  Subject: " + subject + "\n  Body: " + body;
        }

        private static double EvaluateExpression(string expr)
        {
            // Simple left-to-right evaluator for + and -
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

    // ──────────────────────────────────────────────
    // Structured Output Types
    // ──────────────────────────────────────────────

    public class WeatherReport
    {
        public string City { get; set; }
        public double Temperature { get; set; }
        public string Condition { get; set; }
        public int Humidity { get; set; }
    }
}
