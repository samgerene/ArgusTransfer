// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusExceptionHandlerMiddleware.cs">
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
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Middleware that catches unhandled exceptions thrown by later middleware or the endpoint handler,
    /// logs them, and replaces the response with a <see cref="ArgusStatusCode.InternalServerError"/>
    /// <see cref="ArgusProblemDetails"/> response whose <see cref="ArgusProblemDetails.Instance"/> is the
    /// request correlation token. By default the response contains a generic message only; see
    /// <see cref="ArgusExceptionHandlerOptions.IncludeExceptionDetails"/>.
    /// </summary>
    /// <remarks>
    /// An <see cref="OperationCanceledException"/> raised because <see cref="ArgusContext.RequestAborted"/> was
    /// cancelled (request timeout or server shutdown) is not handled, so the pipe host can respond to it as usual.
    /// Register the middleware with <c>AddArgusExceptionHandler()</c> to have it run as the outermost global middleware.
    /// </remarks>
    public class ArgusExceptionHandlerMiddleware : IArgusMiddleware
    {
        /// <summary>
        /// The generic <see cref="ArgusProblemDetails.Detail"/> returned when exception details are not included
        /// </summary>
        public const string GenericErrorDetail = "An unexpected error occurred while processing the request.";

        /// <summary>
        /// The <see cref="ILogger{ArgusExceptionHandlerMiddleware}"/> used to log unhandled exceptions
        /// </summary>
        private readonly ILogger<ArgusExceptionHandlerMiddleware> logger;

        /// <summary>
        /// The <see cref="ArgusExceptionHandlerOptions"/> that configure the middleware
        /// </summary>
        private readonly ArgusExceptionHandlerOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusExceptionHandlerMiddleware"/> class
        /// </summary>
        /// <param name="logger">
        /// The <see cref="ILogger{ArgusExceptionHandlerMiddleware}"/> used to log unhandled exceptions
        /// </param>
        /// <param name="options">
        /// The <see cref="IOptions{ArgusExceptionHandlerOptions}"/> that configure the middleware.
        /// When <c>null</c>, the default <see cref="ArgusExceptionHandlerOptions"/> are used
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="logger"/> is <c>null</c>
        /// </exception>
        public ArgusExceptionHandlerMiddleware(ILogger<ArgusExceptionHandlerMiddleware> logger, IOptions<ArgusExceptionHandlerOptions> options = null)
        {
            ArgumentNullException.ThrowIfNull(logger);

            this.logger = logger;
            this.options = options?.Value ?? new ArgusExceptionHandlerOptions();
        }

        /// <summary>
        /// Invokes the next middleware in the pipeline and, when it throws an unhandled exception,
        /// logs the exception and sets <see cref="ArgusContext.Response"/> to a 500 problem details response
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
            try
            {
                await next(context);
            }
            catch (Exception ex) when (!IsRequestAborted(ex, context))
            {
                this.logger.LogError(
                    ex,
                    "Unhandled exception while processing ARGUS {Verb} {Route} [{CorrelationToken}]",
                    context.Request.Verb,
                    context.Request.Route,
                    context.CorrelationToken);

                context.Response = CreateErrorResponse(context.CorrelationToken, ex, this.options.IncludeExceptionDetails);
            }
        }

        /// <summary>
        /// Creates the <see cref="ArgusStatusCode.InternalServerError"/> problem details response for an unhandled exception
        /// </summary>
        /// <param name="correlationToken">
        /// The correlation token of the failed request, used as the problem's instance and the response correlation token
        /// </param>
        /// <param name="exception">
        /// The unhandled <see cref="Exception"/>
        /// </param>
        /// <param name="includeExceptionDetails">
        /// Whether to include the exception message, type and stack trace in the response
        /// </param>
        /// <returns>
        /// The 500 <see cref="ArgusResponse"/>
        /// </returns>
        internal static ArgusResponse CreateErrorResponse(Guid correlationToken, Exception exception, bool includeExceptionDetails)
        {
            Dictionary<string, object> extensions = null;
            var detail = GenericErrorDetail;

            if (includeExceptionDetails)
            {
                detail = exception.Message;
                extensions = new Dictionary<string, object>
                {
                    ["exceptionType"] = exception.GetType().FullName,
                    ["exception"] = exception.ToString()
                };
            }

            var response = ArgusProblemDetails
                .Create(ArgusStatusCode.InternalServerError, detail, extensions, instance: correlationToken.ToString())
                .ToResponse();

            response.CorrelationToken = correlationToken;

            return response;
        }

        /// <summary>
        /// Determines whether the exception is a cancellation caused by <see cref="ArgusContext.RequestAborted"/>
        /// </summary>
        /// <param name="exception">
        /// The <see cref="Exception"/> to inspect
        /// </param>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> for the current request
        /// </param>
        /// <returns>
        /// <c>true</c> if the request was aborted and the exception is an <see cref="OperationCanceledException"/>
        /// </returns>
        private static bool IsRequestAborted(Exception exception, ArgusContext context)
        {
            return exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested;
        }
    }
}
