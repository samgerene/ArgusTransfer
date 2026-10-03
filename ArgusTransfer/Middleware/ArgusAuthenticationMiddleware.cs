// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusAuthenticationMiddleware.cs">
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
    using System.Threading.Tasks;

    using ArgusTransfer.Authentication;
    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Middleware that authenticates every request with the registered <see cref="IArgusAuthenticationHandler"/>, stores the
    /// caller on <see cref="ArgusContext.User"/>, and short-circuits endpoints that require authentication with a
    /// <see cref="ArgusStatusCode.Unauthorized"/> (no or invalid credentials) or <see cref="ArgusStatusCode.Forbidden"/>
    /// <see cref="ArgusProblemDetails"/> response
    /// </summary>
    /// <remarks>
    /// Whether an endpoint requires authentication follows <see cref="ArgusAuthenticationOptions.RequireAuthentication"/>,
    /// overridden per endpoint by the <see cref="ArgusAuthenticationOptions.AuthorizeMetadataKey"/> metadata (see
    /// <see cref="ArgusAuthenticationEndpointExtensions"/>). Register it with <c>AddArgusAuthentication&lt;THandler&gt;()</c>
    /// to have it run right after the exception handler and before any middleware added by modules.
    /// </remarks>
    public class ArgusAuthenticationMiddleware : IArgusMiddleware
    {
        /// <summary>
        /// The generic <see cref="ArgusProblemDetails.Detail"/> of a 401 response when the handler gives no reason
        /// </summary>
        public const string UnauthorizedDetail = "Authentication is required to access this resource.";

        /// <summary>
        /// The generic <see cref="ArgusProblemDetails.Detail"/> of a 403 response when the handler gives no reason
        /// </summary>
        public const string ForbiddenDetail = "Access to this resource is forbidden.";

        /// <summary>
        /// The <see cref="IArgusAuthenticationHandler"/> that authenticates requests
        /// </summary>
        private readonly IArgusAuthenticationHandler handler;

        /// <summary>
        /// The <see cref="ILogger{ArgusAuthenticationMiddleware}"/> used to log rejected requests
        /// </summary>
        private readonly ILogger<ArgusAuthenticationMiddleware> logger;

        /// <summary>
        /// The <see cref="ArgusAuthenticationOptions"/> that configure the middleware
        /// </summary>
        private readonly ArgusAuthenticationOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusAuthenticationMiddleware"/> class
        /// </summary>
        /// <param name="handler">
        /// The <see cref="IArgusAuthenticationHandler"/> that authenticates requests
        /// </param>
        /// <param name="logger">
        /// The <see cref="ILogger{ArgusAuthenticationMiddleware}"/> used to log rejected requests
        /// </param>
        /// <param name="options">
        /// The <see cref="IOptions{ArgusAuthenticationOptions}"/> that configure the middleware.
        /// When <c>null</c>, the default <see cref="ArgusAuthenticationOptions"/> are used
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="handler"/> or <paramref name="logger"/> is <c>null</c>
        /// </exception>
        public ArgusAuthenticationMiddleware(IArgusAuthenticationHandler handler, ILogger<ArgusAuthenticationMiddleware> logger, IOptions<ArgusAuthenticationOptions> options = null)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(logger);

            this.handler = handler;
            this.logger = logger;
            this.options = options?.Value ?? new ArgusAuthenticationOptions();
        }

        /// <summary>
        /// Authenticates the request and either invokes the next middleware or short-circuits with 401 or 403
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
            var result = await this.handler.AuthenticateAsync(context) ?? ArgusAuthenticationResult.NoResult();

            if (result.Succeeded)
            {
                context.User = result.Principal;
            }

            if (result.Succeeded || !this.RequiresAuthentication(context))
            {
                await next(context);
                return;
            }

            var forbidden = result.Status == ArgusAuthenticationStatus.Forbidden;

            this.logger.LogWarning(
                "Rejected ARGUS {Verb} {Route} [{CorrelationToken}]: {AuthenticationStatus}",
                context.Request.Verb,
                context.Request.Route,
                context.CorrelationToken,
                result.Status);

            context.Problem(
                forbidden ? ArgusStatusCode.Forbidden : ArgusStatusCode.Unauthorized,
                result.FailureReason ?? (forbidden ? ForbiddenDetail : UnauthorizedDetail));
        }

        /// <summary>
        /// Determines whether the endpoint of the request requires an authenticated caller. The endpoint's
        /// <see cref="ArgusAuthenticationOptions.AuthorizeMetadataKey"/> metadata wins over
        /// <see cref="ArgusAuthenticationOptions.RequireAuthentication"/>; a value other than "true" or "false" requires
        /// authentication, so a typo never opens an endpoint.
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> for the current request
        /// </param>
        /// <returns>
        /// <c>true</c> if the endpoint requires authentication
        /// </returns>
        private bool RequiresAuthentication(ArgusContext context)
        {
            if (context.EndpointMetadata == null
                || !context.EndpointMetadata.TryGetValue(ArgusAuthenticationOptions.AuthorizeMetadataKey, out var value))
            {
                return this.options.RequireAuthentication;
            }

            return !bool.TryParse(value, out var requiresAuthentication) || requiresAuthentication;
        }
    }
}
