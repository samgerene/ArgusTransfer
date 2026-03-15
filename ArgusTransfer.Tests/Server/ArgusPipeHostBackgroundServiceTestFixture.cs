// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusPipeHostBackgroundServiceTestFixture.cs">
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

namespace ArgusTransfer.Transport.Tests.Server
{
    using System;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;
    using ArgusTransfer.Server;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusPipeHostBackgroundService"/> class
    /// </summary>
    [TestFixture]
    public class ArgusPipeHostBackgroundServiceTestFixture
    {
        private ArgusPipeHostBackgroundService service;

        private Mock<ILogger<ArgusPipeHostBackgroundService>> mockLogger;

        [SetUp]
        public void SetUp()
        {
            this.mockLogger = new Mock<ILogger<ArgusPipeHostBackgroundService>>();

            var router = new ArgusRouter();

            router.MapGet("/test", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok,
                    Body = """{"message":"hello"}"""
                };

                return Task.CompletedTask;
            });

            router.MapPost("/test", context =>
            {
                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Created,
                    Body = context.Request.Body
                };

                return Task.CompletedTask;
            });

            var options = Options.Create(new ArgusPipeHostOptions());

            this.service = new ArgusPipeHostBackgroundService(this.mockLogger.Object, router, options);
        }

        [Test]
        public async Task Verify_that_HandleRequestAsync_dispatches_GET_to_router()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/test"
            };

            var response = await this.service.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo("""{"message":"hello"}"""));
        }

        [Test]
        public async Task Verify_that_HandleRequestAsync_dispatches_POST_to_router()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/test",
                Body = """{"name":"value"}"""
            };

            var response = await this.service.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
            Assert.That(response.Body, Is.EqualTo("""{"name":"value"}"""));
        }

        [Test]
        public async Task Verify_that_unknown_route_returns_NotFound()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/unknown"
            };

            var response = await this.service.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
        }

        [Test]
        public async Task Verify_that_wrong_verb_returns_BadRequest()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.PATCH,
                Route = "/test"
            };

            var response = await this.service.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
        }

        [Test]
        public async Task Verify_that_response_preserves_CorrelationToken()
        {
            var correlationToken = Guid.NewGuid();

            var request = new ArgusRequest
            {
                CorrelationToken = correlationToken,
                Verb = ArgusVerb.GET,
                Route = "/test"
            };

            var response = await this.service.HandleRequestAsync(request);

            Assert.That(response.CorrelationToken, Is.EqualTo(correlationToken));
        }
    }
}
