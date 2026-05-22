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
            Console.WriteLine("  1. AIAgent auto-creates SkillManager and injects skill tools");
            Console.WriteLine("  2. System prompt: <available_skills> with names + descriptions only");
            Console.WriteLine("  3. Tools injected: load_skill, read_file, write_file, grep, ...");
            Console.WriteLine("  4. LLM calls load_skill('xxx') -> gets full instructions");
            Console.WriteLine("  5. Runtime: agent.SkillManager.AddDirectory() for dynamic skills");
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

            Console.WriteLine("\nStep 1: AIAgent auto-discovers and integrates skills");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("AIAgent creates SkillManager internally with skillsDirectories.");
            Console.WriteLine("Skill catalog auto-injected into system prompt.");

            Console.WriteLine("\nStep 2: Inspect via agent.SkillManager");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("BuildProgressivePrompt() generates XML catalog:");
            var sm = new SkillManager(directoryPath);
            var skills = sm.Skills;
            if (skills.Count > 0)
            {
                Console.WriteLine("Found " + skills.Count + " skill(s):");
                foreach (var skill in skills)
                {
                    Console.WriteLine("  " + skill.Name + ": " + skill.Description);
                }
            }
            else
            {
                Console.WriteLine("No skills found.");
            }
            Console.WriteLine();
            Console.WriteLine(sm.BuildProgressivePrompt());

            Console.WriteLine("\nStep 3: Common Agent Tools");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("AgentTools.CreateDefaultTools() provides file system tools.");
            Console.WriteLine("  read_file, write_file, list_directory, grep, glob, ...");

            Console.WriteLine("\nStep 4: Test with Provider");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("  1. OpenAI (auto-integration via AIAgent constructor)");
            Console.WriteLine("  2. Anthropic (auto-integration via AIAgent constructor)");

            if (skills.Count > 0)
            {
                Console.WriteLine("  3. Show skill 'read_skill' via agent.SkillManager");
            }

            Console.WriteLine("  0. Skip");
            Console.Write("\nYour choice: ");
            string providerChoice = Console.ReadLine();

            if (providerChoice == "1")
            {
                TestWithOpenAI(directoryPath, skills);
            }
            else if (providerChoice == "2")
            {
                TestWithAnthropic(directoryPath, skills);
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

        private void TestWithOpenAI(string directoryPath, List<SkillInfo> skills)
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

            string instructions = config.SystemPrompt ?? "You are a helpful assistant.";
            var agent = new Common.AIAgent(client, config.Model, instructions,
                AgentTools.CreateDefaultTools(), true,
                new string[] { directoryPath });

            Console.WriteLine("\nAIAgent created with skills auto-integrated.");
            Console.WriteLine("  agent.SkillManager access: " + (agent.SkillManager != null ? "available" : "null"));
            Console.WriteLine("  agent.SkillManager.Skills.Count: " + agent.SkillManager.Skills.Count);
            Console.WriteLine("  Runtime add: agent.SkillManager.AddDirectory(\"path\")");
            Console.WriteLine("  Runtime remove: agent.SkillManager.RemoveDirectory(\"path\")");
            Console.WriteLine("  Force refresh: agent.SkillManager.Refresh()");

            RunInteractiveLoop(agent);
        }

        private void TestWithAnthropic(string directoryPath, List<SkillInfo> skills)
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

            string instructions = config.SystemPrompt ?? "You are a helpful assistant.";
            var agent = new Common.AIAgent(client, config.Model, instructions,
                AgentTools.CreateDefaultTools(), true,
                new string[] { directoryPath });

            Console.WriteLine("\nAIAgent created with skills auto-integrated.");
            Console.WriteLine("  agent.SkillManager access: " + (agent.SkillManager != null ? "available" : "null"));

            RunInteractiveLoop(agent);
        }

        private void RunInteractiveLoop(AIAgent agent)
        {
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
