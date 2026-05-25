using System;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// 所有插件的基接口
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// 插件的唯一标识符
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 插件名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 插件版本
        /// </summary>
        string Version { get; }

        /// <summary>
        /// 插件描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 插件作者
        /// </summary>
        string Author { get; }

        /// <summary>
        /// 插件主页
        /// </summary>
        string Website { get; }

        /// <summary>
        /// 插件依赖的其他插件
        /// </summary>
        string[] Dependencies { get; }

        /// <summary>
        /// 初始化插件
        /// </summary>
        /// <param name="config">插件配置</param>
        void Initialize(PluginConfig config);

        /// <summary>
        /// 验证插件是否可以正常加载
        /// </summary>
        /// <returns>验证结果和错误信息</returns>
        PluginValidationResult Validate();
    }

    /// <summary>
    /// 插件验证结果
    /// </summary>
    public class PluginValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }

        public static PluginValidationResult Success()
        {
            return new PluginValidationResult { IsValid = true };
        }

        public static PluginValidationResult Failure(string error)
        {
            return new PluginValidationResult { IsValid = false, ErrorMessage = error };
        }
    }

    /// <summary>
    /// 插件配置
    /// </summary>
    public class PluginConfig
    {
        public string PluginId { get; set; }
        public bool IsEnabled { get; set; }
        public Dictionary<string, object> Settings { get; set; }
    }
}
