// ------------------------------------------------------------------------------------------------
// <copyright file="SampleClientServiceTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Sample
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Client;
    using ArgusTransfer.Protocol;
    using ArgusTransfer.Sample;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Moq;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="SampleClientService"/> class, which run the sample end to end
    /// </summary>
    [TestFixture]
    public class SampleClientServiceTestFixture
    {
        [Test]
        public async Task Verify_that_the_sample_completes_all_its_steps_against_its_own_host()
        {
            var pipeName = $"argus-sample-test-{Guid.NewGuid():N}";

            // The same registrations as Program.Main, on a pipe of its own
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    Program.ConfigureServices(services, pipeName);
                    services.AddHostedService<SampleClientService>();
                })
                .Build();

            await host.StartAsync();

            try
            {
                var clientService = host.Services.GetServices<IHostedService>().OfType<SampleClientService>().Single();

                // The service checks the status of every response, so a step that goes wrong faults its task
                Assert.That(
                    async () => await clientService.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(30)),
                    Throws.Nothing);
                Assert.That(clientService.ExecuteTask.IsCompletedSuccessfully, Is.True);

                // The last step of the sample stops the application
                var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

                Assert.That(lifetime.ApplicationStopping.IsCancellationRequested, Is.True);
            }
            finally
            {
                await host.StopAsync();
            }
        }

        [Test]
        public async Task Verify_that_an_unexpected_status_faults_the_service_and_does_not_stop_the_application()
        {
            var client = new Mock<IArgusClient>();
            var lifetime = new Mock<IHostApplicationLifetime>();

            client
                .Setup(c => c.SendAsync(It.IsAny<ArgusRequest>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ArgusResponse { StatusCode = ArgusStatusCode.InternalServerError });

            using var service = new SampleClientService(client.Object, lifetime.Object);

            await service.StartAsync(CancellationToken.None);

            // The end-to-end test relies on this: a step that gets another status than it expects ends the sample
            Assert.That(
                async () => await service.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(30)),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("GET /sampleitems")
                    .And.Message.Contains("500")
                    .And.Message.Contains("200"));

            client.Verify(c => c.SendAsync(It.IsAny<ArgusRequest>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
            lifetime.Verify(l => l.StopApplication(), Times.Never);
        }

        [Test]
        public async Task Verify_that_the_sample_sends_JSON_bodies_with_the_JSON_content_type_and_its_credentials()
        {
            var id = Guid.NewGuid();
            var requests = new List<ArgusRequest>();

            // The response each step of the sample expects, in order
            var responses = new Queue<ArgusResponse>(
            [
                new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = "[]" },
                new ArgusResponse { StatusCode = ArgusStatusCode.Unauthorized },
                new ArgusResponse { StatusCode = ArgusStatusCode.BadRequest },
                new ArgusResponse { StatusCode = ArgusStatusCode.Created, Body = $"{{\"id\":\"{id}\",\"name\":\"Widget\"}}" },
                new ArgusResponse { StatusCode = ArgusStatusCode.Ok },
                new ArgusResponse { StatusCode = ArgusStatusCode.Ok },
                new ArgusResponse { StatusCode = ArgusStatusCode.Ok },
                new ArgusResponse { StatusCode = ArgusStatusCode.NotFound }
            ]);

            var client = new Mock<IArgusClient>();
            var lifetime = new Mock<IHostApplicationLifetime>();

            client
                .Setup(c => c.SendAsync(It.IsAny<ArgusRequest>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ArgusRequest request, TimeSpan? _, CancellationToken _) =>
                {
                    requests.Add(request);

                    return responses.Dequeue();
                });

            using var service = new SampleClientService(client.Object, lifetime.Object);

            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.That(requests, Has.Count.EqualTo(8));
            Assert.That(requests.Select(r => r.Accept), Is.All.EqualTo("application/json"));

            var requestsWithBody = requests.Where(r => r.Body != null).ToList();

            Assert.That(requestsWithBody, Has.Count.EqualTo(4));
            Assert.That(requestsWithBody.Select(r => r.Headers[ArgusHeaderNames.ContentType]), Is.All.EqualTo("application/json"));

            // The first two steps show an anonymous endpoint and a rejected request, so they carry no credentials
            Assert.That(requests.Take(2).Select(r => r.Authorization), Is.All.Null);
            Assert.That(requests.Skip(2).Select(r => r.AuthorizationScheme), Is.All.EqualTo(SampleApiKeyAuthenticationHandler.Scheme));
            Assert.That(requests.Skip(2).Select(r => r.AuthorizationParameter), Is.All.EqualTo(SampleApiKeyAuthenticationHandler.SampleApiKey));

            // The steps after the creation use the identifier the server assigned
            Assert.That(requests.Skip(4).Select(r => r.Route), Is.All.EqualTo($"/sampleitems/{id}"));

            lifetime.Verify(l => l.StopApplication(), Times.Once);
        }
    }
}
