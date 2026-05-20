using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 技能函数处理器，实现 load_skill 和 read_skill 工具调用
    /// </summary>
    internal sealed class SkillFunctionHandler
    {
        private readonly List<SkillInfo> _skills;

        /// <summary>
        /// 创建技能函数处理器
        /// </summary>
        /// <param name="skills">可用技能列表</param>
        public SkillFunctionHandler(List<SkillInfo> skills)
        {
            _skills = skills;
        }

        /// <summary>
        /// 按需加载技能的完整指令内容
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

            SkillInfo matched = FindSkill(_skills, skillName);
            if (matched == null)
            {
                return "Error: Skill '" + skillName + "' not found. Available skills: " +
                    string.Join(", ", _skills.ConvertAll(s => s.Name).ToArray());
            }

            var content = "";
            if (File.Exists(matched.SkillFilePath))
            {
                content = File.ReadAllText(matched.SkillFilePath);
                content = content.TrimStart();
                if (content.StartsWith("---"))
                {
                    int endIndex = content.IndexOf("---", 3);
                    if (endIndex > 3)
                    {
                        content = content.Substring(endIndex + 3).Trim();
                    }
                }
            }

            return "# Skill: " + matched.Name + "\n\n" + content;
        }

        /// <summary>
        /// 读取技能文件的原始内容（包含 front matter）
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