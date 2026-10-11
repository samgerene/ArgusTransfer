// -------------------------------------------------------------------------------------------------
//   <copyright file="ProgramTestFixture.cs">
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

namespace ArgusTransfer.Tests.Sample
{
    using System;
    using System.Linq;

    using ArgusTransfer.Authentication;
    using ArgusTransfer.Client;
    using ArgusTransfer.Middleware;
    using ArgusTransfer.Sample;
    using ArgusTransfer.Serialization;
    using ArgusTransfer.Server;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="Program"/> class of the sample
    /// </summary>
    [TestFixture]
    public class ProgramTestFixture
    {
        [Test]
        public void Verify_that_ConfigureServices_registers_the_features_the_sample_demonstrates()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            Program.ConfigureServices(services, "argus-sample-configuration-test");

            using var provider = services.BuildServiceProvider();

            // Compression cannot be observed through the client, which decodes the body, so it is checked here
            var hostOptions = provider.GetRequiredService<IOptions<ArgusPipeHostOptions>>().Value;

            Assert.That(hostOptions.PipeName, Is.EqualTo("argus-sample-configuration-test"));
            Assert.That(hostOptions.Compression.Enabled, Is.True);

            using var client = provider.GetRequiredService<IArgusClient>();

            Assert.That(client, Is.TypeOf<ArgusClient>());

            var argusClient = (ArgusClient)client;

            Assert.That(argusClient.Compression.Enabled, Is.True);
            Assert.That(argusClient.RetryPolicy, Is.Not.Null);
            Assert.That(argusClient.RetryPolicy.MaxRetries, Is.EqualTo(3));
            Assert.That(argusClient.RetryPolicy.InitialDelay, Is.EqualTo(TimeSpan.FromMilliseconds(200)));
            Assert.That(argusClient.RetryPolicy.Strategy, Is.EqualTo(RetryBackoffStrategy.Exponential));

            Assert.That(provider.GetService<ArgusExceptionHandlerMiddleware>(), Is.Not.Null);
            Assert.That(provider.GetService<IArgusAuthenticationHandler>(), Is.TypeOf<SampleApiKeyAuthenticationHandler>());
            Assert.That(provider.GetServices<IArgusBodySerializer>().OfType<JsonArgusBodySerializer>().Count(), Is.EqualTo(1));
        }
    }
}
