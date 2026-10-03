// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusExceptionHandlerMiddlewareTestFixture.cs">
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
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Middleware;
    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusExceptionHandlerMiddleware"/> class
    /// </summary>
    [TestFixture]
    public class ArgusExceptionHandlerMiddlewareTestFixture
    {
        private const string SecretMessage = "connection string Password=hunter2";

        private Mock<ILogger<ArgusExceptionHandlerMiddleware>> mockLogger;

        [SetUp]
        public void SetUp()
        {
            this.mockLogger = new Mock<ILogger<ArgusExceptionHandlerMiddleware>>();
        }

        private ArgusExceptionHandlerMiddleware CreateMiddleware(bool includeExceptionDetails = false)
        {
            return new ArgusExceptionHandlerMiddleware(
                this.mockLogger.Object,
                Options.Create(new ArgusExceptionHandlerOptions { IncludeExceptionDetails = includeExceptionDetails }));
        }

        private static ArgusContext CreateContext(CancellationToken requestAborted = default)
        {
            return new ArgusContext(new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items" }, requestAborted);
        }

        [Test]
        public void Verify_that_constructor_throws_for_null_logger()
        {
            Assert.That(() => new ArgusExceptionHandlerMiddleware(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public async Task Verify_that_response_is_unchanged_when_next_succeeds()
        {
            var middleware = this.CreateMiddleware();
            var context = CreateContext();
            var expected = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = "ok" };

            await middleware.InvokeAsync(context, ctx =>
            {
                ctx.Response = expected;
                return Task.CompletedTask;
            });

            Assert.That(context.Response, Is.SameAs(expected));
        }

        [Test]
        public async Task Verify_that_exception_is_converted_to_InternalServerError_problem_details()
        {
            var middleware = this.CreateMiddleware();
            var context = CreateContext();

            await middleware.InvokeAsync(context, _ => throw new InvalidOperationException(SecretMessage));

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(context.Response.CorrelationToken, Is.EqualTo(context.CorrelationToken));
            Assert.That(ArgusProblemDetails.TryRead(context.Response, out var problem), Is.True);
            Assert.That(problem.Status, Is.EqualTo(500));
            Assert.That(problem.Title, Is.EqualTo(ArgusStatusCode.InternalServerError.ToReasonPhrase()));
            Assert.That(problem.Detail, Is.EqualTo(ArgusExceptionHandlerMiddleware.GenericErrorDetail));
            Assert.That(problem.Instance, Is.EqualTo(context.CorrelationToken.ToString()));
        }

        [Test]
        public async Task Verify_that_exception_details_are_not_included_by_default()
        {
            var middleware = this.CreateMiddleware();
            var context = CreateContext();

            await middleware.InvokeAsync(context, _ => throw new InvalidOperationException(SecretMessage));

            Assert.That(context.Response.Body, Does.Not.Contain("hunter2"));
            Assert.That(context.Response.Body, Does.Not.Contain(nameof(InvalidOperationException)));
            Assert.That(ArgusProblemDetails.TryRead(context.Response, out var problem), Is.True);
            Assert.That(problem.Extensions, Is.Empty);
        }

        [Test]
        public async Task Verify_that_exception_details_are_included_when_enabled()
        {
            var middleware = this.CreateMiddleware(includeExceptionDetails: true);
            var context = CreateContext();

            await middleware.InvokeAsync(context, _ => throw new InvalidOperationException(SecretMessage));

            Assert.That(ArgusProblemDetails.TryRead(context.Response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo(SecretMessage));
            Assert.That(((JsonElement)problem.Extensions["exceptionType"]).GetString(), Is.EqualTo(typeof(InvalidOperationException).FullName));
            Assert.That(((JsonElement)problem.Extensions["exception"]).GetString(), Does.Contain(SecretMessage));
        }

        [Test]
        public async Task Verify_that_exception_is_logged_as_error()
        {
            var middleware = this.CreateMiddleware();
            var context = CreateContext();
            var exception = new InvalidOperationException(SecretMessage);

            await middleware.InvokeAsync(context, _ => throw exception);

            this.mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task Verify_that_response_set_before_the_exception_is_replaced()
        {
            var middleware = this.CreateMiddleware();
            var context = CreateContext();

            await middleware.InvokeAsync(context, ctx =>
            {
                ctx.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = "partial" };
                throw new InvalidOperationException("late failure");
            });

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
        }

        [Test]
        public async Task Verify_that_async_exception_is_handled()
        {
            var middleware = this.CreateMiddleware();
            var context = CreateContext();

            await middleware.InvokeAsync(context, async _ =>
            {
                await Task.Yield();
                throw new NotSupportedException("async failure");
            });

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
        }

        [Test]
        public void Verify_that_cancellation_of_RequestAborted_is_rethrown()
        {
            var middleware = this.CreateMiddleware();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var context = CreateContext(cts.Token);

            Assert.That(
                async () => await middleware.InvokeAsync(context, ctx => throw new OperationCanceledException(ctx.RequestAborted)),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(context.Response, Is.Null);
        }

        [Test]
        public async Task Verify_that_cancellation_unrelated_to_RequestAborted_is_handled()
        {
            var middleware = this.CreateMiddleware();
            var context = CreateContext();

            await middleware.InvokeAsync(context, _ => throw new TaskCanceledException("downstream call timed out"));

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
        }

        [Test]
        public async Task Verify_that_middleware_handles_exception_from_router_handler()
        {
            var router = new ArgusRouter();
            router.UseMiddleware(this.CreateMiddleware());
            router.MapGet("/boom", _ => throw new InvalidOperationException(SecretMessage));

            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/boom" };
            var context = new ArgusContext(request, CancellationToken.None);

            await router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(context.Response.CorrelationToken, Is.EqualTo(request.CorrelationToken));
            Assert.That(ArgusProblemDetails.TryRead(context.Response, out var problem), Is.True);
            Assert.That(problem.Instance, Is.EqualTo(request.CorrelationToken.ToString()));
        }

        [Test]
        public async Task Verify_that_HEAD_request_error_response_has_no_body()
        {
            var router = new ArgusRouter();
            router.UseMiddleware(this.CreateMiddleware());
            router.MapHead("/boom", _ => throw new InvalidOperationException(SecretMessage));

            var context = new ArgusContext(new ArgusRequest { Verb = ArgusVerb.HEAD, Route = "/boom" }, CancellationToken.None);

            await router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(context.Response.Body, Is.Null);
        }
    }
}
