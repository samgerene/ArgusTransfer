// -------------------------------------------------------------------------------------------------
//   <copyright file="JsonArgusBodySerializer.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, software
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Sample
{
    using ArgusTransfer.Serialization;

    /// <summary>
    /// An <see cref="IArgusBodySerializer"/> for <c>application/json</c>. Registering it makes the host accept
    /// <c>Accept: application/json</c> and label its responses with that content type. The handlers and the client
    /// produce and parse the JSON themselves, so the body passes through unchanged.
    /// </summary>
    public class JsonArgusBodySerializer : IArgusBodySerializer
    {
        /// <summary>
        /// The content type of a JSON body
        /// </summary>
        public const string JsonContentType = "application/json";

        /// <summary>
        /// Gets the content type this serializer handles
        /// </summary>
        public string ContentType => JsonContentType;

        /// <summary>
        /// Returns the body unchanged; it already is JSON
        /// </summary>
        /// <param name="body">
        /// The JSON body to write
        /// </param>
        /// <returns>
        /// The <paramref name="body"/>
        /// </returns>
        public string WriteBody(string body)
        {
            return body;
        }

        /// <summary>
        /// Returns the body unchanged; the receiver parses the JSON
        /// </summary>
        /// <param name="body">
        /// The JSON body that was read
        /// </param>
        /// <returns>
        /// The <paramref name="body"/>
        /// </returns>
        public string ReadBody(string body)
        {
            return body;
        }
    }
}
