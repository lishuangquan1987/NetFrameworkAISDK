using System;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// 标记类为插件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PluginAttribute : Attribute
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Website { get; set; }
        public string[] Dependencies { get; set; }

        public PluginAttribute(string id, string version)
        {
            Id = id;
            Version = version;
            Name = id;
            Description = "";
            Author = "";
            Website = "";
            Dependencies = new string[0];
        }
    }

    /// <summary>
    /// 标记类为模型客户端插件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ModelClientPluginAttribute : Attribute
    {
        public string ProviderName { get; set; }
        public string[] SupportedModels { get; set; }

        public ModelClientPluginAttribute(params string[] supportedModels)
        {
            SupportedModels = supportedModels ?? new string[0];
        }
    }

    /// <summary>
    /// 标记类为工具提供器插件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ToolProviderPluginAttribute : Attribute
    {
        public string Category { get; set; }
        public int Order { get; set; }

        public ToolProviderPluginAttribute(string category)
        {
            Category = category;
            Order = 0;
        }
    }

    /// <summary>
    /// 标记类为存储插件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StoragePluginAttribute : Attribute
    {
        public string StorageType { get; set; }

        public StoragePluginAttribute(string storageType)
        {
            StorageType = storageType;
        }
    }

    /// <summary>
    /// 标记类为中间件插件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class MiddlewarePluginAttribute : Attribute
    {
        public string MiddlewareType { get; set; }
        public int Order { get; set; }

        public MiddlewarePluginAttribute(string middlewareType)
        {
            MiddlewareType = middlewareType;
            Order = 0;
        }
    }

    /// <summary>
    /// 标记类为技能提供器插件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SkillProviderPluginAttribute : Attribute
    {
        public string ProviderType { get; set; }

        public SkillProviderPluginAttribute(string providerType)
        {
            ProviderType = providerType;
        }
    }
}
