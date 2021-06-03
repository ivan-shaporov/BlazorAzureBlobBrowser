# Sample Blazor application with Azure Active Directory authorization

This sample demonstrates how to configure AAD Authorization in a Blazor browser hosted application. Configured AAD then is used to access blobs in an Azure Storage Account. This approach doesn't require any secrets and opens possibilities for using it at the client side in a browser in a static web hosting environment.

## Workarounds for functionality not supported by Web Assembly

### Cryptography

Some Azure SDK-s like Azure Storage client libraries for .NET require Cryptography .Net library that is not implemented for Web Assembly. This repo contains a sample workaround using JavaScript `crypto.subtle` component for getting SAS signatures for Azure Blobs. The workaround was to copy the code from the [Microsoft Open Source SDK](https://github.com/Azure/azure-sdk-for-net) and modify it replacing `System.Security.Cryptography.HMACSHA256` calls with `crypto.subtle` calls. The code is located in [AzureBlobStorage](./AzureBlobStorage) folder. It requires [HmacSha256.js](./wwwroot/HmacSha256.js) to be placed in the `wwwroot` folder.

### Static website hosting in Azure Storage

Azure storage based static websites "as is" aren't compatible with AAD callbacks because the default Blazor framework is expected to handle dynamic `authentication/login-callback` URL. As a workaround a redirect html file [login-callback](./wwwroot/authentication/login-callback) is placed in the `authentication` folder.

``` c#
options.ProviderOptions.LoginMode = "redirect";
```
Is not supported with this approach and `popup` (default) mode should ne used.

### Environment specific appsettings.json

There is an issue with environment specific `appsettings.json` and a proposed [workaround](https://github.com/dotnet/aspnetcore/issues/25152#issuecomment-718698235). The workaround requires Visual Studio. If you use other build tool make sure that you implement the pre-build steps in some other way.

The workaround also requires the `js` folder to exist. Either create it or add one more step to the build process.

```
if not exist "$(ProjectDir)wwwroot\js" mkdir $(ProjectDir)wwwroot\js
```

## Prerequisites
1. Instructions below require Unix shell and were tested on Windows Subsystem for Linux ([WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10)).
1. [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli).
1. Azure Storage Account.

## Create Azure App Registration

AAD authorization in this sample is based on an AAD App Registration. You will need to create and configure the app, allow the app access to an Azure Storage Account and allow access to the account for your Azure user login.

1. Make sure you are logged in into Azure CLI by running `az login`.
1. Run the script below. It will create the app, set current user as the owner and print AAD parameters for the client.

    ``` bash
    AppName={replace with desired app registration display name}

    AppId=`az ad app create --display-name $AppName --oauth2-allow-implicit-flow true --reply-urls https://localhost:5001/authentication/login-callback --query appId -o tsv`

    UserId=`az ad signed-in-user show --query objectId -o tsv`

    az ad app owner add --id $AppId --owner-object-id $UserId

    TenantId=`az account show --query tenantId -o tsv`
    
    echo Authority: https://login.microsoftonline.com/$TenantId
    echo ClientId: $AppId
    ```
1. Copy appsettings.json to appsettings.Debug.json in the `wwwroot` directory and update Authority and ClientId getting the values from the output of the script above.
1. Open Azure Portal and select your new App Registration in the Azure Active Directory > App Registrations blade.
1. Switch to Authentication blade.
1. Find `This app has implicit grant settings enabled. If you are using any of these URIs in a SPA with MSAL.js 2.0, you should migrate URIs.` and migrate the URI-s to SPA configuration.
1. Uncheck `Access tokens (used for implicit flows)` and save changes.

Start the Blazor application and make sure that it is available at `https://localhost:5001/`.

**Note:** it may take couple of minutes before the permissions propagate.

Click `Login` in the upper right corner. It should open Microsoft login popup window. Complete the login accepting the permissions request. The upper line should show your name registered with Microsoft account. Blobs will not be available at this point and `Access denied` message will appear.

## Setup Azure Storage access

The App Registration configured above will need permissions to access blobs on user's behalf. To enable it run the following script:

``` bash
AzureStorageAppId=e406a681-f3d4-42a8-90b6-c2b029497af1
PermissionId=`az ad sp show --id $AzureStorageAppId --query oauth2Permissions[0].id -o tsv`
az ad app permission add --api $AzureStorageAppId --id $AppId --api-permissions $PermissionId=Scope
az ad app permission grant --id $AppId --api $AzureStorageAppId --query resourceId
az ad app permission admin-consent --id $AppId
az storage cors add --methods GET HEAD OPTIONS POST --origins * --services b --account-name 
```

## Setup user access to the blobs.

By default users don't have permissions for accessing their own storage accounts via AAD. Add the permissions executing the script below. It will add read only permissions for the current user limited to the specified storage container.

``` bash
StorageAccountName={replace with existing storage account name}
ContainerName={replace with existing container in the storage account}

StorageAccountId=`az storage account show -n $StorageAccountName --query id -o tsv`
UserPrincipalName=`az ad signed-in-user show --query userPrincipalName -o tsv`
az role assignment create --assignee $UserPrincipalName --role "Storage Blob Data Reader" --scope $StorageAccountId/blobServices/default/containers/$ContainerName --query id

az role assignment create  --role "Storage Blob Data Contributor"  --assignee $UserPrincipalName --scope $StorageAccountId
```

## Setup Azure Storage Account CORS

To access storage from a browser enable CORS:

``` bash
az storage cors add --methods GET HEAD OPTIONS POST --origins '*' --allowed-headers '*' --exposed-headers '*' --services b --account-name $StorageAccountName --max-age 3600
```


Update `appsettings.Debug.json` in the `wwwroot` directory setting storage account name and a container name in that storage account.

Restart the application and login. The list of blobs in the configured container should show up on the web page. **Note:** it may take couple of minutes before the permissions propagate.

**Note:** Sometimes local build caches some files preventing the app from working if there were incompatible changes in the Azure. Deleting `bin` and `obj` directories will fix that particular issue.

## Deployment

Create `appsettings.Release.json` from `appsettings.json` and update similar to `appsettings.Debug.json` with the release values.

After deploying the Application go to the 
Azure Portal > Azure Active Directory > App Registrations blade > Authentication blade > Single-page application section.
And add Redirect URI to the location corresponding to your deployment similar to existing local host setup.

For other topics, like compression optimizations see [Host and deploy ASP.NET Core Blazor WebAssembly](https://docs.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly?view=aspnetcore-5.0).

## References

- [Secure an ASP.NET Core Blazor WebAssembly standalone app with Azure Active Directory](https://docs.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/standalone-with-azure-active-directory?view=aspnetcore-5.0).
- [Configure AAD access to Storage Account](https://docs.microsoft.com/en-us/azure/storage/common/storage-auth-aad-app?tabs=dotnet#view-and-run-the-completed-sample).
- [Configure user access to Storage Account](https://docs.microsoft.com/en-us/azure/storage/common/storage-auth-aad-rbac-portal#assign-azure-roles-using-the-azure-portal).
- [AAD Delegation SAS format](https://docs.microsoft.com/en-us/rest/api/storageservices/create-user-delegation-sas).
- [AAD Delegation SAS construction](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-blob-user-delegation-sas-create-dotnet).