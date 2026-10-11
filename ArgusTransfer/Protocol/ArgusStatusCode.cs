// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusStatusCode.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
        /// The request was successful and there is no content to return
        /// </summary>
        NoContent = 204,

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
        /// The request syntax is valid but the contained data failed validation
        /// </summary>
        UnprocessableEntity = 422,

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
