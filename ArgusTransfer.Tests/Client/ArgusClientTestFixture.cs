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

    using System.Collections.Generic;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;
    using ArgusTransfer.Client;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    using Moq;

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

        /// <summary>
        /// Runs a single-shot fake pipe server that reads one request, optionally validates it,
        /// and writes back a response with the given status code and body
        /// </summary>
        /// <param name="pipeName">The name of the pipe to listen on</param>
        /// <param name="validateRequest">Optional callback invoked with the deserialized request for assertions</param>
        /// <param name="responseStatus">The status code to return to the client</param>
        /// <param name="responseBody">The optional response body</param>
        /// <returns>A task that completes once the server has written its response</returns>
        private Task RunFakeServerAsync(string pipeName, Action<ArgusRequest> validateRequest, ArgusStatusCode responseStatus, string responseBody = null)
        {
            return Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                validateRequest?.Invoke(request);

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = responseStatus,
                    Body = responseBody
                };

                this.responseSerializer.Write(writer, response);
            });
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

        [Test]
        public void Verify_that_DefaultTimeout_is_30_seconds()
        {
            using var client = new ArgusClient("test-pipe");
            Assert.That(client.DefaultTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public async Task Verify_that_SendAsync_throws_TimeoutException_when_server_is_slow()
        {
            var pipeName = $"argus-timeout-test-{Guid.NewGuid():N}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync();
                // Server deliberately does not respond - simulates a hang
                await Task.Delay(5000);
            });

            using var client = new ArgusClient(pipeName);
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/test" };

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await client.SendAsync(request, timeout: TimeSpan.FromMilliseconds(200));
            });
        }

        [Test]
        public async Task Verify_that_per_request_timeout_overrides_default()
        {
            var pipeName = $"argus-timeout-override-{Guid.NewGuid():N}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync();
                await Task.Delay(5000);
            });

            using var client = new ArgusClient(pipeName);
            client.DefaultTimeout = TimeSpan.FromSeconds(60); // Long default

            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/test" };

            // Short per-request timeout should override the long default
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await client.SendAsync(request, timeout: TimeSpan.FromMilliseconds(200));
            });
        }

        [Test]
        public async Task Verify_that_GetAsync_with_query_parameters_sends_query_parameters()
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
                Assert.That(request.QueryParameters["key"], Is.EqualTo("value"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            var queryParams = new Dictionary<string, string> { { "key", "value" } };

            var clientResponse = await client.GetAsync("/test", queryParams);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PostAsync_sends_POST_with_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var body = "post-body";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.POST));
                Assert.That(request.Body, Is.EqualTo(body));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Created
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientResponse = await client.PostAsync("/test", body);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
        }

        [Test]
        public async Task Verify_that_PostAsync_with_query_parameters_sends_query_parameters()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.POST));
                Assert.That(request.QueryParameters["key"], Is.EqualTo("value"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Created
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            var queryParams = new Dictionary<string, string> { { "key", "value" } };

            var clientResponse = await client.PostAsync("/test", queryParams, "body");

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
        }

        [Test]
        public async Task Verify_that_PutAsync_sends_PUT_with_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var body = "put-body";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.PUT));
                Assert.That(request.Body, Is.EqualTo(body));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientResponse = await client.PutAsync("/test", body);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PutAsync_with_query_parameters_sends_query_parameters()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.PUT));
                Assert.That(request.QueryParameters["key"], Is.EqualTo("value"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            var queryParams = new Dictionary<string, string> { { "key", "value" } };

            var clientResponse = await client.PutAsync("/test", queryParams, "body");

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PatchAsync_sends_PATCH_with_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var body = "patch-body";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.PATCH));
                Assert.That(request.Body, Is.EqualTo(body));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientResponse = await client.PatchAsync("/test", body);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PatchAsync_with_query_parameters_sends_query_parameters()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.PATCH));
                Assert.That(request.QueryParameters["key"], Is.EqualTo("value"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            var queryParams = new Dictionary<string, string> { { "key", "value" } };

            var clientResponse = await client.PatchAsync("/test", queryParams, "body");

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_DeleteAsync_sends_DELETE()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.DELETE));
                Assert.That(request.Route, Does.StartWith("/test"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientResponse = await client.DeleteAsync("/test");

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_DeleteAsync_with_query_parameters_sends_query_parameters()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.DELETE));
                Assert.That(request.QueryParameters["key"], Is.EqualTo("value"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            var queryParams = new Dictionary<string, string> { { "key", "value" } };

            var clientResponse = await client.DeleteAsync("/test", queryParams);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_HeadAsync_sends_HEAD()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.HEAD));
                Assert.That(request.Route, Does.StartWith("/test"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientResponse = await client.HeadAsync("/test");

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_HeadAsync_with_query_parameters_sends_query_parameters()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.HEAD));
                Assert.That(request.QueryParameters["key"], Is.EqualTo("value"));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            var queryParams = new Dictionary<string, string> { { "key", "value" } };

            var clientResponse = await client.HeadAsync("/test", queryParams);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public void Verify_that_constructor_with_registry_creates_client()
        {
            var registry = new ArgusBodySerializerRegistry(new[] { new PlainTextArgusBodySerializer() });

            using var client = new ArgusClient("test-pipe", registry);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.DefaultTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void Verify_that_Dispose_can_be_called_multiple_times()
        {
            var client = new ArgusClient("test-pipe");

            client.Dispose();
            client.Dispose();

            Assert.Pass();
        }

        [Test]
        public async Task Verify_that_GetEnsureSuccessAsync_returns_response_when_status_is_2xx()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

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
                    StatusCode = ArgusStatusCode.Ok,
                    Body = "ok"
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var clientResponse = await client.GetEnsureSuccessAsync("/healthendpoint");

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(clientResponse.Body, Is.EqualTo("ok"));
        }

        [Test]
        public async Task Verify_that_GetEnsureSuccessAsync_throws_when_status_is_not_2xx()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";

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
                    StatusCode = ArgusStatusCode.NotFound,
                    Body = "missing"
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);

            var exception = await Assert.ThrowsAsync<ArgusRequestException>(async () =>
            {
                await client.GetEnsureSuccessAsync("/healthendpoint");
            });

            await serverTask;

            Assert.That(exception.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
            Assert.That(exception.ReasonPhrase, Is.EqualTo(ArgusStatusCode.NotFound.ToReasonPhrase()));
            Assert.That(exception.ResponseBody, Is.EqualTo("missing"));
        }

        [Test]
        public async Task Verify_that_SendEnsureSuccessAsync_forwards_request_and_returns_response()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.PUT));
                Assert.That(r.Route, Is.EqualTo("/items"));
            }, ArgusStatusCode.Ok, "ok");

            using var client = new ArgusClient(pipeName);

            var response = await client.SendEnsureSuccessAsync(new ArgusRequest { Verb = ArgusVerb.PUT, Route = "/items" });

            await serverTask;
            Assert.That(response.Body, Is.EqualTo("ok"));
        }

        [Test]
        public async Task Verify_that_GetAsync_sends_null_query_parameter_value_as_empty_value()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Route, Is.EqualTo("/items"));
                Assert.That(r.QueryParameters["filter"], Is.EqualTo(string.Empty));
            }, ArgusStatusCode.Ok, "ok");

            using var client = new ArgusClient(pipeName);

            var response = await client.GetAsync("/items", new Dictionary<string, string> { ["filter"] = null });

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_GetEnsureSuccessAsync_with_query_parameters_sends_query_parameters()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.GET));
                Assert.That(r.QueryParameters["key"], Is.EqualTo("value"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.GetEnsureSuccessAsync("/items", new Dictionary<string, string> { { "key", "value" } });

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PostEnsureSuccessAsync_with_string_body_sends_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.POST));
                Assert.That(r.Body, Is.EqualTo("post-body"));
            }, ArgusStatusCode.Created);

            using var client = new ArgusClient(pipeName);

            var response = await client.PostEnsureSuccessAsync("/items", "post-body");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
        }

        [Test]
        public async Task Verify_that_PostEnsureSuccessAsync_with_query_parameters_sends_query_parameters_and_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.POST));
                Assert.That(r.QueryParameters["key"], Is.EqualTo("value"));
                Assert.That(r.Body, Is.EqualTo("post-body"));
            }, ArgusStatusCode.Created);

            using var client = new ArgusClient(pipeName);

            var response = await client.PostEnsureSuccessAsync("/items", new Dictionary<string, string> { { "key", "value" } }, "post-body");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
        }

        [Test]
        public async Task Verify_that_PostEnsureSuccessAsync_with_stream_sends_body_and_content_type()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.POST));
                Assert.That(r.Headers[ArgusHeaderNames.ContentType], Is.EqualTo("application/json"));
                Assert.That(r.BodyStream, Is.Not.Null);
            }, ArgusStatusCode.Created);

            using var client = new ArgusClient(pipeName);
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes("stream-body"));

            var response = await client.PostEnsureSuccessAsync("/items", bodyStream, "application/json");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
        }

        [Test]
        public async Task Verify_that_PutEnsureSuccessAsync_with_string_body_sends_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.PUT));
                Assert.That(r.Body, Is.EqualTo("put-body"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.PutEnsureSuccessAsync("/items", "put-body");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PutEnsureSuccessAsync_with_query_parameters_sends_query_parameters_and_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.PUT));
                Assert.That(r.QueryParameters["key"], Is.EqualTo("value"));
                Assert.That(r.Body, Is.EqualTo("put-body"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.PutEnsureSuccessAsync("/items", new Dictionary<string, string> { { "key", "value" } }, "put-body");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PutEnsureSuccessAsync_with_stream_sends_body_without_explicit_content_type()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.PUT));
                Assert.That(r.BodyStream, Is.Not.Null);
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes("stream-body"));

            var response = await client.PutEnsureSuccessAsync("/items", bodyStream);

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PatchEnsureSuccessAsync_with_string_body_sends_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.PATCH));
                Assert.That(r.Body, Is.EqualTo("patch-body"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.PatchEnsureSuccessAsync("/items", "patch-body");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PatchEnsureSuccessAsync_with_query_parameters_sends_query_parameters_and_body()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.PATCH));
                Assert.That(r.QueryParameters["key"], Is.EqualTo("value"));
                Assert.That(r.Body, Is.EqualTo("patch-body"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.PatchEnsureSuccessAsync("/items", new Dictionary<string, string> { { "key", "value" } }, "patch-body");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PatchEnsureSuccessAsync_with_stream_sends_body_and_content_type()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.PATCH));
                Assert.That(r.Headers[ArgusHeaderNames.ContentType], Is.EqualTo("text/plain"));
                Assert.That(r.BodyStream, Is.Not.Null);
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes("stream-body"));

            var response = await client.PatchEnsureSuccessAsync("/items", bodyStream, "text/plain");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_DeleteEnsureSuccessAsync_sends_DELETE()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.DELETE));
                Assert.That(r.Route, Is.EqualTo("/items"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.DeleteEnsureSuccessAsync("/items");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_DeleteEnsureSuccessAsync_with_query_parameters_sends_query_parameters()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.DELETE));
                Assert.That(r.QueryParameters["key"], Is.EqualTo("value"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.DeleteEnsureSuccessAsync("/items", new Dictionary<string, string> { { "key", "value" } });

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_HeadEnsureSuccessAsync_sends_HEAD()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.HEAD));
                Assert.That(r.Route, Is.EqualTo("/items"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.HeadEnsureSuccessAsync("/items");

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_HeadEnsureSuccessAsync_with_query_parameters_sends_query_parameters()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var serverTask = this.RunFakeServerAsync(pipeName, r =>
            {
                Assert.That(r.Verb, Is.EqualTo(ArgusVerb.HEAD));
                Assert.That(r.QueryParameters["key"], Is.EqualTo("value"));
            }, ArgusStatusCode.Ok);

            using var client = new ArgusClient(pipeName);

            var response = await client.HeadEnsureSuccessAsync("/items", new Dictionary<string, string> { { "key", "value" } });

            await serverTask;
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PostAsync_with_stream_sends_body_and_content_type()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var payload = "stream-post-payload";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.POST));
                Assert.That(request.Headers[ArgusHeaderNames.ContentType], Is.EqualTo("application/json"));
                Assert.That(request.BodyStream, Is.Not.Null);

                using var bodyReader = new StreamReader(request.BodyStream, new UTF8Encoding(false));
                var receivedBody = await bodyReader.ReadToEndAsync();
                Assert.That(receivedBody, Is.EqualTo(payload));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Created
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var clientResponse = await client.PostAsync("/upload", bodyStream, "application/json");

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
        }

        [Test]
        public async Task Verify_that_PutAsync_with_stream_sends_body_without_explicit_content_type()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var payload = "stream-put-payload";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.PUT));
                Assert.That(request.BodyStream, Is.Not.Null);

                using var bodyReader = new StreamReader(request.BodyStream, new UTF8Encoding(false));
                var receivedBody = await bodyReader.ReadToEndAsync();
                Assert.That(receivedBody, Is.EqualTo(payload));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var clientResponse = await client.PutAsync("/upload", bodyStream);

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_PatchAsync_with_stream_sends_body_and_content_type()
        {
            var pipeName = $"argus-test-{Guid.NewGuid()}";
            var payload = "stream-patch-payload";

            var serverTask = Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                await server.WaitForConnectionAsync();

                var reader = new StreamReader(server, new UTF8Encoding(false));
                var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                Assert.That(request.Verb, Is.EqualTo(ArgusVerb.PATCH));
                Assert.That(request.Headers[ArgusHeaderNames.ContentType], Is.EqualTo("text/plain"));
                Assert.That(request.BodyStream, Is.Not.Null);

                using var bodyReader = new StreamReader(request.BodyStream, new UTF8Encoding(false));
                var receivedBody = await bodyReader.ReadToEndAsync();
                Assert.That(receivedBody, Is.EqualTo(payload));

                var response = new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                this.responseSerializer.Write(writer, response);
            });

            using var client = new ArgusClient(pipeName);
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var clientResponse = await client.PatchAsync("/upload", bodyStream, "text/plain");

            await serverTask;

            Assert.That(clientResponse.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        /// <summary>
        /// Runs a fake pipe server that accepts <paramref name="connectionCount"/> sequential connections.
        /// Each connection reads one request and records its body; the first <paramref name="dropCount"/>
        /// connections then close without responding, the remaining ones respond with 200 OK
        /// </summary>
        /// <param name="pipeName">The name of the pipe to listen on</param>
        /// <param name="dropCount">The number of connections to close without responding</param>
        /// <param name="connectionCount">The total number of connections to accept</param>
        /// <param name="receivedBodies">Collects the body of every request received, in order</param>
        /// <returns>A task that completes once all connections have been served</returns>
        private Task RunFlakyServerAsync(string pipeName, int dropCount, int connectionCount, List<string> receivedBodies)
        {
            return Task.Run(async () =>
            {
                for (var connection = 0; connection < connectionCount; connection++)
                {
                    using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();

                    var reader = new StreamReader(server, new UTF8Encoding(false));
                    var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = false };

                    var request = await this.requestSerializer.ReadAsync(reader, CancellationToken.None);

                    var body = request.IsStreamed
                        ? await new StreamReader(request.BodyStream, Encoding.UTF8).ReadToEndAsync()
                        : request.Body;

                    lock (receivedBodies)
                    {
                        receivedBodies.Add(body);
                    }

                    if (connection < dropCount)
                    {
                        continue;
                    }

                    this.responseSerializer.Write(writer, new ArgusResponse
                    {
                        CorrelationToken = request.CorrelationToken,
                        StatusCode = ArgusStatusCode.Ok,
                        Body = "ok"
                    });
                }
            });
        }

        [Test]
        public void Verify_that_Compression_is_disabled_by_default_and_null_restores_defaults()
        {
            using var client = new ArgusClient("argus-test");

            Assert.That(client.Compression, Is.Not.Null);
            Assert.That(client.Compression.Enabled, Is.False);

            client.Compression = new ArgusCompressionOptions { Enabled = true };
            client.Compression = null;

            Assert.That(client.Compression, Is.Not.Null);
            Assert.That(client.Compression.Enabled, Is.False);
        }

        [Test]
        public void Verify_that_RetryPolicy_is_null_by_default()
        {
            using var client = new ArgusClient("argus-test");

            Assert.That(client.RetryPolicy, Is.Null);
        }

        [Test]
        public void Verify_that_Logger_defaults_to_NullLogger_and_null_restores_default()
        {
            using var client = new ArgusClient("argus-test");

            Assert.That(client.Logger, Is.SameAs(NullLogger.Instance));

            client.Logger = new Mock<ILogger>().Object;
            client.Logger = null;

            Assert.That(client.Logger, Is.SameAs(NullLogger.Instance));
        }

        [Test]
        public async Task Verify_that_SendAsync_retries_idempotent_request_after_connection_is_dropped()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 2, connectionCount: 3, receivedBodies);

            using var client = new ArgusClient(pipeName);
            client.RetryPolicy = new ArgusRetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(10) };

            var response = await client.GetAsync("/items", timeout: TimeSpan.FromSeconds(10));

            await serverTask;

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo("ok"));
            Assert.That(receivedBodies, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task Verify_that_SendAsync_without_retry_policy_does_not_retry()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 1, connectionCount: 1, receivedBodies);

            using var client = new ArgusClient(pipeName);

            await Assert.ThatAsync(async () => await client.GetAsync("/items", timeout: TimeSpan.FromSeconds(10)),
                Throws.InstanceOf<IOException>());

            await serverTask;

            Assert.That(receivedBodies, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Verify_that_SendAsync_does_not_retry_non_idempotent_request_after_it_was_sent()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 1, connectionCount: 1, receivedBodies);

            using var client = new ArgusClient(pipeName);
            client.RetryPolicy = new ArgusRetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(10) };

            await Assert.ThatAsync(async () => await client.PostAsync("/items", "new item", timeout: TimeSpan.FromSeconds(10)),
                Throws.InstanceOf<IOException>());

            await serverTask;

            Assert.That(receivedBodies, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Verify_that_SendAsync_retries_non_idempotent_request_when_enabled()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 1, connectionCount: 2, receivedBodies);

            using var client = new ArgusClient(pipeName);
            client.RetryPolicy = new ArgusRetryPolicy
            {
                InitialDelay = TimeSpan.FromMilliseconds(10),
                RetryNonIdempotentRequests = true
            };

            var response = await client.PostAsync("/items", "new item", timeout: TimeSpan.FromSeconds(10));

            await serverTask;

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(receivedBodies, Is.EqualTo(new[] { "new item", "new item" }));
        }

        [Test]
        public async Task Verify_that_SendAsync_stops_retrying_after_MaxRetries()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 3, connectionCount: 3, receivedBodies);

            using var client = new ArgusClient(pipeName);
            client.RetryPolicy = new ArgusRetryPolicy { MaxRetries = 2, InitialDelay = TimeSpan.FromMilliseconds(10) };

            await Assert.ThatAsync(async () => await client.GetAsync("/items", timeout: TimeSpan.FromSeconds(10)),
                Throws.InstanceOf<IOException>());

            await serverTask;

            Assert.That(receivedBodies, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task Verify_that_SendAsync_rewinds_seekable_body_stream_on_retry()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 1, connectionCount: 2, receivedBodies);

            using var client = new ArgusClient(pipeName);
            client.RetryPolicy = new ArgusRetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(10) };

            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes("streamed payload"));

            var response = await client.PutAsync("/upload", bodyStream, "text/plain", timeout: TimeSpan.FromSeconds(10));

            await serverTask;

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(receivedBodies, Is.EqualTo(new[] { "streamed payload", "streamed payload" }));
        }

        [Test]
        public async Task Verify_that_SendAsync_honours_cancellation_between_retries()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 1, connectionCount: 1, receivedBodies);

            using var client = new ArgusClient(pipeName);
            client.RetryPolicy = new ArgusRetryPolicy { InitialDelay = TimeSpan.FromMinutes(1), MaxDelay = TimeSpan.FromMinutes(1) };

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            await Assert.ThatAsync(async () => await client.GetAsync("/items", timeout: TimeSpan.FromMinutes(5), cancellationToken: cts.Token),
                Throws.InstanceOf<OperationCanceledException>());

            await serverTask;

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
            Assert.That(receivedBodies, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Verify_that_SendAsync_throws_TimeoutException_when_retry_delay_exceeds_timeout()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 1, connectionCount: 1, receivedBodies);

            using var client = new ArgusClient(pipeName);
            client.RetryPolicy = new ArgusRetryPolicy { InitialDelay = TimeSpan.FromMinutes(1), MaxDelay = TimeSpan.FromMinutes(1) };

            await Assert.ThatAsync(async () => await client.GetAsync("/items", timeout: TimeSpan.FromMilliseconds(500)),
                Throws.TypeOf<TimeoutException>());

            await serverTask;

            Assert.That(receivedBodies, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Verify_that_SendAsync_logs_a_warning_for_each_retry()
        {
            var pipeName = $"argus-retry-{Guid.NewGuid():N}";
            var receivedBodies = new List<string>();
            var serverTask = this.RunFlakyServerAsync(pipeName, dropCount: 2, connectionCount: 3, receivedBodies);

            var logger = new Mock<ILogger>();

            using var client = new ArgusClient(pipeName);
            client.RetryPolicy = new ArgusRetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(10) };
            client.Logger = logger.Object;

            await client.GetAsync("/items", timeout: TimeSpan.FromSeconds(10));

            await serverTask;

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<IOException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Exactly(2));
        }

        [Test]
        public void Verify_that_CanRetry_returns_false_without_policy()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items" };

            Assert.That(ArgusClient.CanRetry(null, new IOException(), request, false, -1, 0, CancellationToken.None), Is.False);
        }

        [TestCase(ArgusVerb.POST, false, true)]
        [TestCase(ArgusVerb.PATCH, false, true)]
        [TestCase(ArgusVerb.POST, true, false)]
        [TestCase(ArgusVerb.PATCH, true, false)]
        [TestCase(ArgusVerb.GET, true, true)]
        [TestCase(ArgusVerb.HEAD, true, true)]
        [TestCase(ArgusVerb.PUT, true, true)]
        [TestCase(ArgusVerb.DELETE, true, true)]
        public void Verify_that_CanRetry_only_retries_non_idempotent_requests_before_they_are_sent(ArgusVerb verb, bool requestSent, bool expected)
        {
            var policy = new ArgusRetryPolicy();
            var request = new ArgusRequest { Verb = verb, Route = "/items" };

            Assert.That(ArgusClient.CanRetry(policy, new IOException(), request, requestSent, -1, 0, CancellationToken.None), Is.EqualTo(expected));
        }

        [Test]
        public void Verify_that_CanRetry_retries_sent_non_idempotent_request_when_enabled()
        {
            var policy = new ArgusRetryPolicy { RetryNonIdempotentRequests = true };
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/items" };

            Assert.That(ArgusClient.CanRetry(policy, new IOException(), request, true, -1, 0, CancellationToken.None), Is.True);
        }

        [Test]
        public void Verify_that_CanRetry_does_not_retry_sent_request_with_non_seekable_body_stream()
        {
            var policy = new ArgusRetryPolicy();
            using var bodyStream = new MemoryStream();
            var request = new ArgusRequest { Verb = ArgusVerb.PUT, Route = "/upload", BodyStream = bodyStream };

            Assert.That(ArgusClient.CanRetry(policy, new IOException(), request, true, -1, 0, CancellationToken.None), Is.False);
            Assert.That(ArgusClient.CanRetry(policy, new IOException(), request, true, 0, 0, CancellationToken.None), Is.True);
            Assert.That(ArgusClient.CanRetry(policy, new IOException(), request, false, -1, 0, CancellationToken.None), Is.True);
        }

        [Test]
        public void Verify_that_CanRetry_never_retries_timeout_or_cancellation()
        {
            var policy = new ArgusRetryPolicy { ShouldRetry = _ => true };
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items" };

            Assert.That(ArgusClient.CanRetry(policy, new TimeoutException(), request, false, -1, 0, CancellationToken.None), Is.False);
            Assert.That(ArgusClient.CanRetry(policy, new OperationCanceledException(), request, false, -1, 0, CancellationToken.None), Is.False);
            Assert.That(ArgusClient.CanRetry(policy, new IOException(), request, false, -1, 0, new CancellationToken(true)), Is.False);
        }

        [Test]
        public void Verify_that_CanRetry_returns_false_when_retries_are_exhausted()
        {
            var policy = new ArgusRetryPolicy { MaxRetries = 2 };
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items" };

            Assert.That(ArgusClient.CanRetry(policy, new IOException(), request, false, -1, 1, CancellationToken.None), Is.True);
            Assert.That(ArgusClient.CanRetry(policy, new IOException(), request, false, -1, 2, CancellationToken.None), Is.False);
        }

        [Test]
        public void Verify_that_CanRetry_uses_the_ShouldRetry_predicate()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items" };

            Assert.That(ArgusClient.CanRetry(new ArgusRetryPolicy(), new FormatException(), request, false, -1, 0, CancellationToken.None), Is.False);
            Assert.That(ArgusClient.CanRetry(new ArgusRetryPolicy { ShouldRetry = e => e is FormatException }, new FormatException(), request, false, -1, 0, CancellationToken.None), Is.True);
            Assert.That(ArgusClient.CanRetry(new ArgusRetryPolicy { ShouldRetry = null }, new IOException(), request, false, -1, 0, CancellationToken.None), Is.False);
        }

        [Test]
        public void Verify_that_MaxResponseBodySize_defaults_to_unlimited_and_rejects_negative_values()
        {
            using var client = new ArgusClient("unused");

            Assert.That(client.MaxResponseBodySize, Is.Zero);
            Assert.That(() => client.MaxResponseBodySize = -1, Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public async Task Verify_that_response_with_Content_Length_above_MaxResponseBodySize_is_rejected_before_the_body_is_read()
        {
            var pipeName = $"argus-max-response-{Guid.NewGuid():N}";

            // The server announces 2 GB but sends nothing: the client must reject the header instead of allocating or waiting
            var serverTask = this.RunRawResponseServerAsync(pipeName, async server =>
            {
                await server.WriteAsync(Encoding.ASCII.GetBytes("ARGUS/1.0 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2147483647\r\n\r\n"));
                await server.FlushAsync();
            });

            using var client = new ArgusClient(pipeName) { MaxResponseBodySize = 1024 };

            var exception = await Assert.ThrowsAsync<ArgusProtocolException>(async () => await client.GetAsync("/big", timeout: TimeSpan.FromSeconds(5)));

            Assert.That(exception.Message, Does.Contain("1024"));
            await serverTask;
        }

        [Test]
        public async Task Verify_that_endless_chunked_response_is_rejected_when_it_exceeds_MaxResponseBodySize()
        {
            var pipeName = $"argus-max-response-{Guid.NewGuid():N}";

            var serverTask = this.RunRawResponseServerAsync(pipeName, async server =>
            {
                await server.WriteAsync(Encoding.ASCII.GetBytes("ARGUS/1.0 200 OK\r\nContent-Type: application/octet-stream\r\nTransfer-Encoding: chunked\r\n\r\n"));
                var chunk = Encoding.ASCII.GetBytes($"400\r\n{new string('x', 1024)}\r\n");

                // Keep sending until the client gives up and closes the pipe
                while (true)
                {
                    await server.WriteAsync(chunk);
                }
            });

            using var client = new ArgusClient(pipeName) { MaxResponseBodySize = 64 * 1024 };

            await Assert.ThrowsAsync<ArgusProtocolException>(async () => await client.GetAsync("/endless", timeout: TimeSpan.FromSeconds(5)));
            await serverTask;
        }

        [Test]
        public async Task Verify_that_compressed_response_is_rejected_when_its_decompressed_size_exceeds_MaxResponseBodySize()
        {
            var pipeName = $"argus-max-response-{Guid.NewGuid():N}";

            // 10 MB of zeros compresses to about 10 KB: small on the wire, large after decompression
            using var compressed = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(new byte[10 * 1024 * 1024]);
            }

            var bomb = compressed.ToArray();

            var serverTask = this.RunRawResponseServerAsync(pipeName, async server =>
            {
                await server.WriteAsync(Encoding.ASCII.GetBytes($"ARGUS/1.0 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\nContent-Length: {bomb.Length}\r\n\r\n"));
                await server.WriteAsync(bomb);
                await server.FlushAsync();
            });

            using var client = new ArgusClient(pipeName) { MaxResponseBodySize = 1024 * 1024 };

            Assert.That(bomb.Length, Is.LessThan(client.MaxResponseBodySize));
            await Assert.ThrowsAsync<ArgusProtocolException>(async () => await client.GetAsync("/bomb", timeout: TimeSpan.FromSeconds(5)));
            await serverTask;
        }

        [Test]
        public async Task Verify_that_response_within_MaxResponseBodySize_is_read()
        {
            var pipeName = $"argus-max-response-{Guid.NewGuid():N}";
            var serverTask = this.RunFakeServerAsync(pipeName, null, ArgusStatusCode.Ok, new string('x', 1024));

            using var client = new ArgusClient(pipeName) { MaxResponseBodySize = 1024 };

            var response = await client.GetAsync("/exact", timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.Body, Has.Length.EqualTo(1024));
            await serverTask;
        }

        /// <summary>
        /// Runs a single-shot pipe server that reads one request and then lets <paramref name="respond"/> write raw bytes;
        /// an <see cref="IOException"/> because the client closed the pipe ends the server quietly
        /// </summary>
        /// <param name="pipeName">The name of the pipe to listen on</param>
        /// <param name="respond">Writes the raw response</param>
        /// <returns>A task that completes when the server is done</returns>
        private Task RunRawResponseServerAsync(string pipeName, Func<NamedPipeServerStream, Task> respond)
        {
            return Task.Run(async () =>
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync();
                await this.requestSerializer.ReadAsync(server, CancellationToken.None);

                try
                {
                    await respond(server);
                }
                catch (IOException)
                {
                    // The client closed the pipe after rejecting the response
                }
            });
        }
    }
}
