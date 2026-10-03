// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusAuthenticationStatus.cs">
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
    /// <summary>
    /// The outcome of authenticating a request
    /// </summary>
    public enum ArgusAuthenticationStatus
    {
        /// <summary>
        /// The request carries no credentials the handler recognizes; answered with 401 Unauthorized when authentication is required
        /// </summary>
        NoResult,

        /// <summary>
        /// The request was authenticated
        /// </summary>
        Success,

        /// <summary>
        /// The request carries invalid credentials; answered with 401 Unauthorized when authentication is required
        /// </summary>
        Failure,

        /// <summary>
        /// The credentials are valid but the caller may not access the endpoint; answered with 403 Forbidden when authentication is required
        /// </summary>
        Forbidden
    }
}
