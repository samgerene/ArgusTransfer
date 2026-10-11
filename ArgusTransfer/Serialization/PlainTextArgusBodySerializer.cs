// ------------------------------------------------------------------------------------------------
// <copyright file="PlainTextArgusBodySerializer.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Serialization
{
    /// <summary>
    /// An <see cref="IArgusBodySerializer"/> implementation for plain text content that passes the body through unchanged
    /// </summary>
    public class PlainTextArgusBodySerializer : IArgusBodySerializer
    {
        /// <summary>
        /// Gets the MIME content type: "text/plain"
        /// </summary>
        public string ContentType => "text/plain";

        /// <summary>
        /// Returns the body unchanged (identity serialization)
        /// </summary>
        /// <param name="body">
        /// The body string to serialize
        /// </param>
        /// <returns>
        /// The body string, unchanged
        /// </returns>
        public string WriteBody(string body)
        {
            return body;
        }

        /// <summary>
        /// Returns the body unchanged (identity deserialization)
        /// </summary>
        /// <param name="body">
        /// The raw body string read from the wire
        /// </param>
        /// <returns>
        /// The body string, unchanged
        /// </returns>
        public string ReadBody(string body)
        {
            return body;
        }
    }
}
