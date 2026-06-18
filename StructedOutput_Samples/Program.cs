// See https://aka.ms/new-console-template for more information
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;

Console.WriteLine("Hello, World!");

string url = "https://u701357-b42c-d29bc5d1.westc.seetacloud.com:8443/v1";
string model = "Qwen3.6-35B-A3B-FP8";
string apiKey = "test";
var client = new OpenAIClient(apiKey, url);
var agent = new AIAgent(client, model, "你是一个信息提取助手，能够根据用户输入的内容智能提取关键字并填充到指定的json", null);

var result = agent.RunStructured<PersonInfo>(
    "张宇，男，1994 年 5 月出生，现居上海，手机号 138-1234-5678，邮箱是zhangyu@email.com。");

if (result.IsSuccess)
{
    Console.WriteLine("===============输出===================");
    Console.WriteLine(result.Result);
}
else
{
    Console.WriteLine($"出错了：{result.Error.Message}");
}
Console.ReadLine();


public class PersonInfo
{
    public string Name { get; set; }
    public Sex Sex { get; set; }
    public DateTime BirthDay { get; set; }
    public string Location { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }

    public override string ToString()
    {
        return $"""
            姓名：{Name},
            性别：{Sex.ToString()},
            生日：{BirthDay},
            现居：{Location},
            手机：{PhoneNumber},
            邮箱：{Email}
            """;
    }
}
public enum Sex
{
    男, 女
}