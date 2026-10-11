// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusExceptionHandlerOptions.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Middleware
{
    /// <summary>
    /// Configuration options for <see cref="ArgusExceptionHandlerMiddleware"/>
    /// </summary>
    public class ArgusExceptionHandlerOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the exception message, type and stack trace are included
        /// in the problem details response. Defaults to <c>false</c>. Enable this only in development
        /// environments, because exception details can expose sensitive information to clients.
        /// </summary>
        public bool IncludeExceptionDetails { get; set; }
    }
}
