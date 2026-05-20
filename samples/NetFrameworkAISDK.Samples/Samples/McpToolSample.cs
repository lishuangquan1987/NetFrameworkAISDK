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
            get { return "MCP Tool Calling - Connect MCP Server + OpenAI/Anthropic"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates how to connect to an MCP server,");
            Console.WriteLine("discover tools, and inject them into OpenAI/Anthropic agents.");
            Console.WriteLine("------------------------------------------------------------------------");

            Console.WriteLine("\nStep 1: Configure MCP Server");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("Enter the path to the MCP server executable.");
            Console.WriteLine("  Example: node C:/path/to/mcp-server/index.js");
            Console.WriteLine("  Example: python C:/path/to/mcp-server/main.py");
            Console.WriteLine("  Example: C:/path/to/mcp-server.exe");
            Console.Write("MCP Server Path: ");
            string serverPath = Console.ReadLine();

            if (string.IsNullOrEmpty(serverPath))
            {
                Console.WriteLine("MCP Server path is required. Skipping sample.");
                return;
            }

            Console.Write("Arguments (optional, press Enter for none): ");
            string arguments = Console.ReadLine();

            McpClient mcpClient = new McpClient();
            Console.Write("\nConnecting to MCP server... ");

            var connectResult = mcpClient.Connect(serverPath, arguments);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine("\nError: " + connectResult.Error.Message);
                return;
            }
            Console.WriteLine("Connected.");

            Console.Write("Initializing... ");
            var initResult = mcpClient.Initialize();
            if (!initResult.IsSuccess)
            {
                Console.WriteLine("\nError: " + initResult.Error.Message);
                mcpClient.Dispose();
                return;
            }
            Console.WriteLine("Done.");

            Console.WriteLine("\nStep 2: Discover MCP Tools");
            Console.WriteLine("----------------------------------------------------------------");
            var toolsResult = mcpClient.ListTools();
            if (!toolsResult.IsSuccess)
            {
                Console.WriteLine("Error listing tools: " + toolsResult.Error.Message);
                mcpClient.Dispose();
                return;
            }

            var mcpTools = toolsResult.Result;
            if (mcpTools == null || mcpTools.Count == 0)
            {
                Console.WriteLine("No tools discovered from MCP server.");
                mcpClient.Dispose();
                return;
            }

            Console.WriteLine("Discovered " + mcpTools.Count + " tool(s):");
            var mcpFunctions = new List<AIFunction>();
            foreach (var tool in mcpTools)
            {
                Console.WriteLine("\n  - " + tool.Name + ": " + (tool.Description ?? "(no description)"));
                mcpFunctions.Add(AIFunction.CreateFromMcpTool(
                    tool.Name,
                    tool.Description,
                    tool.InputSchema,
                    new Func<string, string>(args =>
                    {
                        var result = mcpClient.CallTool(tool.Name, args);
                        if (result.IsSuccess)
                        {
                            return result.Result;
                        }
                        return "Error: " + result.Error.Message;
                    })
                ));
            }

            Console.WriteLine("\nStep 3: Choose AI Provider");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("1. Test with OpenAI (tools/list and tools/call only)");
            Console.WriteLine("2. Use with OpenAI AIAgent (interactive chat)");
            Console.WriteLine("3. Use with Anthropic (interactive chat)");
            Console.WriteLine("0. Skip (just show MCP tools)");
            Console.Write("\nYour choice: ");
            string providerChoice = Console.ReadLine();

            if (providerChoice == "1")
            {
                TestMcpToolsDirectly(mcpClient, mcpTools);
            }
            else if (providerChoice == "2")
            {
                RunOpenAIAgentWithMcp(mcpFunctions);
            }
            else if (providerChoice == "3")
            {
                RunAnthropicWithMcp(mcpFunctions);
            }
            else
            {
                Console.WriteLine("\nSkipping AI provider test.");
            }

            Console.WriteLine("\nStep 4: Cleanup");
            Console.WriteLine("----------------------------------------------------------------");
            mcpClient.Shutdown();
            mcpClient.Dispose();
            Console.WriteLine("MCP server disconnected.");
        }

        private void TestMcpToolsDirectly(McpClient mcpClient, List<McpToolInfo> mcpTools)
        {
            Console.WriteLine("\nDirect tool call test:");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("MCP tools are available. You can now test them by calling tools/call.");

            foreach (var tool in mcpTools)
            {
                Console.Write("\nCalling tool '" + tool.Name + "'... ");
                var result = mcpClient.CallTool(tool.Name, new Dictionary<string, object>());
                if (result.IsSuccess)
                {
                    Console.WriteLine("Result: " + result.Result);
                }
                else
                {
                    Console.WriteLine("Error: " + result.Error.Message);
                }
            }
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