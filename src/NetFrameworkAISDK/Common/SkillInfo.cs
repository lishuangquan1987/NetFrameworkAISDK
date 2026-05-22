using System.Collections.ObjectModel;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 技能信息，包含技能名称、描述和从文件系统加载的内容
    /// </summary>
    public class SkillInfo
    {
        /// <summary>
        /// 技能名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 技能描述（来自 SKILL.md 的 description 字段）
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// SKILL.md 文件的完整路径
        /// </summary>
        public string SkillFilePath { get; set; }

        /// <summary>
        /// 技能文件列表（相对路径）
        /// </summary>
        public ReadOnlyCollection<string> Files { get; set; }
    }
}