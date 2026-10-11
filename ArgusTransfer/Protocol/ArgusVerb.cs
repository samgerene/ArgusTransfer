// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusVerb.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Protocol
{
    /// <summary>
    /// Enumeration of HTTP-like verbs used in the Argus IPC protocol
    /// </summary>
    public enum ArgusVerb
    {
        /// <summary>
        /// Retrieve a resource or collection of resources
        /// </summary>
        GET,

        /// <summary>
        /// Create a new resource
        /// </summary>
        POST,

        /// <summary>
        /// Replace an existing resource
        /// </summary>
        PUT,

        /// <summary>
        /// Partially update an existing resource
        /// </summary>
        PATCH,

        /// <summary>
        /// Retrieve metadata about a resource without the body
        /// </summary>
        HEAD,

        /// <summary>
        /// Delete an existing resource
        /// </summary>
        DELETE
    }
}
