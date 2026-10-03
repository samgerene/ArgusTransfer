// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusAuthenticationEndpointExtensions.cs">
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

namespace ArgusTransfer.Authentication
{
    using ArgusTransfer.Routing;

    /// <summary>
    /// Extension methods that mark endpoints as requiring authentication or allowing anonymous callers
    /// </summary>
    public static class ArgusAuthenticationEndpointExtensions
    {
        /// <summary>
        /// Requires an authenticated caller for the endpoint, even when
        /// <see cref="ArgusAuthenticationOptions.RequireAuthentication"/> is <c>false</c>
        /// </summary>
        /// <param name="builder">
        /// The <see cref="IArgusEndpointConventionBuilder"/> of the endpoint
        /// </param>
        /// <returns>
        /// The <see cref="IArgusEndpointConventionBuilder"/> for method chaining
        /// </returns>
        public static IArgusEndpointConventionBuilder RequireAuthentication(this IArgusEndpointConventionBuilder builder)
        {
            return builder.WithMetadata(ArgusAuthenticationOptions.AuthorizeMetadataKey, "true");
        }

        /// <summary>
        /// Allows anonymous callers for the endpoint, even when <see cref="ArgusAuthenticationOptions.RequireAuthentication"/>
        /// is <c>true</c>. The authentication handler still runs, so <see cref="ArgusContext.User"/> is set when the caller
        /// sends valid credentials.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="IArgusEndpointConventionBuilder"/> of the endpoint
        /// </param>
        /// <returns>
        /// The <see cref="IArgusEndpointConventionBuilder"/> for method chaining
        /// </returns>
        public static IArgusEndpointConventionBuilder AllowAnonymous(this IArgusEndpointConventionBuilder builder)
        {
            return builder.WithMetadata(ArgusAuthenticationOptions.AuthorizeMetadataKey, "false");
        }
    }
}
