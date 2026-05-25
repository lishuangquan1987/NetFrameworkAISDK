using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 内置 Agent 工具集，提供文件读写、目录操作、搜索、命令执行等常用功能。
    /// 所有工具通过 <see cref="CreateDefaultTools"/> 方法一次性注册，
    /// 自动发现所有标记 <see cref="System.ComponentModel.DescriptionAttribute"/> 的私有静态方法。
    /// </summary>
    public static class AgentTools
    {
        [Description("Read the contents of a file at the given path")]
        private static string ReadFile([Description("Absolute or relative path to the file")] string path)
        {
            if (string.IsNullOrEmpty(path)) { return "Error: path is required."; }

            try
            {
                if (!File.Exists(path))
                {
                    return "Error: File not found: " + path;
                }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                return "Error reading file: " + ex.Message;
            }
        }

        [Description("Write content to a file at the given path. Creates the file if it does not exist")]
        private static string WriteFile(
            [Description("Absolute or relative path to the file")] string path,
            [Description("Content to write to the file")] string content)
        {
            if (string.IsNullOrEmpty(path)) { return "Error: path is required."; }
            if (content == null) { return "Error: content is required."; }

            try
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, content);
                return "File written successfully: " + path;
            }
            catch (Exception ex)
            {
                return "Error writing file: " + ex.Message;
            }
        }

        [Description("List files and subdirectories in a directory")]
        private static string ListDirectory(
            [Description("Absolute path to the directory")] string path,
            [Description("Optional glob pattern to filter results (e.g. *.cs, *.md)")] string pattern = null)
        {
            if (string.IsNullOrEmpty(path)) { return "Error: path is required."; }

            try
            {
                if (!Directory.Exists(path))
                {
                    return "Error: Directory not found: " + path;
                }

                var result = new StringBuilder();
                if (string.IsNullOrEmpty(pattern))
                {
                    var dirs = Directory.GetDirectories(path);
                    foreach (var d in dirs)
                    {
                        result.AppendLine("[DIR] " + System.IO.Path.GetFileName(d));
                    }
                    var files = Directory.GetFiles(path);
                    foreach (var f in files)
                    {
                        var info = new FileInfo(f);
                        result.AppendLine("[FILE] " + info.Name + " (" + info.Length + " bytes)");
                    }
                }
                else
                {
                    var matches = System.IO.Directory.GetFiles(path, pattern);
                    foreach (var m in matches)
                    {
                        var info = new FileInfo(m);
                        result.AppendLine("[FILE] " + info.Name + " (" + info.Length + " bytes)");
                    }
                }

                return result.Length > 0 ? result.ToString() : "(empty directory)";
            }
            catch (Exception ex)
            {
                return "Error listing directory: " + ex.Message;
            }
        }

        [Description("Search for a pattern in files. Supports regex patterns. Returns matching file paths and line numbers")]
        private static string Grep(
            [Description("Search pattern (supports regex)")] string pattern,
            [Description("Directory or file to search in")] string path = null,
            [Description("Optional file filter (e.g. *.cs, *.py, *.json)")] string filePattern = null)
        {
            if (string.IsNullOrEmpty(pattern)) { return "Error: pattern is required."; }

            try
            {
                var searchPath = !string.IsNullOrEmpty(path) ? path : Environment.CurrentDirectory;
                var result = new StringBuilder();
                int matchCount = 0;
                int maxMatches = 100;
                int maxFilesToScan = 1000;
                int fileCount = 0;

                IEnumerable<string> files;
                if (!string.IsNullOrEmpty(filePattern))
                {
                    files = System.IO.Directory.EnumerateFiles(searchPath, filePattern, SearchOption.AllDirectories);
                }
                else if (File.Exists(searchPath))
                {
                    files = new string[] { searchPath };
                }
                else
                {
                    files = System.IO.Directory.EnumerateFiles(searchPath, "*", SearchOption.AllDirectories);
                }

                foreach (var file in files)
                {
                    if (fileCount++ >= maxFilesToScan)
                    {
                        result.AppendLine("Warning: Search stopped after scanning " + maxFilesToScan + " files.");
                        break;
                    }
                    if (matchCount >= maxMatches) { break; }
                    try
                    {
                        var lines = File.ReadAllLines(file);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (matchCount >= maxMatches) { break; }
                            try
                            {
                                if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], pattern))
                                {
                                    result.AppendLine(file + ":" + (i + 1) + ": " + lines[i].Trim());
                                    matchCount++;
                                }
                            }
                            catch (Exception regexEx)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    "Grep: Regex error on " + file + ":" + (i + 1) + " - " + regexEx.Message);
                            }
                        }
                    }
                    catch (Exception fileEx)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "Grep: Failed to read file " + file + ": " + fileEx.Message);
                    }
                }

                if (matchCount == 0)
                {
                    return "No matches found.";
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return "Error searching: " + ex.Message;
            }
        }

        [Description("Find files matching a glob pattern. Example patterns: **/*.cs, src/**/*.ts, *.md")]
        private static string Glob(
            [Description("Glob pattern to match files (e.g. **/*.cs, src/**/*.py)")] string pattern,
            [Description("Root directory to search from. Defaults to current directory")] string path = null)
        {
            if (string.IsNullOrEmpty(pattern)) { return "Error: pattern is required."; }

            try
            {
                var searchPath = !string.IsNullOrEmpty(path) ? path : Environment.CurrentDirectory;
                int matchCount = 0;
                int maxMatches = 200;
                var result = new StringBuilder();

                pattern = pattern.Replace('/', '\\');
                string searchRoot = searchPath;
                string filePattern = pattern;
                bool recurse = false;

                int starStarIndex = pattern.IndexOf("**");
                if (starStarIndex >= 0)
                {
                    recurse = true;
                    if (starStarIndex > 0)
                    {
                        string prefix = pattern.Substring(0, starStarIndex).TrimEnd('\\');
                        searchRoot = System.IO.Path.Combine(searchPath, prefix);
                    }
                    if (starStarIndex + 2 < pattern.Length)
                    {
                        filePattern = pattern.Substring(starStarIndex + 2).TrimStart('\\');
                    }
                    else
                    {
                        filePattern = "*";
                    }
                }

                if (!Directory.Exists(searchRoot))
                {
                    return "Error: Directory not found: " + searchRoot;
                }

                SearchOption searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var matches = System.IO.Directory.GetFiles(searchRoot, filePattern, searchOption);
                foreach (var f in matches)
                {
                    if (matchCount >= maxMatches) { break; }
                    result.AppendLine(f);
                    matchCount++;
                }

                if (matchCount == 0)
                {
                    return "No files found matching: " + pattern;
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return "Error globbing: " + ex.Message;
            }
        }

        [Description("Delete a file at the given path")]
        private static string DeleteFile([Description("Absolute or relative path to the file")] string path)
        {
            if (string.IsNullOrEmpty(path)) { return "Error: path is required."; }

            try
            {
                if (!File.Exists(path))
                {
                    return "Error: File not found: " + path;
                }
                File.Delete(path);
                return "File deleted successfully: " + path;
            }
            catch (Exception ex)
            {
                return "Error deleting file: " + ex.Message;
            }
        }

        [Description("Create a directory at the given path")]
        private static string MakeDirectory([Description("Absolute path to create directory")] string path)
        {
            if (string.IsNullOrEmpty(path)) { return "Error: path is required."; }

            try
            {
                if (Directory.Exists(path))
                {
                    return "Directory already exists: " + path;
                }
                Directory.CreateDirectory(path);
                return "Directory created successfully: " + path;
            }
            catch (Exception ex)
            {
                return "Error creating directory: " + ex.Message;
            }
        }

        [Description("Rename a file or directory")]
        private static string RenameFile(
            [Description("Current path of the file or directory")] string oldPath,
            [Description("New name (can be just the name or full path)")] string newName)
        {
            if (string.IsNullOrEmpty(oldPath)) { return "Error: oldPath is required."; }
            if (string.IsNullOrEmpty(newName)) { return "Error: newName is required."; }

            try
            {
                if (!File.Exists(oldPath) && !Directory.Exists(oldPath))
                {
                    return "Error: File or directory not found: " + oldPath;
                }

                string newPath = System.IO.Path.GetDirectoryName(oldPath);
                if (newPath == null)
                {
                    newPath = newName;
                }
                else
                {
                    newPath = System.IO.Path.Combine(newPath, newName);
                }

                if (File.Exists(oldPath))
                {
                    File.Move(oldPath, newPath);
                }
                else
                {
                    Directory.Move(oldPath, newPath);
                }
                return "Renamed successfully: " + oldPath + " -> " + newPath;
            }
            catch (Exception ex)
            {
                return "Error renaming: " + ex.Message;
            }
        }

        [Description("Get information about a file")]
        private static string GetFileInfo([Description("Path to the file")] string path)
        {
            if (string.IsNullOrEmpty(path)) { return "Error: path is required."; }

            try
            {
                if (!File.Exists(path))
                {
                    return "Error: File not found: " + path;
                }

                var info = new FileInfo(path);
                var result = new StringBuilder();
                result.AppendLine("File: " + info.FullName);
                result.AppendLine("Size: " + info.Length + " bytes");
                result.AppendLine("Created: " + info.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
                result.AppendLine("Last Modified: " + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                result.AppendLine("Last Accessed: " + info.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss"));
                result.AppendLine("Extension: " + info.Extension);
                result.AppendLine("Is Read Only: " + info.IsReadOnly);
                return result.ToString();
            }
            catch (Exception ex)
            {
                return "Error getting file info: " + ex.Message;
            }
        }

        [Description("Copy a file from source to destination")]
        private static string CopyFile(
            [Description("Source file path")] string sourcePath,
            [Description("Destination file path")] string destPath)
        {
            if (string.IsNullOrEmpty(sourcePath)) { return "Error: sourcePath is required."; }
            if (string.IsNullOrEmpty(destPath)) { return "Error: destPath is required."; }

            try
            {
                if (!File.Exists(sourcePath))
                {
                    return "Error: Source file not found: " + sourcePath;
                }

                File.Copy(sourcePath, destPath, true);
                return "File copied successfully: " + sourcePath + " -> " + destPath;
            }
            catch (Exception ex)
            {
                return "Error copying file: " + ex.Message;
            }
        }

        [Description("Move a file from source to destination")]
        private static string MoveFile(
            [Description("Source file path")] string sourcePath,
            [Description("Destination file path")] string destPath)
        {
            if (string.IsNullOrEmpty(sourcePath)) { return "Error: sourcePath is required."; }
            if (string.IsNullOrEmpty(destPath)) { return "Error: destPath is required."; }

            try
            {
                if (!File.Exists(sourcePath))
                {
                    return "Error: Source file not found: " + sourcePath;
                }

                File.Move(sourcePath, destPath);
                return "File moved successfully: " + sourcePath + " -> " + destPath;
            }
            catch (Exception ex)
            {
                return "Error moving file: " + ex.Message;
            }
        }

        [Description("Get the value of an environment variable")]
        private static string GetEnvironmentVariable([Description("Name of the environment variable")] string name)
        {
            if (string.IsNullOrEmpty(name)) { return "Error: name is required."; }

            try
            {
                string value = Environment.GetEnvironmentVariable(name);
                if (value == null)
                {
                    return "Environment variable not found: " + name;
                }
                return value;
            }
            catch (Exception ex)
            {
                return "Error getting environment variable: " + ex.Message;
            }
        }

        [Description("Execute a shell command and return the output")]
        private static string RunCommand(
            [Description("Command to execute")] string command,
            [Description("Working directory (optional)")] string workingDir = null)
        {
            if (string.IsNullOrEmpty(command)) { return "Error: command is required."; }

            if (command.Length > 2000)
            {
                return "Error: Command exceeds maximum length.";
            }

            char[] unsafeChars = new char[] { '&', '|', ';', '>', '<', '^', '`', '$', '%', '!', '(', ')', '@', '\t', '\r', '\n', '\0', '"', '\'', '\\', '/' };
            foreach (char c in unsafeChars)
            {
                if (command.IndexOf(c) >= 0)
                {
                    return "Error: Command contains unsafe characters.";
                }
            }

            if (!string.IsNullOrEmpty(workingDir))
            {
                try
                {
                    workingDir = System.IO.Path.GetFullPath(workingDir);
                    if (!System.IO.Directory.Exists(workingDir))
                    {
                        return "Error: Working directory does not exist: " + workingDir;
                    }
                    if (workingDir.Contains(".."))
                    {
                        return "Error: Working directory path contains traversal characters.";
                    }
                }
                catch (Exception)
                {
                    return "Error: Invalid working directory path.";
                }
            }

            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = "/c " + command;
                    if (!string.IsNullOrEmpty(workingDir))
                    {
                        process.StartInfo.WorkingDirectory = workingDir;
                    }
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                    {
                        return "Error (exit code " + process.ExitCode + "): " + error;
                    }
                    return output;
                }
            }
            catch (Exception ex)
            {
                return "Error executing command: " + ex.Message;
            }
        }

        public static List<AIFunction> CreateDefaultTools()
        {
            var tools = new List<AIFunction>();
            var methods = typeof(AgentTools).GetMethods(BindingFlags.NonPublic | BindingFlags.Static);

            foreach (var method in methods)
            {
                if (method.GetCustomAttributes(typeof(DescriptionAttribute), false).Length > 0)
                {
                    tools.Add(AIFunctionFactory.Create(method, null));
                }
            }

            return tools;
        }
    }
}
