// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTransportExtensionsTestFixture.cs">
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

namespace ArgusTransfer.Tests.Extensions
{
    using System;
    using System.Linq;

    using ArgusTransfer.Client;
    using ArgusTransfer.Extensions;
    using ArgusTransfer.Routing;
    using ArgusTransfer.Serialization;
    using ArgusTransfer.Server;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusTransportExtensions"/> and <see cref="ArgusSerializationExtensions"/> classes
    /// </summary>
    [TestFixture]
    public class ArgusTransportExtensionsTestFixture
    {
        [Test]
        public void Verify_that_AddArgusPipeHost_registers_hosted_service()
        {
            var services = new ServiceCollection();

            services.AddArgusPipeHost();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IHostedService));

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor.ImplementationFactory, Is.Not.Null);
        }

        [Test]
        public void Verify_that_AddArgusPipeHost_with_configure_sets_pipe_name()
        {
            var services = new ServiceCollection();

            services.AddArgusPipeHost(options => options.PipeName = "custom-pipe");

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConfigureOptions<ArgusPipeHostOptions>));

            Assert.That(descriptor, Is.Not.Null);
        }

        [Test]
        public void Verify_that_AddArgusPipeHost_without_configure_uses_default_pipe_name()
        {
            var services = new ServiceCollection();

            services.AddArgusPipeHost();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConfigureOptions<ArgusPipeHostOptions>));

            Assert.That(descriptor, Is.Null);
        }

        [Test]
        public void Verify_that_AddArgusTextProtocol_registers_body_serializer()
        {
            var services = new ServiceCollection();

            services.AddArgusPlainTextProtocol();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IArgusBodySerializer));

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(PlainTextArgusBodySerializer)));
            Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        }

        [Test]
        public void Verify_that_AddArgusPipeHost_registers_body_serializer()
        {
            var services = new ServiceCollection();

            services.AddArgusPipeHost();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IArgusBodySerializer));

            Assert.That(descriptor, Is.Not.Null);
        }

        [Test]
        public void Verify_that_AddArgusClient_registers_IArgusClient()
        {
            var services = new ServiceCollection();

            services.AddArgusClient("test-pipe");

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IArgusClient));

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Transient));
        }

        [Test]
        public void Verify_that_AddArgusClient_with_timeout_sets_DefaultTimeout()
        {
            var services = new ServiceCollection();

            services.AddArgusClient("test-pipe", defaultTimeout: TimeSpan.FromSeconds(10));

            var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<IArgusClient>() as ArgusClient;

            Assert.That(client, Is.Not.Null);
            Assert.That(client.DefaultTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void Verify_that_AddArgusClient_uses_registry_when_available()
        {
            var services = new ServiceCollection();

            services.AddArgusPlainTextProtocol();
            services.AddArgusClient("test-pipe");

            var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<IArgusClient>();

            Assert.That(client, Is.Not.Null);
        }

        [Test]
        public void Verify_that_AddArgusBodySerializer_registers_custom_serializer()
        {
            var services = new ServiceCollection();

            services.AddArgusBodySerializer<PlainTextArgusBodySerializer>();

            var descriptors = services.Where(d => d.ServiceType == typeof(IArgusBodySerializer)).ToList();

            Assert.That(descriptors, Has.Count.EqualTo(1));
        }

        [Test]
        public void Verify_that_AddArgusPlainTextProtocol_registers_registry()
        {
            var services = new ServiceCollection();

            services.AddArgusPlainTextProtocol();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IArgusBodySerializerRegistry));

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        }

        [Test]
        public void Verify_that_AddArgusPipeHost_resolves_without_ambiguity()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton<ArgusRouter>();
            services.AddArgusPipeHost();

            var provider = services.BuildServiceProvider();
            var hostedServices = provider.GetServices<IHostedService>().ToList();

            Assert.That(hostedServices.OfType<ArgusPipeHostBackgroundService>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Verify_that_AddArgusBodySerializer_does_not_duplicate()
        {
            var services = new ServiceCollection();

            services.AddArgusBodySerializer<PlainTextArgusBodySerializer>();
            services.AddArgusBodySerializer<PlainTextArgusBodySerializer>();

            var descriptors = services.Where(d => d.ServiceType == typeof(IArgusBodySerializer)).ToList();

            Assert.That(descriptors, Has.Count.EqualTo(1));
        }
    }
}
