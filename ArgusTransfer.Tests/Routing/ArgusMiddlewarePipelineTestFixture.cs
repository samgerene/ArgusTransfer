// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusMiddlewarePipelineTestFixture.cs">
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
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the middleware pipeline in <see cref="ArgusRouter"/>
    /// </summary>
    [TestFixture]
    public class ArgusMiddlewarePipelineTestFixture
    {
        private ArgusRouter router;

        [SetUp]
        public void SetUp()
        {
            this.router = new ArgusRouter();
        }

        [Test]
        public async Task Verify_that_global_middleware_executes_before_handler()
        {
            var executionOrder = new List<string>();

            this.router.UseMiddleware(new TrackingMiddleware("global", executionOrder));

            this.router.MapGet("/resource", context =>
            {
                executionOrder.Add("handler");

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

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(executionOrder, Is.EqualTo(new List<string> { "global-before", "handler", "global-after" }));
        }

        [Test]
        public async Task Verify_that_global_middleware_executes_in_registration_order()
        {
            var executionOrder = new List<string>();

            this.router.UseMiddleware(new TrackingMiddleware("first", executionOrder));
            this.router.UseMiddleware(new TrackingMiddleware("second", executionOrder));

            this.router.MapGet("/resource", context =>
            {
                executionOrder.Add("handler");

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

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(executionOrder, Is.EqualTo(new List<string>
            {
                "first-before", "second-before", "handler", "second-after", "first-after"
            }));
        }

        [Test]
        public async Task Verify_that_per_endpoint_middleware_executes_after_global()
        {
            var executionOrder = new List<string>();

            this.router.UseMiddleware(new TrackingMiddleware("global", executionOrder));

            this.router.MapGet("/resource", context =>
            {
                executionOrder.Add("handler");

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            }).WithMiddleware(new TrackingMiddleware("endpoint", executionOrder));

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(executionOrder, Is.EqualTo(new List<string>
            {
                "global-before", "endpoint-before", "handler", "endpoint-after", "global-after"
            }));
        }

        [Test]
        public async Task Verify_that_per_endpoint_middleware_executes_in_registration_order()
        {
            var executionOrder = new List<string>();

            this.router.MapGet("/resource", context =>
            {
                executionOrder.Add("handler");

                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            })
            .WithMiddleware(new TrackingMiddleware("ep-first", executionOrder))
            .WithMiddleware(new TrackingMiddleware("ep-second", executionOrder));

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(executionOrder, Is.EqualTo(new List<string>
            {
                "ep-first-before", "ep-second-before", "handler", "ep-second-after", "ep-first-after"
            }));
        }

        [Test]
        public async Task Verify_that_middleware_can_short_circuit_pipeline()
        {
            var handlerCalled = false;

            this.router.UseMiddleware(new ShortCircuitMiddleware());

            this.router.MapGet("/resource", context =>
            {
                handlerCalled = true;

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

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(handlerCalled, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
        }

        [Test]
        public async Task Verify_that_middleware_can_modify_response_after_handler()
        {
            this.router.UseMiddleware(new HeaderInjectingMiddleware("X-Modified", "true"));

            this.router.MapGet("/resource", context =>
            {
                context.Response = new ArgusResponse
                {
                    CorrelationToken = context.Request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/resource"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(context.Response.Headers["X-Modified"], Is.EqualTo("true"));
        }

        [Test]
        public async Task Verify_that_middleware_can_use_items_bag_to_share_data()
        {
            this.router.UseMiddleware(new ItemsWriterMiddleware("shared-key", "shared-value"));

            string capturedValue = null;

            this.router.MapGet("/resource", context =>
            {
                capturedValue = context.Items["shared-key"] as string;

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

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(capturedValue, Is.EqualTo("shared-value"));
        }

        [Test]
        public async Task Verify_that_global_middleware_does_not_run_for_unmatched_routes()
        {
            var middlewareCalled = false;

            this.router.UseMiddleware(new CallbackMiddleware(() => middlewareCalled = true));

            this.router.MapGet("/resource", context =>
            {
                context.Response = new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.Ok
                };

                return Task.CompletedTask;
            });

            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/unknown"
            };

            var context = new ArgusContext(request, CancellationToken.None);
            await this.router.RouteAsync(context);

            Assert.That(middlewareCalled, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
        }

        /// <summary>
        /// Test middleware that tracks before/after execution order
        /// </summary>
        private class TrackingMiddleware : IArgusMiddleware
        {
            private readonly string name;
            private readonly List<string> executionOrder;

            public TrackingMiddleware(string name, List<string> executionOrder)
            {
                this.name = name;
                this.executionOrder = executionOrder;
            }

            public async Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next)
            {
                this.executionOrder.Add($"{this.name}-before");
                await next(context);
                this.executionOrder.Add($"{this.name}-after");
            }
        }

        /// <summary>
        /// Test middleware that short-circuits the pipeline without calling next
        /// </summary>
        private class ShortCircuitMiddleware : IArgusMiddleware
        {
            public Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next)
            {
                context.Response = new ArgusResponse
                {
                    CorrelationToken = context.Request.CorrelationToken,
                    StatusCode = ArgusStatusCode.BadRequest
                };

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Test middleware that injects a header on the response after the handler runs
        /// </summary>
        private class HeaderInjectingMiddleware : IArgusMiddleware
        {
            private readonly string key;
            private readonly string value;

            public HeaderInjectingMiddleware(string key, string value)
            {
                this.key = key;
                this.value = value;
            }

            public async Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next)
            {
                await next(context);
                context.Response.Headers[this.key] = this.value;
            }
        }

        /// <summary>
        /// Test middleware that writes a value to the Items bag before calling next
        /// </summary>
        private class ItemsWriterMiddleware : IArgusMiddleware
        {
            private readonly string key;
            private readonly string value;

            public ItemsWriterMiddleware(string key, string value)
            {
                this.key = key;
                this.value = value;
            }

            public async Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next)
            {
                context.Items[this.key] = this.value;
                await next(context);
            }
        }

        /// <summary>
        /// Test middleware that invokes a callback before calling next
        /// </summary>
        private class CallbackMiddleware : IArgusMiddleware
        {
            private readonly System.Action callback;

            public CallbackMiddleware(System.Action callback)
            {
                this.callback = callback;
            }

            public async Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next)
            {
                this.callback();
                await next(context);
            }
        }
    }
}
