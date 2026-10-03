// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusCompressionTestFixture.cs">
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
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusCompression"/> class
    /// </summary>
    [TestFixture]
    public class ArgusCompressionTestFixture
    {
        /// <summary>
        /// A deflate <see cref="IArgusContentEncoding"/> used to test custom encodings
        /// </summary>
        internal sealed class DeflateTestEncoding : IArgusContentEncoding
        {
            public string Name => "deflate";

            public Stream CreateCompressionStream(Stream destination) => new DeflateStream(destination, CompressionLevel.Fastest, leaveOpen: true);

            public Stream CreateDecompressionStream(Stream source) => new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true);
        }

        private static ArgusCompressionOptions EnabledOptions(int minimumBodySize = 10)
        {
            return new ArgusCompressionOptions { Enabled = true, MinimumBodySize = minimumBodySize };
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("br")]
        [TestCase("gzip;q=0")]
        [TestCase("br, gzip;q=0.0")]
        public void Verify_that_SelectAcceptedEncoding_returns_null_when_gzip_is_not_accepted(string acceptEncoding)
        {
            Assert.That(ArgusCompression.SelectAcceptedEncoding(acceptEncoding, EnabledOptions()), Is.Null);
        }

        [TestCase("gzip")]
        [TestCase("GZIP")]
        [TestCase("br, gzip;q=0.5")]
        [TestCase(" deflate , gzip ")]
        [TestCase("*")]
        public void Verify_that_SelectAcceptedEncoding_returns_gzip_when_accepted(string acceptEncoding)
        {
            Assert.That(ArgusCompression.SelectAcceptedEncoding(acceptEncoding, EnabledOptions())?.Name, Is.EqualTo("gzip"));
        }

        [Test]
        public void Verify_that_SelectAcceptedEncoding_prefers_the_preferred_encoding()
        {
            var options = EnabledOptions();
            options.Encodings.Add(new DeflateTestEncoding());
            options.PreferredEncoding = "deflate";

            Assert.That(ArgusCompression.SelectAcceptedEncoding("gzip, deflate", options)?.Name, Is.EqualTo("deflate"));
            Assert.That(ArgusCompression.SelectAcceptedEncoding("gzip", options)?.Name, Is.EqualTo("gzip"));
        }

        [Test]
        public void Verify_that_ApplyToRequest_does_nothing_when_disabled()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/", Body = new string('x', 5000) };

            ArgusCompression.ApplyToRequest(request, new ArgusCompressionOptions());

            Assert.That(request.Headers.ContainsKey(ArgusHeaderNames.AcceptEncoding), Is.False);
            Assert.That(request.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False);
        }

        [Test]
        public void Verify_that_ApplyToRequest_adds_only_Accept_Encoding_for_a_small_body()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/", Body = "tiny" };

            ArgusCompression.ApplyToRequest(request, EnabledOptions());

            Assert.That(request.Headers[ArgusHeaderNames.AcceptEncoding], Is.EqualTo("gzip"));
            Assert.That(request.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False);
        }

        [Test]
        public void Verify_that_ApplyToRequest_adds_Content_Encoding_for_a_large_body()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/", Body = new string('x', 5000) };

            ArgusCompression.ApplyToRequest(request, EnabledOptions());

            Assert.That(request.Headers[ArgusHeaderNames.ContentEncoding], Is.EqualTo("gzip"));
        }

        [Test]
        public void Verify_that_ApplyToRequest_keeps_headers_set_by_the_caller()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/", Body = new string('x', 5000) };
            request.Headers[ArgusHeaderNames.AcceptEncoding] = "identity";
            request.Headers[ArgusHeaderNames.ContentEncoding] = "identity";

            ArgusCompression.ApplyToRequest(request, EnabledOptions());

            Assert.That(request.Headers[ArgusHeaderNames.AcceptEncoding], Is.EqualTo("identity"));
            Assert.That(request.Headers[ArgusHeaderNames.ContentEncoding], Is.EqualTo("identity"));
        }

        [Test]
        public void Verify_that_ApplyToRequest_throws_when_preferred_encoding_is_not_registered()
        {
            var options = EnabledOptions();
            options.PreferredEncoding = "br";
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/", Body = new string('x', 5000) };

            Assert.That(() => ArgusCompression.ApplyToRequest(request, options), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Verify_that_ApplyToResponse_compresses_large_accepted_response()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/" };
            request.Headers[ArgusHeaderNames.AcceptEncoding] = "gzip";
            var response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = new string('x', 5000) };

            ArgusCompression.ApplyToResponse(request, response, EnabledOptions());

            Assert.That(response.Headers[ArgusHeaderNames.ContentEncoding], Is.EqualTo("gzip"));
        }

        [Test]
        public void Verify_that_ApplyToResponse_leaves_response_uncompressed_when_not_applicable()
        {
            var accepting = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/" };
            accepting.Headers[ArgusHeaderNames.AcceptEncoding] = "gzip";
            var notAccepting = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/" };

            var disabled = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = new string('x', 5000) };
            ArgusCompression.ApplyToResponse(accepting, disabled, new ArgusCompressionOptions());

            var noAccept = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = new string('x', 5000) };
            ArgusCompression.ApplyToResponse(notAccepting, noAccept, EnabledOptions());

            var small = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = "tiny" };
            ArgusCompression.ApplyToResponse(accepting, small, EnabledOptions());

            var empty = new ArgusResponse { StatusCode = ArgusStatusCode.NoContent };
            ArgusCompression.ApplyToResponse(accepting, empty, EnabledOptions(minimumBodySize: 0));

            Assert.That(disabled.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False);
            Assert.That(noAccept.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False);
            Assert.That(small.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False);
            Assert.That(empty.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False);
            Assert.That(() => ArgusCompression.ApplyToResponse(accepting, null, EnabledOptions()), Throws.Nothing);
        }

        [Test]
        public void Verify_that_MeetsThreshold_handles_streamed_bodies()
        {
            var seekableSmall = new ArgusRequest { BodyStream = new MemoryStream(new byte[5]) };
            var seekableLarge = new ArgusRequest { BodyStream = new MemoryStream(new byte[5000]) };

            Assert.That(ArgusCompression.MeetsThreshold(seekableSmall, 10), Is.False);
            Assert.That(ArgusCompression.MeetsThreshold(seekableLarge, 10), Is.True);
            Assert.That(ArgusCompression.MeetsThreshold(new ArgusRequest { BodyStream = new MemoryStream() }, 0), Is.False);
        }

        [Test]
        public void Verify_that_HasContentEncoding_ignores_identity()
        {
            var identity = new ArgusResponse();
            identity.Headers[ArgusHeaderNames.ContentEncoding] = "Identity";
            var gzip = new ArgusResponse();
            gzip.Headers[ArgusHeaderNames.ContentEncoding] = "gzip";

            Assert.That(ArgusCompression.HasContentEncoding(new ArgusResponse()), Is.False);
            Assert.That(ArgusCompression.HasContentEncoding(identity), Is.False);
            Assert.That(ArgusCompression.HasContentEncoding(gzip), Is.True);
        }

        [Test]
        public void Verify_that_ResolveContentEncoding_throws_for_unsupported_encoding()
        {
            var message = new ArgusResponse();
            message.Headers[ArgusHeaderNames.ContentEncoding] = "br";

            Assert.That(
                () => ArgusCompression.ResolveContentEncoding(message, new ArgusCompressionOptions().Encodings),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("br"));
        }

        [Test]
        public async Task Verify_that_DecompressAsync_enforces_maximum_size()
        {
            var encoding = new GZipArgusContentEncoding();
            var compressed = ArgusCompression.Compress(encoding, new byte[100_000]);

            Assert.That(compressed.Length, Is.LessThan(1_000));

            await Assert.ThatAsync(
                async () => await ArgusCompression.DecompressAsync(encoding, new MemoryStream(compressed), 1_000, CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("maximum allowed size"));
        }

        [Test]
        public async Task Verify_that_DecompressAsync_reports_corrupt_data_as_InvalidOperationException()
        {
            var corrupt = new MemoryStream(Encoding.ASCII.GetBytes("definitely not gzip"));

            await Assert.ThatAsync(
                async () => await ArgusCompression.DecompressAsync(new GZipArgusContentEncoding(), corrupt, 0, CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.InnerException.TypeOf<InvalidDataException>());
        }
    }
}
