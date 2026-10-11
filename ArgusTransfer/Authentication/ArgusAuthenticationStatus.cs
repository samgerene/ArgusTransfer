// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusAuthenticationStatus.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
