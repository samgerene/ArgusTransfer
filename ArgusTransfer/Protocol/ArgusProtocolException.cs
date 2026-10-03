// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusProtocolException.cs">
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

namespace ArgusTransfer.Protocol
{
    using System;

    /// <summary>
    /// The exception thrown when a received ARGUS/1.0 message violates the protocol or a configured limit, for example a
    /// body that exceeds the maximum allowed size, an unsupported or corrupt <c>Content-Encoding</c>. The pipe host answers
    /// a request that raises this exception with <see cref="ArgusStatusCode.BadRequest"/>.
    /// </summary>
    /// <remarks>
    /// Derives from <see cref="InvalidOperationException"/>, which these conditions raised before this type existed, so
    /// existing handlers for <see cref="InvalidOperationException"/> keep working.
    /// </remarks>
    public class ArgusProtocolException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusProtocolException"/> class
        /// </summary>
        public ArgusProtocolException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusProtocolException"/> class with a message
        /// </summary>
        /// <param name="message">
        /// The message that describes the violation; it is returned to the client in the 400 response, so do not include secrets
        /// </param>
        public ArgusProtocolException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusProtocolException"/> class with a message and the exception that caused it
        /// </summary>
        /// <param name="message">
        /// The message that describes the violation; it is returned to the client in the 400 response, so do not include secrets
        /// </param>
        /// <param name="innerException">
        /// The exception that caused the violation, e.g. an <see cref="System.IO.InvalidDataException"/> for corrupt compressed data
        /// </param>
        public ArgusProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
