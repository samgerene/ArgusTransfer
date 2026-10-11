// ------------------------------------------------------------------------------------------------
// <copyright file="GZipArgusContentEncodingTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Serialization
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="GZipArgusContentEncoding"/> class
    /// </summary>
    [TestFixture]
    public class GZipArgusContentEncodingTestFixture
    {
        [Test]
        public void Verify_that_name_is_gzip_and_level_defaults_to_fastest()
        {
            var encoding = new GZipArgusContentEncoding();

            Assert.That(encoding.Name, Is.EqualTo("gzip"));
            Assert.That(encoding.CompressionLevel, Is.EqualTo(CompressionLevel.Fastest));
        }

        [Test]
        public void Verify_that_data_round_trips_and_inner_streams_stay_open()
        {
            var encoding = new GZipArgusContentEncoding();
            var data = Encoding.UTF8.GetBytes(new string('x', 10_000) + "héllo");

            using var compressed = new MemoryStream();

            using (var compressor = encoding.CreateCompressionStream(compressed))
            {
                compressor.Write(data, 0, data.Length);
            }

            Assert.That(compressed.CanWrite, Is.True, "destination must stay open");
            Assert.That(compressed.Length, Is.LessThan(data.Length));
            Assert.That(compressed.ToArray()[0], Is.EqualTo(0x1F));
            Assert.That(compressed.ToArray()[1], Is.EqualTo(0x8B));

            compressed.Position = 0;
            using var decompressed = new MemoryStream();

            using (var decompressor = encoding.CreateDecompressionStream(compressed))
            {
                decompressor.CopyTo(decompressed);
            }

            Assert.That(compressed.CanRead, Is.True, "source must stay open");
            Assert.That(decompressed.ToArray(), Is.EqualTo(data));
        }
    }
}
