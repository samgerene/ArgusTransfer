// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusModuleExtensionsTestFixture.cs">
//
//     Copyright (c) 2025-2026 Sam Gerene
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

namespace ArgusTransfer.Tests.Routing
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Extensions;
    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.DependencyInjection;

    using NUnit.Framework;

    /// <summary>
    /// A test module that registers a single GET /module-test route
    /// </summary>
    public class TestArgusModule : IArgusModule
    {
        public void AddRoutes(IArgusRouteBuilder app)
        {
            app.MapGet("/module-test", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok,
                    Body = "module-test"
                };

                return Task.CompletedTask;
            });
        }
    }

    /// <summary>
    /// A test module that registers a global middleware which throws for GET /exception-handler-test
    /// and passes every other request through, used to verify that the exception handler is the outermost middleware
    /// </summary>
    public class ThrowingMiddlewareArgusModule : IArgusModule
    {
        public void AddRoutes(IArgusRouteBuilder app)
        {
            app.UseMiddleware(new ThrowingMiddleware());

            app.MapGet("/exception-handler-test", context =>
            {
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok };
                return Task.CompletedTask;
            });
        }

        private sealed class ThrowingMiddleware : IArgusMiddleware
        {
            public Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next)
            {
                if (context.Request.Route == "/exception-handler-test")
                {
                    throw new InvalidOperationException("thrown by module middleware");
                }

                return next(context);
            }
        }
    }

    /// <summary>
    /// Suite of tests for the <see cref="ArgusModuleExtensions"/> class
    /// </summary>
    [TestFixture]
    public class ArgusModuleExtensionsTestFixture
    {
        [Test]
        public async Task Verify_that_registered_exception_handler_wraps_module_middleware()
        {
            var services = new ServiceCollection();

            services.AddArgusModules();
            services.AddArgusExceptionHandler();

            using var provider = services.BuildServiceProvider();
            var router = provider.GetRequiredService<ArgusRouter>();

            var context = new ArgusContext(
                new ArgusRequest { Verb = ArgusVerb.GET, Route = "/exception-handler-test" },
                CancellationToken.None);

            await router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(ArgusProblemDetails.TryRead(context.Response, out _), Is.True);
        }

        [Test]
        public async Task Verify_that_registered_authentication_runs_before_module_middleware()
        {
            var services = new ServiceCollection();

            services.AddArgusModules();
            services.AddArgusAuthentication<ArgusTransfer.Tests.Middleware.BearerTokenTestHandler>();

            using var provider = services.BuildServiceProvider();
            var router = provider.GetRequiredService<ArgusRouter>();

            // Without credentials the request is rejected before the throwing module middleware runs
            var anonymous = new ArgusContext(new ArgusRequest { Verb = ArgusVerb.GET, Route = "/exception-handler-test" }, CancellationToken.None);
            await router.RouteAsync(anonymous);

            Assert.That(anonymous.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));

            // With credentials the request passes authentication and reaches the module middleware
            var authenticatedRequest = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/exception-handler-test" };
            authenticatedRequest.SetAuthorization("Bearer", "secret");
            var authenticated = new ArgusContext(authenticatedRequest, CancellationToken.None);

            Assert.That(async () => await router.RouteAsync(authenticated), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public async Task Verify_that_exception_handler_wraps_authentication()
        {
            var services = new ServiceCollection();

            services.AddArgusModules();
            services.AddArgusAuthentication<ThrowingAuthenticationHandler>();
            services.AddArgusExceptionHandler();

            using var provider = services.BuildServiceProvider();
            var router = provider.GetRequiredService<ArgusRouter>();

            var context = new ArgusContext(new ArgusRequest { Verb = ArgusVerb.GET, Route = "/module-test" }, CancellationToken.None);
            await router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
        }

        /// <summary>
        /// An authentication handler that always throws, used to verify middleware ordering
        /// </summary>
        public class ThrowingAuthenticationHandler : ArgusTransfer.Authentication.IArgusAuthenticationHandler
        {
            public Task<ArgusTransfer.Authentication.ArgusAuthenticationResult> AuthenticateAsync(ArgusContext context)
            {
                throw new InvalidOperationException("token store unavailable");
            }
        }

        [Test]
        public void Verify_that_without_exception_handler_module_middleware_exception_propagates()
        {
            var services = new ServiceCollection();

            services.AddArgusModules();

            using var provider = services.BuildServiceProvider();
            var router = provider.GetRequiredService<ArgusRouter>();

            var context = new ArgusContext(
                new ArgusRequest { Verb = ArgusVerb.GET, Route = "/exception-handler-test" },
                CancellationToken.None);

            Assert.That(async () => await router.RouteAsync(context), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Verify_that_AddArgusModules_registers_module_and_router()
        {
            var services = new ServiceCollection();

            services.AddArgusModules();

            var moduleDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IArgusModule));
            Assert.That(moduleDescriptor, Is.Not.Null);

            var routerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ArgusRouter));
            Assert.That(routerDescriptor, Is.Not.Null);
            Assert.That(routerDescriptor.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        }

        [Test]
        public async Task Verify_that_AddArgusModules_wires_module_routes_to_router()
        {
            var services = new ServiceCollection();

            services.AddArgusModules();

            var provider = services.BuildServiceProvider();
            var router = provider.GetRequiredService<ArgusRouter>();

            var context = new ArgusContext(
                new ArgusRequest { Verb = ArgusVerb.GET, Route = "/module-test" },
                System.Threading.CancellationToken.None);

            await router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(context.Response.Body, Is.EqualTo("module-test"));
        }

        [Test]
        public void Verify_that_AddArgusModules_returns_services_for_chaining()
        {
            var services = new ServiceCollection();

            var result = services.AddArgusModules();

            Assert.That(result, Is.SameAs(services));
        }
    }
}
