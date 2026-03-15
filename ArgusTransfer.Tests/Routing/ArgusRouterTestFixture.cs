// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouterTestFixture.cs">
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

namespace ArgusTransfer.Tests.Routing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRouter"/> class
    /// </summary>
    [TestFixture]
    public class ArgusRouterTestFixture
    {
        private ArgusRouter router;

        [SetUp]
        public void SetUp()
        {
            this.router = new ArgusRouter();
        }

        [Test]
        public async Task Verify_that_matching_verb_and_route_dispatches_to_handler()
        {
            this.router.MapPost("/healthendpoint", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Created
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/healthendpoint"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
        }

        [Test]
        public async Task Verify_that_wrong_verb_returns_BadRequest()
        {
            this.router.MapPost("/healthendpoint", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Created
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
        }

        [Test]
        public async Task Verify_that_unknown_route_returns_NotFound()
        {
            this.router.MapPost("/healthendpoint", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Created
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/unknown"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
        }

        [Test]
        public async Task Verify_that_route_values_are_extracted()
        {
            this.router.MapGet("/healthendpoint/{identifier:Guid}", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            var guid = "cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e";

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = $"/healthendpoint/{guid}"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(context.RouteValues, Is.Not.Null);
            Assert.That(context.RouteValues["identifier"], Is.EqualTo(guid));
        }

        [Test]
        public async Task Verify_that_CorrelationToken_is_preserved()
        {
            var correlationToken = Guid.NewGuid();

            var request = new ArgusRequest
            {
                CorrelationToken = correlationToken,
                Verb = ArgusVerb.POST,
                Route = "/unknown"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.CorrelationToken, Is.EqualTo(correlationToken));
        }

        [Test]
        public async Task Verify_that_MapPut_dispatches_to_handler()
        {
            this.router.MapPut("/resource", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.PUT,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_MapPatch_dispatches_to_handler()
        {
            this.router.MapPatch("/resource", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.PATCH,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_MapHead_dispatches_to_handler()
        {
            this.router.MapHead("/resource", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.HEAD,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_MapDelete_dispatches_to_handler()
        {
            this.router.MapDelete("/resource", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.DELETE,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_endpoint_metadata_is_available_on_context()
        {
            this.router.MapGet("/resource", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            }).WithMetadata("tag", "test-value");

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.EndpointMetadata["tag"], Is.EqualTo("test-value"));
        }

        [Test]
        public async Task Verify_that_cancellation_token_is_available_on_context()
        {
            CancellationToken capturedToken = default;

            this.router.MapGet("/resource", context =>
            {
                capturedToken = context.RequestAborted;

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

            using var cts = new CancellationTokenSource();
            var context = new ArgusContext(request, cts.Token);
            await this.router.RouteAsync(context);

            Assert.That(capturedToken, Is.EqualTo(cts.Token));
        }
    }
}
