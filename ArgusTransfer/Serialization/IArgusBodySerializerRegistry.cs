// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusBodySerializerRegistry.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Serialization
{
    using System.Collections.Generic;

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

        /// <summary>
        /// Gets all registered <see cref="IArgusBodySerializer"/> instances, used for content negotiation of
        /// wildcard media ranges such as <c>application/*</c> in an <c>Accept</c> header. The default implementation
        /// returns only <see cref="DefaultSerializer"/>; registries should return every registered serializer
        /// </summary>
        /// <returns>
        /// The registered serializers, in registration order
        /// </returns>
        IReadOnlyCollection<IArgusBodySerializer> GetSerializers() => [this.DefaultSerializer];
    }
}
