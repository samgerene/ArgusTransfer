// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusStatusCodeExtensions.cs" company="Sam Gerené">
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

namespace ArgusTransfer.Protocol
{
    /// <summary>
    /// Extension methods for the <see cref="ArgusStatusCode"/> enumeration
    /// </summary>
    public static class ArgusStatusCodeExtensions
    {
        /// <summary>
        /// Returns the standard reason phrase for the given <see cref="ArgusStatusCode"/>
        /// </summary>
        /// <param name="statusCode">
        /// The <see cref="ArgusStatusCode"/> to get the reason phrase for
        /// </param>
        /// <returns>
        /// A human-readable reason phrase (e.g. "OK", "Created", "Not Found")
        /// </returns>
        public static string ToReasonPhrase(this ArgusStatusCode statusCode)
        {
            switch (statusCode)
            {
                case ArgusStatusCode.Ok:
                    return "OK";
                case ArgusStatusCode.Created:
                    return "Created";
                case ArgusStatusCode.BadRequest:
                    return "Bad Request";
                case ArgusStatusCode.NotFound:
                    return "Not Found";
                case ArgusStatusCode.InternalServerError:
                    return "Internal Server Error";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Attempts to parse an integer status code into an <see cref="ArgusStatusCode"/> value
        /// </summary>
        /// <param name="code">
        /// The integer status code to parse
        /// </param>
        /// <param name="statusCode">
        /// When this method returns <c>true</c>, contains the parsed <see cref="ArgusStatusCode"/>
        /// </param>
        /// <returns>
        /// <c>true</c> if the code corresponds to a defined <see cref="ArgusStatusCode"/>; otherwise <c>false</c>
        /// </returns>
        public static bool TryParse(int code, out ArgusStatusCode statusCode)
        {
            switch (code)
            {
                case 200:
                    statusCode = ArgusStatusCode.Ok;
                    return true;
                case 201:
                    statusCode = ArgusStatusCode.Created;
                    return true;
                case 400:
                    statusCode = ArgusStatusCode.BadRequest;
                    return true;
                case 404:
                    statusCode = ArgusStatusCode.NotFound;
                    return true;
                case 500:
                    statusCode = ArgusStatusCode.InternalServerError;
                    return true;
                default:
                    statusCode = default;
                    return false;
            }
        }
    }
}
