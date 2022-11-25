using FakeItEasy;
using FiftyNine.CCG.KeyVault.Tests.Utils;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace FiftyNine.CCG.KeyVault.Tests
{
    public class CcgCredentialsProviderProviderTests
    {
        const string PlugInInput = "keyVaultName=keyvault;clientId=123;keyVaultSecret=my_secret";
        const string ValidTokenResponse = @"{
  ""access_token"": ""eyJ0eXAi..."",
  ""refresh_token"": """",
  ""expires_in"": ""3599"",
  ""expires_on"": ""1506484173"",
  ""not_before"": ""1506480273"",
  ""resource"": ""https://management.azure.com/"",
  ""token_type"": ""Bearer""
}";
        const string ValidKeyVaultResponse = @"{
  ""value"": ""domain\\username:password"",
  ""id"": ""https://keyvault.vault.azure.net/secrets/my_secret/123"",
  ""attributes"": {
    ""enabled"": true,
    ""created"": 1493938410,
    ""updated"": 1493938410,
    ""recoveryLevel"": ""Recoverable+Purgeable""
  }
}";

        [Theory]
        [InlineData("keyVaultName=keyvault;clientId=123;keyVaultSecret=my_secret")]
        [InlineData("keyVaultName=keyvault;clientId=123;keyVaultSecret=my_secret,logFile=c:\\temp\\logfile.log")]
        public void Can_parse_a_properly_formatted_plugin_input(string input)
        {
            var provider = new CcgCredentialsProviderProvider(() => null, entry => { });

            var config = provider.ParseInput(input);
        }

        [Fact]
        public void Throws_exception_if_plugin_input_has_too_many_values()
        {
            var provider = new CcgCredentialsProviderProvider(() => null, entry => { });

            Assert.Throws<Exception>(() => provider.ParseInput("keyVaultName=keyvault;clientId=123;keyVaultSecret=my_secret;logFile=c:\\temp\\logfile.log;banana=x"));
        }

        [Fact]
        public void Throws_exception_if_plugin_input_is_missing_keyVaultName()
        {
            var provider = new CcgCredentialsProviderProvider(() => null, entry => { });

            Assert.Throws<Exception>(() => provider.ParseInput("clientId=123;keyVaultSecret=my_secret;logFile=c:\\temp\\logfile.log"));
        }

        [Fact]
        public void Throws_exception_if_plugin_input_is_missing_keyVaultSecret()
        {
            var provider = new CcgCredentialsProviderProvider(() => null, entry => { });

            Assert.Throws<Exception>(() => provider.ParseInput("keyVaultName=keyvault;clientId=123;logFile=c:\\temp\\logfile.log"));
        }

        [Fact]
        public void Throws_exception_if_plugin_input_is_missing_clientId()
        {
            var provider = new CcgCredentialsProviderProvider(() => null, entry => { });

            Assert.Throws<Exception>(() => provider.ParseInput("keyVaultName=keyvault;keyVaultSecret=my_secret;logFile=c:\\temp\\logfile.log"));
        }

        [Fact]
        public void Retrieves_access_token_from_token_endpoint()
        {
            var handler = new InterceptingDelegatingHandler(req => {
                if (req.RequestUri == GetTokenEndpoint("123"))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                        Content = new StringContent(ValidTokenResponse)
                    });
                else if (req.RequestUri == GetKeyVaultSecretEndpoint("keyvault", "my_secret"))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(ValidKeyVaultResponse)
                    });
                throw new Exception("Invalid URI");
            });

            var provider = new CcgCredentialsProviderProvider(() => new HttpClient(handler), entry => { });

            provider.GetPasswordCredentials(PlugInInput, out var domainName, out var username, out var password);

            var tokenRequest = handler.Requests.FirstOrDefault(x => x.RequestUri == GetTokenEndpoint("123"));

            Assert.NotNull(tokenRequest);
            Assert.True(tokenRequest.Headers.GetValues("metadata").FirstOrDefault() == "true");
        }

        [Fact]
        public void Retrieves_secret_from_key_vault_endpoint()
        {
            var handler = new InterceptingDelegatingHandler(req => {
                if (req.RequestUri == GetTokenEndpoint("123"))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(ValidTokenResponse)
                    });
                else if (req.RequestUri == GetKeyVaultSecretEndpoint("keyvault", "my_secret"))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(ValidKeyVaultResponse)
                    });
                throw new Exception("Invalid URI");
            });

            var provider = new CcgCredentialsProviderProvider(() => new HttpClient(handler), entry => { });

            provider.GetPasswordCredentials(PlugInInput, out var domainName, out var username, out var password);

            var secretRequest = handler.Requests.FirstOrDefault(x => x.RequestUri == GetKeyVaultSecretEndpoint("keyvault", "my_secret"));

            Assert.NotNull(secretRequest);
            Assert.Equal("Bearer", secretRequest.Headers.Authorization.Scheme);
            Assert.Equal("eyJ0eXAi...", secretRequest.Headers.Authorization.Parameter);
        }

        private Uri GetTokenEndpoint(string clientId) =>
            new Uri($"http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://vault.azure.net&client_id={clientId}");
        private Uri GetKeyVaultSecretEndpoint(string keyVaultName, string secretName)
            => new Uri($"https://{keyVaultName}.vault.azure.net/secrets/{secretName}?api-version=7.3");
    }
}