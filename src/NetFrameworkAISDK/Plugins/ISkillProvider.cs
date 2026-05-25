using System;
using System.Collections.Generic;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// 技能提供器接口
    /// </summary>
    public interface ISkillProvider
    {
        /// <summary>
        /// 提供器名称
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// 检查此提供器是否可以处理给定的技能 URI
        /// </summary>
        /// <param name="skillUri">技能 URI</param>
        /// <returns>是否可以处理</returns>
        bool CanHandle(string skillUri);

        /// <summary>
        /// 获取此提供器的所有技能
        /// </summary>
        /// <returns>技能列表</returns>
        List<SkillInfo> GetSkills();

        /// <summary>
        /// 加载技能内容
        /// </summary>
        /// <param name="skillId">技能 ID</param>
        /// <returns>技能内容</returns>
        string LoadSkillContent(string skillId);

        /// <summary>
        /// 刷新技能列表
        /// </summary>
        void Refresh();
    }

    /// <summary>
    /// 技能提供器插件接口
    /// </summary>
    public interface ISkillProviderPlugin : IPlugin
    {
        /// <summary>
        /// 提供器类型（如 "File"、"Git"、"Database"）
        /// </summary>
        string ProviderType { get; }

        /// <summary>
        /// 创建技能提供器实例
        /// </summary>
        /// <param name="config">插件配置</param>
        /// <returns>技能提供器实例</returns>
        ISkillProvider CreateProvider(PluginConfig config);
    }

    /// <summary>
    /// 扩展的技能信息
    /// </summary>
    public class ExtendedSkillInfo
    {
        public SkillInfo BaseInfo { get; set; }
        public string ProviderId { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Version { get; set; }
        public bool IsEnabled { get; set; }
        public string SourceUri { get; set; }
    }

    /// <summary>
    /// 技能提供器管理器
    /// </summary>
    public class SkillProviderManager
    {
        private readonly List<ISkillProvider> _providers;
        private readonly Dictionary<string, ExtendedSkillInfo> _skillIndex;
        private readonly object _lock = new object();

        public SkillProviderManager()
        {
            _providers = new List<ISkillProvider>();
            _skillIndex = new Dictionary<string, ExtendedSkillInfo>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 注册技能提供器
        /// </summary>
        /// <param name="provider">提供器实例</param>
        public void RegisterProvider(ISkillProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            lock (_lock)
            {
                _providers.Add(provider);
                RefreshIndex();
            }
        }

        /// <summary>
        /// 注销技能提供器
        /// </summary>
        /// <param name="providerName">提供器名称</param>
        public void UnregisterProvider(string providerName)
        {
            lock (_lock)
            {
                _providers.RemoveAll(p => 
                    string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
                RefreshIndex();
            }
        }

        /// <summary>
        /// 获取所有技能（从所有提供器聚合）
        /// </summary>
        /// <returns>聚合后的技能列表</returns>
        public List<SkillInfo> GetAllSkills()
        {
            lock (_lock)
            {
                var result = new List<SkillInfo>();
                foreach (var skill in _skillIndex.Values)
                {
                    if (skill.IsEnabled)
                    {
                        result.Add(skill.BaseInfo);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// 获取所有技能（包括禁用的）
        /// </summary>
        /// <returns>所有技能列表</returns>
        public IEnumerable<ExtendedSkillInfo> GetAllSkillsExtended()
        {
            lock (_lock)
            {
                return new List<ExtendedSkillInfo>(_skillIndex.Values);
            }
        }

        /// <summary>
        /// 查找技能
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>技能信息，不存在返回 null</returns>
        public ExtendedSkillInfo FindSkill(string skillName)
        {
            lock (_lock)
            {
                ExtendedSkillInfo info;
                _skillIndex.TryGetValue(skillName, out info);
                return info;
            }
        }

        /// <summary>
        /// 加载技能内容
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>技能内容</returns>
        public string LoadSkillContent(string skillName)
        {
            lock (_lock)
            {
                ExtendedSkillInfo info;
                if (!_skillIndex.TryGetValue(skillName, out info))
                {
                    return null;
                }

                foreach (var provider in _providers)
                {
                    if (provider.CanHandle(info.SourceUri))
                    {
                        return provider.LoadSkillContent(skillName);
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// 启用技能
        /// </summary>
        /// <param name="skillName">技能名称</param>
        public void EnableSkill(string skillName)
        {
            lock (_lock)
            {
                ExtendedSkillInfo info;
                if (_skillIndex.TryGetValue(skillName, out info))
                {
                    info.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// 禁用技能
        /// </summary>
        /// <param name="skillName">技能名称</param>
        public void DisableSkill(string skillName)
        {
            lock (_lock)
            {
                ExtendedSkillInfo info;
                if (_skillIndex.TryGetValue(skillName, out info))
                {
                    info.IsEnabled = false;
                }
            }
        }

        /// <summary>
        /// 刷新所有提供器的技能列表
        /// </summary>
        public void RefreshAll()
        {
            lock (_lock)
            {
                foreach (var provider in _providers)
                {
                    provider.Refresh();
                }
                RefreshIndex();
            }
        }

        /// <summary>
        /// 获取所有已注册的提供器
        /// </summary>
        /// <returns>提供器列表</returns>
        public IEnumerable<ISkillProvider> GetAllProviders()
        {
            lock (_lock)
            {
                return new List<ISkillProvider>(_providers);
            }
        }

        private void RefreshIndex()
        {
            _skillIndex.Clear();

            foreach (var provider in _providers)
            {
                var skills = provider.GetSkills();
                foreach (var skill in skills)
                {
                    var extInfo = new ExtendedSkillInfo
                    {
                        BaseInfo = skill,
                        ProviderId = provider.ProviderName,
                        LastUpdated = DateTime.UtcNow,
                        Version = "1.0.0",
                        IsEnabled = true,
                        SourceUri = provider.ProviderName + "://" + skill.Name
                    };

                    _skillIndex[skill.Name] = extInfo;
                }
            }
        }
    }
}
