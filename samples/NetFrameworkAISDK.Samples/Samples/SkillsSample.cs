using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using NetFrameworkAISDK.Anthropic;
using System;
using System.Collections.Generic;
using System.IO;

namespace NetFrameworkAISDK.Samples
{
    public class SkillsSample : ISample
    {
        public string Name
        {
            get { return "Agent Skills (MAF style) + Common Tools"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates the MAF-style progressive disclosure pattern.");
            Console.WriteLine("Skills are NEVER fully loaded into system prompt.");
            Console.WriteLine("Only catalog (name + description) is injected. Full content loads on demand.");
            Console.WriteLine("------------------------------------------------------------------------");
            Console.WriteLine();
            Console.WriteLine("=== Protocol ===");
            Console.WriteLine("  1. System prompt: <available_skills> with names + descriptions only");
            Console.WriteLine("  2. Tools injected: load_skill, read_file, write_file, grep, ...");
            Console.WriteLine("  3. LLM calls load_skill('xxx') -> gets full instructions");
            Console.WriteLine("  4. LLM calls file tools -> reads/writes files as needed");
            Console.WriteLine("------------------------------------------------------------------------");

            Console.WriteLine("\nEnter the skills directory path.");
            Console.WriteLine("  Example: C:/Users/YourName/.agents/skills");
            Console.Write("Skills Directory: ");
            string directoryPath = Console.ReadLine();

            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                Console.WriteLine("Directory does not exist. Skipping sample.");
                return;
            }

            Console.WriteLine("\nStep 1: Discover Skills");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("SkillManager scans for directories containing SKILL.md.");
            var skills = SkillManager.DiscoverSkills(directoryPath);

            if (skills == null || skills.Count == 0)
            {
                Console.WriteLine("No skills found. Using demo mode with skill catalog only.");
                Console.WriteLine("Create SKILL.md files to see them here.");
            }
            else
            {
                Console.WriteLine("Found " + skills.Count + " skill(s):");
                int idx = 1;
                foreach (var skill in skills)
                {
                    Console.WriteLine("  [" + idx + "] " + skill.Name + ": " + skill.Description);
                    idx++;
                }
            }

            Console.WriteLine("\nStep 2: Build Progressive Prompt (catalog only, NO full bodies)");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("BuildProgressivePrompt() generates XML catalog:");
            string prompt = SkillManager.BuildProgressivePrompt(skills);
            Console.WriteLine(prompt);

            Console.WriteLine("\nStep 3: Common Agent Tools");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("AgentTools.CreateDefaultTools() provides these tools:");
            Console.WriteLine("  - ReadFile(path)          Read file contents");
            Console.WriteLine("  - WriteFile(path,content) Write file contents");
            Console.WriteLine("  - ListDirectory(path)     List directory entries");
            Console.WriteLine("  - Grep(pattern,path)      Search text with regex");
            Console.WriteLine("  - Glob(pattern)           Find files by pattern");
            Console.WriteLine("  - DeleteFile(path)        Delete a file");
            Console.WriteLine("  - MakeDirectory(path)     Create directory");
            Console.WriteLine("  - RenameFile(old,new)     Rename file/directory");
            Console.WriteLine("  - GetFileInfo(path)       Get file information");
            Console.WriteLine("  - CopyFile(src,dest)      Copy file");
            Console.WriteLine("  - MoveFile(src,dest)      Move file");
            Console.WriteLine("  - GetEnvironmentVariable  Get env variable");
            Console.WriteLine("  - RunCommand(cmd)         Execute shell command");
            Console.WriteLine();
            Console.WriteLine("These tools give the LLM file system access, like Claude Code / Codex CLI.");

            Console.WriteLine("\nStep 4: Test with Provider");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("Available options:");
            Console.WriteLine("  1. OpenAI (progresssive + load_skill + common tools)");
            Console.WriteLine("  2. Anthropic (system parameter + load_skill + common tools)");

            if (skills.Count > 0)
            {
                Console.WriteLine("  3. Show skill-specific 'read_skill' tool");
            }

            Console.WriteLine("  0. Skip");
            Console.Write("\nYour choice: ");
            string providerChoice = Console.ReadLine();

            if (providerChoice == "1")
            {
                TestWithOpenAI(skills, prompt);
            }
            else if (providerChoice == "2")
            {
                TestWithAnthropic(skills, prompt);
            }
            else if (providerChoice == "3" && skills.Count > 0)
            {
                TestReadSkill(skills);
            }
            else
            {
                Console.WriteLine("\nSkipping provider test.");
            }
        }

