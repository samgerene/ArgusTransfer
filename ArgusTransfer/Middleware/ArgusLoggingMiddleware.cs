// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusLoggingMiddleware.cs">
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
        /// and logs the response with elapsed time
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

            await next(context);

            stopwatch.Stop();

            this.logger.LogInformation(
                "ARGUS {Verb} {Route} [{CorrelationToken}] -> {StatusCode} in {ElapsedMs}ms",
                context.Request.Verb,
                context.Request.Route,
                context.CorrelationToken,
                context.Response?.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
