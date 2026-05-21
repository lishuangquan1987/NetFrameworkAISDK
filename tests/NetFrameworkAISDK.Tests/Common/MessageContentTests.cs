using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Tests.Common
{
    [TestFixture]
    public class MessageContentTests
    {
        [Test]
        public void CreateText_ReturnsTextContentBlock()
        {
            var content = MessageContent.CreateText("Hello World");

            Assert.IsNotNull(content);
            Assert.AreEqual(ContentType.Text, content.Type);
            Assert.AreEqual("Hello World", content.Text);
            Assert.IsNull(content.ImageUrl);
            Assert.IsNull(content.ImageBase64);
        }

        [Test]
        public void CreateText_WithEmptyString_ReturnsTextContentBlock()
        {
            var content = MessageContent.CreateText("");

            Assert.IsNotNull(content);
            Assert.AreEqual(ContentType.Text, content.Type);
            Assert.AreEqual("", content.Text);
        }

        [Test]
        public void CreateImageFromUrl_ReturnsImageContentBlock()
        {
            var content = MessageContent.CreateImageFromUrl("https://example.com/photo.jpg");

            Assert.IsNotNull(content);
            Assert.AreEqual(ContentType.Image, content.Type);
            Assert.AreEqual("https://example.com/photo.jpg", content.ImageUrl);
            Assert.IsNull(content.Text);
            Assert.IsNull(content.ImageBase64);
        }

        [Test]
        public void CreateImageFromUrl_WithDetail_ReturnsImageContentBlock()
        {
            var content = MessageContent.CreateImageFromUrl("https://example.com/photo.jpg", "high");

            Assert.IsNotNull(content);
            Assert.AreEqual(ContentType.Image, content.Type);
            Assert.AreEqual("https://example.com/photo.jpg", content.ImageUrl);
            Assert.AreEqual("high", content.Detail);
        }

        [Test]
        public void CreateImageFromBase64_ReturnsImageContentBlock()
        {
            var content = MessageContent.CreateImageFromBase64("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk");

            Assert.IsNotNull(content);
            Assert.AreEqual(ContentType.Image, content.Type);
            Assert.AreEqual("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk", content.ImageBase64);
            Assert.AreEqual("image/png", content.MediaType);
            Assert.IsNull(content.Text);
            Assert.IsNull(content.ImageUrl);
        }

        [Test]
        public void CreateImageFromBase64_WithCustomMediaType_ReturnsImageContentBlock()
        {
            var content = MessageContent.CreateImageFromBase64("base64data", "image/jpeg");

            Assert.IsNotNull(content);
            Assert.AreEqual(ContentType.Image, content.Type);
            Assert.AreEqual("image/jpeg", content.MediaType);
            Assert.AreEqual("base64data", content.ImageBase64);
        }

        [Test]
        public void MessageContent_PropertiesCanBeSetDirectly()
        {
            var content = new MessageContent
            {
                Type = ContentType.Image,
                ImageUrl = "https://example.com/photo.jpg",
                Detail = "auto",
                Text = "A photo"
            };

            Assert.AreEqual(ContentType.Image, content.Type);
            Assert.AreEqual("https://example.com/photo.jpg", content.ImageUrl);
            Assert.AreEqual("auto", content.Detail);
            Assert.AreEqual("A photo", content.Text);
        }

        [Test]
        public void CreateMixedContentBlocks_MultipleTypes()
        {
            var contentParts = new List<MessageContent>
            {
                MessageContent.CreateText("Look at this image:"),
                MessageContent.CreateImageFromUrl("https://example.com/photo.jpg", "high"),
                MessageContent.CreateText("And this one:"),
                MessageContent.CreateImageFromBase64("base64data", "image/jpeg")
            };

            Assert.AreEqual(4, contentParts.Count);
            Assert.AreEqual(ContentType.Text, contentParts[0].Type);
            Assert.AreEqual(ContentType.Image, contentParts[1].Type);
            Assert.AreEqual(ContentType.Text, contentParts[2].Type);
            Assert.AreEqual(ContentType.Image, contentParts[3].Type);
        }
    }
}