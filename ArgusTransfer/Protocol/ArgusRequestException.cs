// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestException.cs">
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
    /// Represents an error raised when an <see cref="ArgusResponse"/> indicates a non-success status code
    /// </summary>
    public class ArgusRequestException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusRequestException"/> class
        /// </summary>
        public ArgusRequestException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusRequestException"/> class
        /// with the specified error message
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        public ArgusRequestException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusRequestException"/> class
        /// with the specified error message and a reference to the inner exception that is the cause of this exception
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception
        /// </param>
        public ArgusRequestException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusRequestException"/> class
        /// populated from the details of a non-success <see cref="ArgusResponse"/>
        /// </summary>
        /// <param name="statusCode">
        /// The <see cref="ArgusStatusCode"/> returned by the server
        /// </param>
        /// <param name="reasonPhrase">
        /// The reason phrase associated with <paramref name="statusCode"/>
        /// </param>
        /// <param name="responseBody">
        /// The body of the response, or <c>null</c> if the response had no body
        /// </param>
        public ArgusRequestException(ArgusStatusCode statusCode, string reasonPhrase, string responseBody)
            : base(BuildMessage(statusCode, reasonPhrase))
        {
            this.StatusCode = statusCode;
            this.ReasonPhrase = reasonPhrase;
            this.ResponseBody = responseBody;
        }

        /// <summary>
        /// Gets the <see cref="ArgusStatusCode"/> returned by the server
        /// </summary>
        public ArgusStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the reason phrase associated with <see cref="StatusCode"/>
        /// </summary>
        public string ReasonPhrase { get; }

        /// <summary>
        /// Gets the body of the response that triggered the exception, or <c>null</c> if the response had no body
        /// </summary>
        public string ResponseBody { get; }

        /// <summary>
        /// Builds the exception message from a status code and reason phrase
        /// </summary>
        /// <param name="statusCode">
        /// The <see cref="ArgusStatusCode"/> returned by the server
        /// </param>
        /// <param name="reasonPhrase">
        /// The reason phrase associated with <paramref name="statusCode"/>
        /// </param>
        /// <returns>
        /// A human-readable error message
        /// </returns>
        private static string BuildMessage(ArgusStatusCode statusCode, string reasonPhrase)
        {
            return $"Argus request failed with status code {(int)statusCode} ({reasonPhrase}).";
        }
    }
}
