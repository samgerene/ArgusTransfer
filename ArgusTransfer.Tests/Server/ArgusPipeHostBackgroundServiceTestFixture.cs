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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Authentication;
    using ArgusTransfer.Client;
    using ArgusTransfer.Middleware;

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

        [Test]
        public async Task Verify_that_HandleRequestAsync_returns_generic_InternalServerError_for_unhandled_exception()
        {
            var router = new ArgusRouter();
            router.MapGet("/boom", _ => throw new InvalidOperationException("Password=hunter2"));

            var hostService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                Options.Create(new ArgusPipeHostOptions()),
                new PlainTextArgusBodySerializer());

            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/boom" };

            var response = await hostService.HandleRequestAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(response.CorrelationToken, Is.EqualTo(request.CorrelationToken));
            Assert.That(response.Body, Does.Not.Contain("hunter2"));
            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo(ArgusExceptionHandlerMiddleware.GenericErrorDetail));
            Assert.That(problem.Instance, Is.EqualTo(request.CorrelationToken.ToString()));

            this.mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<InvalidOperationException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task Verify_that_HandleRequestAsync_returns_InternalServerError_when_handler_sets_no_response()
        {
            var router = new ArgusRouter();
            router.MapGet("/forgetful", _ => Task.CompletedTask);

            var hostService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                Options.Create(new ArgusPipeHostOptions()),
                new PlainTextArgusBodySerializer());

            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/forgetful" };

            var response = await hostService.HandleRequestAsync(request);

            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(response.CorrelationToken, Is.EqualTo(request.CorrelationToken));
            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo(ArgusExceptionHandlerMiddleware.GenericErrorDetail));
            Assert.That(problem.Instance, Is.EqualTo(request.CorrelationToken.ToString()));

            this.mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task Verify_that_HandleRequestAsync_returns_InternalServerError_without_body_for_HEAD_without_response()
        {
            var router = new ArgusRouter();
            router.MapHead("/forgetful", _ => Task.CompletedTask);

            var hostService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                Options.Create(new ArgusPipeHostOptions()),
                new PlainTextArgusBodySerializer());

            var response = await hostService.HandleRequestAsync(new ArgusRequest { Verb = ArgusVerb.HEAD, Route = "/forgetful" });

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(response.Body, Is.Null);
        }

        /// <summary>
        /// Starts a host whose GET /work handler signals that it started and then runs <paramref name="work"/>, sends one
        /// request in the background and waits until the handler is running
        /// </summary>
        private async Task<(ArgusPipeHostBackgroundService Host, CancellationTokenSource Cts, Task Request)> StartHostWithRunningRequestAsync(Func<ArgusContext, Task> work)
        {
            var pipeName = $"argus-shutdown-test-{Guid.NewGuid():N}";
            var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var router = new ArgusRouter();
            router.MapGet("/work", async context =>
            {
                handlerStarted.TrySetResult();
                await work(context);
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok };
            });

            var hostService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                Options.Create(new ArgusPipeHostOptions { PipeName = pipeName, ShutdownDrainTimeout = TimeSpan.FromMilliseconds(100) }),
                new PlainTextArgusBodySerializer())
            {
                CancellationGracePeriod = TimeSpan.FromMilliseconds(200)
            };

            var cts = new CancellationTokenSource();
            await hostService.StartAsync(cts.Token);

            var client = new ArgusClient(pipeName);
            var request = Task.Run(async () =>
            {
                try
                {
                    await client.GetAsync("/work", timeout: TimeSpan.FromSeconds(10));
                }
                catch (Exception ex) when (ex is System.IO.IOException or TimeoutException)
                {
                    // the host may close the connection during shutdown
                }
                finally
                {
                    client.Dispose();
                }
            });

            await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            return (hostService, cts, request);
        }

        [Test]
        public async Task Verify_that_request_ignoring_cancellation_finishes_without_fault_after_shutdown()
        {
            // The handler ignores RequestAborted and keeps running past the drain timeout and the grace period
            var (hostService, cts, request) = await this.StartHostWithRunningRequestAsync(_ => Task.Delay(1500));
            var requestTasks = hostService.GetActiveRequestTasks();

            Assert.That(requestTasks, Has.Count.EqualTo(1));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);

            // When the handler finally completes, its request task releases the semaphore; that must not fault
            var requestTask = requestTasks.Single();
            await Task.WhenAny(requestTask, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.That(requestTask.IsCompleted, Is.True);
            Assert.That(requestTask.IsFaulted, Is.False, () => $"request task faulted: {requestTask.Exception?.GetBaseException()}");

            this.mockLogger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("did not finish after cancellation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            await request.WaitAsync(TimeSpan.FromSeconds(10));
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_request_honouring_cancellation_lets_shutdown_dispose_resources()
        {
            // The handler stops as soon as the drain timeout cancels RequestAborted
            var (hostService, cts, request) = await this.StartHostWithRunningRequestAsync(context => Task.Delay(Timeout.Infinite, context.RequestAborted));
            var requestTasks = hostService.GetActiveRequestTasks();

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);

            var requestTask = requestTasks.Single();

            Assert.That(requestTask.IsCompleted, Is.True, "the cancelled request finishes within the grace period");
            Assert.That(requestTask.IsFaulted, Is.False);

            this.mockLogger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("did not finish after cancellation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);

            await request.WaitAsync(TimeSpan.FromSeconds(10));
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_accept_failures_are_logged_and_the_host_keeps_running()
        {
            var pipeName = $"argus-accept-failure-test-{Guid.NewGuid():N}";

            // Another server instance that allows only one instance occupies the pipe name, so creating the host's
            // instances fails with a real pipe error until it is released
            var occupier = new System.IO.Pipes.NamedPipeServerStream(pipeName, System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);

            var hostService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                new ArgusRouter(),
                Options.Create(new ArgusPipeHostOptions { PipeName = pipeName }),
                new PlainTextArgusBodySerializer())
            {
                AcceptRetryInitialDelay = TimeSpan.FromMilliseconds(20)
            };

            using var cts = new CancellationTokenSource();
            await hostService.StartAsync(cts.Token);

            await Task.Delay(300);

            Assert.That(hostService.ExecuteTask.IsCompleted, Is.False, "the accept loop must survive pipe errors");
            this.mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("Accepting a connection on pipe")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);

            await occupier.DisposeAsync();

            // Once the pipe name is free again, the host recovers and serves clients
            using var client = new ArgusClient(pipeName);
            var response = await client.GetAsync("/unknown", timeout: TimeSpan.FromSeconds(10));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);

            Assert.That(hostService.ExecuteTask.IsFaulted, Is.False, "stopping must end the loop without an exception");
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 4)]
        [TestCase(5, 16)]
        [TestCase(6, 30)]
        [TestCase(1000, 30)]
        public void Verify_that_accept_retry_delay_doubles_up_to_30_seconds(int consecutiveFailures, int expectedSeconds)
        {
            Assert.That(this.service.GetAcceptRetryDelay(consecutiveFailures), Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
        }

        [Test]
        public async Task Verify_that_route_with_spaces_reaches_the_handler_unchanged_over_the_pipe()
        {
            var pipeName = $"argus-route-escaping-test-{Guid.NewGuid():N}";
            var router = new ArgusRouter();
            router.MapGet("/items/{name}", context =>
            {
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = context.RouteValues["name"] };
                return Task.CompletedTask;
            });

            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName);
            var response = await client.GetAsync("/items/a b 50%", timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo("a b 50%"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_response_header_with_CRLF_returns_InternalServerError_over_the_pipe()
        {
            var pipeName = $"argus-header-injection-test-{Guid.NewGuid():N}";
            var router = new ArgusRouter();
            router.MapGet("/items", context =>
            {
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = "data" };
                context.Response.Headers["X-Echo"] = "value\r\nX-Injected: yes";
                return Task.CompletedTask;
            });

            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName);
            var response = await client.GetAsync("/items", timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(response.Headers.ContainsKey("X-Injected"), Is.False);
            Assert.That(ArgusProblemDetails.TryRead(response, out _), Is.True);

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_streamed_body_over_the_limit_gets_BadRequest_promptly()
        {
            var pipeName = $"argus-early-response-stream-test-{Guid.NewGuid():N}";
            var router = CreateEchoRouter();
            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName, MaxRequestBodySize = 10_000 });

            using var client = new ArgusClient(pipeName);
            var stopwatch = Stopwatch.StartNew();

            // A 2 MB streamed body: the host rejects it after 10 KB and stops reading while the client is still writing
            var response = await client.PostAsync("/echo", new System.IO.MemoryStream(new byte[2_000_000]), "application/octet-stream", timeout: TimeSpan.FromSeconds(10));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(response.Body, Does.Contain("maximum allowed size"));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "the client must not wait for its timeout");

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_large_body_over_the_limit_gets_BadRequest_promptly()
        {
            var pipeName = $"argus-early-response-body-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(CreateEchoRouter(), new ArgusPipeHostOptions { PipeName = pipeName, MaxRequestBodySize = 10_000 });

            using var client = new ArgusClient(pipeName);
            var stopwatch = Stopwatch.StartNew();

            // A 2 MB Content-Length body: the host rejects it from the headers and never reads the body
            var response = await client.PostAsync("/echo", new string('x', 2_000_000), timeout: TimeSpan.FromSeconds(10));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "the client must not wait for its timeout");

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_oversized_headers_sent_by_ArgusClient_get_BadRequest_promptly()
        {
            var pipeName = $"argus-early-response-header-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(CreateEchoRouter(), new ArgusPipeHostOptions { PipeName = pipeName, MaxRequestHeaderSize = 1024 });

            using var client = new ArgusClient(pipeName);
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/echo", Body = "hello" };
            request.Headers["X-Big"] = new string('x', 200_000);

            var stopwatch = Stopwatch.StartNew();
            var response = await client.SendAsync(request, TimeSpan.FromSeconds(10));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(response.Body, Does.Contain("header block exceeds"));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "the client must not wait for its timeout");

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public void Verify_that_RequestReadTimeout_and_MaxConcurrentConnections_have_defaults()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(options.RequestReadTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.MaxConcurrentConnections, Is.EqualTo(100));
        }

        private static async Task<System.IO.Pipes.NamedPipeClientStream> ConnectRawAsync(string pipeName, int timeoutMilliseconds = 5000)
        {
            var pipe = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(timeoutMilliseconds);
                return pipe;
            }
            catch
            {
                await pipe.DisposeAsync();
                throw;
            }
        }

        [Test]
        public async Task Verify_that_idle_client_is_disconnected_after_RequestReadTimeout()
        {
            var pipeName = $"argus-idle-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(new ArgusRouter(), new ArgusPipeHostOptions { PipeName = pipeName, RequestReadTimeout = TimeSpan.FromMilliseconds(300) });

            await using var pipe = await ConnectRawAsync(pipeName);
            var stopwatch = Stopwatch.StartNew();

            // Send nothing: the host must close the connection, which the client sees as end of stream
            var read = await pipe.ReadAsync(new byte[16]).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(read, Is.Zero);
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(4)));

            this.mockLogger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("did not send a complete request")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_client_sending_an_incomplete_request_is_disconnected_after_RequestReadTimeout()
        {
            var pipeName = $"argus-trickle-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(new ArgusRouter(), new ArgusPipeHostOptions { PipeName = pipeName, RequestReadTimeout = TimeSpan.FromMilliseconds(300) });

            await using var pipe = await ConnectRawAsync(pipeName);

            // The request line and part of a header, then silence
            await pipe.WriteAsync(System.Text.Encoding.ASCII.GetBytes("GET /items ARGUS/1.0\r\nX-Partial: "));

            var read = await pipe.ReadAsync(new byte[16]).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(read, Is.Zero);

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_client_trickling_bytes_is_disconnected_after_RequestReadTimeout()
        {
            var pipeName = $"argus-slow-trickle-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(new ArgusRouter(), new ArgusPipeHostOptions { PipeName = pipeName, RequestReadTimeout = TimeSpan.FromMilliseconds(500) });

            await using var pipe = await ConnectRawAsync(pipeName);
            using var stopTrickling = new CancellationTokenSource();

            // Keep the connection busy with one byte every 50 ms of a header line that never ends: the timeout limits the
            // total time to receive the request, not the idle time, so activity does not extend it
            var trickle = Task.Run(async () =>
            {
                try
                {
                    await pipe.WriteAsync(System.Text.Encoding.ASCII.GetBytes("GET /items ARGUS/1.0\r\nX-Slow: "), stopTrickling.Token);

                    while (!stopTrickling.IsCancellationRequested)
                    {
                        await pipe.WriteAsync(new[] { (byte)'a' }, stopTrickling.Token);
                        await Task.Delay(50, stopTrickling.Token);
                    }
                }
                catch (Exception ex) when (ex is System.IO.IOException or OperationCanceledException)
                {
                    // the host closed the connection, or the test is done
                }
            });

            var stopwatch = Stopwatch.StartNew();
            var read = await pipe.ReadAsync(new byte[16]).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(read, Is.Zero, "the host must close the connection although the client keeps sending");
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(4)));

            await stopTrickling.CancelAsync();
            await trickle.WaitAsync(TimeSpan.FromSeconds(5));
            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_MaxConcurrentConnections_makes_further_clients_wait()
        {
            var pipeName = $"argus-connection-limit-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(
                new ArgusRouter(),
                new ArgusPipeHostOptions { PipeName = pipeName, MaxConcurrentConnections = 1, RequestReadTimeout = Timeout.InfiniteTimeSpan });

            var first = await ConnectRawAsync(pipeName);

            // While the first client holds the only connection slot, the second client is not served. It connects to the
            // pipe instance the host keeps ready (Windows) or is queued in the socket's listen backlog (Linux, macOS), and
            // must survive until a slot frees up instead of being reset when the first connection closes
            var secondClient = Task.Run(async () =>
            {
                await using var second = await ConnectRawAsync(pipeName, timeoutMilliseconds: 10_000);
                await new ArgusRequestSerializer().WriteAsync(second, new ArgusRequest { Verb = ArgusVerb.GET, Route = "/unknown" });
                return await new ArgusResponseSerializer().ReadAsync(second, CancellationToken.None);
            });

            var completedFirst = await Task.WhenAny(secondClient, Task.Delay(500));

            Assert.That(completedFirst, Is.Not.SameAs(secondClient), "the second client must not be served while the first holds the only connection slot");

            await first.DisposeAsync();

            // Once the first connection is closed, the second client is served
            var response = await secondClient.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public void Verify_that_MaxRequestHeaderSize_defaults_to_32_KB()
        {
            Assert.That(new ArgusPipeHostOptions().MaxRequestHeaderSize, Is.EqualTo(32 * 1024));
        }

        [Test]
        public async Task Verify_that_oversized_header_block_over_the_pipe_returns_BadRequest()
        {
            var pipeName = $"argus-header-limit-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(new ArgusRouter(), new ArgusPipeHostOptions { PipeName = pipeName, MaxRequestHeaderSize = 1024 });

            using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000);

            // Write in the background: the server stops reading at the limit, so the rest of the write never completes
            var oversized = System.Text.Encoding.ASCII.GetBytes("GET /items ARGUS/1.0\r\nX-Big: " + new string('x', 8 * 1024) + "\r\n\r\n");
            var writeTask = Task.Run(async () =>
            {
                try
                {
                    await pipe.WriteAsync(oversized);
                }
                catch (System.IO.IOException)
                {
                    // the server closes the pipe after responding
                }
            });

            var response = await new ArgusResponseSerializer().ReadAsync(pipe, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(response.Body, Does.Contain("header block exceeds the maximum allowed size of 1024 bytes"));

            await writeTask.WaitAsync(TimeSpan.FromSeconds(5));
            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_malformed_request_over_the_pipe_returns_BadRequest()
        {
            var pipeName = $"argus-malformed-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(new ArgusRouter(), new ArgusPipeHostOptions { PipeName = pipeName });

            using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000);

            var malformed = System.Text.Encoding.ASCII.GetBytes("FETCH /items ARGUS/1.0\r\n\r\n");
            await pipe.WriteAsync(malformed);

            var response = await new ArgusResponseSerializer().ReadAsync(pipe, CancellationToken.None);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(response.Body, Does.Contain("Unknown verb"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_response_with_unsupported_Content_Encoding_returns_InternalServerError_not_BadRequest()
        {
            var pipeName = $"argus-response-encoding-test-{Guid.NewGuid():N}";
            var router = new ArgusRouter();
            router.MapGet("/items", context =>
            {
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = "data" };
                context.Response.Headers[ArgusHeaderNames.ContentEncoding] = "br";
                return Task.CompletedTask;
            });

            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName);
            var response = await client.GetAsync("/items", timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo(ArgusExceptionHandlerMiddleware.GenericErrorDetail));
            Assert.That(response.Body, Does.Not.Contain("Unsupported"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_missing_response_over_the_pipe_returns_InternalServerError_and_is_not_retried()
        {
            var pipeName = $"argus-missing-response-test-{Guid.NewGuid():N}";
            var invocationCount = 0;

            var router = new ArgusRouter();
            router.MapGet("/forgetful", _ =>
            {
                Interlocked.Increment(ref invocationCount);
                return Task.CompletedTask;
            });

            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName)
            {
                RetryPolicy = new ArgusRetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(10) }
            };

            var response = await client.GetAsync("/forgetful", timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(ArgusProblemDetails.TryRead(response, out _), Is.True);
            Assert.That(invocationCount, Is.EqualTo(1));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_HandleRequestAsync_returns_InternalServerError_without_body_for_HEAD()
        {
            var router = new ArgusRouter();
            router.MapHead("/boom", _ => throw new InvalidOperationException("failure"));

            var hostService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                Options.Create(new ArgusPipeHostOptions()),
                new PlainTextArgusBodySerializer());

            var response = await hostService.HandleRequestAsync(new ArgusRequest { Verb = ArgusVerb.HEAD, Route = "/boom" });

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(response.Body, Is.Null);
        }

        [Test]
        public async Task Verify_that_handler_exception_over_the_pipe_returns_InternalServerError_and_is_not_retried()
        {
            var pipeName = $"argus-handler-exception-test-{Guid.NewGuid():N}";
            var invocationCount = 0;

            var router = new ArgusRouter();
            router.MapGet("/boom", _ =>
            {
                Interlocked.Increment(ref invocationCount);
                throw new InvalidOperationException("Password=hunter2");
            });

            var hostService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                Options.Create(new ArgusPipeHostOptions { PipeName = pipeName }),
                new PlainTextArgusBodySerializer());

            using var cts = new CancellationTokenSource();
            await hostService.StartAsync(cts.Token);

            using var client = new ArgusClient(pipeName)
            {
                RetryPolicy = new ArgusRetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(10) }
            };

            var response = await client.GetAsync("/boom", timeout: TimeSpan.FromSeconds(10));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(response.Body, Does.Not.Contain("hunter2"));
            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo(ArgusExceptionHandlerMiddleware.GenericErrorDetail));
            Assert.That(invocationCount, Is.EqualTo(1));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
        }

        private async Task<(ArgusPipeHostBackgroundService Host, CancellationTokenSource Cts)> StartHostAsync(ArgusRouter router, ArgusPipeHostOptions options)
        {
            var hostService = new ArgusPipeHostBackgroundService(
                this.mockLogger.Object,
                router,
                Options.Create(options),
                new PlainTextArgusBodySerializer());

            var cts = new CancellationTokenSource();
            await hostService.StartAsync(cts.Token);

            return (hostService, cts);
        }

        [Test]
        public async Task Verify_that_non_ascii_string_body_round_trips_over_the_pipe()
        {
            var pipeName = $"argus-non-ascii-test-{Guid.NewGuid():N}";
            var router = new ArgusRouter();
            router.MapPost("/echo", context =>
            {
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = context.Request.Body };
                return Task.CompletedTask;
            });

            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName);
            var response = await client.PostAsync("/echo", "héllo wörld € 😀", timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo("héllo wörld € 😀"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_binary_streamed_bodies_round_trip_over_the_pipe()
        {
            var pipeName = $"argus-binary-test-{Guid.NewGuid():N}";
            var payload = new byte[100_000];
            new Random(11).NextBytes(payload);
            byte[] receivedByServer = null;

            var router = new ArgusRouter();
            router.MapPost("/echo", async context =>
            {
                var copy = new System.IO.MemoryStream();
                await context.Request.BodyStream.CopyToAsync(copy);
                receivedByServer = copy.ToArray();

                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, BodyStream = new System.IO.MemoryStream(receivedByServer) };
            });

            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName);
            var response = await client.PostAsync("/echo", new System.IO.MemoryStream(payload), "application/octet-stream", timeout: TimeSpan.FromSeconds(5));

            var receivedByClient = new System.IO.MemoryStream();
            await response.BodyStream.CopyToAsync(receivedByClient);

            Assert.That(receivedByServer, Is.EqualTo(payload));
            Assert.That(receivedByClient.ToArray(), Is.EqualTo(payload));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_oversized_request_body_returns_BadRequest_over_the_pipe()
        {
            var pipeName = $"argus-oversized-test-{Guid.NewGuid():N}";
            var router = new ArgusRouter();
            router.MapPost("/echo", context =>
            {
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok };
                return Task.CompletedTask;
            });

            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName, MaxRequestBodySize = 10 });

            using var client = new ArgusClient(pipeName);
            var response = await client.PostAsync("/echo", new string('x', 100), timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(response.Body, Does.Contain("maximum allowed size"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        private static readonly string CompressibleText = string.Concat(System.Linq.Enumerable.Repeat("compress me, compress me ", 400));

        private static ArgusRouter CreateEchoRouter(Action<ArgusRequest> inspect = null)
        {
            var router = new ArgusRouter();
            router.MapPost("/echo", context =>
            {
                inspect?.Invoke(context.Request);
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = context.Request.Body };
                return Task.CompletedTask;
            });

            return router;
        }

        [Test]
        public async Task Verify_that_authentication_middleware_protects_endpoints_over_the_pipe()
        {
            var pipeName = $"argus-authentication-test-{Guid.NewGuid():N}";
            string authenticatedUser = null;

            var router = new ArgusRouter();
            router.UseMiddleware(new ArgusAuthenticationMiddleware(
                new ArgusTransfer.Tests.Middleware.BearerTokenTestHandler(),
                new Mock<ILogger<ArgusAuthenticationMiddleware>>().Object));
            router.MapGet("/secure", context =>
            {
                authenticatedUser = context.User?.Identity?.Name;
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok };
                return Task.CompletedTask;
            });
            router.MapGet("/health", context =>
            {
                context.Response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok };
                return Task.CompletedTask;
            }).AllowAnonymous();

            var (hostService, cts) = await this.StartHostAsync(router, new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName);

            var anonymous = await client.GetAsync("/secure", timeout: TimeSpan.FromSeconds(5));
            var health = await client.GetAsync("/health", timeout: TimeSpan.FromSeconds(5));

            var authenticatedRequest = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/secure" };
            authenticatedRequest.SetAuthorization("Bearer", "secret");
            var authenticated = await client.SendAsync(authenticatedRequest, TimeSpan.FromSeconds(5));

            Assert.That(anonymous.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
            Assert.That(ArgusProblemDetails.TryRead(anonymous, out _), Is.True);
            Assert.That(health.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(authenticated.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(authenticatedUser, Is.EqualTo("alice"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_handler_receives_parsed_Authorization_over_the_pipe()
        {
            var pipeName = $"argus-authorization-test-{Guid.NewGuid():N}";
            ArgusRequest receivedRequest = null;

            var (hostService, cts) = await this.StartHostAsync(CreateEchoRouter(r => receivedRequest = r), new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName);
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/echo", Body = "hello" };
            request.SetAuthorization("Bearer", "token123");

            var response = await client.SendAsync(request, TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(receivedRequest.AuthorizationScheme, Is.EqualTo("Bearer"));
            Assert.That(receivedRequest.AuthorizationParameter, Is.EqualTo("token123"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_compressed_request_and_response_round_trip_over_the_pipe()
        {
            var pipeName = $"argus-gzip-test-{Guid.NewGuid():N}";
            ArgusRequest receivedRequest = null;

            var options = new ArgusPipeHostOptions { PipeName = pipeName };
            options.Compression.Enabled = true;

            var (hostService, cts) = await this.StartHostAsync(CreateEchoRouter(r => receivedRequest = r), options);

            using var client = new ArgusClient(pipeName);
            client.Compression.Enabled = true;

            var response = await client.PostAsync("/echo", CompressibleText, timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo(CompressibleText));
            Assert.That(response.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False, "decoded responses drop the header");
            Assert.That(receivedRequest.Body, Is.EqualTo(CompressibleText));
            Assert.That(receivedRequest.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False, "decoded requests drop the header");
            Assert.That(receivedRequest.Headers[ArgusHeaderNames.AcceptEncoding], Is.EqualTo("gzip"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_host_compresses_response_on_the_wire_only_when_accepted()
        {
            var pipeName = $"argus-gzip-wire-test-{Guid.NewGuid():N}";
            var options = new ArgusPipeHostOptions { PipeName = pipeName };
            options.Compression.Enabled = true;

            var (hostService, cts) = await this.StartHostAsync(CreateEchoRouter(), options);

            async Task<(string Head, ArgusResponse Response)> SendRawAsync(string acceptEncoding)
            {
                var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/echo", Body = CompressibleText };

                if (acceptEncoding != null)
                {
                    request.Headers[ArgusHeaderNames.AcceptEncoding] = acceptEncoding;
                }

                using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
                await pipe.ConnectAsync(5000);
                await new ArgusRequestSerializer().WriteAsync(pipe, request);

                using var raw = new System.IO.MemoryStream();
                await pipe.CopyToAsync(raw);

                var bytes = raw.ToArray();
                var text = System.Text.Encoding.Latin1.GetString(bytes);
                var head = text.Substring(0, text.IndexOf("\r\n\r\n", StringComparison.Ordinal));

                raw.Position = 0;
                return (head, await new ArgusResponseSerializer().ReadAsync(raw, CancellationToken.None));
            }

            var (acceptedHead, acceptedResponse) = await SendRawAsync("gzip");
            var (plainHead, plainResponse) = await SendRawAsync(null);

            Assert.That(acceptedHead, Does.Contain("Content-Encoding: gzip"));
            Assert.That(acceptedResponse.Body, Is.EqualTo(CompressibleText));
            Assert.That(plainHead, Does.Not.Contain("Content-Encoding"));
            Assert.That(plainResponse.Body, Is.EqualTo(CompressibleText));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_host_decodes_compressed_request_when_its_own_compression_is_disabled()
        {
            var pipeName = $"argus-gzip-decode-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(CreateEchoRouter(), new ArgusPipeHostOptions { PipeName = pipeName });

            using var client = new ArgusClient(pipeName);
            client.Compression.Enabled = true;

            var response = await client.PostAsync("/echo", CompressibleText, timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Body, Is.EqualTo(CompressibleText));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }

        [Test]
        public async Task Verify_that_host_rejects_request_whose_decompressed_size_exceeds_the_limit()
        {
            var pipeName = $"argus-gzip-bomb-test-{Guid.NewGuid():N}";
            var (hostService, cts) = await this.StartHostAsync(CreateEchoRouter(), new ArgusPipeHostOptions { PipeName = pipeName, MaxRequestBodySize = 10_000 });

            using var client = new ArgusClient(pipeName);
            client.Compression.Enabled = true;

            var response = await client.PostAsync("/echo", new string('a', 200_000), timeout: TimeSpan.FromSeconds(5));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(response.Body, Does.Contain("Decompressed body size"));

            await cts.CancelAsync();
            await hostService.StopAsync(CancellationToken.None);
            cts.Dispose();
        }
    }
}
