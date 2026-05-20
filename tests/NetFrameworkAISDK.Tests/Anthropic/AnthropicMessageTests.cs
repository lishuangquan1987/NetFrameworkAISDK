using NetFrameworkAISDK.Anthropic;
using NUnit.Framework;

namespace NetFrameworkAISDK.Tests.Anthropic
{
    [TestFixture]
    public class AnthropicMessageTests
    {
        [Test]
        public void AnthropicRole_Constants_HaveCorrectValues()
        {
            Assert.AreEqual("user", AnthropicRole.User);
            Assert.AreEqual("assistant", AnthropicRole.Assistant);
        }

        [Test]
        public void AnthropicMessage_DefaultValues_AreCorrect()
        {
            var message = new AnthropicMessage();
            Assert.IsNull(message.Role);
            Assert.IsNull(message.Content);
        }

        [Test]
        public void AnthropicMessage_SetProperties_WorksCorrectly()
        {
            var message = new AnthropicMessage
            {
                Role = AnthropicRole.User,
                Content = "Hello"
            };

            Assert.AreEqual(AnthropicRole.User, message.Role);
            Assert.AreEqual("Hello", message.Content);
        }

        [Test]
        public void ContentBlock_DefaultValues_AreCorrect()
        {
            var block = new ContentBlock();
            Assert.IsNull(block.Type);
            Assert.IsNull(block.Text);
        }

        [Test]
        public void ContentBlock_SetProperties_WorksCorrectly()
        {
            var block = new ContentBlock
            {
                Type = "text",
                Text = "Hello"
            };

            Assert.AreEqual("text", block.Type);
            Assert.AreEqual("Hello", block.Text);
        }
    }
}
