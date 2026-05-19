using NetFrameworkAI.Common;
using NUnit.Framework;

namespace NetFrameworkAI.Tests.Common
{
    [TestFixture]
    public class ApiErrorTests
    {
        [Test]
        public void ApiError_DefaultValues_AreCorrect()
        {
            var error = new ApiError();
            Assert.IsNull(error.Message);
            Assert.IsNull(error.Type);
            Assert.IsNull(error.HttpStatusCode);
        }

        [Test]
        public void ApiError_SetProperties_WorksCorrectly()
        {
            var error = new ApiError
            {
                Message = "Test error message",
                Type = "test_error",
                HttpStatusCode = 400
            };

            Assert.AreEqual("Test error message", error.Message);
            Assert.AreEqual("test_error", error.Type);
            Assert.AreEqual(400, error.HttpStatusCode);
        }

        [Test]
        public void ApiResponse_WithResult_IsSuccessIsTrue()
        {
            var response = new ApiResponse<string>
            {
                Result = "test result"
            };

            Assert.IsTrue(response.IsSuccess);
            Assert.IsNull(response.Error);
            Assert.AreEqual("test result", response.Result);
        }

        [Test]
        public void ApiResponse_WithError_IsSuccessIsFalse()
        {
            var response = new ApiResponse<string>
            {
                Error = new ApiError { Message = "error" }
            };

            Assert.IsFalse(response.IsSuccess);
            Assert.IsNotNull(response.Error);
        }
    }
}
