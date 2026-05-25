using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetFrameworkAISDK.Plugins;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Tests.Plugins
{
    [TestClass]
    public class ToolRegistryTests
    {
        private ToolRegistry _registry;

        [TestInitialize]
        public void Setup()
        {
            _registry = new ToolRegistry();
        }

        [TestMethod]
        public void TestRegisterTool()
        {
            var tool = CreateTestTool("test-tool", "Test tool description");

            _registry.Register(tool);

            Assert.IsTrue(_registry.Exists("test-tool"));
            Assert.AreEqual(1, _registry.Count);
        }

        [TestMethod]
        public void TestRegisterMultipleTools()
        {
            var tools = new List<AIFunction>
            {
                CreateTestTool("tool-1", "Tool 1"),
                CreateTestTool("tool-2", "Tool 2"),
                CreateTestTool("tool-3", "Tool 3")
            };

            _registry.RegisterRange(tools);

            Assert.AreEqual(3, _registry.Count);
            Assert.IsTrue(_registry.Exists("tool-1"));
            Assert.IsTrue(_registry.Exists("tool-2"));
            Assert.IsTrue(_registry.Exists("tool-3"));
        }

        [TestMethod]
        public void TestGetTool()
        {
            var tool = CreateTestTool("test-tool", "Test description");

            _registry.Register(tool);

            var retrieved = _registry.Get("test-tool");

            Assert.IsNotNull(retrieved);
            Assert.AreEqual("test-tool", retrieved.Name);
            Assert.AreEqual("Test description", retrieved.Description);
        }

        [TestMethod]
        public void TestGetNonExistentTool()
        {
            var tool = _registry.Get("non-existent");

            Assert.IsNull(tool);
        }

        [TestMethod]
        public void TestUnregisterTool()
        {
            var tool = CreateTestTool("test-tool", "Test");

            _registry.Register(tool);
            Assert.IsTrue(_registry.Exists("test-tool"));

            _registry.Unregister("test-tool");
            Assert.IsFalse(_registry.Exists("test-tool"));
            Assert.AreEqual(0, _registry.Count);
        }

        [TestMethod]
        public void TestToolPermission()
        {
            var tool = CreateTestTool("sensitive-tool", "Sensitive tool");

            _registry.Register(tool);

            var permission = new ToolPermission
            {
                ToolName = "sensitive-tool",
                Level = ToolPermissionLevel.RequiresApproval,
                Description = "Requires user approval"
            };

            _registry.SetPermission("sensitive-tool", permission);

            var retrievedPermission = _registry.GetPermission("sensitive-tool");

            Assert.IsNotNull(retrievedPermission);
            Assert.AreEqual(ToolPermissionLevel.RequiresApproval, retrievedPermission.Level);
            Assert.AreEqual("Requires user approval", retrievedPermission.Description);
        }

        [TestMethod]
        public void TestGetAllTools()
        {
            var tools = new List<AIFunction>
            {
                CreateTestTool("tool-1", "Tool 1"),
                CreateTestTool("tool-2", "Tool 2")
            };

            _registry.RegisterRange(tools);

            var allTools = _registry.GetAll().ToList();

            Assert.AreEqual(2, allTools.Count);
        }

        [TestMethod]
        public void TestDuplicateToolRegistration()
        {
            var tool1 = CreateTestTool("test-tool", "Tool 1");
            var tool2 = CreateTestTool("test-tool", "Tool 2");

            _registry.Register(tool1);
            _registry.Register(tool2);

            Assert.AreEqual(1, _registry.Count);

            var retrieved = _registry.Get("test-tool");
            Assert.AreEqual("Tool 2", retrieved.Description);
        }

        [TestMethod]
        public void TestToolOverwrite()
        {
            var tool1 = CreateTestTool("test-tool", "Original");
            var tool2 = CreateTestTool("test-tool", "Updated");

            _registry.Register(tool1);
            _registry.Register(tool2);

            var retrieved = _registry.Get("test-tool");
            Assert.AreEqual("Updated", retrieved.Description);
        }

        [TestMethod]
        public void TestNullToolRegistration()
        {
            _registry.Register(null);
            Assert.AreEqual(0, _registry.Count);
        }

        [TestMethod]
        public void TestEmptyNameToolRegistration()
        {
            var tool = CreateTestTool("", "Test");
            _registry.Register(tool);
            Assert.AreEqual(0, _registry.Count);
        }

        private static AIFunction CreateTestTool(string name, string description)
        {
            return new AIFunction
            {
                Name = name,
                Description = description,
                Parameters = new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() },
                    { "required", new List<string>() }
                },
                Execute = args => "result"
            };
        }
    }
}