        private void TestWithOpenAI(List<SkillInfo> skills, string catalogPrompt)
        {
            var config = SampleConfig.ReadFromConsole("OpenAI", "https://api.openai.com/v1", "gpt-4o",
                includeSystemPrompt: true);
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping OpenAI test.");
                return;
            }

            OpenAIClient client;
            if (!string.IsNullOrEmpty(config.BaseUrl))
            {
                client = new OpenAIClient(config.ApiKey, config.BaseUrl);
            }
            else
            {
                client = new OpenAIClient(config.ApiKey);
            }

            string instructions = catalogPrompt;
            if (!string.IsNullOrEmpty(config.SystemPrompt))
            {
                instructions = config.SystemPrompt + "\n\n" + catalogPrompt;
            }

            var tools = AgentTools.CreateDefaultTools();
            tools.Add(SkillManager.CreateLoadSkillFunction(skills));

            var agent = new Common.AIAgent(client, config.Model, instructions, tools);

            Console.WriteLine("\nProgressive disclosure mode. Tools available:");
            Console.WriteLine("  read_file, write_file, list_directory, grep, glob, load_skill");
            Console.WriteLine("\nEnter your message (type 'exit' to quit):");
            Console.Write("You: ");
            string userInput = Console.ReadLine();

            while (userInput != "exit")
            {
                Console.Write("\nAssistant: ");

                var response = agent.Run(userInput, onToolCall: (e) =>
                {
                    Console.WriteLine("\n>>> Tool: " + e.FunctionName);
                    Console.WriteLine(">>> Args: " + e.FunctionArguments);
                    Console.WriteLine(">>> Result: " + e.Result);
                });

                if (response.IsSuccess)
                {
                    Console.WriteLine(response.Result);
                }
                else
                {
                    Console.WriteLine("Error: " + response.Error.Message);
                }

                Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                Console.Write("You: ");
                userInput = Console.ReadLine();
            }
        }

        private void TestWithAnthropic(List<SkillInfo> skills, string catalogPrompt)
        {
            var config = SampleConfig.ReadFromConsole("Anthropic", "https://api.anthropic.com/v1",
                "claude-3-sonnet-20240229", 1024, null, true);
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping Anthropic test.");
                return;
            }

            AnthropicClient client;
            if (!string.IsNullOrEmpty(config.BaseUrl))
            {
                client = new AnthropicClient(config.ApiKey, config.BaseUrl);
            }
            else
            {
                client = new AnthropicClient(config.ApiKey);
            }

            string instructions = catalogPrompt;
            if (!string.IsNullOrEmpty(config.SystemPrompt))
            {
                instructions = config.SystemPrompt + "\n\n" + catalogPrompt;
            }

            Console.WriteLine("\nProgressive disclosure mode. Skills catalog in system parameter.");
            Console.WriteLine("Note: Tool calling for Anthropic requires building ToolDefinition manually.");
            Console.WriteLine("Enter your message (type 'exit' to quit):");
            Console.Write("You: ");
            string userInput = Console.ReadLine();

            while (userInput != "exit")
            {
                var messages = new List<AnthropicMessage>
                {
                    new AnthropicMessage { Role = AnthropicRole.User, Content = userInput }
                };

                Console.Write("\nAssistant: ");

                var response = client.CreateMessage(
                    config.Model,
                    messages,
                    config.MaxTokens,
                    string.IsNullOrEmpty(instructions) ? null : instructions,
                    config.Temperature);

                if (response.IsSuccess)
                {
                    if (response.Result.Content != null && response.Result.Content.Count > 0)
                    {
                        Console.WriteLine(response.Result.Content[0].Text);
                    }
                }
                else
                {
                    Console.WriteLine("Error: " + response.Error.Message);
                }

                Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                Console.Write("You: ");
                userInput = Console.ReadLine();
            }
        }

        private void TestReadSkill(List<SkillInfo> skills)
        {
            Console.WriteLine("\nAvailable skills:");
            for (int i = 0; i < skills.Count; i++)
            {
                Console.WriteLine("  [" + (i + 1) + "] " + skills[i].Name);
            }

            Console.Write("\nEnter number to read skill content: ");
            string choice = Console.ReadLine();

            int idx;
            if (int.TryParse(choice, out idx) && idx > 0 && idx <= skills.Count)
            {
                var skill = skills[idx - 1];
                Console.WriteLine("\n" + skill.Name + " - " + skill.Description);
                Console.WriteLine("----------------------------------------------------------------");
                string content = File.ReadAllText(skill.SkillFilePath);
                Console.WriteLine(content);
            }
        }
    }
}