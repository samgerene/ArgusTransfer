// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouteEncodingTestFixture.cs">
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

namespace ArgusTransfer.Tests.Protocol
{
    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRouteEncoding"/> class
    /// </summary>
    [TestFixture]
    public class ArgusRouteEncodingTestFixture
    {
        [TestCase("/items/a b", "/items/a%20b")]
        [TestCase("/discount/50%", "/discount/50%25")]
        [TestCase("/x\r\nX-Injected: yes", "/x%0D%0AX-Injected:%20yes")]
        [TestCase("/tab\there", "/tab%09here")]
        [TestCase("/items/3fa85f64-5717-4562-b3fc-2c963f66afa6", "/items/3fa85f64-5717-4562-b3fc-2c963f66afa6")]
        [TestCase("/café/€", "/café/€")]
        [TestCase("/items?name=a b&code=%41", "/items?name=a%20b&code=%41")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void Verify_that_EncodeRoute_encodes_only_what_the_request_line_cannot_carry(string route, string expected)
        {
            Assert.That(ArgusRouteEncoding.EncodeRoute(route), Is.EqualTo(expected));
        }

        [TestCase("/items/a b")]
        [TestCase("/discount/50%")]
        [TestCase("/already/encoded%20value")]
        [TestCase("/x\r\nX-Injected: yes")]
        [TestCase("/café/€/😀")]
        [TestCase("/%2F/literal")]
        [TestCase("/control\u0001\u007f\u0085")]
        public void Verify_that_a_path_survives_an_encode_decode_round_trip(string path)
        {
            Assert.That(ArgusRouteEncoding.DecodePath(ArgusRouteEncoding.EncodeRoute(path)), Is.EqualTo(path));
        }

        [Test]
        public void Verify_that_DecodePath_never_decodes_an_encoded_slash()
        {
            Assert.That(ArgusRouteEncoding.DecodePath("/files/a%2Fb"), Is.EqualTo("/files/a%2Fb"));
            Assert.That(ArgusRouteEncoding.DecodePath("/files/a%2fb"), Is.EqualTo("/files/a%2fb"));
        }

        [TestCase("/bad/%zz", "/bad/%zz")]
        [TestCase("/end/%4", "/end/%4")]
        [TestCase("/euro/%E2%82%AC", "/euro/€")]
        [TestCase("/plain", "/plain")]
        public void Verify_that_DecodePath_decodes_valid_sequences_and_keeps_invalid_ones(string encoded, string expected)
        {
            Assert.That(ArgusRouteEncoding.DecodePath(encoded), Is.EqualTo(expected));
        }
    }
}
