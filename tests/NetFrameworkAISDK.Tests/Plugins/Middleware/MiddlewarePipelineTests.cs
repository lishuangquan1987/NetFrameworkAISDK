using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetFrameworkAISDK.Plugins;
using NetFrameworkAISDK.Plugins.Middleware;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Tests.Plugins.Middleware
{
    [TestClass]
    public class MiddlewarePipelineTests
    {
        [TestMethod]
        public void TestMiddlewarePipelineExecution()
        {
            var pipeline = new MiddlewarePipeline();
            var executionLog = new List<string>();

            pipeline.Use(new TestMiddleware("Middleware1", 1, executionLog))
                   .Use(new TestMiddleware("Middleware2", 2, executionLog))
                   .Use(new TestMiddleware("Middleware3", 3, executionLog));

            var context = new AgentContext
            {
                UserMessage = "Test"
            };

            var result = pipeline.Execute(context, () =>
            {
                executionLog.Add("Handler");
                return new ApiResponse<string> { Result = "Success" };
            });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Success", result.Result);
            Assert.AreEqual(4, executionLog.Count);
            Assert.AreEqual("Middleware1", executionLog[0]);
            Assert.AreEqual("Middleware2", executionLog[1]);
            Assert.AreEqual("Middleware3", executionLog[2]);
            Assert.AreEqual("Handler", executionLog[3]);
        }

        [TestMethod]
        public void TestMiddlewarePipelineWithException()
        {
            var pipeline = new MiddlewarePipeline();
            var executionLog = new List<string>();

            pipeline.Use(new TestMiddleware("Middleware1", 1, executionLog));

            var context = new AgentContext
            {
                UserMessage = "Test"
            };

            var result = pipeline.Execute(context, () =>
            {
                throw new Exception("Test exception");
            });

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Error.Message.Contains("Test exception"));
        }

        [TestMethod]
        public void TestMiddlewarePipelineEmpty()
        {
            var pipeline = new MiddlewarePipeline();

            var context = new AgentContext
            {
                UserMessage = "Test"
            };

            var result = pipeline.Execute(context, () =>
            {
                return new ApiResponse<string> { Result = "Success" };
            });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Success", result.Result);
        }

        [TestMethod]
        public void TestMiddlewarePipelineRemove()
        {
            var pipeline = new MiddlewarePipeline();
            var executionLog = new List<string>();

            pipeline.Use(new TestMiddleware("Middleware1", 1, executionLog))
                   .Use(new TestMiddleware("Middleware2", 2, executionLog));

            Assert.AreEqual(2, pipeline.Count);

            pipeline.Remove("Middleware1");

            Assert.AreEqual(1, pipeline.Count);
        }

        [TestMethod]
        public void TestLoggingMiddleware()
        {
            var logMessages = new List<string>();
            var middleware = new LoggingMiddleware(msg => logMessages.Add(msg));

            var context = new AgentContext
            {
                UserMessage = "Test message"
            };

            var result = middleware.Invoke(context, () =>
            {
                return new ApiResponse<string> { Result = "Response" };
            });

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(logMessages.Count > 0);
        }

        [TestMethod]
        public void TestCachingMiddleware()
        {
            var middleware = new CachingMiddleware(30, 100);

            var context = new AgentContext
            {
                UserMessage = "Test message"
            };

            var callCount = 0;

            var result1 = middleware.Invoke(context, () =>
            {
                callCount++;
                return new ApiResponse<string> { Result = "Response 1" };
            });

            Assert.AreEqual(1, callCount);

            var result2 = middleware.Invoke(context, () =>
            {
                callCount++;
                return new ApiResponse<string> { Result = "Response 2" };
            });

            Assert.AreEqual(1, callCount);
            Assert.AreEqual("Response 1", result2.Result);

            middleware.ClearCache();
            Assert.AreEqual(0, middleware.GetCacheSize());
        }

        [TestMethod]
        public void TestRateLimitingMiddleware()
        {
            var middleware = new RateLimitingMiddleware(5, 100);

            var context = new AgentContext
            {
                UserMessage = "Test"
            };

            for (int i = 0; i < 5; i++)
            {
                var result = middleware.Invoke(context, () =>
                {
                    return new ApiResponse<string> { Result = "OK" };
                });
                Assert.IsTrue(result.IsSuccess);
            }

            var blocked = middleware.Invoke(context, () =>
            {
                return new ApiResponse<string> { Result = "OK" };
            });

            Assert.IsFalse(blocked.IsSuccess);
            Assert.IsTrue(blocked.Error.Message.Contains("Rate limit exceeded"));
        }

        [TestMethod]
        public void TestSecurityMiddleware()
        {
            var blockedPatterns = new List<string> { "badword" };
            var middleware = new SecurityMiddleware(true, false, blockedPatterns);

            var context = new AgentContext
            {
                UserMessage = "This contains badword"
            };

            var result = middleware.Invoke(context, () =>
            {
                return new ApiResponse<string> { Result = "Response" };
            });

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Error.Message.Contains("blocked"));
        }

        private class TestMiddleware : AgentMiddlewareBase
        {
            private readonly string _name;
            private readonly List<string> _log;

            public TestMiddleware(string name, int order, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public override string Name
            {
                get { return _name; }
            }

            public override int Order
            {
                get { return 0; }
            }

            public override ApiResponse<string> Invoke(AgentContext context, Func<ApiResponse<string>> next)
            {
                _log.Add(_name);
                return next();
            }
        }
    }
}
