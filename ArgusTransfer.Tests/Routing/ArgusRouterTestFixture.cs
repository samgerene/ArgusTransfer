// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouterTestFixture.cs" company="Sam Gerené">
//
//     Copyright (c) 2026 Sam Gerené
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
    using System.Collections.Generic;
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
            this.router.MapPost("/healthendpoint", (request, routeValues) =>
            {
                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Created
                });
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/healthendpoint"
            };

            var response = await this.router.RouteAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
        }

        [Test]
        public async Task Verify_that_wrong_verb_returns_BadRequest()
        {
            this.router.MapPost("/healthendpoint", (request, routeValues) =>
            {
                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Created
                });
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint"
            };

            var response = await this.router.RouteAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
        }

        [Test]
        public async Task Verify_that_unknown_route_returns_NotFound()
        {
            this.router.MapPost("/healthendpoint", (request, routeValues) =>
            {
                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Created
                });
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/unknown"
            };

            var response = await this.router.RouteAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
        }

        [Test]
        public async Task Verify_that_route_values_are_extracted()
        {
            IReadOnlyDictionary<string, string> capturedRouteValues = null;

            this.router.MapGet("/healthendpoint/{identifier:Guid}", (request, routeValues) =>
            {
                capturedRouteValues = routeValues;

                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                });
            });

            var guid = "cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e";

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = $"/healthendpoint/{guid}"
            };

            var response = await this.router.RouteAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(capturedRouteValues, Is.Not.Null);
            Assert.That(capturedRouteValues["identifier"], Is.EqualTo(guid));
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

            var response = await this.router.RouteAsync(request);

            Assert.That(response.CorrelationToken, Is.EqualTo(correlationToken));
        }
    }
}
