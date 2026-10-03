// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusAuthenticationMiddlewareTestFixture.cs">
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
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Authentication;
    using ArgusTransfer.Middleware;
    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using NUnit.Framework;

    /// <summary>
    /// A test <see cref="IArgusAuthenticationHandler"/> that accepts "Bearer secret" as user "alice",
    /// forbids "Bearer read-only", returns no result without an Authorization header and fails otherwise
    /// </summary>
    public class BearerTokenTestHandler : IArgusAuthenticationHandler
    {
        public Task<ArgusAuthenticationResult> AuthenticateAsync(ArgusContext context)
        {
            var request = context.Request;

            if (request.AuthorizationScheme == null)
            {
                return Task.FromResult(ArgusAuthenticationResult.NoResult());
            }

            if (!string.Equals(request.AuthorizationScheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ArgusAuthenticationResult.Fail("Unsupported authorization scheme."));
            }

            return Task.FromResult(request.AuthorizationParameter switch
            {
                "secret" => ArgusAuthenticationResult.Success(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "alice") }, "Bearer"))),
                "read-only" => ArgusAuthenticationResult.Forbidden(),
                _ => ArgusAuthenticationResult.Fail()
            });
        }
    }

    /// <summary>
    /// Suite of tests for the <see cref="ArgusAuthenticationMiddleware"/> class
    /// </summary>
    [TestFixture]
    public class ArgusAuthenticationMiddlewareTestFixture
    {
        private Mock<ILogger<ArgusAuthenticationMiddleware>> mockLogger;

        [SetUp]
        public void SetUp()
        {
            this.mockLogger = new Mock<ILogger<ArgusAuthenticationMiddleware>>();
        }

        private ArgusAuthenticationMiddleware CreateMiddleware(bool requireAuthentication = true, IArgusAuthenticationHandler handler = null)
        {
            return new ArgusAuthenticationMiddleware(
                handler ?? new BearerTokenTestHandler(),
                this.mockLogger.Object,
                Options.Create(new ArgusAuthenticationOptions { RequireAuthentication = requireAuthentication }));
        }

        /// <summary>
        /// Routes a GET /items request with the given Authorization header through a router that has the middleware
        /// </summary>
        private static async Task<(ArgusContext Context, bool HandlerInvoked)> RouteAsync(ArgusAuthenticationMiddleware middleware, string authorization, Action<IArgusEndpointConventionBuilder> configureEndpoint = null)
        {
            var handlerInvoked = false;
            var router = new ArgusRouter();
            router.UseMiddleware(middleware);

            var endpoint = router.MapGet("/items", ctx =>
            {
                handlerInvoked = true;
                ctx.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok };
                return Task.CompletedTask;
            });

            configureEndpoint?.Invoke(endpoint);

            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items" };
            request.Authorization = authorization;

            var context = new ArgusContext(request, CancellationToken.None);
            await router.RouteAsync(context);

            return (context, handlerInvoked);
        }

        [Test]
        public void Verify_that_constructor_throws_for_null_arguments()
        {
            Assert.That(() => new ArgusAuthenticationMiddleware(null, this.mockLogger.Object), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new ArgusAuthenticationMiddleware(new BearerTokenTestHandler(), null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public async Task Verify_that_authenticated_request_reaches_handler_with_User_set()
        {
            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(), "Bearer secret");

            Assert.That(handlerInvoked, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(context.User.Identity.Name, Is.EqualTo("alice"));
        }

        [Test]
        public async Task Verify_that_request_without_credentials_gets_401_problem_details()
        {
            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(), null);

            Assert.That(handlerInvoked, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
            Assert.That(context.User, Is.Null);
            Assert.That(ArgusProblemDetails.TryRead(context.Response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo(ArgusAuthenticationMiddleware.UnauthorizedDetail));
            Assert.That(problem.Instance, Is.EqualTo(context.CorrelationToken.ToString()));
        }

        [Test]
        public async Task Verify_that_invalid_credentials_get_401_with_handler_reason()
        {
            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(), "Basic dXNlcjpwYXNz");

            Assert.That(handlerInvoked, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
            Assert.That(ArgusProblemDetails.TryRead(context.Response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo("Unsupported authorization scheme."));
        }

        [Test]
        public async Task Verify_that_forbidden_result_gets_403_problem_details()
        {
            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(), "Bearer read-only");

            Assert.That(handlerInvoked, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Forbidden));
            Assert.That(ArgusProblemDetails.TryRead(context.Response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo(ArgusAuthenticationMiddleware.ForbiddenDetail));
        }

        [Test]
        public async Task Verify_that_null_result_from_handler_is_treated_as_no_result()
        {
            var handler = new Mock<IArgusAuthenticationHandler>();
            handler.Setup(h => h.AuthenticateAsync(It.IsAny<ArgusContext>())).ReturnsAsync((ArgusAuthenticationResult)null);

            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(handler: handler.Object), "Bearer secret");

            Assert.That(handlerInvoked, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
        }

        [Test]
        public async Task Verify_that_rejection_is_logged_as_warning()
        {
            await RouteAsync(this.CreateMiddleware(), "Bearer wrong");

            this.mockLogger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task Verify_that_AllowAnonymous_endpoint_is_reachable_without_credentials()
        {
            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(), null, e => e.AllowAnonymous());

            Assert.That(handlerInvoked, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(context.User, Is.Null);
        }

        [Test]
        public async Task Verify_that_AllowAnonymous_endpoint_still_sets_User_for_valid_credentials()
        {
            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(), "Bearer secret", e => e.AllowAnonymous());

            Assert.That(handlerInvoked, Is.True);
            Assert.That(context.User.Identity.Name, Is.EqualTo("alice"));
        }

        [Test]
        public async Task Verify_that_endpoints_are_open_when_authentication_is_not_required_by_default()
        {
            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(requireAuthentication: false), "Bearer wrong");

            Assert.That(handlerInvoked, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(context.User, Is.Null);
        }

        [Test]
        public async Task Verify_that_RequireAuthentication_endpoint_is_protected_when_not_required_by_default()
        {
            var (context, handlerInvoked) = await RouteAsync(this.CreateMiddleware(requireAuthentication: false), null, e => e.RequireAuthentication());

            Assert.That(handlerInvoked, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
        }

        [Test]
        public async Task Verify_that_invalid_authorize_metadata_requires_authentication()
        {
            var (context, handlerInvoked) = await RouteAsync(
                this.CreateMiddleware(requireAuthentication: false),
                null,
                e => e.WithMetadata(ArgusAuthenticationOptions.AuthorizeMetadataKey, "flase"));

            Assert.That(handlerInvoked, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
        }

        [Test]
        public void Verify_that_exception_from_authentication_handler_propagates()
        {
            var handler = new Mock<IArgusAuthenticationHandler>();
            handler.Setup(h => h.AuthenticateAsync(It.IsAny<ArgusContext>())).ThrowsAsync(new InvalidOperationException("token store unavailable"));

            Assert.That(
                async () => await RouteAsync(this.CreateMiddleware(handler: handler.Object), "Bearer secret"),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Verify_that_endpoint_extensions_set_authorize_metadata()
        {
            var endpoint = new Mock<IArgusEndpointConventionBuilder>();

            endpoint.Object.RequireAuthentication();
            endpoint.Object.AllowAnonymous();

            endpoint.Verify(b => b.WithMetadata(ArgusAuthenticationOptions.AuthorizeMetadataKey, "true"), Times.Once);
            endpoint.Verify(b => b.WithMetadata(ArgusAuthenticationOptions.AuthorizeMetadataKey, "false"), Times.Once);
        }
    }
}
