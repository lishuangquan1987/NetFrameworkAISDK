using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using NetFrameworkAISDK.Anthropic;
using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Samples
{
    public class McpToolSample : ISample
    {
        public string Name
        {
            get { return "MCP Tool Calling — SQLite3 + CodeGraph"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample connects to two MCP servers and discovers their tools.");
            Console.WriteLine("------------------------------------------------------------------------");

            // MCP Server 1: mcp_sqlite3 (database tools)
            var sqliteFunctions = TestMcpServer(
                "mcp_sqlite3",
                "python",
                "-m mcp_sqlite3 \"E:\\Yofc\\Code\\OTDR\\OTDR3001\\YOFC.OTDR3001\\YOFC.OTDR3001\\bin\\Debug\\netcoreapp3.1-windows\\win-x64\\TestData\\otdr_data.db\"",
                runAgentTest: true);

            Console.WriteLine("\n");

            // MCP Server 2: codegraph (code intelligence tools)
            TestMcpServer(
                "codegraph",
                "node",
                "\"C:\\Users\\5001494\\AppData\\Roaming\\npm\\node_modules\\@colbymchenry\\codegraph\\npm-shim.js\" serve --mcp -p \"E:\\Project2026\\NetFramework-AI-SDK\"",
                runAgentTest: false);
        }

        private List<AIFunction> TestMcpServer(string label, string serverPath, string arguments, bool runAgentTest)
        {
            Console.WriteLine("\n============================================================");
            Console.WriteLine("  MCP Server: " + label);
            Console.WriteLine("  Command: " + serverPath + " " + arguments);
            Console.WriteLine("============================================================\n");

            McpClient mcpClient = new McpClient();
            Console.Write("\nConnecting to MCP server... ");
            var connectResult = mcpClient.Connect(serverPath, arguments);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine("FAIL: " + connectResult.Error.Message);
                return null;
            }
            Console.WriteLine("Connected and initialized.");

            Console.WriteLine("\nStep 2: Discover MCP Tools (ListAsAIFunctions)");
            Console.WriteLine("----------------------------------------------------------------");
            var functionsResult = mcpClient.ListAsAIFunctions();
            if (!functionsResult.IsSuccess)
            {
                Console.WriteLine("Error: " + functionsResult.Error.Message);
                mcpClient.Dispose();
                return null;
            }

            var mcpFunctions = functionsResult.Result;
            if (mcpFunctions == null || mcpFunctions.Count == 0)
            {
                Console.WriteLine("No tools discovered.");
                mcpClient.Dispose();
                return null;
            }

            Console.WriteLine("Discovered " + mcpFunctions.Count + " tool(s):");
            foreach (var f in mcpFunctions)
            {
                Console.WriteLine("  - " + f.Name + ": " + (f.Description != null ? f.Description.Substring(0, Math.Min(f.Description.Length, 80)) : "(no description)"));
            }

            // AIAgent 交互测试
            if (runAgentTest)
            {
                Console.WriteLine("\nStep 4: AIAgent with MCP Tools (auto-test)");
                Console.WriteLine("----------------------------------------------------------------");
                RunAIAgentAutoTest(mcpFunctions);

                // 重置 mcpClient 以继续使用
                Console.WriteLine("\nResetting MCP connection...");
                mcpClient.Dispose();
                return mcpFunctions;
            }

            Console.WriteLine("\nStep 3: Cleanup");
            Console.WriteLine("----------------------------------------------------------------");
            mcpClient.Dispose();
            Console.WriteLine("MCP server disconnected.");
            return mcpFunctions;
        }

        private void RunAIAgentAutoTest(List<AIFunction> mcpFunctions)
        {
            const string url = "https://u701357-b42c-d29bc5d1.westc.seetacloud.com:8443/v1";
            const string model = "Qwen3.6-35B-A3B-FP8";

            Console.WriteLine("Creating OpenAI client...");
            var client = new OpenAIClient("111", url);

            string toolNames = "";
            foreach (var f in mcpFunctions) toolNames += (toolNames.Length > 0 ? ", " : "") + f.Name;

            var agent = new AIAgent(client, model,
                "You are a database assistant. Use the MCP tools to answer questions.", mcpFunctions);
            agent.MaxIterations = 5;

            Console.WriteLine("Agent ready with tools: " + toolNames);
            Console.WriteLine("\n--- Auto-test: ask AI to list tables ---");
            Console.WriteLine("User: 枚举数据库中的表，看看有多少个");
            Console.Write("Assistant: ");

            agent.RunStreaming(
                "请先用 connect_database 连接 E:\\Yofc\\Code\\OTDR\\OTDR3001\\YOFC.OTDR3001\\YOFC.OTDR3001\\bin\\Debug\\netcoreapp3.1-windows\\win-x64\\TestData\\otdr_data.db，然后用 list_tables 列出所有表，最后告诉我一共有多少个表。",
                onUpdate: chunk => Console.Write(chunk),
                onError: err => Console.Write("[ERROR: " + err.Message + "]"),
                onToolCall: e => Console.Write("\n  [Tool: " + e.FunctionName + "(" + e.FunctionArguments + ")] "));
            Console.WriteLine();
        }

        private void RunOpenAIAgentWithMcp(List<AIFunction> mcpFunctions)
        {
            Console.WriteLine("\nOpenAI configuration for MCP-powered Agent:");
            var config = SampleConfig.ReadFromConsole("OpenAI", "https://api.openai.com/v1", "gpt-3.5-turbo");
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping OpenAI test.");
                return;
            }

            OpenAIClient client;
            if (!string.IsNullOrEmpty(config.BaseUrl))
            {
                client = new OpenAIClient(config.ApiKey, config.BaseUrl);
            }
            else
            {
                client = new OpenAIClient(config.ApiKey);
            }

            string toolNames = "";
            foreach (var f in mcpFunctions)
            {
                if (toolNames.Length > 0)
                {
                    toolNames = toolNames + ", ";
                }
                toolNames = toolNames + f.Name;
            }

            var agent = new Common.AIAgent(
                client,
                config.Model,
                instructions: "You are a helpful assistant. You have MCP tools available: " + toolNames + ". Use them when needed.",
                tools: mcpFunctions
            );

            Console.WriteLine("\nMCP tools injected into OpenAI AIAgent: " + toolNames);
            Console.WriteLine("\nEnter your message (type 'exit' to quit):");
            Console.Write("You: ");
            string userInput = Console.ReadLine();

            while (userInput != "exit")
            {
                Console.Write("\nAssistant: ");

                var response = agent.Run(userInput, onToolCall: (e) =>
                {
                    Console.WriteLine("\n>>> MCP Tool: " + e.FunctionName);
                    Console.WriteLine(">>> Arguments: " + e.FunctionArguments);
                    Console.WriteLine(">>> Result: " + e.Result);
                });

                if (response.IsSuccess)
                {
                    Console.WriteLine(response.Result);
                }
                else
                {
                    Console.WriteLine("Error: " + response.Error.Message);
                }

                Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                Console.Write("You: ");
                userInput = Console.ReadLine();
            }
        }

        private void RunAnthropicWithMcp(List<AIFunction> mcpFunctions)
        {
            Console.WriteLine("\nAnthropic configuration for MCP-powered Agent:");
            var config = SampleConfig.ReadFromConsole("Anthropic", "https://api.anthropic.com/v1",
                "claude-3-haiku-20240307");
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping Anthropic test.");
                return;
            }

            AnthropicClient client;
            if (!string.IsNullOrEmpty(config.BaseUrl))
            {
                client = new AnthropicClient(config.ApiKey, config.BaseUrl);
            }
            else
            {
                client = new AnthropicClient(config.ApiKey);
            }

            string toolNames = "";
            foreach (var f in mcpFunctions)
            {
                if (toolNames.Length > 0)
                {
                    toolNames = toolNames + ", ";
                }
                toolNames = toolNames + f.Name;
            }

            var agent = new Common.AIAgent(
                client,
                config.Model,
                instructions: "You are a helpful assistant. You have MCP tools available: " + toolNames + ". Use them when needed.",
                tools: mcpFunctions
            );

            Console.WriteLine("\nMCP tools injected into Anthropic AIAgent: " + toolNames);
            Console.WriteLine("\nEnter your message (type 'exit' to quit):");
            Console.Write("You: ");
            string userInput = Console.ReadLine();

            while (userInput != "exit")
            {
                Console.Write("\nAssistant: ");

                var response = agent.Run(userInput, onToolCall: (e) =>
                {
                    Console.WriteLine("\n>>> MCP Tool: " + e.FunctionName);
                    Console.WriteLine(">>> Arguments: " + e.FunctionArguments);
                    Console.WriteLine(">>> Result: " + e.Result);
                });

                if (response.IsSuccess)
                {
                    Console.WriteLine(response.Result);
                }
                else
                {
                    Console.WriteLine("Error: " + response.Error.Message);
                }

                Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                Console.Write("You: ");
                userInput = Console.ReadLine();
            }
        }
    }
}