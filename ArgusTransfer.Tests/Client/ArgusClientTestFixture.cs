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

        [Test]
        public void Verify_that_DefaultTimeout_is_30_seconds()
        {
            using var client = new ArgusClient("test-pipe");
            Assert.That(client.DefaultTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void Verify_that_SendAsync_throws_TimeoutException_when_server_is_slow()
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

            Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await client.SendAsync(request, timeout: TimeSpan.FromMilliseconds(200));
            });
        }

        [Test]
        public void Verify_that_per_request_timeout_overrides_default()
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
            Assert.ThrowsAsync<TimeoutException>(async () =>
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
        public void Verify_that_GetEnsureSuccessAsync_throws_when_status_is_not_2xx()
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

            var exception = Assert.ThrowsAsync<ArgusRequestException>(async () =>
            {
                await client.GetEnsureSuccessAsync("/healthendpoint");
            });

            serverTask.GetAwaiter().GetResult();

            Assert.That(exception.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
            Assert.That(exception.ReasonPhrase, Is.EqualTo(ArgusStatusCode.NotFound.ToReasonPhrase()));
            Assert.That(exception.ResponseBody, Is.EqualTo("missing"));
        }
    }
}
