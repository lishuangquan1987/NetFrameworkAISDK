using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 技能管理器，实现 MAF 风格的渐进式披露模式。
    /// 仅在 system prompt 中注入技能摘要，通过工具调用按需加载完整技能内容。
    /// </summary>
    public static class SkillManager
    {
        /// <summary>
        /// 扫描目录，发现所有包含 SKILL.md 的技能子目录
        /// </summary>
        /// <param name="directoryPath">要扫描的根目录路径</param>
        /// <returns>发现的技能信息列表</returns>
        public static List<SkillInfo> DiscoverSkills(string directoryPath)
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
        /// 扫描多个目录，按优先级去重合并技能。
        /// 数组中越靠后的目录优先级越高（同名 skill 以后者为准）。
        /// </summary>
        /// <param name="directoryPaths">要扫描的目录路径数组</param>
        /// <returns>合并去重后的技能信息列表</returns>
        public static List<SkillInfo> DiscoverSkills(string[] directoryPaths)
        {
            var allSkills = new List<SkillInfo>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (directoryPaths == null || directoryPaths.Length == 0)
            {
                return allSkills;
            }

            for (int i = directoryPaths.Length - 1; i >= 0; i--)
            {
                var dirSkills = DiscoverSkills(directoryPaths[i]);
                if (dirSkills != null)
                {
                    foreach (var skill in dirSkills)
                    {
                        if (!seenNames.Contains(skill.Name))
                        {
                            seenNames.Add(skill.Name);
                            allSkills.Insert(0, skill);
                        }
                    }
                }
            }

            return allSkills;
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

            var dirPath = Path.GetDirectoryName(filePath);
            var files = new List<string>();

            if (Directory.Exists(dirPath))
            {
                var allFiles = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories);
                foreach (var f in allFiles)
                {
                    var relPath = f.Substring(dirPath.Length).TrimStart('\\', '/');
                    files.Add(relPath);
                }
            }

            return new SkillInfo
            {
                Name = name,
                Description = description ?? name,
                DirectoryPath = dirPath,
                SkillFilePath = filePath,
                Files = new System.Collections.ObjectModel.ReadOnlyCollection<string>(files)
            };
        }

        /// <summary>
        /// 加载 SKILL.md 文件的正文部分（跳过 YAML front matter）
        /// </summary>
        private static string LoadSkillBody(string filePath)
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
        /// 加载技能的正文内容
        /// </summary>
        private static string LoadSkillBody(SkillInfo skill)
        {
            return LoadSkillBody(skill.SkillFilePath);
        }

        /// <summary>
        /// 构建渐进式披露提示词。仅在 system prompt 中注入技能名称和摘要，
        /// 完整指令通过工具调用按需加载。
        /// </summary>
        /// <param name="skills">已发现的技能列表</param>
        /// <returns>格式化的可用技能提示文本</returns>
        public static string BuildProgressivePrompt(List<SkillInfo> skills)
        {
            if (skills == null || skills.Count == 0)
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
            foreach (var skill in skills)
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
        /// <param name="skills">可用技能列表</param>
        /// <returns>load_skill AI 函数</returns>
        public static AIFunction CreateLoadSkillFunction(List<SkillInfo> skills)
        {
            var handler = new SkillFunctionHandler(skills);
            var method = typeof(SkillFunctionHandler).GetMethod("LoadSkill");
            return AIFunctionFactory.Create(method, handler);
        }

        /// <summary>
        /// 创建 read_skill 工具函数，用于读取技能文件的原始内容
        /// </summary>
        /// <param name="skills">可用技能列表</param>
        /// <returns>read_skill AI 函数</returns>
        public static AIFunction CreateReadSkillTool(List<SkillInfo> skills)
        {
            var handler = new SkillFunctionHandler(skills);
            var method = typeof(SkillFunctionHandler).GetMethod("ReadSkill");
            return AIFunctionFactory.Create(method, handler);
        }

        /// <summary>
        /// 按名称（大小写不敏感）查找技能
        /// </summary>
        private static SkillInfo FindSkill(List<SkillInfo> skills, string skillName)
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