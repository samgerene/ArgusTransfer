// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusStatusCode.cs">
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
    /// <summary>
    /// Enumeration of HTTP-like status codes used in the Argus IPC protocol
    /// </summary>
    public enum ArgusStatusCode
    {
        /// <summary>
        /// The request was successful
        /// </summary>
        Ok = 200,

        /// <summary>
        /// A new resource was successfully created
        /// </summary>
        Created = 201,

        /// <summary>
        /// The request was malformed or contained invalid data
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// The request lacks valid authentication credentials
        /// </summary>
        Unauthorized = 401,

        /// <summary>
        /// The authenticated caller lacks permission for the requested resource
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// The requested resource was not found
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// The server cannot produce a response matching the Accept header
        /// </summary>
        NotAcceptable = 406,

        /// <summary>
        /// The request could not be completed due to a conflict with the current state of the resource
        /// </summary>
        Conflict = 409,

        /// <summary>
        /// An unexpected error occurred on the server
        /// </summary>
        InternalServerError = 500,

        /// <summary>
        /// The server does not support the functionality required to fulfill the request
        /// </summary>
        NotImplemented = 501,

        /// <summary>
        /// The server is currently unable to handle the request due to temporary overloading or maintenance
        /// </summary>
        ServiceUnavailable = 503,
    }
}
