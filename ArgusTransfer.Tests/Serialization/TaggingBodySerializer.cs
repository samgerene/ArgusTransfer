// ------------------------------------------------------------------------------------------------
// <copyright file="TaggingBodySerializer.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Serialization
{
    using ArgusTransfer.Serialization;

    /// <summary>
    /// A test body serializer for an arbitrary content type that prefixes the body with its content type on write,
    /// so tests can see which serializer wrote a body
    /// </summary>
    public class TaggingBodySerializer : IArgusBodySerializer
    {
        public TaggingBodySerializer(string contentType)
        {
            this.ContentType = contentType;
        }

        public string ContentType { get; }

        public string WriteBody(string body)
        {
            return $"[{this.ContentType}]{body}";
        }

        public string ReadBody(string body)
        {
            return body;
        }
    }
}
