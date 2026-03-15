// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusBodySerializer.cs">
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
