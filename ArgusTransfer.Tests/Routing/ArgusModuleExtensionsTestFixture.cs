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
    using System.Linq;
    using System.Threading.Tasks;

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
    /// Suite of tests for the <see cref="ArgusModuleExtensions"/> class
    /// </summary>
    [TestFixture]
    public class ArgusModuleExtensionsTestFixture
    {
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
