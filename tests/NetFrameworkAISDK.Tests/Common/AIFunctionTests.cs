using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NetFrameworkAISDK.Tests.Common
{
    [TestFixture]
    public class AIFunctionTests
    {
        [Test]
        public void SystemDescriptionAttribute_Initializes_Correctly()
        {
            var attr = new System.ComponentModel.DescriptionAttribute("Test description");
            Assert.AreEqual("Test description", attr.Description);
        }

        [Test]
        public void AIFunction_Properties_SetAndGet_Correctly()
        {
            var func = new AIFunction
            {
                Name = "TestFunction",
                Description = "Test function",
                Parameters = new object(),
                Execute = (input) => "Output: " + input
            };

            Assert.AreEqual("TestFunction", func.Name);
            Assert.AreEqual("Test function", func.Description);
            Assert.NotNull(func.Parameters);
            Assert.AreEqual("Output: test", func.Execute("test"));
        }

        [Test]
        public void AIFunctionFactory_CreateFromDelegate_Works()
        {
            Func<string, string> testFunc = (input) => "Hello " + input;
            var aiFunc = AIFunctionFactory.Create(testFunc, "Test function");

            Assert.IsNotNull(aiFunc);
            Assert.AreEqual("testFunc", aiFunc.Name);
            Assert.AreEqual("Test function", aiFunc.Description);
            Assert.AreEqual("Hello World", aiFunc.Execute("{\"input\":\"World\"}"));
        }

        [Test]
        public void AIFunctionFactory_CreateFromNoArgDelegate_Works()
        {
            Func<string> testFunc = () => "Hello World";
            var aiFunc = AIFunctionFactory.Create(testFunc, "Test function");

            Assert.IsNotNull(aiFunc);
            Assert.AreEqual("testFunc", aiFunc.Name);
            Assert.AreEqual("Test function", aiFunc.Description);
            Assert.AreEqual("Hello World", aiFunc.Execute("{}"));
        }

        [Test]
        public void AIFunction_ToToolDefinition_ReturnsValidTool()
        {
            var aiFunc = new AIFunction
            {
                Name = "TestFunction",
                Description = "Test function",
                Parameters = new object()
            };

            var toolDef = aiFunc.ToToolDefinition();

            Assert.IsNotNull(toolDef);
            Assert.AreEqual("function", toolDef.Type);
            Assert.AreEqual("TestFunction", toolDef.Function.Name);
            Assert.AreEqual("Test function", toolDef.Function.Description);
            Assert.NotNull(toolDef.Function.Parameters);
        }

        [Test]
        public void AIFunctionFactory_CreateFromDelegate_MAFStyle_Works()
        {
            var aiFunc = AIFunctionFactory.Create(new Func<string, string>(GetWeatherMethod));

            Assert.IsNotNull(aiFunc);
            Assert.AreEqual("GetWeatherMethod", aiFunc.Name);
            Assert.AreEqual("Get weather for location", aiFunc.Description);
            Assert.AreEqual("Weather in Tokyo", aiFunc.Execute("{\"location\":\"Tokyo\"}"));
        }

        [System.ComponentModel.Description("Get weather for location")]
        private string GetWeatherMethod([System.ComponentModel.Description("The location")] string location)
        {
            return "Weather in " + location;
        }

        [Test]
        public void ConfigureTools_RaceCondition_DoesNotThrow()
        {
            var client = new TestableAIClient("key", "http://localhost");
            var tools = new List<AIFunction>
            {
                AIFunction.Create(new Func<string>(delegate() { return "a"; }), "Tool A", "a"),
                AIFunction.Create(new Func<string>(delegate() { return "b"; }), "Tool B", "b")
            };

            var exceptions = new List<Exception>();
            var thread1 = new Thread(delegate()
            {
                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        client.ConfigureTools(tools);
                    }
                }
                catch (Exception ex) { lock (exceptions) { exceptions.Add(ex); } }
            });
            var thread2 = new Thread(delegate()
            {
                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        client.ConfigureTools(tools);
                    }
                }
                catch (Exception ex) { lock (exceptions) { exceptions.Add(ex); } }
            });

            thread1.Start(); thread2.Start();
            thread1.Join(); thread2.Join();

            Assert.AreEqual(0, exceptions.Count,
                "No exceptions should be thrown");
        }

        // 测试辅助类：暴露 ConfigureTools 以测试线程安全
        private class TestableAIClient : AIClientBase
        {
            public TestableAIClient(string key, string url) : base(key, url) { }
            public override ApiResponse<ConversationResponse> SendConversation(
                List<ConversationMessage> m, ConversationOptions o) { return null; }
            public override void SendConversationStreaming(
                List<ConversationMessage> m, ConversationOptions o,
                Action<ConversationResponse> c, Action<ApiError> e) { }
        }
    }
}
