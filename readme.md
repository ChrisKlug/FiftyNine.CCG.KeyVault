# CCG plug-in for KeyVault

This repo contains the code for a Container Credentials Guard plug-in that enables you to store gMSA account retrieval credentials in Azure KeyVault, and use Managed Identities to access it. It is based on the PoC created by [Andrew Stakhov](https://stakhov.pro/), located at https://github.com/macsux/gmsa-ccg-plugin.

## Usage

To use the plug-in, it needs to be registered on the server as a COM component, and as a CCG plug-in. This is done by using the [install-plugin.ps1](./resources/install-plugin.ps1) script. It will register the COM-component, set the component to run as _NT AUTHORITY\NetworkService_ and create the required registry edits to enable the CCG to use it.

Finally, you also need to create a credential spec file. It should be located at `C:\ProgramData\Docker\CredentialSpecs`. It should look something like this

```
{
  "CmsPlugins": [ "ActiveDirectory" ],
  "DomainJoinConfig": {
    "Sid": "S-1-5-21-XXXXXXXXXX-XXXXXXXXXX-XXXXXXXXXX",
    "MachineAccountName": "gMSA ACCOUNT NAME",
    "Guid": "<DOMAIN GUID>",
    "DnsTreeName": "<DOMAIN DNS NAME>",
    "DnsName": "<DOMAIN DNS NAME>",
    "NetBiosName": "<DOMAIN NETBIOS NAME>"
  },
  "ActiveDirectoryConfig": {
    "GroupManagedServiceAccounts": [
      {
        "Name": "<gMSA ACCOUNT NAME>",
        "Scope": "<DOMAIN DNS NAME>"
      },
      {
        "Name": "<gMSA ACCOUNT NAME>",
        "Scope": "<DOMAIN NETBIOS NAME>"
      }
    ],
    "HostAccountConfig": {
      "PluginGUID": "{f919de1a-efc4-4902-b7e5-56a314a87262}",
      "PluginInput": "keyVaultName=<KEY VAULT NAME>;clientId=<CLIENT ID>;keyVaultSecret=<SECRET NAME>[,logFile=<PATH TO LOG FILE>]",
      "PortableCcgVersion": "1"
    }
  }
}
```

Once that is in place, you should be ablt to start a Docker container using a command like this

```bash
> docker run --security-opt "credentialspec=file://my_cred_spec_file.json" -it mcr.microsoft.com/windows/servercore:ltsc2019 powershell
```

Once inside the container, you can verify that it all works by running

```bash
> nltest /sc_verify:<DOMAIN>
```

This should return a `NERR` response.

## Running unit tests

To run the unit tests, you need to compile the application using the `Test` configuration. This removes the inheritance from `ServicedComponent`, allowing the code to be tested in a simple unit test.

## More information

There is a blog post covering this plug-in, and some other stuff if you want to know more about it. It is located at: [Running a Windows container with gMSA, on a non-domain joined host](https://fearofoblivion.com/running-a-windows-container-under-gmsa)

## Feedback

For feedback, feel free to put up an issue, or contact me at [@ZeroKoll](https://twitter.com/zerokoll)