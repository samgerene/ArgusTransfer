// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusAuthenticationOptions.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
