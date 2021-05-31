# Sample Blazor application with Azure Active Directory authorization

This sample demonstartes how to configure AAD Authorization in a Blazor browser hosted application.

## Setup Azure App Registration

AAD authorization in this sample is based on an AAD App Registration.

### Prerequisites
1. Instructions below require Unix shell and were tested on Windows Subsystem for Linux ([WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10)).
1. [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)

### Setup

1. Make sure you are logged in into Azure CLI by running `az login`.
1. Run the script below:

    ``` bash
    AppName={app registration display name}

    AppId=`az ad app create --display-name $AppName --oauth2-allow-implicit-flow true --reply-urls https://localhost:5001/authentication/login-callback --query appId -o tsv`

    UserId=`az ad signed-in-user show --query objectId -o tsv`

    az ad app owner add --id $AppId --owner-object-id $UserId

    TenantId=`az account show --query tenantId -o tsv`
    
    echo Authority: https://login.microsoftonline.com/$TenantId
    echo ClientId: $AppId
    ```
1. Copy appsettings.json to appsettings.Development.json in the `wwwroot` directory and update Authority and ClientId getting the values from the output od the script above.
1. Open Azure Portal and select your new App Registration in the Azure Active Directory > App Registrations blade.
1. Switch to Authentication blade.
1. Find `This app has implicit grant settings enabled. If you are using any of these URIs in a SPA with MSAL.js 2.0, you should migrate URIs.` and migrate the URI-s to SPA configuration.
1. Uncheck `Access tokens (used for implicit flows)` and save changes.

Start the application and make sure that it is available at `https://localhost:5001/`.

Click `Login` in the upper right corner. It should open Microsoft login popup window. Complete the login accepting the permissions request. The upper line should show your name registered with Microsft account.

## References

- [Secure an ASP.NET Core Blazor WebAssembly standalone app with Azure Active Directory](https://docs.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/standalone-with-azure-active-directory?view=aspnetcore-5.0)
