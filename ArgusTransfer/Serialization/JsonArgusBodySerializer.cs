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
    /// An <see cref="IArgusBodySerializer"/> implementation for JSON content that passes the body through unchanged
    /// </summary>
    public class JsonArgusBodySerializer : IArgusBodySerializer
    {
        /// <summary>
        /// Gets the MIME content type: "application/json"
        /// </summary>
        public string ContentType => "application/json";

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
