// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusAuthenticationResult.cs">
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
    using System;
    using System.Security.Claims;

    /// <summary>
    /// The result returned by an <see cref="IArgusAuthenticationHandler"/>
    /// </summary>
    public sealed class ArgusAuthenticationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusAuthenticationResult"/> class
        /// </summary>
        /// <param name="status">
        /// The <see cref="ArgusAuthenticationStatus"/>
        /// </param>
        /// <param name="principal">
        /// The authenticated <see cref="ClaimsPrincipal"/>, or <c>null</c>
        /// </param>
        /// <param name="failureReason">
        /// The reason returned to the client for a rejected request, or <c>null</c>
        /// </param>
        private ArgusAuthenticationResult(ArgusAuthenticationStatus status, ClaimsPrincipal principal, string failureReason)
        {
            this.Status = status;
            this.Principal = principal;
            this.FailureReason = failureReason;
        }

        /// <summary>
        /// Gets the <see cref="ArgusAuthenticationStatus"/> of the result
        /// </summary>
        public ArgusAuthenticationStatus Status { get; }

        /// <summary>
        /// Gets a value indicating whether the request was authenticated
        /// </summary>
        public bool Succeeded => this.Status == ArgusAuthenticationStatus.Success;

        /// <summary>
        /// Gets the authenticated <see cref="ClaimsPrincipal"/>; <c>null</c> unless <see cref="Succeeded"/> is <c>true</c>
        /// </summary>
        public ClaimsPrincipal Principal { get; }

        /// <summary>
        /// Gets the reason returned to the client as the problem details <c>detail</c> when the request is rejected,
        /// or <c>null</c> to use a generic message. Do not include secrets.
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// Creates a result for an authenticated request
        /// </summary>
        /// <param name="principal">
        /// The <see cref="ClaimsPrincipal"/> representing the caller
        /// </param>
        /// <returns>
        /// A successful <see cref="ArgusAuthenticationResult"/>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="principal"/> is <c>null</c>
        /// </exception>
        public static ArgusAuthenticationResult Success(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            return new ArgusAuthenticationResult(ArgusAuthenticationStatus.Success, principal, null);
        }

        /// <summary>
        /// Creates a result for a request without credentials the handler recognizes
        /// </summary>
        /// <returns>
        /// An <see cref="ArgusAuthenticationStatus.NoResult"/> result
        /// </returns>
        public static ArgusAuthenticationResult NoResult()
        {
            return new ArgusAuthenticationResult(ArgusAuthenticationStatus.NoResult, null, null);
        }

        /// <summary>
        /// Creates a result for a request with invalid credentials
        /// </summary>
        /// <param name="reason">
        /// An optional reason returned to the client; do not include secrets
        /// </param>
        /// <returns>
        /// An <see cref="ArgusAuthenticationStatus.Failure"/> result
        /// </returns>
        public static ArgusAuthenticationResult Fail(string reason = null)
        {
            return new ArgusAuthenticationResult(ArgusAuthenticationStatus.Failure, null, reason);
        }

        /// <summary>
        /// Creates a result for a caller whose credentials are valid but who may not access the endpoint
        /// </summary>
        /// <param name="reason">
        /// An optional reason returned to the client; do not include secrets
        /// </param>
        /// <returns>
        /// An <see cref="ArgusAuthenticationStatus.Forbidden"/> result
        /// </returns>
        public static ArgusAuthenticationResult Forbidden(string reason = null)
        {
            return new ArgusAuthenticationResult(ArgusAuthenticationStatus.Forbidden, null, reason);
        }
    }
}
