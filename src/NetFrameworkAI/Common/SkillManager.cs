using System;
using System.Collections.Generic;
using System.IO;

namespace NetFrameworkAI.Common
{
    public class SkillInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string DirectoryPath { get; set; }
        public string SkillFilePath { get; set; }
    }

    public static class SkillManager
    {
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
                    catch
                    {
                    }
                }
            }

            return skills;
        }

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
                DirectoryPath = Path.GetDirectoryName(filePath),
                SkillFilePath = filePath
            };
        }

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

        private static string LoadSkillBody(SkillInfo skill)
        {
            return LoadSkillBody(skill.SkillFilePath);
        }

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

        public static AIFunction CreateLoadSkillFunction(List<SkillInfo> skills)
        {
            var parameters = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", new Dictionary<string, object>
                    {
                        { "skillName", new Dictionary<string, object>
                            {
                                { "type", "string" },
                                { "description", "The name of the skill to load" }
                            }
                        }
                    }
                },
                { "required", new List<string> { "skillName" } }
            };

            return new AIFunction
            {
                Name = "load_skill",
                Description = "Loads the full content and instructions of a specific skill",
                Parameters = parameters,
                Execute = new Func<string, string>(argsJson =>
                {
                    var args = JsonHelper.Deserialize<Dictionary<string, string>>(argsJson);
                    string skillName = null;
                    if (args.ContainsKey("skillName"))
                    {
                        skillName = args["skillName"];
                    }
                    else if (args.ContainsKey("input"))
                    {
                        skillName = args["input"];
                    }

                    if (string.IsNullOrEmpty(skillName))
                    {
                        return "Error: Skill name cannot be empty.";
                    }

                    SkillInfo matched = null;
                    foreach (var s in skills)
                    {
                        if (s.Name == skillName)
                        {
                            matched = s;
                            break;
                        }
                    }

                    if (matched == null)
                    {
                        return "Error: Skill '" + skillName + "' not found. Available skills: " + string.Join(", ", skills.ConvertAll(s => s.Name).ToArray());
                    }

                    return "# Skill: " + matched.Name + "\n\n" + LoadSkillBody(matched);
                })
            };
        }

        public static AIFunction CreateReadSkillTool(List<SkillInfo> skills)
        {
            var parameters = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", new Dictionary<string, object>
                    {
                        { "skillName", new Dictionary<string, object>
                            {
                                { "type", "string" },
                                { "description", "The name of the skill whose full content to read" }
                            }
                        }
                    }
                },
                { "required", new List<string> { "skillName" } }
            };

            return new AIFunction
            {
                Name = "read_skill",
                Description = "Read the full content of a specific skill, including its instructions, scripts, and resources",
                Parameters = parameters,
                Execute = new Func<string, string>(argsJson =>
                {
                    var args = JsonHelper.Deserialize<Dictionary<string, string>>(argsJson);
                    string skillName = null;
                    if (args.ContainsKey("skillName"))
                    {
                        skillName = args["skillName"];
                    }
                    else if (args.ContainsKey("input"))
                    {
                        skillName = args["input"];
                    }

                    if (string.IsNullOrEmpty(skillName))
                    {
                        return "Error: Skill name cannot be empty.";
                    }

                    SkillInfo matched = null;
                    foreach (var s in skills)
                    {
                        if (s.Name == skillName)
                        {
                            matched = s;
                            break;
                        }
                    }

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
                })
            };
        }
    }
}