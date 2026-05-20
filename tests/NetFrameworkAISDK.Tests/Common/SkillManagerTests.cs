using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Tests.Common
{
    [TestFixture]
    public class SkillManagerTests
    {
        [Test]
        public void DiscoverSkills_WithNonExistentPath_ReturnsEmpty()
        {
            var skills = SkillManager.DiscoverSkills("Z:\\nonexistent\\path");

            Assert.IsNotNull(skills);
            Assert.AreEqual(0, skills.Count);
        }

        [Test]
        public void BuildProgressivePrompt_WithNullSkills_ReturnsEmpty()
        {
            var result = SkillManager.BuildProgressivePrompt(null);

            Assert.AreEqual("", result);
        }

        [Test]
        public void BuildProgressivePrompt_WithEmptySkills_ReturnsEmpty()
        {
            var result = SkillManager.BuildProgressivePrompt(new List<SkillInfo>());

            Assert.AreEqual("", result);
        }

        [Test]
        public void BuildProgressivePrompt_WithSingleSkill_ReturnsCatalog()
        {
            var skills = new List<SkillInfo>
            {
                new SkillInfo
                {
                    Name = "pdf-processing",
                    Description = "Extract PDF text and fill forms"
                }
            };

            var result = SkillManager.BuildProgressivePrompt(skills);

            Assert.IsFalse(string.IsNullOrEmpty(result));
            Assert.IsTrue(result.Contains("<available_skills>"));
            Assert.IsTrue(result.Contains("<name>pdf-processing</name>"));
            Assert.IsTrue(result.Contains("<description>Extract PDF text and fill forms</description>"));
            Assert.IsTrue(result.Contains("</available_skills>"));
            Assert.IsTrue(result.Contains("load_skill"));
            Assert.IsTrue(result.Contains("Only load what is needed"));
        }

        [Test]
        public void BuildProgressivePrompt_WithMultipleSkills_ContainsAllNames()
        {
            var skills = new List<SkillInfo>
            {
                new SkillInfo { Name = "skill-a", Description = "First skill" },
                new SkillInfo { Name = "skill-b", Description = "Second skill" },
                new SkillInfo { Name = "skill-c", Description = "Third skill" }
            };

            var result = SkillManager.BuildProgressivePrompt(skills);

            Assert.IsTrue(result.Contains("skill-a"));
            Assert.IsTrue(result.Contains("skill-b"));
            Assert.IsTrue(result.Contains("skill-c"));
        }

        [Test]
        public void CreateLoadSkillFunction_ReturnsCorrectAIFunction()
        {
            var skills = new List<SkillInfo>
            {
                new SkillInfo { Name = "test-skill", Description = "Test skill" }
            };

            var func = SkillManager.CreateLoadSkillFunction(skills);

            Assert.IsNotNull(func);
            Assert.AreEqual("load_skill", func.Name);
            Assert.IsTrue(func.Description.Contains("Loads the full content"));
            Assert.IsNotNull(func.Parameters);
            Assert.IsNotNull(func.Execute);
        }

        [Test]
        public void CreateLoadSkillFunction_WithEmptyName_ReturnsError()
        {
            var skills = new List<SkillInfo>
            {
                new SkillInfo { Name = "test-skill", Description = "Test skill" }
            };

            var func = SkillManager.CreateLoadSkillFunction(skills);
            var result = func.Execute("{\"skillName\":\"\"}");

            Assert.IsTrue(result.Contains("Error"));
            Assert.IsTrue(result.Contains("empty"));
        }

        [Test]
        public void CreateLoadSkillFunction_WithUnknownName_ReturnsError()
        {
            var skills = new List<SkillInfo>
            {
                new SkillInfo { Name = "test-skill", Description = "Test skill" }
            };

            var func = SkillManager.CreateLoadSkillFunction(skills);
            var result = func.Execute("{\"skillName\":\"nonexistent\"}");

            Assert.IsTrue(result.Contains("Error"));
            Assert.IsTrue(result.Contains("not found"));
        }

        [Test]
        public void CreateReadSkillTool_ReturnsCorrectAIFunction()
        {
            var skills = new List<SkillInfo>
            {
                new SkillInfo { Name = "test-skill", Description = "Test skill" }
            };

            var func = SkillManager.CreateReadSkillTool(skills);

            Assert.IsNotNull(func);
            Assert.AreEqual("read_skill", func.Name);
            Assert.IsTrue(func.Description.Contains("full content"));
            Assert.IsNotNull(func.Parameters);
            Assert.IsNotNull(func.Execute);
        }
    }
}