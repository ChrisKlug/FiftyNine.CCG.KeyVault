using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyNine.CCG.KeyVault.Tests.Utils
{
    internal class InterceptingDelegatingHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> callback;

        public InterceptingDelegatingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback)
            => this.callback = callback;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return this.callback(request);
        }

        public List<HttpRequestMessage> Requests = new List<HttpRequestMessage>();
    }
}
