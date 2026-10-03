// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusCompression.cs">
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

namespace ArgusTransfer.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Content-encoding negotiation and the compression helpers used by the serializers, <c>ArgusClient</c> and the pipe host
    /// </summary>
    internal static class ArgusCompression
    {
        /// <summary>
        /// The token for "no encoding"
        /// </summary>
        private const string Identity = "identity";

        /// <summary>
        /// The <c>Accept-Encoding</c> token that accepts any encoding
        /// </summary>
        private const string Wildcard = "*";

        /// <summary>
        /// The buffer size used when decompressing
        /// </summary>
        private const int BufferSize = 8192;

        /// <summary>
        /// Prepares a request for sending: when compression is enabled, adds an <c>Accept-Encoding</c> header listing the
        /// supported encodings and, for a body of at least <see cref="ArgusCompressionOptions.MinimumBodySize"/> bytes,
        /// a <c>Content-Encoding</c> header naming the preferred encoding. Headers the caller already set are kept.
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to prepare
        /// </param>
        /// <param name="options">
        /// The <see cref="ArgusCompressionOptions"/> to apply
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="ArgusCompressionOptions.PreferredEncoding"/> is not one of the registered encodings
        /// </exception>
        public static void ApplyToRequest(ArgusRequest request, ArgusCompressionOptions options)
        {
            if (!options.Enabled)
            {
                return;
            }

            if (!request.Headers.ContainsKey(ArgusHeaderNames.AcceptEncoding) && options.Encodings.Count > 0)
            {
                request.Headers[ArgusHeaderNames.AcceptEncoding] = string.Join(", ", options.Encodings.Select(encoding => encoding.Name));
            }

            if (!request.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding) && MeetsThreshold(request, options.MinimumBodySize))
            {
                var preferred = Find(options.Encodings, options.PreferredEncoding)
                    ?? throw new InvalidOperationException($"The preferred encoding '{options.PreferredEncoding}' is not one of the registered encodings.");

                request.Headers[ArgusHeaderNames.ContentEncoding] = preferred.Name;
            }
        }

        /// <summary>
        /// Prepares a response for sending: when compression is enabled, the response has no <c>Content-Encoding</c> yet,
        /// its body is at least <see cref="ArgusCompressionOptions.MinimumBodySize"/> bytes and the request accepts one of the
        /// supported encodings, adds a <c>Content-Encoding</c> header naming that encoding
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> whose <c>Accept-Encoding</c> header is honored
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to prepare; may be <c>null</c>
        /// </param>
        /// <param name="options">
        /// The <see cref="ArgusCompressionOptions"/> to apply
        /// </param>
        public static void ApplyToResponse(ArgusRequest request, ArgusResponse response, ArgusCompressionOptions options)
        {
            if (!options.Enabled
                || response == null
                || response.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding)
                || !MeetsThreshold(response, options.MinimumBodySize))
            {
                return;
            }

            var acceptEncoding = request.Headers.TryGetValue(ArgusHeaderNames.AcceptEncoding, out var value) ? value : null;
            var encoding = SelectAcceptedEncoding(acceptEncoding, options);

            if (encoding != null)
            {
                response.Headers[ArgusHeaderNames.ContentEncoding] = encoding.Name;
            }
        }

        /// <summary>
        /// Selects the encoding to compress with, given an <c>Accept-Encoding</c> header value. The preferred encoding wins
        /// when it is accepted; otherwise the first registered encoding that is accepted is used. Tokens with <c>q=0</c>
        /// are not accepted and <c>*</c> accepts any encoding.
        /// </summary>
        /// <param name="acceptEncoding">
        /// The <c>Accept-Encoding</c> header value, or <c>null</c>
        /// </param>
        /// <param name="options">
        /// The <see cref="ArgusCompressionOptions"/> with the supported encodings
        /// </param>
        /// <returns>
        /// The selected <see cref="IArgusContentEncoding"/>, or <c>null</c> when no supported encoding is accepted
        /// </returns>
        public static IArgusContentEncoding SelectAcceptedEncoding(string acceptEncoding, ArgusCompressionOptions options)
        {
            if (string.IsNullOrWhiteSpace(acceptEncoding))
            {
                return null;
            }

            var accepted = ParseAcceptEncoding(acceptEncoding);
            var acceptsAny = accepted.Contains(Wildcard);
            var preferred = Find(options.Encodings, options.PreferredEncoding);

            if (preferred != null && (acceptsAny || accepted.Contains(preferred.Name)))
            {
                return preferred;
            }

            return options.Encodings.FirstOrDefault(encoding => acceptsAny || accepted.Contains(encoding.Name));
        }

        /// <summary>
        /// Determines whether a message names a content encoding other than <c>identity</c>
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to inspect
        /// </param>
        /// <returns>
        /// <c>true</c> if the body is (to be) encoded
        /// </returns>
        public static bool HasContentEncoding(ArgusMessage message)
        {
            return message.Headers.TryGetValue(ArgusHeaderNames.ContentEncoding, out var value)
                && !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value.Trim(), Identity, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the <see cref="IArgusContentEncoding"/> named by the <c>Content-Encoding</c> header of a message that is
        /// about to be written. An unsupported encoding is a programming or configuration error of the sender.
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to inspect
        /// </param>
        /// <param name="encodings">
        /// The supported encodings
        /// </param>
        /// <returns>
        /// The encoding, or <c>null</c> when the header is absent or <c>identity</c>
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the header names an unsupported encoding or more than one encoding
        /// </exception>
        public static IArgusContentEncoding ResolveContentEncoding(ArgusMessage message, IEnumerable<IArgusContentEncoding> encodings)
        {
            return TryResolveContentEncoding(message, encodings, out var encoding)
                ? encoding
                : throw new InvalidOperationException(UnsupportedContentEncodingMessage(message));
        }

        /// <summary>
        /// Resolves the <see cref="IArgusContentEncoding"/> named by the <c>Content-Encoding</c> header of a received message.
        /// An unsupported encoding is a protocol violation by the peer.
        /// </summary>
        /// <param name="message">
        /// The received <see cref="ArgusMessage"/>
        /// </param>
        /// <param name="encodings">
        /// The supported encodings
        /// </param>
        /// <returns>
        /// The encoding, or <c>null</c> when the header is absent or <c>identity</c>
        /// </returns>
        /// <exception cref="ArgusProtocolException">
        /// Thrown when the header names an unsupported encoding or more than one encoding
        /// </exception>
        public static IArgusContentEncoding ResolveReceivedContentEncoding(ArgusMessage message, IEnumerable<IArgusContentEncoding> encodings)
        {
            return TryResolveContentEncoding(message, encodings, out var encoding)
                ? encoding
                : throw new ArgusProtocolException(UnsupportedContentEncodingMessage(message));
        }

        /// <summary>
        /// Attempts to resolve the <see cref="IArgusContentEncoding"/> named by a message's <c>Content-Encoding</c> header
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to inspect
        /// </param>
        /// <param name="encodings">
        /// The supported encodings
        /// </param>
        /// <param name="encoding">
        /// When this method returns <c>true</c>, the encoding, or <c>null</c> when the header is absent or <c>identity</c>
        /// </param>
        /// <returns>
        /// <c>false</c> when the header names an unsupported encoding or more than one encoding; otherwise <c>true</c>
        /// </returns>
        public static bool TryResolveContentEncoding(ArgusMessage message, IEnumerable<IArgusContentEncoding> encodings, out IArgusContentEncoding encoding)
        {
            encoding = null;

            if (!HasContentEncoding(message))
            {
                return true;
            }

            encoding = Find(encodings, message.Headers[ArgusHeaderNames.ContentEncoding].Trim());

            return encoding != null;
        }

        /// <summary>
        /// Determines whether a message body is large enough to be compressed
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to inspect
        /// </param>
        /// <param name="minimumBodySize">
        /// The minimum body size in bytes
        /// </param>
        /// <returns>
        /// <c>true</c> for a string body of at least <paramref name="minimumBodySize"/> UTF-8 bytes, and for a streamed body
        /// that is either at least that long or of unknown length; <c>false</c> for an empty body
        /// </returns>
        public static bool MeetsThreshold(ArgusMessage message, int minimumBodySize)
        {
            if (message.IsStreamed)
            {
                var stream = message.BodyStream;

                return !stream.CanSeek || stream.Length - stream.Position >= Math.Max(1, minimumBodySize);
            }

            return !string.IsNullOrEmpty(message.Body) && Encoding.UTF8.GetByteCount(message.Body) >= minimumBodySize;
        }

        /// <summary>
        /// Compresses a byte array
        /// </summary>
        /// <param name="encoding">
        /// The <see cref="IArgusContentEncoding"/> to compress with
        /// </param>
        /// <param name="data">
        /// The data to compress
        /// </param>
        /// <returns>
        /// The compressed data
        /// </returns>
        public static byte[] Compress(IArgusContentEncoding encoding, byte[] data)
        {
            using var output = new MemoryStream();

            using (var compressor = encoding.CreateCompressionStream(output))
            {
                compressor.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        /// <summary>
        /// Decompresses a stream into a <see cref="MemoryStream"/>, enforcing a maximum decompressed size
        /// </summary>
        /// <param name="encoding">
        /// The <see cref="IArgusContentEncoding"/> to decompress with
        /// </param>
        /// <param name="source">
        /// The compressed data
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed decompressed size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="MemoryStream"/> with the decompressed data, positioned at the beginning
        /// </returns>
        /// <exception cref="ArgusProtocolException">
        /// Thrown when the decompressed data exceeds <paramref name="maxBodySize"/> or is not valid for the encoding
        /// </exception>
        public static async Task<MemoryStream> DecompressAsync(IArgusContentEncoding encoding, Stream source, long maxBodySize, CancellationToken cancellationToken)
        {
            var result = new MemoryStream();
            var buffer = new byte[BufferSize];
            long totalBytes = 0;

            try
            {
                await using var decompressor = encoding.CreateDecompressionStream(source);
                int read;

                while ((read = await decompressor.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    totalBytes += read;

                    if (maxBodySize > 0 && totalBytes > maxBodySize)
                    {
                        throw new ArgusProtocolException(
                            $"Decompressed body size exceeds the maximum allowed size of {maxBodySize} bytes.");
                    }

                    await result.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            catch (InvalidDataException ex)
            {
                await result.DisposeAsync();
                throw new ArgusProtocolException($"The body could not be decoded with Content-Encoding '{encoding.Name}'.", ex);
            }
            catch (ArgusProtocolException)
            {
                await result.DisposeAsync();
                throw;
            }

            result.Position = 0;
            return result;
        }

        /// <summary>
        /// Builds the error message for an unsupported <c>Content-Encoding</c>
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> with the unsupported encoding
        /// </param>
        /// <returns>
        /// The error message
        /// </returns>
        private static string UnsupportedContentEncodingMessage(ArgusMessage message)
        {
            return $"Unsupported Content-Encoding '{message.Headers[ArgusHeaderNames.ContentEncoding].Trim()}'.";
        }

        /// <summary>
        /// Finds an encoding by its token, ignoring case
        /// </summary>
        /// <param name="encodings">
        /// The encodings to search
        /// </param>
        /// <param name="name">
        /// The token to find
        /// </param>
        /// <returns>
        /// The matching <see cref="IArgusContentEncoding"/>, or <c>null</c>
        /// </returns>
        private static IArgusContentEncoding Find(IEnumerable<IArgusContentEncoding> encodings, string name)
        {
            return encodings.FirstOrDefault(encoding => string.Equals(encoding.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Parses an <c>Accept-Encoding</c> header value into the set of accepted tokens, leaving out tokens with <c>q=0</c>
        /// </summary>
        /// <param name="acceptEncoding">
        /// The header value, e.g. "gzip, br;q=0.8, identity;q=0"
        /// </param>
        /// <returns>
        /// The accepted tokens, compared case-insensitively
        /// </returns>
        private static HashSet<string> ParseAcceptEncoding(string acceptEncoding)
        {
            var accepted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in acceptEncoding.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = entry.Split(';', StringSplitOptions.TrimEntries);
                var quality = parts.Skip(1)
                    .Where(parameter => parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
                    .Select(parameter => double.TryParse(parameter.AsSpan(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var q) ? q : 0d)
                    .DefaultIfEmpty(1d)
                    .First();

                if (quality > 0 && parts[0].Length > 0)
                {
                    accepted.Add(parts[0]);
                }
            }

            return accepted;
        }
    }
}
