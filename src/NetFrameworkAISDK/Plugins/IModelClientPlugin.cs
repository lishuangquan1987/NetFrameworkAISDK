using System;
using System.Collections.Generic;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// 模型客户端插件接口
    /// </summary>
    public interface IModelClientPlugin : IPlugin
    {
        /// <summary>
        /// 提供商名称（如 "DeepSeek"、"Qwen"）
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// 支持的模型列表
        /// </summary>
        string[] SupportedModels { get; }

        /// <summary>
        /// 创建 AI 客户端实例
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="options">可选的客户端配置</param>
        /// <returns>AI 客户端实例</returns>
        IAIClient CreateClient(string apiKey, ModelClientOptions options = null);
    }

    /// <summary>
    /// 模型客户端配置选项
    /// </summary>
    public class ModelClientOptions
    {
        public string BaseUrl { get; set; }
        public int TimeoutMilliseconds { get; set; }
        public string DefaultModel { get; set; }
        public Dictionary<string, string> AdditionalHeaders { get; set; }
    }

    /// <summary>
    /// 模型客户端注册表
    /// </summary>
    public class ModelClientRegistry
    {
        private readonly Dictionary<string, IModelClientPlugin> _plugins;
        private readonly object _lock = new object();

        public ModelClientRegistry()
        {
            _plugins = new Dictionary<string, IModelClientPlugin>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 注册模型客户端插件
        /// </summary>
        /// <param name="plugin">插件实例</param>
        public void Register(IModelClientPlugin plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException("plugin");
            }

            lock (_lock)
            {
                _plugins[plugin.ProviderName] = plugin;
            }
        }

        /// <summary>
        /// 根据提供商名称获取插件
        /// </summary>
        /// <param name="providerName">提供商名称</param>
        /// <returns>插件实例，不存在返回 null</returns>
        public IModelClientPlugin GetByProvider(string providerName)
        {
            lock (_lock)
            {
                IModelClientPlugin plugin;
                _plugins.TryGetValue(providerName, out plugin);
                return plugin;
            }
        }

        /// <summary>
        /// 根据模型名称获取插件
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <returns>插件实例，不存在返回 null</returns>
        public IModelClientPlugin GetByModel(string modelName)
        {
            lock (_lock)
            {
                foreach (var plugin in _plugins.Values)
                {
                    foreach (var model in plugin.SupportedModels)
                    {
                        if (string.Equals(model, modelName, StringComparison.OrdinalIgnoreCase))
                        {
                            return plugin;
                        }
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// 创建 AI 客户端
        /// </summary>
        /// <param name="providerName">提供商名称</param>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="options">可选配置</param>
        /// <returns>AI 客户端实例</returns>
        public IAIClient CreateClient(string providerName, string apiKey, ModelClientOptions options = null)
        {
            var plugin = GetByProvider(providerName);
            if (plugin == null)
            {
                throw new InvalidOperationException(
                    "Model client plugin for provider '" + providerName + "' not found.");
            }

            return plugin.CreateClient(apiKey, options);
        }

        /// <summary>
        /// 创建 AI 客户端（根据模型名称自动选择提供商）
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="options">可选配置</param>
        /// <returns>AI 客户端实例</returns>
        public IAIClient CreateClientByModel(string modelName, string apiKey, ModelClientOptions options = null)
        {
            var plugin = GetByModel(modelName);
            if (plugin == null)
            {
                throw new InvalidOperationException(
                    "No model client plugin found for model '" + modelName + "'.");
            }

            return plugin.CreateClient(apiKey, options);
        }

        /// <summary>
        /// 获取所有已注册的插件
        /// </summary>
        /// <returns>插件列表</returns>
        public IEnumerable<IModelClientPlugin> GetAll()
        {
            lock (_lock)
            {
                return _plugins.Values;
            }
        }
    }
}
