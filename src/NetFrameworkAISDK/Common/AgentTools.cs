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
        private static readonly ILogger _logger = new ConsoleLogger();

        /// <summary>
        /// 允许访问的根目录路径。设置后，所有文件操作都将限制在此目录及其子目录内。
        /// 设为 null 表示不限制（默认）。
        /// </summary>
        public static string AllowedRootPath { get; set; }

        /// <summary>
        /// 验证路径是否在允许范围内
        /// </summary>
        private static bool IsPathAllowed(string path)
        {
            if (string.IsNullOrEmpty(AllowedRootPath))
            {
                return true;
            }
            try
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                string fullRoot = System.IO.Path.GetFullPath(AllowedRootPath);
                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ValidatePath(string path)
        {
            if (!IsPathAllowed(path))
            {
                return "Error: Path is outside allowed directory: " + (AllowedRootPath ?? "(not set)");
            }
            return null;
        }

        [Description("Read the contents of a file at the given path")]
        private static string ReadFile([Description("Absolute or relative path to the file")] string path)
        {
            if (string.IsNullOrEmpty(path)) { return "Error: path is required."; }
            string pathError = ValidatePath(path);
            if (pathError != null) { return pathError; }

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
            string pathError = ValidatePath(path);
            if (pathError != null) { return pathError; }

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
            string pathError = ValidatePath(path);
            if (pathError != null) { return pathError; }

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

                // 展开花括号：{a,b} → 多个独立模式
                var patterns = ExpandBraces(pattern);

                foreach (var expandedPattern in patterns)
                {
                    if (matchCount >= maxMatches) { break; }

                    var normalized = expandedPattern.Replace('/', '\\');

                    if (normalized.Contains("**"))
                    {
                        // 多段 ** 模式：按 ** 拆分为目录段 + 最终文件模式
                        CollectWithMultiStar(searchPath, normalized, result, ref matchCount, maxMatches);
                    }
                    else
                    {
                        // 无 **：在当前目录单层匹配
                        CollectFlat(searchPath, normalized, result, ref matchCount, maxMatches);
                    }
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

        /// <summary>
        /// 展开 {a,b,c} 花括号模式为多个独立模式
        /// </summary>
        private static List<string> ExpandBraces(string pattern)
        {
            var results = new List<string>();

            int openBrace = pattern.IndexOf('{');
            if (openBrace < 0)
            {
                results.Add(pattern);
                return results;
            }

            int closeBrace = pattern.IndexOf('}', openBrace);
            if (closeBrace < 0)
            {
                results.Add(pattern);
                return results;
            }

            string prefix = pattern.Substring(0, openBrace);
            string suffix = pattern.Substring(closeBrace + 1);
            string braceContent = pattern.Substring(openBrace + 1, closeBrace - openBrace - 1);

            var options = braceContent.Split(',');
            foreach (var option in options)
            {
                var trimmed = option.Trim();
                if (trimmed.Length > 0)
                {
                    var combined = prefix + trimmed + suffix;
                    // 递归展开嵌套花括号
                    var nested = ExpandBraces(combined);
                    results.AddRange(nested);
                }
            }

            return results;
        }

        /// <summary>
        /// 按 ** 拆分后递归遍历目录树，匹配最终文件模式
        /// </summary>
        private static void CollectWithMultiStar(string basePath, string pattern, StringBuilder result, ref int count, int max)
        {
            // 按 ** 分割：第一段是目录前缀，后续段是递归目录 + 文件模式
            int firstStar = pattern.IndexOf("**");
            string prefix = "";
            string remainder = pattern;

            if (firstStar > 0)
            {
                prefix = pattern.Substring(0, firstStar).TrimEnd('\\');
                remainder = pattern.Substring(firstStar + 2).TrimStart('\\');
            }
            else if (firstStar == 0)
            {
                remainder = pattern.Substring(2).TrimStart('\\');
            }

            string searchRoot = string.IsNullOrEmpty(prefix)
                ? basePath
                : System.IO.Path.Combine(basePath, prefix);

            if (!Directory.Exists(searchRoot))
            {
                return;
            }

            // 如果剩余部分还有 **，递归遍历子目录
            if (remainder.Contains("**"))
            {
                WalkDirectoriesForMultiStar(searchRoot, remainder, result, ref count, max);
            }
            else
            {
                // 最后一层：在当前目录中匹配文件
                WalkDirectoriesForFile(searchRoot, remainder, result, ref count, max);
            }
        }

        /// <summary>
        /// 递归遍历目录，对每个子目录按剩余模式继续匹配
        /// </summary>
        private static void WalkDirectoriesForMultiStar(string root, string remainder, StringBuilder result, ref int count, int max)
        {
            // 先尝试在当前目录直接匹配
            if (Directory.Exists(root))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (count >= max) { return; }
                        result.AppendLine(file);
                        count++;
                    }
                }
                catch { }

                // 然后递归子目录
                try
                {
                    foreach (var subDir in Directory.EnumerateDirectories(root))
                    {
                        if (count >= max) { return; }
                        // 在每个子目录中继续处理余下的 ** 模式
                        int nextStar = remainder.IndexOf("**");
                        string afterStar = remainder.Substring(nextStar + 2).TrimStart('\\');
                        if (afterStar.Contains("**"))
                        {
                            WalkDirectoriesForMultiStar(subDir, afterStar, result, ref count, max);
                        }
                        else if (string.IsNullOrEmpty(afterStar) || afterStar == "*")
                        {
                            // 模式以 ** 结束，列出所有子文件
                            CollectAllFiles(subDir, result, ref count, max);
                        }
                        else
                        {
                            WalkDirectoriesForFile(subDir, afterStar, result, ref count, max);
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 在指定目录下递归搜索匹配文件模式的路径
        /// </summary>
        private static void WalkDirectoriesForFile(string root, string filePattern, StringBuilder result, ref int count, int max)
        {
            if (count >= max) { return; }
            if (!Directory.Exists(root)) { return; }

            try
            {
                foreach (var file in Directory.EnumerateFiles(root, filePattern, SearchOption.TopDirectoryOnly))
                {
                    if (count >= max) { return; }
                    result.AppendLine(file);
                    count++;
                }
            }
            catch { }

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(root))
                {
                    if (count >= max) { return; }
                    WalkDirectoriesForFile(subDir, filePattern, result, ref count, max);
                }
            }
            catch { }
        }

        /// <summary>
        /// 递归收集目录下的所有文件
        /// </summary>
        private static void CollectAllFiles(string root, StringBuilder result, ref int count, int max)
        {
            if (count >= max) { return; }
            if (!Directory.Exists(root)) { return; }

            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                {
                    if (count >= max) { return; }
                    result.AppendLine(file);
                    count++;
                }
            }
            catch { }

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(root))
                {
                    if (count >= max) { return; }
                    CollectAllFiles(subDir, result, ref count, max);
                }
            }
            catch { }
        }

        /// <summary>
        /// 在当前目录单层匹配（无 ** 递归）
        /// </summary>
        private static void CollectFlat(string basePath, string filePattern, StringBuilder result, ref int count, int max)
        {
            if (count >= max) { return; }
            if (!Directory.Exists(basePath)) { return; }

            try
            {
                foreach (var file in Directory.EnumerateFiles(basePath, filePattern, SearchOption.TopDirectoryOnly))
                {
                    if (count >= max) { return; }
                    result.AppendLine(file);
                    count++;
                }
            }
            catch { }
        }

        [Description("Delete a file at the given path")]
        private static string DeleteFile([Description("Absolute or relative path to the file")] string path)
        {
            if (string.IsNullOrEmpty(path)) { return "Error: path is required."; }
            string pathError = ValidatePath(path);
            if (pathError != null) { return pathError; }

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
            string pathError = ValidatePath(path);
            if (pathError != null) { return pathError; }

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
            string pathError = ValidatePath(sourcePath);
            if (pathError != null) { return pathError; }
            pathError = ValidatePath(destPath);
            if (pathError != null) { return pathError; }

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
            string pathError = ValidatePath(sourcePath);
            if (pathError != null) { return pathError; }
            pathError = ValidatePath(destPath);
            if (pathError != null) { return pathError; }

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

            // 仅阻止真正危险的命令注入字符：& | ;
            char[] unsafeChars = new char[] { '&', '|', ';' };
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
                    // 验证路径规范化后不包含路径穿越
                    string normalizedPath = workingDir;
                    string originalPath = workingDir;
                    // 简单的路径穿越检测
                    if (originalPath.Contains("..") && !normalizedPath.Contains(".."))
                    {
                        // 路径被规范化后不包含 ..，说明可能有穿越
                        return "Error: Working directory path contains traversal characters.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log("Invalid working directory path: " + ex.Message, "ERROR");
                    return "Error: Invalid working directory path.";
                }
            }

            try
            {
                _logger.Log(string.Format("Executing command: {0}", command), "INFO");

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

                    _logger.Log(string.Format("Command completed with exit code: {0}", process.ExitCode), "DEBUG");

                    if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                    {
                        return "Error (exit code " + process.ExitCode + "): " + error;
                    }
                    return output;
                }
            }
            catch (Exception ex)
            {
                _logger.Log("Error executing command: " + ex.Message, "ERROR");
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
