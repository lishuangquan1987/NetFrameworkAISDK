using System;
using System.Collections.Generic;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// 会话存储插件接口
    /// </summary>
    public interface IConversationStore : IDisposable
    {
        /// <summary>
        /// 保存或更新会话
        /// </summary>
        /// <param name="sessionId">会话 ID</param>
        /// <param name="history">对话历史</param>
        void SaveSession(string sessionId, List<ConversationMessage> history);

        /// <summary>
        /// 加载会话
        /// </summary>
        /// <param name="sessionId">会话 ID</param>
        /// <returns>对话历史，不存在返回 null</returns>
        List<ConversationMessage> LoadSession(string sessionId);

        /// <summary>
        /// 列出所有会话
        /// </summary>
        /// <returns>会话信息列表</returns>
        IEnumerable<SessionInfo> ListSessions();

        /// <summary>
        /// 删除会话
        /// </summary>
        /// <param name="sessionId">会话 ID</param>
        /// <returns>是否成功删除</returns>
        bool DeleteSession(string sessionId);

        /// <summary>
        /// 保存会话快照
        /// </summary>
        /// <param name="sessionId">会话 ID</param>
        /// <param name="snapshotName">快照名称</param>
        /// <param name="history">对话历史</param>
        void SaveSnapshot(string sessionId, string snapshotName, List<ConversationMessage> history);

        /// <summary>
        /// 加载会话快照
        /// </summary>
        /// <param name="sessionId">会话 ID</param>
        /// <param name="snapshotName">快照名称</param>
        /// <returns>对话历史，不存在返回 null</returns>
        List<ConversationMessage> LoadSnapshot(string sessionId, string snapshotName);

        /// <summary>
        /// 列出会话的所有快照
        /// </summary>
        /// <param name="sessionId">会话 ID</param>
        /// <returns>快照信息列表</returns>
        IEnumerable<SnapshotInfo> ListSnapshots(string sessionId);

        /// <summary>
        /// 删除快照
        /// </summary>
        /// <param name="sessionId">会话 ID</param>
        /// <param name="snapshotName">快照名称</param>
        /// <returns>是否成功删除</returns>
        bool DeleteSnapshot(string sessionId, string snapshotName);

        /// <summary>
        /// 更新会话元数据
        /// </summary>
        /// <param name="sessionId">会话 ID</param>
        /// <param name="metadata">元数据</param>
        void UpdateSessionMetadata(string sessionId, SessionMetadata metadata);
    }

    /// <summary>
    /// 会话信息
    /// </summary>
    public class SessionInfo
    {
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public int MessageCount { get; set; }
        public SessionMetadata Metadata { get; set; }
    }

    /// <summary>
    /// 会话元数据
    /// </summary>
    public class SessionMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, string> CustomData { get; set; }

        public SessionMetadata()
        {
            Tags = new List<string>();
            CustomData = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// 快照信息
    /// </summary>
    public class SnapshotInfo
    {
        public string SnapshotName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MessageCount { get; set; }
    }

    /// <summary>
    /// 存储插件接口
    /// </summary>
    public interface IStoragePlugin : IPlugin
    {
        /// <summary>
        /// 存储类型（如 "File"、"SQLite"、"SQLServer"）
        /// </summary>
        string StorageType { get; }

        /// <summary>
        /// 创建会话存储实例
        /// </summary>
        /// <param name="config">插件配置</param>
        /// <returns>会话存储实例</returns>
        IConversationStore CreateStore(PluginConfig config);
    }

    /// <summary>
    /// 存储插件管理器
    /// </summary>
    public class StoragePluginManager
    {
        private readonly Dictionary<string, IStoragePlugin> _plugins;
        private readonly object _lock = new object();

        public StoragePluginManager()
        {
            _plugins = new Dictionary<string, IStoragePlugin>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 注册存储插件
        /// </summary>
        /// <param name="plugin">插件实例</param>
        public void Register(IStoragePlugin plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException("plugin");
            }

            lock (_lock)
            {
                _plugins[plugin.StorageType] = plugin;
            }
        }

        /// <summary>
        /// 获取存储插件
        /// </summary>
        /// <param name="storageType">存储类型</param>
        /// <returns>插件实例</returns>
        public IStoragePlugin Get(string storageType)
        {
            lock (_lock)
            {
                IStoragePlugin plugin;
                _plugins.TryGetValue(storageType, out plugin);
                return plugin;
            }
        }

        /// <summary>
        /// 创建会话存储
        /// </summary>
        /// <param name="storageType">存储类型</param>
        /// <param name="config">插件配置</param>
        /// <returns>会话存储实例</returns>
        public IConversationStore CreateStore(string storageType, PluginConfig config)
        {
            var plugin = Get(storageType);
            if (plugin == null)
            {
                throw new InvalidOperationException(
                    "Storage plugin for type '" + storageType + "' not found.");
            }

            return plugin.CreateStore(config);
        }

        /// <summary>
        /// 获取所有已注册的存储类型
        /// </summary>
        /// <returns>存储类型列表</returns>
        public IEnumerable<string> GetAvailableTypes()
        {
            lock (_lock)
            {
                return new List<string>(_plugins.Keys);
            }
        }
    }
}
