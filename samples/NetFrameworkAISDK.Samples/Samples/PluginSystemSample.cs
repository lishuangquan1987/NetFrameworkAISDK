using System;
using System.Collections.Generic;
using System.Linq;
using NetFrameworkAISDK;
using NetFrameworkAISDK.Plugins;
using NetFrameworkAISDK.Plugins.Middleware;
using NetFrameworkAISDK.Plugins.Storage;
using NetFrameworkAISDK.Plugins.Tools;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Samples
{
    public class PluginSystemSample
    {
        public static void RunAllSamples()
        {
            Console.WriteLine("=== Plugin System Samples ===\n");

            PluginManagerSample();
            MiddlewarePipelineSample();
            StorageSample();
            ToolRegistrySample();
        }

        private static void PluginManagerSample()
        {
            Console.WriteLine("--- Plugin Manager Sample ---");

            var pluginManager = new PluginManager();

            var assembly = typeof(PluginSystemSample).Assembly;
            int loadedCount = pluginManager.LoadPluginsFromAssembly(assembly);

            Console.WriteLine("Loaded " + loadedCount + " plugins from assembly");

            foreach (var plugin in pluginManager.GetAllPlugins())
            {
                Console.WriteLine("  - " + plugin.Name + " v" + plugin.Version);
            }

            Console.WriteLine();
        }

        private static void MiddlewarePipelineSample()
        {
            Console.WriteLine("--- Middleware Pipeline Sample ---");

            var pipeline = new MiddlewarePipeline();

            pipeline.Use(new LoggingMiddleware(msg => Console.WriteLine("[LOG] " + msg), true, true, true))
                   .Use(new ExceptionHandlingMiddleware(null, 0, 1000))
                   .Use(new CachingMiddleware(30, 100))
                   .Use(new RateLimitingMiddleware(60, 1000))
                   .Use(new SecurityMiddleware(true, false, null, msg => Console.WriteLine("[SECURITY] " + msg)));

            Console.WriteLine("Pipeline: " + pipeline.GetPipelineDescription());
            Console.WriteLine();

            var context = new AgentContext
            {
                UserMessage = "Hello, this is a test message",
                ContentParts = null,
                Options = new ConversationOptions()
            };

            var result = pipeline.Execute(context, () =>
            {
                return new ApiResponse<string> { Result = "This is the response from the agent" };
            });

            Console.WriteLine("Result: " + (result.IsSuccess ? result.Result : "Error: " + result.Error.Message));
            Console.WriteLine();
        }

        private static void StorageSample()
        {
            Console.WriteLine("--- Storage Sample ---");

            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PluginStorageTest");
            if (!System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.CreateDirectory(tempDir);
            }

            var fileStorage = new FileConversationStore(tempDir);

            var sessionId = "test-session-001";
            var messages = new List<ConversationMessage>
            {
                new ConversationMessage { Role = MessageRole.User, Content = "Hello!" },
                new ConversationMessage { Role = MessageRole.Assistant, Content = "Hi there!" }
            };

            fileStorage.SaveSession(sessionId, messages);
            Console.WriteLine("Saved session: " + sessionId);

            var loadedMessages = fileStorage.LoadSession(sessionId);
            Console.WriteLine("Loaded " + (loadedMessages != null ? loadedMessages.Count : 0) + " messages");

            fileStorage.SaveSnapshot(sessionId, "checkpoint-1", messages);
            Console.WriteLine("Saved snapshot: checkpoint-1");

            var snapshots = fileStorage.ListSnapshots(sessionId);
            Console.WriteLine("Found " + snapshots.Count() + " snapshots");

            var sessions = fileStorage.ListSessions();
            Console.WriteLine("Total sessions: " + sessions.Count());

            fileStorage.DeleteSession(sessionId);
            Console.WriteLine("Deleted session: " + sessionId);

            Console.WriteLine();
        }

        private static void ToolRegistrySample()
        {
            Console.WriteLine("--- Tool Registry Sample ---");

            var registry = new ToolRegistry();

            var tools = AgentTools.CreateDefaultTools();
            registry.RegisterRange(tools);

            Console.WriteLine("Registered " + registry.Count + " default tools");

            var readFileTool = registry.Get("ReadFile");
            if (readFileTool != null)
            {
                Console.WriteLine("  Found tool: " + readFileTool.Name + " - " + readFileTool.Description);
            }

            registry.SetPermission("ReadFile", new ToolPermission
            {
                ToolName = "ReadFile",
                Level = ToolPermissionLevel.Public,
                Description = "Read files from disk"
            });

            var permission = registry.GetPermission("ReadFile");
            Console.WriteLine("  Tool permission: " + permission.Level);

            Console.WriteLine();
        }
    }
}
