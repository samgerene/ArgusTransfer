// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusLoggingMiddleware.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Middleware
{
    using System.Diagnostics;
    using System.Threading.Tasks;

    using ArgusTransfer.Routing;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Middleware that logs the start and completion of each Argus request,
    /// including the verb, route, correlation token, status code, and elapsed time
    /// </summary>
    public class ArgusLoggingMiddleware : IArgusMiddleware
    {
        /// <summary>
        /// The <see cref="ILogger{ArgusLoggingMiddleware}"/> used for logging
        /// </summary>
        private readonly ILogger<ArgusLoggingMiddleware> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusLoggingMiddleware"/> class
        /// </summary>
        /// <param name="logger">
        /// The <see cref="ILogger{ArgusLoggingMiddleware}"/> used for logging
        /// </param>
        public ArgusLoggingMiddleware(ILogger<ArgusLoggingMiddleware> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Logs the incoming request, invokes the next middleware in the pipeline,
        /// and logs the response with elapsed time. When the rest of the pipeline throws (including cancellation),
        /// a warning with the elapsed time is logged instead and the exception propagates unchanged.
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> for the current request
        /// </param>
        /// <param name="next">
        /// The <see cref="ArgusRequestDelegate"/> representing the next middleware in the pipeline
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public async Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next)
        {
            var stopwatch = Stopwatch.StartNew();

            this.logger.LogInformation(
                "ARGUS {Verb} {Route} [{CorrelationToken}]",
                context.Request.Verb,
                context.Request.Route,
                context.CorrelationToken);

            var completed = false;

            try
            {
                await next(context);
                completed = true;
            }
            finally
            {
                stopwatch.Stop();

                if (completed)
                {
                    this.logger.LogInformation(
                        "ARGUS {Verb} {Route} [{CorrelationToken}] -> {StatusCode} in {ElapsedMs}ms",
                        context.Request.Verb,
                        context.Request.Route,
                        context.CorrelationToken,
                        context.Response?.StatusCode,
                        stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    // The exception itself is not logged here: it propagates to the exception handler or the host, which log it
                    this.logger.LogWarning(
                        "ARGUS {Verb} {Route} [{CorrelationToken}] -> failed in {ElapsedMs}ms",
                        context.Request.Verb,
                        context.Request.Route,
                        context.CorrelationToken,
                        stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}
