// -------------------------------------------------------------------------------------------------
//   <copyright file="SampleApiKeyAuthenticationHandler.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, software
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Sample
{
    using System;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using ArgusTransfer.Authentication;
    using ArgusTransfer.Routing;

    /// <summary>
    /// An <see cref="IArgusAuthenticationHandler"/> that accepts requests carrying
    /// <c>Authorization: ApiKey {key}</c> with the key of the sample. A real application reads its keys from
    /// configuration or a secret store and never keeps them in the source code.
    /// </summary>
    public class SampleApiKeyAuthenticationHandler : IArgusAuthenticationHandler
    {
        /// <summary>
        /// The authorization scheme this handler recognizes
        /// </summary>
        public const string Scheme = "ApiKey";

        /// <summary>
        /// The API key the sample client sends. It is a constant only because this is a sample.
        /// </summary>
        public const string SampleApiKey = "sample-api-key";

        /// <summary>
        /// Authenticates the caller from the <c>Authorization</c> header of the request
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> of the request
        /// </param>
        /// <returns>
        /// A <see cref="Task{ArgusAuthenticationResult}"/> with no result when the request has no credentials, a failure
        /// when the scheme or the key is not the expected one, and otherwise the authenticated caller
        /// </returns>
        public Task<ArgusAuthenticationResult> AuthenticateAsync(ArgusContext context)
        {
            var request = context.Request;

            if (request.AuthorizationScheme == null)
            {
                return Task.FromResult(ArgusAuthenticationResult.NoResult());
            }

            // Compare a secret in constant time, so the time the comparison takes does not reveal how much of it matched
            var keyMatches = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(request.AuthorizationParameter ?? string.Empty),
                Encoding.UTF8.GetBytes(SampleApiKey));

            if (!string.Equals(request.AuthorizationScheme, Scheme, StringComparison.OrdinalIgnoreCase) || !keyMatches)
            {
                return Task.FromResult(ArgusAuthenticationResult.Fail("Invalid API key"));
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "sample-client")], Scheme);

            return Task.FromResult(ArgusAuthenticationResult.Success(new ClaimsPrincipal(identity)));
        }
    }
}
