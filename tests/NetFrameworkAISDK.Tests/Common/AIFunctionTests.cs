using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System;

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
    }
}
