// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusBodySerializerRegistry.cs">
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
    /// Defines the contract for a registry that resolves <see cref="IArgusBodySerializer"/> instances by content type
    /// </summary>
    public interface IArgusBodySerializerRegistry
    {
        /// <summary>
        /// Gets the default <see cref="IArgusBodySerializer"/> used when no content type is specified
        /// </summary>
        IArgusBodySerializer DefaultSerializer { get; }

        /// <summary>
        /// Attempts to retrieve an <see cref="IArgusBodySerializer"/> for the specified content type
        /// </summary>
        /// <param name="contentType">
        /// The MIME content type to look up (e.g. "application/json")
        /// </param>
        /// <param name="serializer">
        /// When this method returns, contains the serializer associated with the specified content type,
        /// or <c>null</c> if the content type is not registered
        /// </param>
        /// <returns>
        /// <c>true</c> if a serializer was found for the content type; otherwise, <c>false</c>
        /// </returns>
        bool TryGetSerializer(string contentType, out IArgusBodySerializer serializer);
    }
}
