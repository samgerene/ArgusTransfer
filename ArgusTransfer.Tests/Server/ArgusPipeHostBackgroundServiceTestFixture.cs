// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusPipeHostBackgroundServiceTestFixture.cs">
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

namespace ArgusTransfer.Transport.Tests.Server
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Client;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;
    using ArgusTransfer.Serialization;
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

            this.service = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());
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
        public async Task Verify_that_wrong_verb_returns_NotImplemented()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.PATCH,
                Route = "/test"
            };

            var response = await this.service.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotImplemented));
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

        [Test]
        public void Verify_that_ShutdownDrainTimeout_defaults_to_30_seconds()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(options.ShutdownDrainTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void Verify_that_MaxRequestBodySize_defaults_to_1MB()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(options.MaxRequestBodySize, Is.EqualTo(1_048_576));
        }

        [Test]
        public async Task Verify_that_StopAsync_waits_for_in_flight_request_to_complete()
        {
            var pipeName = $"argus-drain-{Guid.NewGuid():N}";
            var handlerCompleted = false;

            var router = new ArgusRouter();

            router.MapGet("/slow", async context =>
            {
                await Task.Delay(500, context.RequestAborted);

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok,
                    Body = "done"
                };

                handlerCompleted = true;
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                PipeName = pipeName,
                ShutdownDrainTimeout = TimeSpan.FromSeconds(5)
            });

            var drainService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            await drainService.StartAsync(CancellationToken.None);

            using var client = new ArgusClient(pipeName);
            var responseTask = client.GetAsync("/slow", timeout: TimeSpan.FromSeconds(10));

            await Task.Delay(100);

            await drainService.StopAsync(CancellationToken.None);

            var response = await responseTask;

            Assert.That(handlerCompleted, Is.True);
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo("done"));
        }

        [Test]
        public async Task Verify_that_StopAsync_cancels_requests_after_drain_timeout()
        {
            var pipeName = $"argus-drain-timeout-{Guid.NewGuid():N}";

            var router = new ArgusRouter();

            router.MapGet("/very-slow", async context =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    // Expected — drain timeout cancelled us
                }

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                PipeName = pipeName,
                ShutdownDrainTimeout = TimeSpan.FromMilliseconds(200)
            });

            var drainService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            await drainService.StartAsync(CancellationToken.None);

            using var client = new ArgusClient(pipeName);
            _ = client.GetAsync("/very-slow", timeout: TimeSpan.FromSeconds(10));

            await Task.Delay(100);

            var stopwatch = Stopwatch.StartNew();
            await drainService.StopAsync(CancellationToken.None);
            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void Verify_that_RequestTimeout_defaults_to_60_seconds()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(options.RequestTimeout, Is.EqualTo(TimeSpan.FromSeconds(60)));
        }

        [Test]
        public async Task Verify_that_timed_out_request_returns_ServiceUnavailable()
        {
            var router = new ArgusRouter();

            router.MapGet("/slow", async context =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                RequestTimeout = TimeSpan.FromMilliseconds(100)
            });

            var timeoutService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/slow"
            };

            var response = await timeoutService.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.ServiceUnavailable));
            Assert.That(response.Body, Does.Contain("timed out"));
        }

        [Test]
        public async Task Verify_that_request_within_timeout_succeeds()
        {
            var router = new ArgusRouter();

            router.MapGet("/fast", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok,
                    Body = "fast"
                };

                return Task.CompletedTask;
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(5)
            });

            var timeoutService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/fast"
            };

            var response = await timeoutService.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo("fast"));
        }

        [Test]
        public async Task Verify_that_infinite_timeout_disables_per_request_timeout()
        {
            var router = new ArgusRouter();

            router.MapGet("/delayed", async context =>
            {
                await Task.Delay(200);

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok,
                    Body = "completed"
                };
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                RequestTimeout = Timeout.InfiniteTimeSpan
            });

            var timeoutService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/delayed"
            };

            var response = await timeoutService.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo("completed"));
        }

        [Test]
        public void Verify_that_caller_cancellation_propagates_through_timeout()
        {
            var router = new ArgusRouter();

            router.MapGet("/slow", async context =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };
            });

            var options = Options.Create(new ArgusPipeHostOptions());

            var timeoutService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/slow"
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.That(
                async () => await timeoutService.HandleRequestAsync(request, cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void Verify_that_MaxConcurrentRequests_defaults_to_10()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(options.MaxConcurrentRequests, Is.EqualTo(10));
        }

        [Test]
        public async Task Verify_that_request_within_concurrency_limit_succeeds()
        {
            var router = new ArgusRouter();

            router.MapGet("/test", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok,
                    Body = "ok"
                };

                return Task.CompletedTask;
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                MaxConcurrentRequests = 5
            });

            var concurrencyService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/test"
            };

            var response = await concurrencyService.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
        }

        [Test]
        public async Task Verify_that_concurrent_requests_beyond_limit_return_ServiceUnavailable()
        {
            var pipeName = $"argus-concurrency-test-{Guid.NewGuid():N}";
            var handlerBarrier = new TaskCompletionSource();

            var router = new ArgusRouter();

            router.MapGet("/slow", async context =>
            {
                await handlerBarrier.Task;

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                PipeName = pipeName,
                MaxConcurrentRequests = 1,
                RequestTimeout = Timeout.InfiniteTimeSpan
            });

            var concurrencyService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            using var cts = new CancellationTokenSource();

            await concurrencyService.StartAsync(cts.Token);

            // First request: occupies the single slot
            var client1 = new ArgusClient(pipeName);
            var request1Task = client1.SendAsync(new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/slow"
            });

            // Give the first request time to acquire the semaphore
            await Task.Delay(200);

            // Second request: should be rejected with 503
            var client2 = new ArgusClient(pipeName);
            var response2 = await client2.SendAsync(new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/slow"
            });

            Assert.That(response2.StatusCode, Is.EqualTo(ArgusStatusCode.ServiceUnavailable));
            Assert.That(response2.Body, Does.Contain("concurrency limit"));
            Assert.That(concurrencyService.RejectedRequestCount, Is.EqualTo(1));

            // Release the first request
            handlerBarrier.SetResult();
            await request1Task;

            await cts.CancelAsync();
            await concurrencyService.StopAsync(CancellationToken.None);
        }

        [Test]
        public async Task Verify_that_concurrency_slot_is_released_after_request_completes()
        {
            var pipeName = $"argus-slot-release-test-{Guid.NewGuid():N}";
            var handlerBarrier = new TaskCompletionSource();

            var router = new ArgusRouter();

            router.MapGet("/slow", async context =>
            {
                await handlerBarrier.Task;

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };
            });

            router.MapGet("/fast", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok,
                    Body = "fast"
                };

                return Task.CompletedTask;
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                PipeName = pipeName,
                MaxConcurrentRequests = 1,
                RequestTimeout = Timeout.InfiniteTimeSpan
            });

            var concurrencyService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            using var cts = new CancellationTokenSource();

            await concurrencyService.StartAsync(cts.Token);

            // First request: occupies the single slot
            var client1 = new ArgusClient(pipeName);
            var request1Task = client1.SendAsync(new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/slow"
            });

            // Give the first request time to acquire the semaphore
            await Task.Delay(200);

            // Release the first request so the slot frees up
            handlerBarrier.SetResult();
            await request1Task;

            // Give time for semaphore release
            await Task.Delay(100);

            // Next request should succeed
            var client2 = new ArgusClient(pipeName);
            var response2 = await client2.SendAsync(new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/fast"
            });

            Assert.That(response2.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response2.Body, Is.EqualTo("fast"));

            await cts.CancelAsync();
            await concurrencyService.StopAsync(CancellationToken.None);
        }

        [Test]
        public void Verify_that_constructor_with_registry_creates_service()
        {
            var registry = new ArgusBodySerializerRegistry(new[] { new PlainTextArgusBodySerializer() });

            var options = Options.Create(new ArgusPipeHostOptions());

            var registryService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                new ArgusRouter(),
                options,
                registry);

            Assert.That(registryService, Is.Not.Null);
        }

        [Test]
        public async Task Verify_that_CurrentRequestCount_reflects_in_flight_requests()
        {
            var pipeName = $"argus-count-test-{Guid.NewGuid():N}";
            var handlerBarrier = new TaskCompletionSource();

            var router = new ArgusRouter();

            router.MapGet("/slow", async context =>
            {
                await handlerBarrier.Task;

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                PipeName = pipeName,
                RequestTimeout = Timeout.InfiniteTimeSpan
            });

            var countService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                new PlainTextArgusBodySerializer());

            using var cts = new CancellationTokenSource();

            await countService.StartAsync(cts.Token);

            var client1 = new ArgusClient(pipeName);
            _ = client1.SendAsync(new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/slow"
            });

            await Task.Delay(200);

            Assert.That(countService.CurrentRequestCount, Is.EqualTo(1));

            handlerBarrier.SetResult();

            await Task.Delay(200);

            Assert.That(countService.CurrentRequestCount, Is.EqualTo(0));

            await cts.CancelAsync();
            await countService.StopAsync(CancellationToken.None);
        }

        [Test]
        public async Task Verify_that_unsupported_accept_type_returns_NotAcceptable()
        {
            var pipeName = $"argus-accept-test-{Guid.NewGuid():N}";

            var registry = new ArgusBodySerializerRegistry(new[] { new PlainTextArgusBodySerializer() });

            var router = new ArgusRouter();

            router.MapGet("/test", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok,
                    Body = "ok"
                };

                return Task.CompletedTask;
            });

            var options = Options.Create(new ArgusPipeHostOptions
            {
                PipeName = pipeName
            });

            var acceptService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                options,
                registry);

            using var cts = new CancellationTokenSource();

            await acceptService.StartAsync(cts.Token);

            using var client = new ArgusClient(pipeName);
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/test"
            };

            request.Headers[ArgusHeaderNames.Accept] = "application/xml";

            var response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotAcceptable));

            await cts.CancelAsync();
            await acceptService.StopAsync(CancellationToken.None);
        }
    }
}
