using NetFrameworkAISDK.OpenAI;
using NUnit.Framework;

namespace NetFrameworkAISDK.Tests.OpenAI
{
    [TestFixture]
    public class ChatMessageTests
    {
        [Test]
        public void ChatRole_Constants_HaveCorrectValues()
        {
            Assert.AreEqual("system", ChatRole.System);
            Assert.AreEqual("user", ChatRole.User);
            Assert.AreEqual("assistant", ChatRole.Assistant);
            Assert.AreEqual("tool", ChatRole.Tool);
        }

        [Test]
        public void ChatMessage_DefaultValues_AreCorrect()
        {
            var message = new ChatMessage();
            Assert.IsNull(message.Role);
            Assert.IsNull(message.Content);
            Assert.IsNull(message.Name);
            Assert.IsNull(message.ToolCallId);
        }

        [Test]
        public void ChatMessage_SetProperties_WorksCorrectly()
        {
            var message = new ChatMessage
            {
                Role = ChatRole.User,
                Content = "Hello",
                Name = "Test",
                ToolCallId = "call_123"
            };

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.AreEqual("Hello", message.Content);
            Assert.AreEqual("Test", message.Name);
            Assert.AreEqual("call_123", message.ToolCallId);
        }
    }
}
