// -------------------------------------------------------------------------------------------------
//   <copyright file="TaggingBodySerializer.cs">
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
