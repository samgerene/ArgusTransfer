// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusEndpointConventionBuilderTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Routing
{
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using Moq;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusEndpointConventionBuilder"/> class
    /// </summary>
    [TestFixture]
    public class ArgusEndpointConventionBuilderTestFixture
    {
        [Test]
        public void Verify_that_WithMetadata_adds_entry_to_endpoint_metadata()
        {
            var endpoint = new ArgusRouteEndpoint();
            var builder = new ArgusEndpointConventionBuilder(endpoint);

            builder.WithMetadata("key", "value");

            Assert.That(endpoint.Metadata["key"], Is.EqualTo("value"));
        }

        [Test]
        public void Verify_that_WithMetadata_returns_same_builder_for_chaining()
        {
            var endpoint = new ArgusRouteEndpoint();
            var builder = new ArgusEndpointConventionBuilder(endpoint);

            var result = builder.WithMetadata("key", "value");

            Assert.That(result, Is.SameAs(builder));
        }

        [Test]
        public void Verify_that_MapGet_returns_convention_builder()
        {
            var router = new ArgusRouter();

            var result = router.MapGet("/route", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<IArgusEndpointConventionBuilder>());
        }

        [Test]
        public void Verify_that_WithMetadata_is_chainable_through_router()
        {
            var router = new ArgusRouter();

            var result = router.MapGet("/route", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            })
            .WithMetadata("first", "1")
            .WithMetadata("second", "2");

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void Verify_that_WithMiddleware_adds_middleware_to_endpoint()
        {
            var endpoint = new ArgusRouteEndpoint();
            var builder = new ArgusEndpointConventionBuilder(endpoint);
            var middleware = new Mock<IArgusMiddleware>().Object;

            builder.WithMiddleware(middleware);

            Assert.That(endpoint.Middlewares.Count, Is.EqualTo(1));
            Assert.That(endpoint.Middlewares[0], Is.SameAs(middleware));
        }

        [Test]
        public void Verify_that_WithMiddleware_returns_same_builder_for_chaining()
        {
            var endpoint = new ArgusRouteEndpoint();
            var builder = new ArgusEndpointConventionBuilder(endpoint);
            var middleware = new Mock<IArgusMiddleware>().Object;

            var result = builder.WithMiddleware(middleware);

            Assert.That(result, Is.SameAs(builder));
        }
    }
}
