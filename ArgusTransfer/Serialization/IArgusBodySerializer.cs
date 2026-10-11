// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusBodySerializer.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Serialization
{
    /// <summary>
    /// Defines the contract for serializing and deserializing message body content
    /// </summary>
    public interface IArgusBodySerializer
    {
        /// <summary>
        /// Gets the MIME content type produced by this serializer (e.g. "application/json")
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Serializes the body string for writing to the wire
        /// </summary>
        /// <param name="body">
        /// The body string to serialize
        /// </param>
        /// <returns>
        /// The serialized body string
        /// </returns>
        string WriteBody(string body);

        /// <summary>
        /// Deserializes the body string read from the wire
        /// </summary>
        /// <param name="body">
        /// The raw body string read from the wire
        /// </param>
        /// <returns>
        /// The deserialized body string
        /// </returns>
        string ReadBody(string body);
    }
}
