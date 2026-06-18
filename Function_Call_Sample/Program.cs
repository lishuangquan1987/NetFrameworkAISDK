// See https://aka.ms/new-console-template for more information
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System.ComponentModel;

Console.WriteLine("Hello, World!");

string url = "https://u701357-b42c-d29bc5d1.westc.seetacloud.com:8443/v1";
string model = "Qwen3.6-35B-A3B-FP8";
string apiKey = "test";

[Description("获取一个目录下的所有文件")]
static string[] GetAllFiles([Description("目录名称")] string dir)
{
    return Directory.GetFiles(dir);
}

[Description("获取文件内容")]
static string GetFileContent([Description("文件路径")] string filePath)
{
    return File.ReadAllText(filePath);
}
var client = new OpenAIClient(apiKey, url);
var agent = new AIAgent(client, model, "你是一个专家，负责解答用户的提出的问题", new List<AIFunction>()
{
    AIFunctionFactory.Create(GetAllFiles),
    AIFunctionFactory.Create(GetFileContent)
});

while (true)
{
    Console.WriteLine("请输入问题：");
    var question = Console.ReadLine();
    var answer = agent.Run(question);
    if (answer.IsSuccess)
    {
        Console.WriteLine(answer.Result);
    }
    else
    {
        Console.WriteLine("错误：" + answer.Error.Message);
    }
}


