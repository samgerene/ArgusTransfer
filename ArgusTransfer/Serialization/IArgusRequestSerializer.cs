// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusRequestSerializer.cs">
//
//     Copyright (c) 2026 Sam Gerené
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
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Defines the contract for serializing and deserializing <see cref="ArgusRequest"/> messages
    /// </summary>
    public interface IArgusRequestSerializer
    {
        /// <summary>
        /// Writes an <see cref="ArgusRequest"/> to a <see cref="StreamWriter"/>
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to serialize
        /// </param>
        void Write(StreamWriter writer, ArgusRequest request);

        /// <summary>
        /// Asynchronously reads an <see cref="ArgusRequest"/> from a <see cref="StreamReader"/>
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StreamReader"/> to read from
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusRequest"/>
        /// </returns>
        Task<ArgusRequest> ReadAsync(StreamReader reader, CancellationToken cancellationToken);
    }
}
