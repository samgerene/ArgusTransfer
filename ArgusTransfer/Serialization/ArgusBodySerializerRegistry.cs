// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusBodySerializerRegistry.cs">
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
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A registry that resolves <see cref="IArgusBodySerializer"/> instances by content type.
    /// At least one serializer must be provided. If a serializer with content type "text/plain"
    /// is registered it becomes the default; otherwise the first registered serializer is used.
    /// </summary>
    public class ArgusBodySerializerRegistry : IArgusBodySerializerRegistry
    {
        /// <summary>
        /// The backing dictionary that maps content types to serializers
        /// </summary>
        private readonly Dictionary<string, IArgusBodySerializer> serializers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusBodySerializerRegistry"/> class
        /// </summary>
        /// <param name="serializers">
        /// The collection of <see cref="IArgusBodySerializer"/> instances to register.
        /// Must contain at least one element.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="serializers"/> is <c>null</c> or empty
        /// </exception>
        public ArgusBodySerializerRegistry(IEnumerable<IArgusBodySerializer> serializers)
        {
            if (serializers == null || !serializers.Any())
            {
                throw new ArgumentException("At least one body serializer must be registered.", nameof(serializers));
            }

            this.serializers = new Dictionary<string, IArgusBodySerializer>(StringComparer.OrdinalIgnoreCase);

            foreach (var serializer in serializers)
            {
                this.serializers[serializer.ContentType] = serializer;
            }

            this.DefaultSerializer = this.serializers.ContainsKey("text/plain")
                ? this.serializers["text/plain"]
                : serializers.First();
        }

        /// <summary>
        /// Gets the default <see cref="IArgusBodySerializer"/> used when no content type is specified.
        /// Returns the "text/plain" serializer if registered; otherwise the first registered serializer.
        /// </summary>
        public IArgusBodySerializer DefaultSerializer { get; }

        /// <summary>
        /// Attempts to retrieve an <see cref="IArgusBodySerializer"/> for the specified content type.
        /// Content type matching is case-insensitive.
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
        public bool TryGetSerializer(string contentType, out IArgusBodySerializer serializer)
        {
            return this.serializers.TryGetValue(contentType, out serializer);
        }
    }
}
