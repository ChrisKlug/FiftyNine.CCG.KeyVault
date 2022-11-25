using System;
using System.EnterpriseServices;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace FiftyNine.CCG.KeyVault
{

    [Guid("f919de1a-efc4-4902-b7e5-56a314a87262")]
    [ProgId("CcgCredentialsProviderProvider")]
    public class CcgCredentialsProviderProvider
#if !TEST
        : ServicedComponent, ICcgDomainAuthCredentials
#endif
    {
        private Func<HttpClient> httpClientFactory;
        private Action<string> log;

        public CcgCredentialsProviderProvider()
        {
            httpClientFactory = () => new HttpClient();
            log = entry => { };
        }
        public CcgCredentialsProviderProvider(Func<HttpClient> httpClientFactory, Action<string> log)
        { 
            this.httpClientFactory = httpClientFactory;
            this.log = log;
        }

        public void GetPasswordCredentials(
            [MarshalAs(UnmanagedType.LPWStr), In] string pluginInput,
            [MarshalAs(UnmanagedType.LPWStr)] out string domainName,
            [MarshalAs(UnmanagedType.LPWStr)] out string username,
            [MarshalAs(UnmanagedType.LPWStr)] out string password)
        {
            var config = ParseInput(pluginInput);

            if (config.LoggingEnabled)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(config.LogFile));
                log = entry => File.AppendAllText(config.LogFile, entry + "\r\n");
            }

            log("Using KeyVault: " + config.KeyVaultName);
            log("Using ClientId: " + config.ClientId);
            log("Using KeyVault Secret: " + config.KeyVaultSecretName);

            log("Getting Access Token from token endpoint");
            var token = GetAccessToken(config.ClientId);

            log("Getting Password from KeyVault");
            var secret = GetSecret(config.KeyVaultName, config.KeyVaultSecretName, token);

            var separatorIndex = secret.IndexOf(':');
            var usernameParts = secret.Substring(0, separatorIndex).Split('\\');

            domainName = usernameParts[0];
            username = usernameParts[1];
            password = secret.Substring(separatorIndex + 1);

            log("Got Domain: " + domainName);
            log("Got Username: " + username);
            log("Got Password: " + "".PadLeft(password.Length, '*'));
        }

        public Config ParseInput(string input)
        {
            var entries = input.Split(';').ToDictionary(str => str.Split('=')[0].ToUpper(), str => str.Split('=')[1]);

            if (entries.Count() > 4)
            {
                throw new Exception("Invalid configuration");
            }

            var config = new Config
            {
                KeyVaultName = entries.ContainsKey("KEYVAULTNAME") ? entries["KEYVAULTNAME"] : throw new Exception("Missing keyVaultName config"),
                KeyVaultSecretName = entries.ContainsKey("KEYVAULTSECRET") ? entries["KEYVAULTSECRET"] : throw new Exception("Missing keyVaultSecret config"),
                ClientId = entries.ContainsKey("CLIENTID") ? entries["CLIENTID"] : throw new Exception("Missing clientId config"),
                LogFile = entries.ContainsKey("LOGFILE") ? entries["LOGFILE"] : null
            };

            return config;
        }

        private string GetAccessToken(string clientId)
        {
            var tokenEndpointUri = "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01" +
                                    $"&resource=https://vault.azure.net&client_id={clientId}";
            var httpClient = httpClientFactory();
            httpClient.DefaultRequestHeaders.Add("metadata", "true");
            string response;
            try
            {
                response = httpClient.GetStringAsync(tokenEndpointUri).Result;
            }
            catch (Exception ex)
            {
                log("ERROR: " + ex.GetBaseException().Message);
                throw;
            }

            log("Got response: " + response);

            var responseValues = response.Trim('{').TrimEnd('}').Split(',');
            var tokenValues = responseValues[0].Split(':');
            return tokenValues[1].Trim().Trim('"');
        }
        private string GetSecret(string keyVaultName, string secretName, string accessToken)
        {
            var kvUri = $"https://{keyVaultName}.vault.azure.net";
            var secretUri = kvUri + $"/secrets/{secretName}?api-version=7.3";
            var httpClient = httpClientFactory();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            string response;
            try
            {
                log("Calling: " + secretUri);
                response = httpClient.GetStringAsync(secretUri).Result;
            }
            catch (Exception ex)
            {
                log("ERROR: " + ex.GetBaseException().Message);
                throw;
            }

            var responseValues = response.Trim('{').TrimEnd('}').Split(',');
            var secretValueString = responseValues.First(x => x.Trim().StartsWith("\"value\":"));
            var secret = secretValueString.Substring(secretValueString.IndexOf(":") + 1).Trim().Trim('"').Replace("\\\\", "\\");
            return secret;
        }

        public class Config
        {
            public string KeyVaultName { get; set; }
            public string KeyVaultSecretName { get; set; }
            public string ClientId { get; set; }
            public string LogFile { get; set; }
            public bool LoggingEnabled => !string.IsNullOrEmpty(LogFile);
        }
    }
}