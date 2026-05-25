using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetFrameworkAISDK.Plugins;
using NetFrameworkAISDK.Plugins.Middleware;

namespace NetFrameworkAISDK.Tests.Plugins
{
    [TestClass]
    public class PluginManagerTests
    {
        [TestMethod]
        public void TestPluginRegistration()
        {
            var manager = new PluginManager();

            var testPlugin = new TestPlugin
            {
                Id = "TestPlugin",
                Name = "Test Plugin",
                Version = "1.0.0"
            };

            manager.RegisterPlugin(testPlugin);

            var retrieved = manager.GetPlugin("TestPlugin");

            Assert.IsNotNull(retrieved);
            Assert.AreEqual("TestPlugin", retrieved.Id);
            Assert.AreEqual("Test Plugin", retrieved.Name);
        }

        [TestMethod]
        public void TestPluginConfiguration()
        {
            var manager = new PluginManager();

            var testPlugin = new TestPlugin
            {
                Id = "TestPlugin",
                Name = "Test Plugin",
                Version = "1.0.0"
            };

            manager.RegisterPlugin(testPlugin);

            var config = new PluginConfig
            {
                PluginId = "TestPlugin",
                IsEnabled = true,
                Settings = new Dictionary<string, object>
                {
                    { "key1", "value1" }
                }
            };

            manager.ConfigurePlugin("TestPlugin", config);

            var retrievedConfig = manager.GetPluginConfig("TestPlugin");

            Assert.IsNotNull(retrievedConfig);
            Assert.IsTrue(retrievedConfig.IsEnabled);
            Assert.AreEqual("value1", retrievedConfig.Settings["key1"]);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestDuplicatePluginRegistration()
        {
            var manager = new PluginManager();

            var plugin1 = new TestPlugin { Id = "TestPlugin", Name = "Test 1", Version = "1.0.0" };
            var plugin2 = new TestPlugin { Id = "TestPlugin", Name = "Test 2", Version = "1.0.0" };

            manager.RegisterPlugin(plugin1);
            manager.RegisterPlugin(plugin2);
        }

        [TestMethod]
        public void TestPluginEnableDisable()
        {
            var manager = new PluginManager();

            var testPlugin = new TestPlugin
            {
                Id = "TestPlugin",
                Name = "Test Plugin",
                Version = "1.0.0"
            };

            manager.RegisterPlugin(testPlugin);

            manager.EnablePlugin("TestPlugin");
            Assert.IsTrue(manager.IsPluginEnabled("TestPlugin"));

            manager.DisablePlugin("TestPlugin");
            Assert.IsFalse(manager.IsPluginEnabled("TestPlugin"));
        }

        [TestMethod]
        public void TestGetAllPlugins()
        {
            var manager = new PluginManager();

            var plugin1 = new TestPlugin { Id = "Plugin1", Name = "Plugin 1", Version = "1.0.0" };
            var plugin2 = new TestPlugin { Id = "Plugin2", Name = "Plugin 2", Version = "1.0.0" };

            manager.RegisterPlugin(plugin1);
            manager.RegisterPlugin(plugin2);

            var allPlugins = manager.GetAllPlugins().ToList();

            Assert.AreEqual(2, allPlugins.Count);
        }

        [TestMethod]
        public void TestUnregisterPlugin()
        {
            var manager = new PluginManager();

            var testPlugin = new TestPlugin
            {
                Id = "TestPlugin",
                Name = "Test Plugin",
                Version = "1.0.0"
            };

            manager.RegisterPlugin(testPlugin);
            Assert.IsNotNull(manager.GetPlugin("TestPlugin"));

            manager.UnregisterPlugin("TestPlugin");
            Assert.IsNull(manager.GetPlugin("TestPlugin"));
        }

        private class TestPlugin : IPlugin
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Version { get; set; }
            public string Description { get { return ""; } }
            public string Author { get { return ""; } }
            public string Website { get { return ""; } }
            public string[] Dependencies { get { return new string[0]; } }

            public void Initialize(PluginConfig config)
            {
            }

            public PluginValidationResult Validate()
            {
                return PluginValidationResult.Success();
            }
        }
    }
}
