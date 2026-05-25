using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// 插件管理器，负责插件的发现、加载和管理
    /// </summary>
    public class PluginManager
    {
        private readonly Dictionary<string, IPlugin> _plugins;
        private readonly Dictionary<string, PluginConfig> _configs;
        private readonly object _lock = new object();

        public PluginManager()
        {
            _plugins = new Dictionary<string, IPlugin>(StringComparer.OrdinalIgnoreCase);
            _configs = new Dictionary<string, PluginConfig>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 从程序集发现并加载所有插件
        /// </summary>
        /// <param name="assembly">要扫描的程序集</param>
        /// <returns>加载的插件数量</returns>
        public int LoadPluginsFromAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }

            int count = 0;
            var pluginTypes = assembly.GetTypes();

            foreach (var type in pluginTypes)
            {
                if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
                {
                    continue;
                }

                var pluginAttr = type.GetCustomAttributes(typeof(PluginAttribute), false) as PluginAttribute[];
                if (pluginAttr != null && pluginAttr.Length > 0)
                {
                    try
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(type);
                        var config = GetOrCreateConfig(plugin.Id);
                        plugin.Initialize(config);
                        
                        var validation = plugin.Validate();
                        if (!validation.IsValid)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                "Plugin " + plugin.Id + " validation failed: " + validation.ErrorMessage);
                            continue;
                        }

                        RegisterPlugin(plugin);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "Failed to load plugin " + type.FullName + ": " + ex.Message);
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// 从目录加载所有 DLL 中的插件
        /// </summary>
        /// <param name="directory">插件目录路径</param>
        /// <returns>加载的插件数量</returns>
        public int LoadPluginsFromDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
            {
                return 0;
            }

            int count = 0;
            var dllFiles = System.IO.Directory.GetFiles(directory, "*.dll");

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllFile);
                    count += LoadPluginsFromAssembly(assembly);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "Failed to load assembly " + dllFile + ": " + ex.Message);
                }
            }

            return count;
        }

        /// <summary>
        /// 注册插件
        /// </summary>
        /// <param name="plugin">插件实例</param>
        public void RegisterPlugin(IPlugin plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException("plugin");
            }

            lock (_lock)
            {
                if (_plugins.ContainsKey(plugin.Id))
                {
                    throw new InvalidOperationException(
                        "Plugin with ID '" + plugin.Id + "' is already registered.");
                }

                _plugins[plugin.Id] = plugin;
            }
        }

        /// <summary>
        /// 注销插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>是否成功注销</returns>
        public bool UnregisterPlugin(string pluginId)
        {
            lock (_lock)
            {
                return _plugins.Remove(pluginId);
            }
        }

        /// <summary>
        /// 获取插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>插件实例，不存在返回 null</returns>
        public IPlugin GetPlugin(string pluginId)
        {
            lock (_lock)
            {
                IPlugin plugin;
                _plugins.TryGetValue(pluginId, out plugin);
                return plugin;
            }
        }

        /// <summary>
        /// 获取指定类型的所有插件
        /// </summary>
        /// <typeparam name="T">插件接口类型</typeparam>
        /// <returns>插件列表</returns>
        public IEnumerable<T> GetAllPlugins<T>() where T : class, IPlugin
        {
            lock (_lock)
            {
                return _plugins.Values
                    .Where(p => p is T)
                    .Select(p => (T)p)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取所有已注册的插件
        /// </summary>
        /// <returns>所有插件列表</returns>
        public IEnumerable<IPlugin> GetAllPlugins()
        {
            lock (_lock)
            {
                return _plugins.Values.ToList();
            }
        }

        /// <summary>
        /// 配置插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="config">插件配置</param>
        public void ConfigurePlugin(string pluginId, PluginConfig config)
        {
            lock (_lock)
            {
                _configs[pluginId] = config;

                IPlugin plugin;
                if (_plugins.TryGetValue(pluginId, out plugin))
                {
                    plugin.Initialize(config);
                }
            }
        }

        /// <summary>
        /// 获取插件配置
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>插件配置，不存在返回 null</returns>
        public PluginConfig GetPluginConfig(string pluginId)
        {
            lock (_lock)
            {
                PluginConfig config;
                _configs.TryGetValue(pluginId, out config);
                return config;
            }
        }

        /// <summary>
        /// 启用插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        public void EnablePlugin(string pluginId)
        {
            var config = GetOrCreateConfig(pluginId);
            config.IsEnabled = true;
            ConfigurePlugin(pluginId, config);
        }

        /// <summary>
        /// 禁用插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        public void DisablePlugin(string pluginId)
        {
            var config = GetOrCreateConfig(pluginId);
            config.IsEnabled = false;
            ConfigurePlugin(pluginId, config);
        }

        /// <summary>
        /// 检查插件是否已启用
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>是否已启用</returns>
        public bool IsPluginEnabled(string pluginId)
        {
            var config = GetPluginConfig(pluginId);
            return config != null && config.IsEnabled;
        }

        /// <summary>
        /// 初始化所有已注册的插件
        /// </summary>
        public void InitializeAll()
        {
            lock (_lock)
            {
                foreach (var plugin in _plugins.Values)
                {
                    var config = GetOrCreateConfig(plugin.Id);
                    if (config.IsEnabled)
                    {
                        plugin.Initialize(config);
                    }
                }
            }
        }

        /// <summary>
        /// 释放所有插件资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var plugin in _plugins.Values)
                {
                    var disposable = plugin as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }
                _plugins.Clear();
                _configs.Clear();
            }
        }

        private PluginConfig GetOrCreateConfig(string pluginId)
        {
            lock (_lock)
            {
                PluginConfig config;
                if (!_configs.TryGetValue(pluginId, out config))
                {
                    config = new PluginConfig
                    {
                        PluginId = pluginId,
                        IsEnabled = true,
                        Settings = new Dictionary<string, object>()
                    };
                    _configs[pluginId] = config;
                }
                return config;
            }
        }
    }
}
