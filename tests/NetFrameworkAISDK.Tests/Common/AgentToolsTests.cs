using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace NetFrameworkAISDK.Tests.Common
{
    [TestFixture]
    public class AgentToolsTests
    {
        private AIFunction FindTool(string name)
        {
            var tools = AgentTools.CreateDefaultTools();
            foreach (var t in tools)
            {
                if (t.Name == name) { return t; }
            }
            return null;
        }

        [Test]
        public void CreateDefaultTools_ReturnsAllDiscoveredTools()
        {
            var tools = AgentTools.CreateDefaultTools();

            Assert.IsNotNull(tools);
            Assert.AreEqual(13, tools.Count);

            var names = new List<string>();
            foreach (var t in tools)
            {
                names.Add(t.Name);
            }

            Assert.Contains("ReadFile", names);
            Assert.Contains("WriteFile", names);
            Assert.Contains("ListDirectory", names);
            Assert.Contains("Grep", names);
            Assert.Contains("Glob", names);
            Assert.Contains("DeleteFile", names);
            Assert.Contains("MakeDirectory", names);
            Assert.Contains("RenameFile", names);
            Assert.Contains("GetFileInfo", names);
            Assert.Contains("CopyFile", names);
            Assert.Contains("MoveFile", names);
            Assert.Contains("GetEnvironmentVariable", names);
            Assert.Contains("RunCommand", names);
        }

        [Test]
        public void ReadFileTool_HasCorrectStructure()
        {
            var tool = FindTool("ReadFile");
            Assert.IsNotNull(tool, "ReadFile tool should be discovered");
            Assert.AreEqual("ReadFile", tool.Name);
            Assert.IsTrue(tool.Description.Contains("contents of a file"));
            Assert.IsNotNull(tool.Parameters);
            Assert.IsNotNull(tool.Execute);
        }

        [Test]
        public void WriteFileTool_HasCorrectStructure()
        {
            var tool = FindTool("WriteFile");
            Assert.IsNotNull(tool, "WriteFile tool should be discovered");
            Assert.IsTrue(tool.Description.Contains("Write content to a file"));
            Assert.IsNotNull(tool.Parameters);
            Assert.IsNotNull(tool.Execute);
        }

        [Test]
        public void ListDirectoryTool_HasCorrectStructure()
        {
            var tool = FindTool("ListDirectory");
            Assert.IsNotNull(tool, "ListDirectory tool should be discovered");
            Assert.IsTrue(tool.Description.Contains("List files"));
            Assert.IsNotNull(tool.Parameters);
            Assert.IsNotNull(tool.Execute);
        }

        [Test]
        public void GrepTool_HasCorrectStructure()
        {
            var tool = FindTool("Grep");
            Assert.IsNotNull(tool, "Grep tool should be discovered");
            Assert.IsTrue(tool.Description.Contains("Search"));
            Assert.IsNotNull(tool.Parameters);
            Assert.IsNotNull(tool.Execute);
        }

        [Test]
        public void GlobTool_HasCorrectStructure()
        {
            var tool = FindTool("Glob");
            Assert.IsNotNull(tool, "Glob tool should be discovered");
            Assert.IsTrue(tool.Description.Contains("Find files"));
            Assert.IsNotNull(tool.Parameters);
            Assert.IsNotNull(tool.Execute);
        }

        [Test]
        public void DeleteFileTool_HasCorrectStructure()
        {
            var tool = FindTool("DeleteFile");
            Assert.IsNotNull(tool, "DeleteFile tool should be discovered");
            Assert.IsTrue(tool.Description.Contains("Delete"));
            Assert.IsNotNull(tool.Parameters);
            Assert.IsNotNull(tool.Execute);
        }

        [Test]
        public void MakeDirectoryTool_HasCorrectStructure()
        {
            var tool = FindTool("MakeDirectory");
            Assert.IsNotNull(tool, "MakeDirectory tool should be discovered");
            Assert.IsTrue(tool.Description.Contains("Create a directory"));
            Assert.IsNotNull(tool.Parameters);
            Assert.IsNotNull(tool.Execute);
        }

        [Test]
        public void ReadFileTool_WithEmptyPath_ReturnsError()
        {
            var tool = FindTool("ReadFile");
            Assert.IsNotNull(tool);

            var result = tool.Execute("{\"path\":\"\"}");
            Assert.IsTrue(result.Contains("Error"));
        }

        [Test]
        public void Grep_WithAccessibleFiles_FindsMatches()
        {
            var tool = FindTool("Grep");
            Assert.IsNotNull(tool);

            var tempDir = Path.Combine(Path.GetTempPath(), "GrepTest_" + Guid.NewGuid());
            try
            {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello world\nfoo bar");
                File.WriteAllText(Path.Combine(tempDir, "test2.txt"), "hello again");

                var result = tool.Execute(
                    "{\"pattern\":\"hello\",\"path\":\"" + tempDir.Replace("\\", "\\\\") + "\"}");

                Assert.IsTrue(result.Contains("test.txt"));
                Assert.IsTrue(result.Contains("test2.txt"));
                Assert.IsFalse(result.Contains("Error searching"));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
