// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusClientTestFixture.cs">
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

namespace ArgusTransfer.Tests.Client
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;
    using ArgusTransfer.Client;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusClient"/> class
    /// </summary>
    [TestFixture]
    public class ArgusClientTestFixture
    {
        private ArgusRequestSerializer requestSerializer;

        private ArgusResponseSerializer responseSerializer;

        [SetUp]
        public void SetUp()
        {
            this.requestSerializer = new ArgusRequestSerializer();
            this.responseSerializer = new ArgusResponseSerializer();
        }

        [Test]
        public async Task Verify_that_SendAsync_round_trips_request_and_response()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.GET));
                Assert.That(request.Route, Is.EqualTo("/healthendpoint"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok,
                    Body = """[{"identifier":"cfb2e590-eed6-4223-b2ba-271ed0cb06da","name":"ep1","url":"https://example.com","frequency":30,"timeout":5,"retryCount":3}]"""
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientRequest = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint"
            };

            var clientResponse = await client.SendAsync(clientRequest);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(clientResponse.Body, Is.Not.Null.And.Not.Empty);
            Assert.That(clientResponse.Body, Does.Contain("ep1"));
        }

        [Test]
        public async Task Verify_that_SendAsync_preserves_correlation_token()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var correlationToken = Guid.NewGuid();

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientRequest = new ArgusRequest
            {
                CorrelationToken = correlationToken,
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint"
            };

            var clientResponse = await client.SendAsync(clientRequest);

            await serverTask;

            Assert.That(clientResponse.CorrelationToken, Is.EqualTo(correlationToken));
        }

        [Test]
        public async Task Verify_that_SendAsync_sends_request_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var requestBody = """{"name":"test","url":"https://example.com"}""";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.POST));
                Assert.That(request.Route, Is.EqualTo("/healthendpoint"));
                Assert.That(request.Body, Is.EqualTo(requestBody));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Created,
                    Body = requestBody
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientRequest = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/healthendpoint",
                Body = requestBody
            };

            var clientResponse = await client.SendAsync(clientRequest);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
            Assert.That(clientResponse.Body, Is.EqualTo(requestBody));
        }
    }
}
