// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusLoggingMiddlewareTestFixture.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, softwareUseCases
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Middleware
{
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Middleware;
    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.Logging;

    using Moq;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusLoggingMiddleware"/> class
    /// </summary>
    [TestFixture]
    public class ArgusLoggingMiddlewareTestFixture
    {
        private Mock<ILogger<ArgusLoggingMiddleware>> mockLogger;

        private ArgusLoggingMiddleware middleware;

        [SetUp]
        public void SetUp()
        {
            this.mockLogger = new Mock<ILogger<ArgusLoggingMiddleware>>();
            this.middleware = new ArgusLoggingMiddleware(this.mockLogger.Object);
        }

        [Test]
        public async Task Verify_that_middleware_calls_next()
        {
            var nextCalled = false;

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/test"
            };

            var context = new ArgusContext(request, CancellationToken.None);

            await this.middleware.InvokeAsync(context, ctx =>
            {
                nextCalled = true;

                ctx.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            Assert.That(nextCalled, Is.True);
        }

        [Test]
        public async Task Verify_that_middleware_logs_request_and_response()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/items"
            };

            var context = new ArgusContext(request, CancellationToken.None);

            await this.middleware.InvokeAsync(context, ctx =>
            {
                ctx.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Created
                };

                return Task.CompletedTask;
            });

            this.mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("ARGUS POST /items")),
                    null,
                    It.IsAny<System.Func<It.IsAnyType, System.Exception, string>>()),
                Times.Exactly(2));
        }

        [Test]
        public async Task Verify_that_middleware_logs_correlation_token()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/test"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            var expectedToken = context.CorrelationToken.ToString();

            await this.middleware.InvokeAsync(context, ctx =>
            {
                ctx.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            this.mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains(expectedToken)),
                    null,
                    It.IsAny<System.Func<It.IsAnyType, System.Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task Verify_that_middleware_integrates_with_router_pipeline()
        {
            var router = new ArgusRouter();
            router.UseMiddleware(this.middleware);

            router.MapGet("/resource", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));

            this.mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<System.Func<It.IsAnyType, System.Exception, string>>()),
                Times.Exactly(2));
        }
    }
}
