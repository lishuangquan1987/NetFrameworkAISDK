using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;

Console.WriteLine("Hello, World!");

string dbPath = "E:\\Yofc\\Code\\OTDR\\OTDR3001\\YOFC.OTDR3001\\YOFC.OTDR3001\\bin\\Debug\\netcoreapp3.1-windows\\win-x64\\TestData\\otdr_data.db";
string url = "https://u701357-b42c-d29bc5d1.westc.seetacloud.com:8443/v1";
string model = "Qwen3.6-35B-A3B-FP8";
string apiKey = "test";


//{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}

// 使用 CopyToOutputDirectory 后的全路径
string exePath = Path.Combine(AppContext.BaseDirectory, "dbmcp.exe");
Console.WriteLine("MCP server: " + exePath);

//MCP 连接
var mcpClient = new McpClient();
var connectResult = mcpClient.Connect(exePath, "stdio --db-backend sqlite --db-name " + dbPath);
if (!connectResult.IsSuccess)
{
    Console.WriteLine("连接出错：" + connectResult.Error.Message);
    return;
}

//MCP 初始化
var initMcpResult = mcpClient.Initialize();
if (!initMcpResult.IsSuccess)
{
    Console.WriteLine("初始化出错：" + initMcpResult.Error.Message);
    return;
}

//查询可调用的方法
var listToolsResult = mcpClient.ListAsAIFunctions();
if (!listToolsResult.IsSuccess)
{
    Console.WriteLine("查询可调用的方法出错：" + listToolsResult.Error.Message);
    return;
}

Console.WriteLine("已发现可调用的方法:" + listToolsResult.Result.Count + "个");

//将方法注入到tools字段中
var client = new OpenAIClient(apiKey, url);
var agent = new AIAgent(client, model, "你是一个数据库专家，专为用户查询用户想要的数据", listToolsResult.Result);

while (true)
{
    Console.WriteLine("请输入问题：");
    var question = Console.ReadLine();
    if (string.IsNullOrEmpty(question)) break;
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
