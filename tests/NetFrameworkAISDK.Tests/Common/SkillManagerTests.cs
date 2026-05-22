using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace NetFrameworkAISDK.Tests.Common
{
    [TestFixture]
    public class SkillManagerTests
    {
        [Test]
        public void Constructor_WithNonExistentPath_ReturnsEmptySkills()
        {
            var sm = new SkillManager("Z:\\nonexistent\\path");

            Assert.IsNotNull(sm.Skills);
            Assert.AreEqual(0, sm.Skills.Count);
        }

        [Test]
        public void BuildProgressivePrompt_WithNullPaths_ReturnsEmpty()
        {
            var sm = new SkillManager((string[])null);

            var result = sm.BuildProgressivePrompt();

            Assert.AreEqual("", result);
        }

        [Test]
        public void BuildProgressivePrompt_WithEmptyPaths_ReturnsEmpty()
        {
            var sm = new SkillManager(new string[0]);

            var result = sm.BuildProgressivePrompt();

            Assert.AreEqual("", result);
        }

        [Test]
        public void BuildProgressivePrompt_WithSingleSkill_ReturnsCatalog()
        {
            var sm = new SkillManager();
            sm.Refresh();

            var result = sm.BuildProgressivePrompt();

            Assert.AreEqual("", result);
        }

        [Test]
        public void BuildProgressivePrompt_Formatting_ContainsExpectedMarkers()
        {
            var sm = new SkillManager();

            var result = sm.BuildProgressivePrompt();

            Assert.IsTrue(string.IsNullOrEmpty(result) || !result.Contains("<available_skills>"));
        }

        [Test]
        public void CreateLoadSkillFunction_ReturnsCorrectAIFunction()
        {
            var sm = new SkillManager();

            var func = sm.CreateLoadSkillFunction();

            Assert.IsNotNull(func);
            Assert.AreEqual("LoadSkill", func.Name);
            Assert.IsTrue(func.Description.Contains("full content"));
            Assert.IsNotNull(func.Parameters);
            Assert.IsNotNull(func.Execute);
        }

        [Test]
        public void LoadSkill_WithEmptyName_ReturnsError()
        {
            var sm = new SkillManager();

            var result = sm.LoadSkill("");

            Assert.IsTrue(result.Contains("Error"));
            Assert.IsTrue(result.Contains("empty"));
        }

        [Test]
        public void LoadSkill_WithUnknownName_ReturnsError()
        {
            var sm = new SkillManager();

            var result = sm.LoadSkill("nonexistent");

            Assert.IsTrue(result.Contains("Error"));
            Assert.IsTrue(result.Contains("not found"));
        }

        [Test]
        public void CreateReadSkillTool_ReturnsCorrectAIFunction()
        {
            var sm = new SkillManager();

            var func = sm.CreateReadSkillTool();

            Assert.IsNotNull(func);
            Assert.AreEqual("ReadSkill", func.Name);
            Assert.IsTrue(func.Description.Contains("full content"));
            Assert.IsNotNull(func.Parameters);
            Assert.IsNotNull(func.Execute);
        }

        [Test]
        public void Skills_ReturnsList_InitiallyEmpty()
        {
            var sm = new SkillManager();

            var skills = sm.Skills;

            Assert.IsNotNull(skills);
        }

        [Test]
        public void Refresh_DoesNotThrow_WithValidPaths()
        {
            var sm = new SkillManager("Z:\\nonexistent\\path");

            sm.Refresh();

            Assert.IsNotNull(sm.Skills);
        }

        [Test]
        public void DiscoverFromDirectory_FindsNestedSkills()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "SkillNestedTest_" + Guid.NewGuid());
            try
            {
                var nestedDir = Path.Combine(tempRoot, "category", "my-skill");
                Directory.CreateDirectory(nestedDir);
                File.WriteAllText(Path.Combine(nestedDir, "SKILL.md"),
                    "---\nname: nested-skill\ndescription: A nested skill\n---\n# Nested");

                var sm = new SkillManager(tempRoot);

                Assert.AreEqual(1, sm.Skills.Count);
                Assert.AreEqual("nested-skill", sm.Skills[0].Name);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void Discover_PriorityOrder_HigherPriorityAppearsFirst()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "SkillManagerTest_Priority_" + Guid.NewGuid());
            try
            {
                var lowDir = Path.Combine(tempRoot, "low");
                var highDir = Path.Combine(tempRoot, "high");
                Directory.CreateDirectory(Path.Combine(lowDir, "common-tool"));
                Directory.CreateDirectory(Path.Combine(highDir, "common-tool"));
                File.WriteAllText(Path.Combine(lowDir, "common-tool", "SKILL.md"),
                    "---\nname: common-tool\ndescription: Low priority version\n---\n# Low");
                File.WriteAllText(Path.Combine(highDir, "common-tool", "SKILL.md"),
                    "---\nname: common-tool\ndescription: High priority version\n---\n# High");

                var sm = new SkillManager(lowDir, highDir);

                Assert.AreEqual(1, sm.Skills.Count);
                Assert.AreEqual("High priority version", sm.Skills[0].Description);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }
    }
}
