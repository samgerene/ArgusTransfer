// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusHeaderNames.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Protocol
{
    /// <summary>
    /// Defines the standard header names used by the ARGUS/1.0 wire protocol
    /// </summary>
    public static class ArgusHeaderNames
    {
        /// <summary>
        /// The Accept header name
        /// </summary>
        public const string Accept = "Accept";

        /// <summary>
        /// The Authorization header name, carrying the client's credentials as <c>{scheme} {parameter}</c>
        /// </summary>
        public const string Authorization = "Authorization";

        /// <summary>
        /// The Accept-Encoding header name
        /// </summary>
        public const string AcceptEncoding = "Accept-Encoding";

        /// <summary>
        /// The Content-Encoding header name
        /// </summary>
        public const string ContentEncoding = "Content-Encoding";

        /// <summary>
        /// The Content-Type header name
        /// </summary>
        public const string ContentType = "Content-Type";

        /// <summary>
        /// The Content-Length header name
        /// </summary>
        public const string ContentLength = "Content-Length";

        /// <summary>
        /// The X-Correlation-Token header name
        /// </summary>
        public const string CorrelationToken = "X-Correlation-Token";

        /// <summary>
        /// The X-Timestamp header name
        /// </summary>
        public const string Timestamp = "X-Timestamp";
    }
}
