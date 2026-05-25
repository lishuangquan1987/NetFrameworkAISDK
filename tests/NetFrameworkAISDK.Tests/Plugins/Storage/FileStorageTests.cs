using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetFrameworkAISDK.Plugins;
using NetFrameworkAISDK.Plugins.Storage;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Tests.Plugins.Storage
{
    [TestClass]
    public class FileStorageTests
    {
        private string _testDirectory;
        private FileConversationStore _store;

        [TestInitialize]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "FileStorageTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _store = new FileConversationStore(_testDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_store != null)
            {
                _store.Dispose();
            }

            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public void TestSaveAndLoadSession()
        {
            var sessionId = "test-session-001";
            var messages = CreateTestMessages();

            _store.SaveSession(sessionId, messages);

            var loadedMessages = _store.LoadSession(sessionId);

            Assert.IsNotNull(loadedMessages);
            Assert.AreEqual(3, loadedMessages.Count);
            Assert.AreEqual("User message 1", loadedMessages[0].Content);
            Assert.AreEqual(MessageRole.User, loadedMessages[0].Role);
        }

        [TestMethod]
        public void TestLoadNonExistentSession()
        {
            var messages = _store.LoadSession("non-existent-session");

            Assert.IsNull(messages);
        }

        [TestMethod]
        public void TestDeleteSession()
        {
            var sessionId = "test-session-002";
            var messages = CreateTestMessages();

            _store.SaveSession(sessionId, messages);
            Assert.IsNotNull(_store.LoadSession(sessionId));

            _store.DeleteSession(sessionId);
            Assert.IsNull(_store.LoadSession(sessionId));
        }

        [TestMethod]
        public void TestListSessions()
        {
            _store.SaveSession("session-1", CreateTestMessages());
            _store.SaveSession("session-2", CreateTestMessages());

            var sessions = _store.ListSessions().ToList();

            Assert.AreEqual(2, sessions.Count);
        }

        [TestMethod]
        public void TestSaveAndLoadSnapshot()
        {
            var sessionId = "test-session-003";
            var messages = CreateTestMessages();

            _store.SaveSnapshot(sessionId, "checkpoint-1", messages);

            var loadedSnapshot = _store.LoadSnapshot(sessionId, "checkpoint-1");

            Assert.IsNotNull(loadedSnapshot);
            Assert.AreEqual(3, loadedSnapshot.Count);
        }

        [TestMethod]
        public void TestListSnapshots()
        {
            var sessionId = "test-session-004";
            var messages = CreateTestMessages();

            _store.SaveSnapshot(sessionId, "snapshot-1", messages);
            _store.SaveSnapshot(sessionId, "snapshot-2", messages);

            var snapshots = _store.ListSnapshots(sessionId).ToList();

            Assert.AreEqual(2, snapshots.Count);
        }

        [TestMethod]
        public void TestDeleteSnapshot()
        {
            var sessionId = "test-session-005";
            var messages = CreateTestMessages();

            _store.SaveSnapshot(sessionId, "snapshot-to-delete", messages);
            Assert.IsNotNull(_store.LoadSnapshot(sessionId, "snapshot-to-delete"));

            _store.DeleteSnapshot(sessionId, "snapshot-to-delete");
            Assert.IsNull(_store.LoadSnapshot(sessionId, "snapshot-to-delete"));
        }

        [TestMethod]
        public void TestUpdateSessionMetadata()
        {
            var sessionId = "test-session-006";
            var messages = CreateTestMessages();

            _store.SaveSession(sessionId, messages);

            var metadata = new SessionMetadata
            {
                Name = "Test Session",
                Description = "This is a test session",
                Tags = new List<string> { "test", "unit-test" }
            };

            _store.UpdateSessionMetadata(sessionId, metadata);

            var sessions = _store.ListSessions().ToList();
            Assert.AreEqual(1, sessions.Count);
            Assert.IsNotNull(sessions[0].Metadata);
        }

        [TestMethod]
        public void TestOverwriteSession()
        {
            var sessionId = "test-session-007";

            _store.SaveSession(sessionId, CreateTestMessages());
            Assert.AreEqual(3, _store.LoadSession(sessionId).Count);

            var newMessages = new List<ConversationMessage>
            {
                new ConversationMessage { Role = MessageRole.User, Content = "New message" }
            };

            _store.SaveSession(sessionId, newMessages);
            Assert.AreEqual(1, _store.LoadSession(sessionId).Count);
        }

        [TestMethod]
        public void TestEmptySession()
        {
            var sessionId = "test-session-empty";
            var emptyMessages = new List<ConversationMessage>();

            _store.SaveSession(sessionId, emptyMessages);
            var loadedMessages = _store.LoadSession(sessionId);

            Assert.IsNotNull(loadedMessages);
            Assert.AreEqual(0, loadedMessages.Count);
        }

        [TestMethod]
        public void TestNullSession()
        {
            var sessionId = "test-session-null";
            _store.SaveSession(sessionId, null);
            var loadedMessages = _store.LoadSession(sessionId);

            Assert.IsNotNull(loadedMessages);
            Assert.AreEqual(0, loadedMessages.Count);
        }

        private static List<ConversationMessage> CreateTestMessages()
        {
            return new List<ConversationMessage>
            {
                new ConversationMessage { Role = MessageRole.User, Content = "User message 1" },
                new ConversationMessage { Role = MessageRole.Assistant, Content = "Assistant message 1" },
                new ConversationMessage { Role = MessageRole.User, Content = "User message 2" }
            };
        }
    }
}
