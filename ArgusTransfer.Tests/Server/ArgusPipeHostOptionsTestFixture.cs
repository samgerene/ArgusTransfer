// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusPipeHostOptionsTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Server;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusPipeHostOptions"/> class
    /// </summary>
    [TestFixture]
    public class ArgusPipeHostOptionsTestFixture
    {
        /// <summary>
        /// The timeout options for which <see cref="TimeSpan.Zero"/> is not a valid value, with their names
        /// </summary>
        private static readonly Dictionary<string, Action<ArgusPipeHostOptions, TimeSpan>> RequestTimeouts = new()
        {
            [nameof(ArgusPipeHostOptions.RequestReadTimeout)] = (options, value) => options.RequestReadTimeout = value,
            [nameof(ArgusPipeHostOptions.RequestTimeout)] = (options, value) => options.RequestTimeout = value,
            [nameof(ArgusPipeHostOptions.ResponseWriteTimeout)] = (options, value) => options.ResponseWriteTimeout = value
        };

        /// <summary>
        /// The options that must be greater than zero, with their names
        /// </summary>
        private static readonly Dictionary<string, Action<ArgusPipeHostOptions, int>> PositiveOptions = new()
        {
            [nameof(ArgusPipeHostOptions.MaxConcurrentRequests)] = (options, value) => options.MaxConcurrentRequests = value,
            [nameof(ArgusPipeHostOptions.MaxConcurrentConnections)] = (options, value) => options.MaxConcurrentConnections = value,
            [nameof(ArgusPipeHostOptions.MaxRequestHeaderSize)] = (options, value) => options.MaxRequestHeaderSize = value
        };

        [Test]
        public void Verify_that_the_defaults_are_valid_values()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(options.PipeName, Is.EqualTo("argus"));
            Assert.That(options.MaxRequestBodySize, Is.EqualTo(1_048_576));
            Assert.That(options.MaxRequestHeaderSize, Is.EqualTo(32 * 1024));
            Assert.That(options.ShutdownDrainTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.RequestTimeout, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(options.MaxConcurrentRequests, Is.EqualTo(10));
            Assert.That(options.RequestReadTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.ResponseWriteTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.MaxConcurrentConnections, Is.EqualTo(100));

            // Assigning each default back must be accepted
            Assert.That(
                () => new ArgusPipeHostOptions
                {
                    PipeName = options.PipeName,
                    MaxRequestBodySize = options.MaxRequestBodySize,
                    MaxRequestHeaderSize = options.MaxRequestHeaderSize,
                    ShutdownDrainTimeout = options.ShutdownDrainTimeout,
                    RequestTimeout = options.RequestTimeout,
                    MaxConcurrentRequests = options.MaxConcurrentRequests,
                    RequestReadTimeout = options.RequestReadTimeout,
                    ResponseWriteTimeout = options.ResponseWriteTimeout,
                    MaxConcurrentConnections = options.MaxConcurrentConnections
                },
                Throws.Nothing);
        }

        [TestCase(nameof(ArgusPipeHostOptions.MaxConcurrentRequests), 0)]
        [TestCase(nameof(ArgusPipeHostOptions.MaxConcurrentRequests), -1)]
        [TestCase(nameof(ArgusPipeHostOptions.MaxConcurrentConnections), 0)]
        [TestCase(nameof(ArgusPipeHostOptions.MaxConcurrentConnections), -1)]
        [TestCase(nameof(ArgusPipeHostOptions.MaxRequestHeaderSize), 0)]
        [TestCase(nameof(ArgusPipeHostOptions.MaxRequestHeaderSize), -1)]
        public void Verify_that_a_zero_or_negative_limit_is_rejected(string option, int value)
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(
                () => PositiveOptions[option](options, value),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains(option));
        }

        [TestCase(nameof(ArgusPipeHostOptions.MaxConcurrentRequests))]
        [TestCase(nameof(ArgusPipeHostOptions.MaxConcurrentConnections))]
        [TestCase(nameof(ArgusPipeHostOptions.MaxRequestHeaderSize))]
        public void Verify_that_a_limit_of_one_is_accepted(string option)
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(() => PositiveOptions[option](options, 1), Throws.Nothing);
        }

        [Test]
        public void Verify_that_a_negative_MaxRequestBodySize_is_rejected_and_zero_means_no_limit()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(
                () => options.MaxRequestBodySize = -1,
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains(nameof(ArgusPipeHostOptions.MaxRequestBodySize)));

            options.MaxRequestBodySize = 0;

            Assert.That(options.MaxRequestBodySize, Is.Zero);
        }

        [Test]
        public void Verify_that_a_null_PipeName_is_rejected()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(
                () => options.PipeName = null,
                Throws.TypeOf<ArgumentNullException>().With.Message.Contains(nameof(ArgusPipeHostOptions.PipeName)));
            Assert.That(options.PipeName, Is.EqualTo("argus"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Verify_that_an_empty_PipeName_is_rejected(string pipeName)
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(
                () => options.PipeName = pipeName,
                Throws.TypeOf<ArgumentException>().With.Message.Contains(nameof(ArgusPipeHostOptions.PipeName)));
            Assert.That(options.PipeName, Is.EqualTo("argus"));
        }

        [TestCase(nameof(ArgusPipeHostOptions.RequestReadTimeout))]
        [TestCase(nameof(ArgusPipeHostOptions.RequestTimeout))]
        [TestCase(nameof(ArgusPipeHostOptions.ResponseWriteTimeout))]
        public void Verify_that_an_invalid_timeout_is_rejected(string option)
        {
            var options = new ArgusPipeHostOptions();
            var set = RequestTimeouts[option];

            var invalidValues = new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromTicks(1),
                ArgusPipeHostOptions.MinTimeout - TimeSpan.FromTicks(1),
                TimeSpan.FromSeconds(-30),
                TimeSpan.FromMilliseconds(-2),
                TimeSpan.MinValue,
                ArgusPipeHostOptions.MaxTimeout + TimeSpan.FromMilliseconds(1),
                TimeSpan.MaxValue
            };

            foreach (var value in invalidValues)
            {
                Assert.That(
                    () => set(options, value),
                    Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains(option),
                    $"{option} = {value}");
            }
        }

        [TestCase(nameof(ArgusPipeHostOptions.RequestReadTimeout))]
        [TestCase(nameof(ArgusPipeHostOptions.RequestTimeout))]
        [TestCase(nameof(ArgusPipeHostOptions.ResponseWriteTimeout))]
        public void Verify_that_a_timeout_of_at_least_one_millisecond_or_infinite_is_accepted(string option)
        {
            var options = new ArgusPipeHostOptions();
            var set = RequestTimeouts[option];

            var validValues = new[]
            {
                ArgusPipeHostOptions.MinTimeout,
                TimeSpan.FromHours(1),
                ArgusPipeHostOptions.MaxTimeout,
                Timeout.InfiniteTimeSpan
            };

            foreach (var value in validValues)
            {
                Assert.That(() => set(options, value), Throws.Nothing, $"{option} = {value}");
            }
        }

        [Test]
        public void Verify_that_ShutdownDrainTimeout_accepts_zero_and_infinite_and_rejects_other_negative_values()
        {
            var options = new ArgusPipeHostOptions();

            // Zero cancels the in-flight requests immediately, which is a meaningful choice at shutdown
            options.ShutdownDrainTimeout = TimeSpan.Zero;
            Assert.That(options.ShutdownDrainTimeout, Is.EqualTo(TimeSpan.Zero));

            options.ShutdownDrainTimeout = Timeout.InfiniteTimeSpan;
            Assert.That(options.ShutdownDrainTimeout, Is.EqualTo(Timeout.InfiniteTimeSpan));

            options.ShutdownDrainTimeout = ArgusPipeHostOptions.MaxTimeout;
            Assert.That(options.ShutdownDrainTimeout, Is.EqualTo(ArgusPipeHostOptions.MaxTimeout));

            // Less than a millisecond drains like zero, which is valid here, unlike for the request timeouts
            options.ShutdownDrainTimeout = TimeSpan.FromTicks(1);
            Assert.That(options.ShutdownDrainTimeout, Is.EqualTo(TimeSpan.FromTicks(1)));

            var invalidValues = new[]
            {
                TimeSpan.FromTicks(-1),
                TimeSpan.FromMilliseconds(-2),
                TimeSpan.FromSeconds(-30),
                TimeSpan.MinValue,
                ArgusPipeHostOptions.MaxTimeout + TimeSpan.FromMilliseconds(1),
                TimeSpan.MaxValue
            };

            foreach (var value in invalidValues)
            {
                Assert.That(
                    () => options.ShutdownDrainTimeout = value,
                    Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains(nameof(ArgusPipeHostOptions.ShutdownDrainTimeout)),
                    $"{nameof(ArgusPipeHostOptions.ShutdownDrainTimeout)} = {value}");
            }

            Assert.That(options.ShutdownDrainTimeout, Is.EqualTo(TimeSpan.FromTicks(1)));
        }

        [Test]
        public void Verify_that_a_rejected_value_leaves_the_option_unchanged()
        {
            var options = new ArgusPipeHostOptions { RequestTimeout = TimeSpan.FromSeconds(5), MaxConcurrentRequests = 3 };

            Assert.That(() => options.RequestTimeout = TimeSpan.Zero, Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => options.MaxConcurrentRequests = 0, Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(options.RequestTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(options.MaxConcurrentRequests, Is.EqualTo(3));
        }

        [Test]
        public void Verify_that_each_option_returns_the_value_it_was_given()
        {
            // Every value is different, so an option that stores into the field of another one is noticed
            var options = new ArgusPipeHostOptions
            {
                PipeName = "argus-options-test",
                MaxRequestBodySize = 11,
                MaxRequestHeaderSize = 12,
                ShutdownDrainTimeout = TimeSpan.FromSeconds(13),
                RequestTimeout = TimeSpan.FromSeconds(14),
                MaxConcurrentRequests = 15,
                RequestReadTimeout = TimeSpan.FromSeconds(16),
                ResponseWriteTimeout = TimeSpan.FromSeconds(17),
                MaxConcurrentConnections = 18
            };

            Assert.That(options.PipeName, Is.EqualTo("argus-options-test"));
            Assert.That(options.MaxRequestBodySize, Is.EqualTo(11));
            Assert.That(options.MaxRequestHeaderSize, Is.EqualTo(12));
            Assert.That(options.ShutdownDrainTimeout, Is.EqualTo(TimeSpan.FromSeconds(13)));
            Assert.That(options.RequestTimeout, Is.EqualTo(TimeSpan.FromSeconds(14)));
            Assert.That(options.MaxConcurrentRequests, Is.EqualTo(15));
            Assert.That(options.RequestReadTimeout, Is.EqualTo(TimeSpan.FromSeconds(16)));
            Assert.That(options.ResponseWriteTimeout, Is.EqualTo(TimeSpan.FromSeconds(17)));
            Assert.That(options.MaxConcurrentConnections, Is.EqualTo(18));
        }

        [Test]
        public void Verify_that_an_out_of_range_exception_carries_the_rejected_value()
        {
            var options = new ArgusPipeHostOptions();
            var timeout = TimeSpan.FromSeconds(-30);

            // The rejected value ends up in the exception message, and so in the log of a host that fails to start
            Assert.That(
                () => options.MaxRequestBodySize = -1,
                Throws.TypeOf<ArgumentOutOfRangeException>()
                    .With.Property(nameof(ArgumentOutOfRangeException.ActualValue)).EqualTo(-1L)
                    .And.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("value"));
            Assert.That(
                () => options.MaxConcurrentRequests = 0,
                Throws.TypeOf<ArgumentOutOfRangeException>()
                    .With.Property(nameof(ArgumentOutOfRangeException.ActualValue)).EqualTo(0)
                    .And.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("value"));
            Assert.That(
                () => options.RequestTimeout = timeout,
                Throws.TypeOf<ArgumentOutOfRangeException>()
                    .With.Property(nameof(ArgumentOutOfRangeException.ActualValue)).EqualTo(timeout)
                    .And.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("value"));
        }

        [Test]
        public void Verify_that_Compression_may_be_set_to_null()
        {
            var options = new ArgusPipeHostOptions();

            Assert.That(() => options.Compression = null, Throws.Nothing);
        }

        [Test]
        public async Task Verify_that_the_longest_accepted_timeout_is_the_longest_the_timers_support()
        {
            using var cts = new CancellationTokenSource();

            // The host applies the timeouts with CancelAfter and Task.Delay; what the options accept must not throw there
            Assert.That(() => cts.CancelAfter(ArgusPipeHostOptions.MaxTimeout), Throws.Nothing);

            var delay = Task.Delay(ArgusPipeHostOptions.MaxTimeout, cts.Token);

            Assert.That(
                () => cts.CancelAfter(ArgusPipeHostOptions.MaxTimeout + TimeSpan.FromMilliseconds(1)),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            await cts.CancelAsync();

            Assert.That(async () => await delay, Throws.InstanceOf<OperationCanceledException>());
        }
    }
}
