// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusAuthenticationHandler.cs">
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
    using System.Threading.Tasks;

    using ArgusTransfer.Routing;

    /// <summary>
    /// Authenticates incoming requests, typically from the <c>Authorization</c> header
    /// (<see cref="Protocol.ArgusRequest.AuthorizationScheme"/> and <see cref="Protocol.ArgusRequest.AuthorizationParameter"/>).
    /// Registered as a singleton by <c>AddArgusAuthentication&lt;THandler&gt;()</c>, so implementations must be thread-safe.
    /// </summary>
    public interface IArgusAuthenticationHandler
    {
        /// <summary>
        /// Authenticates the request of the specified context
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> of the request; its <see cref="ArgusContext.EndpointMetadata"/> and
        /// <see cref="ArgusContext.RequestAborted"/> are available
        /// </param>
        /// <returns>
        /// The <see cref="ArgusAuthenticationResult"/>: <see cref="ArgusAuthenticationResult.Success"/> with the caller's
        /// identity, <see cref="ArgusAuthenticationResult.NoResult"/> without credentials, <see cref="ArgusAuthenticationResult.Fail"/>
        /// for invalid credentials, or <see cref="ArgusAuthenticationResult.Forbidden"/> for a caller that may not access the endpoint
        /// </returns>
        Task<ArgusAuthenticationResult> AuthenticateAsync(ArgusContext context);
    }
}
