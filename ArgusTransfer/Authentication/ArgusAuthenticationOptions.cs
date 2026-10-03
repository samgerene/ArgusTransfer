// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusAuthenticationOptions.cs">
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
    /// Configuration options for <see cref="Middleware.ArgusAuthenticationMiddleware"/>
    /// </summary>
    public class ArgusAuthenticationOptions
    {
        /// <summary>
        /// The endpoint metadata key that overrides <see cref="RequireAuthentication"/> for a single endpoint:
        /// "true" requires an authenticated caller, "false" allows anonymous callers
        /// </summary>
        public const string AuthorizeMetadataKey = "authorize";

        /// <summary>
        /// Gets or sets a value indicating whether endpoints require an authenticated caller unless their
        /// <see cref="AuthorizeMetadataKey"/> metadata is "false". Defaults to <c>true</c> (secure by default). When
        /// <c>false</c>, only endpoints whose <see cref="AuthorizeMetadataKey"/> metadata is "true" require authentication.
        /// </summary>
        public bool RequireAuthentication { get; set; } = true;
    }
}
