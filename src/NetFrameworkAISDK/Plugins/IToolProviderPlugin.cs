using System;
using System.Collections.Generic;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// 工具提供器插件接口
    /// </summary>
    public interface IToolProviderPlugin : IPlugin
    {
        /// <summary>
        /// 工具分类（如 "Database"、"Web"）
        /// </summary>
        string ToolCategory { get; }

        /// <summary>
        /// 获取该插件提供的所有工具
        /// </summary>
        /// <returns>工具列表</returns>
        IEnumerable<AIFunction> GetTools();

        /// <summary>
        /// 获取该插件提供的工具数量
        /// </summary>
        /// <returns>工具数量</returns>
        int GetToolCount();
    }

    /// <summary>
    /// 工具权限级别
    /// </summary>
    public enum ToolPermissionLevel
    {
        /// <summary>
        /// 所有用户可使用
        /// </summary>
        Public,
        
        /// <summary>
        /// 需要审批
        /// </summary>
        RequiresApproval,
        
        /// <summary>
        /// 仅管理员可使用
        /// </summary>
        AdminOnly
    }

    /// <summary>
    /// 工具权限信息
    /// </summary>
    public class ToolPermission
    {
        public string ToolName { get; set; }
        public ToolPermissionLevel Level { get; set; }
        public string[] AllowedRoles { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// 工具注册表
    /// </summary>
    public class ToolRegistry
    {
        private readonly Dictionary<string, AIFunction> _tools;
        private readonly Dictionary<string, ToolPermission> _permissions;
        private readonly object _lock = new object();

        public ToolRegistry()
        {
            _tools = new Dictionary<string, AIFunction>(StringComparer.OrdinalIgnoreCase);
            _permissions = new Dictionary<string, ToolPermission>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 注册工具
        /// </summary>
        /// <param name="function">工具实例</param>
        public void Register(AIFunction function)
        {
            if (function == null || string.IsNullOrEmpty(function.Name))
            {
                return;
            }

            lock (_lock)
            {
                _tools[function.Name] = function;
            }
        }

        /// <summary>
        /// 注册工具（带权限）
        /// </summary>
        /// <param name="function">工具实例</param>
        /// <param name="permission">权限信息</param>
        public void Register(AIFunction function, ToolPermission permission)
        {
            Register(function);
            if (permission != null)
            {
                permission.ToolName = function.Name;
                lock (_lock)
                {
                    _permissions[function.Name] = permission;
                }
            }
        }

        /// <summary>
        /// 注册多个工具
        /// </summary>
        /// <param name="functions">工具列表</param>
        public void RegisterRange(IEnumerable<AIFunction> functions)
        {
            if (functions == null)
            {
                return;
            }

            lock (_lock)
            {
                foreach (var function in functions)
                {
                    if (function != null && !string.IsNullOrEmpty(function.Name))
                    {
                        _tools[function.Name] = function;
                    }
                }
            }
        }

        /// <summary>
        /// 注销工具
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <returns>是否成功注销</returns>
        public bool Unregister(string toolName)
        {
            lock (_lock)
            {
                var result = _tools.Remove(toolName);
                _permissions.Remove(toolName);
                return result;
            }
        }

        /// <summary>
        /// 获取工具
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <returns>工具实例，不存在返回 null</returns>
        public AIFunction Get(string toolName)
        {
            lock (_lock)
            {
                AIFunction function;
                _tools.TryGetValue(toolName, out function);
                return function;
            }
        }

        /// <summary>
        /// 获取所有工具
        /// </summary>
        /// <returns>工具列表</returns>
        public IEnumerable<AIFunction> GetAll()
        {
            lock (_lock)
            {
                return new List<AIFunction>(_tools.Values);
            }
        }

        /// <summary>
        /// 按分类获取工具
        /// </summary>
        /// <param name="category">分类名称</param>
        /// <returns>该分类下的工具列表</returns>
        public IEnumerable<AIFunction> GetByCategory(string category)
        {
            lock (_lock)
            {
                var result = new List<AIFunction>();
                foreach (var tool in _tools.Values)
                {
                    var categoryAttr = tool.GetType().GetProperty("Category");
                    if (categoryAttr != null)
                    {
                        var value = categoryAttr.GetValue(tool, null) as string;
                        if (string.Equals(value, category, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(tool);
                        }
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// 获取工具权限
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <returns>权限信息，不存在返回 null</returns>
        public ToolPermission GetPermission(string toolName)
        {
            lock (_lock)
            {
                ToolPermission permission;
                _permissions.TryGetValue(toolName, out permission);
                return permission;
            }
        }

        /// <summary>
        /// 设置工具权限
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="permission">权限信息</param>
        public void SetPermission(string toolName, ToolPermission permission)
        {
            lock (_lock)
            {
                permission.ToolName = toolName;
                _permissions[toolName] = permission;
            }
        }

        /// <summary>
        /// 检查工具是否存在
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <returns>是否存在</returns>
        public bool Exists(string toolName)
        {
            lock (_lock)
            {
                return _tools.ContainsKey(toolName);
            }
        }

        /// <summary>
        /// 获取工具数量
        /// </summary>
        /// <returns>工具数量</returns>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _tools.Count;
                }
            }
        }
    }
}
