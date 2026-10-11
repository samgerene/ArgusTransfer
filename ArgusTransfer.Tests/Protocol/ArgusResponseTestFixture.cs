// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusResponseTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Protocol
{
    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusResponse"/> class
    /// </summary>
    [TestFixture]
    public class ArgusResponseTestFixture
    {
        [TestCase(ArgusStatusCode.Ok)]
        [TestCase(ArgusStatusCode.Created)]
        [TestCase(ArgusStatusCode.NoContent)]
        public void Verify_that_EnsureSuccessStatusCode_returns_self_for_2xx(ArgusStatusCode statusCode)
        {
            var response = new ArgusResponse { StatusCode = statusCode };

            var result = response.EnsureSuccessStatusCode();

            Assert.That(result, Is.SameAs(response));
        }

        [TestCase(ArgusStatusCode.BadRequest)]
        [TestCase(ArgusStatusCode.Unauthorized)]
        [TestCase(ArgusStatusCode.Forbidden)]
        [TestCase(ArgusStatusCode.NotFound)]
        [TestCase(ArgusStatusCode.NotAcceptable)]
        [TestCase(ArgusStatusCode.Conflict)]
        [TestCase(ArgusStatusCode.UnprocessableEntity)]
        [TestCase(ArgusStatusCode.InternalServerError)]
        [TestCase(ArgusStatusCode.NotImplemented)]
        [TestCase(ArgusStatusCode.ServiceUnavailable)]
        public void Verify_that_EnsureSuccessStatusCode_throws_for_non_2xx(ArgusStatusCode statusCode)
        {
            var response = new ArgusResponse { StatusCode = statusCode };

            Assert.That(() => response.EnsureSuccessStatusCode(), Throws.TypeOf<ArgusRequestException>());
        }

        [Test]
        public void Verify_that_EnsureSuccessStatusCode_populates_exception_with_status_reason_and_body()
        {
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.NotFound,
                Body = "the requested resource is missing"
            };

            var exception = Assert.Throws<ArgusRequestException>(() => response.EnsureSuccessStatusCode());

            Assert.That(exception.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
            Assert.That(exception.ReasonPhrase, Is.EqualTo(ArgusStatusCode.NotFound.ToReasonPhrase()));
            Assert.That(exception.ResponseBody, Is.EqualTo("the requested resource is missing"));
        }

        [Test]
        public void Verify_that_EnsureSuccessStatusCode_propagates_null_body_to_exception()
        {
            var response = new ArgusResponse { StatusCode = ArgusStatusCode.InternalServerError };

            var exception = Assert.Throws<ArgusRequestException>(() => response.EnsureSuccessStatusCode());

            Assert.That(exception.ResponseBody, Is.Null);
        }
    }
}
