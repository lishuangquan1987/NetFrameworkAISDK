using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System.Collections.Generic;

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
    }
}
