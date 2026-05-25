using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Tests.Common
{
    [TestFixture]
    public class AIAgentTests
    {
        /// <summary>
        /// C# 5.0 兼容的模拟 IAIClient，使用 Func 委托实现多步行为模拟
        /// </summary>
        private class MockAIClient : IAIClient
        {
            public List<ConversationMessage> LastMessages;
            public ConversationOptions LastOptions;
            public string MockResponseContent = "Mock response";
            public List<ToolCallRequest> MockToolCalls;
            public ApiError MockError;
            public bool WasDisposed;
            public int CallCount;

            public Func<List<ConversationMessage>, ConversationOptions, ApiResponse<ConversationResponse>>
                OnSendConversation;

            public ApiResponse<ConversationResponse> SendConversation(
                List<ConversationMessage> messages, ConversationOptions options)
            {
                LastMessages = messages;
                LastOptions = options;
                CallCount++;

                if (OnSendConversation != null)
                {
                    return OnSendConversation(messages, options);
                }

                if (MockError != null)
                    return new ApiResponse<ConversationResponse> { Error = MockError };
                return new ApiResponse<ConversationResponse>
                {
                    Result = new ConversationResponse
                    {
                        Content = MockResponseContent,
                        ToolCalls = MockToolCalls
                    }
                };
            }

            public void SendConversationStreaming(
                List<ConversationMessage> messages, ConversationOptions options,
                Action<ConversationResponse> onChunk, Action<ApiError> onError)
            {
                LastMessages = messages;
                LastOptions = options;
                if (MockError != null) { onError(MockError); return; }
                if (MockToolCalls != null && MockToolCalls.Count > 0)
                {
                    onChunk(new ConversationResponse
                    {
                        Content = MockResponseContent,
                        ToolCalls = MockToolCalls
                    });
                }
                else
                {
                    onChunk(new ConversationResponse { Content = MockResponseContent });
                }
            }

            public void ConfigureTools(IEnumerable<AIFunction> tools) { }
            public void Dispose() { WasDisposed = true; }
        }

        [Test]
        public void Run_SimpleQuery_ReturnsResponseContent()
        {
            var mock = new MockAIClient();
            var agent = new AIAgent(mock, "test-model", "You are helpful.", null, false, null);

            var response = agent.Run("Hello");

            Assert.IsTrue(response.IsSuccess);
            Assert.AreEqual("Mock response", response.Result);
        }

        [Test]
        public void Run_ClientReturnsHttpError_PropagatesError()
        {
            var mock = new MockAIClient
            {
                MockError = new ApiError("HTTP 500")
            };
            var agent = new AIAgent(mock, "test-model", "You are helpful.", null, false, null);

            var response = agent.Run("Hello");

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual("HTTP 500", response.Error.Message);
        }

        [Test]
        public void Run_WithToolCall_ExecutesToolAndReturnsFinalResponse()
        {
            var mock = new MockAIClient();
            mock.OnSendConversation = delegate(List<ConversationMessage> messages, ConversationOptions options)
            {
                if (mock.CallCount == 1)
                {
                    return new ApiResponse<ConversationResponse>
                    {
                        Result = new ConversationResponse
                        {
                            Content = null,
                            ToolCalls = new List<ToolCallRequest>
                            {
                                new ToolCallRequest
                                {
                                    Id = "call_1",
                                    FunctionName = "test_tool",
                                    FunctionArguments = "{\"input\":\"world\"}"
                                }
                            }
                        }
                    };
                }
                return new ApiResponse<ConversationResponse>
                {
                    Result = new ConversationResponse { Content = "Done." }
                };
            };
            var toolFunc = AIFunction.Create(
                new Func<string>(delegate() { return "Tool result"; }), "Test tool", "test_tool");
            var agent = new AIAgent(mock, "test-model", "System.",
                new[] { toolFunc }, false, null);

            var response = agent.Run("Do something");

            Assert.IsTrue(response.IsSuccess);
            Assert.AreEqual("Done.", response.Result);
            Assert.AreEqual(2, mock.CallCount);
        }

        [Test]
        public void Run_MaxIterationsExceeded_ReturnsLastContent()
        {
            var mock = new MockAIClient
            {
                MockToolCalls = new List<ToolCallRequest>
                {
                    new ToolCallRequest
                    {
                        Id = "call_1",
                        FunctionName = "loop_tool",
                        FunctionArguments = "{}"
                    }
                }
            };
            var loopTool = AIFunction.Create(
                new Func<string>(delegate() { return "looping"; }), "Loops forever", "loop_tool");
            var agent = new AIAgent(mock, "test-model", "System.",
                new[] { loopTool }, false, null);
            agent.MaxIterations = 1;

            var response = agent.Run("Start");

            Assert.IsTrue(response.IsSuccess);
        }

        [Test]
        public void ClearHistory_EmptiesConversationHistory()
        {
            var mock = new MockAIClient();
            var agent = new AIAgent(mock, "test-model", "System.", null, false, null);
            agent.Run("Hello");

            agent.ClearHistory();

            var history = agent.GetHistory();
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void SetModel_UpdatesOptionsModel()
        {
            var mock = new MockAIClient();
            var agent = new AIAgent(mock, "model-v1", "System.", null, false, null);

            agent.SetModel("model-v2");
            agent.Run("Hello");

            Assert.AreEqual("model-v2", mock.LastOptions.Model);
        }

        [Test]
        public void AddTool_DynamicTool_BecomesCallable()
        {
            var mock = new MockAIClient();
            mock.OnSendConversation = delegate(List<ConversationMessage> messages, ConversationOptions options)
            {
                if (mock.CallCount == 1)
                {
                    return new ApiResponse<ConversationResponse>
                    {
                        Result = new ConversationResponse
                        {
                            Content = null,
                            ToolCalls = new List<ToolCallRequest>
                            {
                                new ToolCallRequest
                                {
                                    Id = "call_dynamic",
                                    FunctionName = "dynamic_tool",
                                    FunctionArguments = "{}"
                                }
                            }
                        }
                    };
                }
                return new ApiResponse<ConversationResponse>
                {
                    Result = new ConversationResponse { Content = "After dynamic tool." }
                };
            };

            var agent = new AIAgent(mock, "test-model", "System.", null, false, null);
            var dynTool = AIFunction.Create(
                new Func<string>(delegate() { return "dynamic result"; }), "Dynamic tool", "dynamic_tool");
            agent.AddTool(dynTool);

            var response = agent.Run("Trigger");

            Assert.IsTrue(response.IsSuccess);
            Assert.AreEqual("After dynamic tool.", response.Result);
            Assert.AreEqual(2, mock.CallCount);
        }

        [Test]
        public void AgentLoop_WithToolApproval_Rejected()
        {
            var mock = new MockAIClient();
            mock.OnSendConversation = delegate(List<ConversationMessage> messages, ConversationOptions options)
            {
                return new ApiResponse<ConversationResponse>
                {
                    Result = new ConversationResponse
                    {
                        Content = null,
                        ToolCalls = new List<ToolCallRequest>
                        {
                            new ToolCallRequest
                            {
                                Id = "call_1",
                                FunctionName = "dangerous_tool",
                                FunctionArguments = "{}"
                            }
                        }
                    }
                };
            };
            var toolFunc = AIFunction.Create(
                new Func<string>(delegate() { return "executed"; }), "Dangerous tool", "dangerous_tool");
            toolFunc.RequiresApproval = true;
            var agent = new AIAgent(mock, "test-model", "System.",
                new[] { toolFunc }, false, null);
            agent.ToolApproval = delegate(ToolCallEventArgs args) { return false; };

            var toolCallLog = new List<ToolCallEventArgs>();
            var response = agent.Run("Do it", delegate(ToolCallEventArgs args) { toolCallLog.Add(args); });

            Assert.IsTrue(response.IsSuccess);
            // 即使被拒绝，onToolCall 回调也应该被调用
            Assert.AreEqual(1, toolCallLog.Count);
            Assert.IsFalse(toolCallLog[0].IsApproved);
            Assert.AreEqual("[REJECTED]", toolCallLog[0].Result);
        }
    }
}
