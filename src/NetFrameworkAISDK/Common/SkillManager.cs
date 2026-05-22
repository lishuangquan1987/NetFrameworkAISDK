using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 技能管理器，实现 MAF 风格的渐进式披露模式。
    /// 仅在 system prompt 中注入技能摘要，通过工具调用按需加载完整技能内容。
    /// 实例化后持有已发现的技能列表，支持文件变更自动感知和重新扫描。
    /// </summary>
    public class SkillManager
    {
        private readonly List<string> _directoryPaths;
        private List<SkillInfo> _skills;
        private DateTime _lastScanTime;
        private readonly object _lock = new object();

        /// <summary>
        /// 已发现的技能列表
        /// </summary>
        public List<SkillInfo> Skills
        {
            get
            {
                EnsureFresh();
                return _skills;
            }
        }

        /// <summary>
        /// 当前监控的目录路径数组
        /// </summary>
        public string[] DirectoryPaths
        {
            get { return _directoryPaths.ToArray(); }
        }

        /// <summary>
        /// 创建技能管理器并立即扫描指定目录
        /// </summary>
        /// <param name="directoryPaths">技能目录路径数组，优先级从低到高</param>
        public SkillManager(params string[] directoryPaths)
        {
            _directoryPaths = new List<string>();
            if (directoryPaths != null)
            {
                _directoryPaths.AddRange(directoryPaths);
            }
            _skills = new List<SkillInfo>();
            _lastScanTime = DateTime.MinValue;
            Discover();
        }

        /// <summary>
        /// 动态添加新的技能目录并立即扫描
        /// </summary>
        /// <param name="path">技能目录路径</param>
        public void AddDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) { return; }

            lock (_lock)
            {
                if (!_directoryPaths.Contains(path))
                {
                    _directoryPaths.Add(path);
                }
                Discover();
            }
        }

        /// <summary>
        /// 移除技能目录并立即重新扫描
        /// </summary>
        /// <param name="path">要移除的目录路径</param>
        public void RemoveDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) { return; }

            lock (_lock)
            {
                _directoryPaths.Remove(path);
                Discover();
            }
        }

        /// <summary>
        /// 强制重新扫描所有目录
        /// </summary>
        public void Refresh()
        {
            lock (_lock)
            {
                Discover();
            }
        }

        /// <summary>
        /// 构建渐进式披露提示词。仅在 system prompt 中注入技能名称和摘要，
        /// 完整指令通过工具调用按需加载。
        /// </summary>
        /// <returns>格式化的可用技能提示文本</returns>
        public string BuildProgressivePrompt()
        {
            EnsureFresh();
            if (_skills.Count == 0)
            {
                return "";
            }

            var parts = new List<string>();
            parts.Add("# Available Skills");
            parts.Add("You have access to skills containing domain-specific knowledge and capabilities.");
            parts.Add("When a task aligns with a skill's domain, call load_skill to retrieve the full instructions.");
            parts.Add("Only load what is needed, when it is needed.");
            parts.Add("");
            parts.Add("<available_skills>");
            foreach (var skill in _skills)
            {
                parts.Add("  <skill>");
                parts.Add("    <name>" + skill.Name + "</name>");
                parts.Add("    <description>" + skill.Description + "</description>");
                parts.Add("  </skill>");
            }
            parts.Add("</available_skills>");

            return string.Join("\n", parts.ToArray());
        }

        /// <summary>
        /// 创建 load_skill 工具函数，用于按需加载技能完整内容
        /// </summary>
        /// <returns>load_skill AI 函数</returns>
        public AIFunction CreateLoadSkillFunction()
        {
            var method = typeof(SkillManager).GetMethod("LoadSkill");
            return AIFunctionFactory.Create(method, this);
        }

        /// <summary>
        /// 创建 read_skill 工具函数，用于读取技能文件的原始内容
        /// </summary>
        /// <returns>read_skill AI 函数</returns>
        public AIFunction CreateReadSkillTool()
        {
            var method = typeof(SkillManager).GetMethod("ReadSkill");
            return AIFunctionFactory.Create(method, this);
        }

        /// <summary>
        /// 按需加载技能的完整指令内容（工具回调方法）
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>技能的完整指令文本</returns>
        [Description("Loads the full content and instructions of a specific skill")]
        public string LoadSkill(
            [Description("The name of the skill")] string skillName)
        {
            if (string.IsNullOrEmpty(skillName))
            {
                return "Error: Skill name cannot be empty.";
            }

            EnsureFresh();
            SkillInfo matched = FindSkill(_skills, skillName);
            if (matched == null)
            {
                return "Error: Skill '" + skillName + "' not found. Available skills: " +
                    string.Join(", ", _skills.ConvertAll(s => s.Name).ToArray());
            }

            var body = LoadSkillBody(matched.SkillFilePath);
            return "# Skill: " + matched.Name + "\n\n" + body;
        }

        /// <summary>
        /// 读取技能文件的原始内容（包含 front matter）（工具回调方法）
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>技能文件的完整内容</returns>
        [Description("Read the full content of a specific skill, including its instructions, scripts, and resources")]
        public string ReadSkill(
            [Description("The name of the skill")] string skillName)
        {
            if (string.IsNullOrEmpty(skillName))
            {
                return "Error: Skill name cannot be empty.";
            }

            EnsureFresh();
            SkillInfo matched = FindSkill(_skills, skillName);
            if (matched == null)
            {
                return "Error: Skill '" + skillName + "' not found.";
            }

            try
            {
                return File.ReadAllText(matched.SkillFilePath);
            }
            catch (Exception ex)
            {
                return "Error reading skill file: " + ex.Message;
            }
        }

        /// <summary>
        /// 检查所有目录的最后写入时间，如果任何目录有变更则重新扫描
        /// </summary>
        private void EnsureFresh()
        {
            lock (_lock)
            {
                DateTime latest = DateTime.MinValue;
                foreach (var dir in _directoryPaths)
                {
                    if (Directory.Exists(dir))
                    {
                        var writeTime = Directory.GetLastWriteTime(dir);
                        if (writeTime > latest)
                        {
                            latest = writeTime;
                        }
                    }
                }
                if (latest > _lastScanTime)
                {
                    Discover();
                }
            }
        }

        /// <summary>
        /// 内部扫描：使用当前 _directoryPaths 重新发现技能
        /// </summary>
        private void Discover()
        {
            Discover(_directoryPaths);
        }

        /// <summary>
        /// 使用指定目录列表重新发现技能
        /// </summary>
        private void Discover(IList<string> paths)
        {
            var result = new List<SkillInfo>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (paths != null)
            {
                for (int i = paths.Count - 1; i >= 0; i--)
                {
                    var dirSkills = DiscoverFromDirectory(paths[i]);
                    if (dirSkills != null)
                    {
                        foreach (var skill in dirSkills)
                        {
                            if (!seenNames.Contains(skill.Name))
                            {
                                seenNames.Add(skill.Name);
                                result.Insert(0, skill);
                            }
                        }
                    }
                }
            }

            _skills = result;
            _lastScanTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 扫描目录，发现所有包含 SKILL.md 的技能子目录
        /// </summary>
        private static List<SkillInfo> DiscoverFromDirectory(string directoryPath)
        {
            var skills = new List<SkillInfo>();

            if (!Directory.Exists(directoryPath))
            {
                return skills;
            }

            foreach (var dir in Directory.GetDirectories(directoryPath))
            {
                var skillMdPath = Path.Combine(dir, "SKILL.md");
                if (File.Exists(skillMdPath))
                {
                    try
                    {
                        var skill = ParseSkillFile(skillMdPath);
                        if (skill != null)
                        {
                            skills.Add(skill);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Warning: Failed to parse skill at " + skillMdPath + ": " + ex.Message);
                    }
                }
            }

            return skills;
        }

        /// <summary>
        /// 解析 SKILL.md 文件，提取名称和描述
        /// </summary>
        private static SkillInfo ParseSkillFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var content = File.ReadAllText(filePath);
            if (string.IsNullOrEmpty(content))
            {
                return null;
            }

            string name = null;
            string description = null;
            content = content.TrimStart();

            if (content.StartsWith("---"))
            {
                int endIndex = content.IndexOf("---", 3);
                if (endIndex > 3)
                {
                    var frontmatter = content.Substring(3, endIndex - 3);
                    var lines = frontmatter.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("name:"))
                        {
                            name = trimmed.Substring(5).Trim().Trim('"', '\'', ' ');
                        }
                        else if (trimmed.StartsWith("description:"))
                        {
                            description = trimmed.Substring(12).Trim().Trim('"', '\'', ' ');
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                name = Path.GetFileName(Path.GetDirectoryName(filePath));
            }

            return new SkillInfo
            {
                Name = name,
                Description = description ?? name,
                SkillFilePath = filePath,
                Files = new ReadOnlyCollection<string>(new string[0])
            };
        }

        /// <summary>
        /// 加载 SKILL.md 文件的正文部分（跳过 YAML front matter）
        /// </summary>
        internal static string LoadSkillBody(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return "";
            }

            var content = File.ReadAllText(filePath);
            if (string.IsNullOrEmpty(content))
            {
                return "";
            }

            content = content.TrimStart();

            if (content.StartsWith("---"))
            {
                int endIndex = content.IndexOf("---", 3);
                if (endIndex > 3)
                {
                    return content.Substring(endIndex + 3).Trim();
                }
            }

            return content;
        }

        /// <summary>
        /// 按名称（大小写不敏感）查找技能
        /// </summary>
        internal static SkillInfo FindSkill(List<SkillInfo> skills, string skillName)
        {
            foreach (var s in skills)
            {
                if (string.Equals(s.Name, skillName, StringComparison.OrdinalIgnoreCase))
                {
                    return s;
                }
            }
            return null;
        }
    }
}
