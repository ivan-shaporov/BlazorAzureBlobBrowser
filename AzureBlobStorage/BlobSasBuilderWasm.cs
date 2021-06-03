using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BlazorAzureBlobBrowser.AzureBlobStorage
{
    /// <summary>
    /// Copy from https://github.com/Azure/azure-sdk-for-net modified to work with Blazor Wasm.
    /// Requires HmacSha256.js with the replacement of <see cref="System.Security.Cryptography.HMACSHA256"/>.
    /// <see cref="BlobSasBuilder"/> is used to generate a Shared Access
    /// Signature (SAS) for an Azure Storage container or blob.
    /// For more information, see
    /// <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/constructing-a-service-sas">
    /// Create a service SAS</see>.
    /// </summary>
    public class BlobSasBuilderWasm : BlobSasBuilder, IDisposable, IAsyncDisposable
    {
        private const string SasTimeFormatSeconds = "yyyy-MM-ddTHH:mm:ssZ";
        private readonly IJSRuntime JsRuntime;
        private IJSObjectReference HmacSha256;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobSasBuilder"/>
        /// class to create a Blob Container Service Sas.
        /// </summary>
        /// <param name="permissions">
        /// The time at which the shared access signature becomes invalid.
        /// This field must be omitted if it has been specified in an
        /// associated stored access policy.
        /// </param>
        /// <param name="expiresOn">
        /// The time at which the shared access signature becomes invalid.
        /// This field must be omitted if it has been specified in an
        /// associated stored access policy.
        /// </param>
        /// <param name="jsRuntime">
        /// JavaScript runtime for accessing js implementation of missing .Net Cryptography.
        /// </param>
        public BlobSasBuilderWasm(BlobSasPermissions permissions, DateTimeOffset expiresOn, IJSRuntime jsRuntime) 
            : base(permissions, expiresOn)
            => JsRuntime = jsRuntime;

        /// <summary>
        /// Creates Secure Access Signature for an Azure Storage object (container, blob,...)
        /// </summary>
        /// <param name="userDelegationKey">
        /// A <see cref="UserDelegationKey"/> returned from
        /// <see cref="Azure.Storage.Blobs.BlobServiceClient.GetUserDelegationKeyAsync"/>.
        /// </param>
        /// <param name="accountName">The name of the storage account.</param>
        /// <returns>Secure Access Signature that can be appended to URL query string for getting access to the object.</returns>
        public async Task<string> GetSas(UserDelegationKey userDelegationKey, string accountName)
        {
            await EnsureState(JsRuntime);

            var startTime = FormatTimesForSasSigning(StartsOn);
            var expiryTime = FormatTimesForSasSigning(ExpiresOn);
            var signedStart = FormatTimesForSasSigning(userDelegationKey.SignedStartsOn);
            var signedExpiry = FormatTimesForSasSigning(userDelegationKey.SignedExpiresOn);

            // See http://msdn.microsoft.com/en-us/library/azure/dn140255.aspx
            var stringToSign = String.Join("\n",
                Permissions,
                startTime,
                expiryTime,
                GetCanonicalName(accountName, BlobContainerName ?? String.Empty, BlobName ?? String.Empty),
                userDelegationKey.SignedObjectId,
                userDelegationKey.SignedTenantId,
                signedStart,
                signedExpiry,
                userDelegationKey.SignedService,
                userDelegationKey.SignedVersion,
                PreauthorizedAgentObjectId,
                null, // AgentObjectId - enabled only in HNS accounts
                CorrelationId,
                IPRange.ToString(),
                Protocol.ToProtocolString(),
                Version,
                Resource,
                Snapshot ?? BlobVersionId,
                CacheControl,
                ContentDisposition,
                ContentEncoding,
                ContentLanguage,
                ContentType);

            var signature = await HmacSha256.InvokeAsync<string>("sha256", stringToSign, userDelegationKey.Value);

            var sasBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(userDelegationKey.SignedObjectId))
            {
                sasBuilder.AppendQueryParameter(SasParameters.KeyObjectId, userDelegationKey.SignedObjectId);
            }

            if (!string.IsNullOrWhiteSpace(userDelegationKey.SignedTenantId))
            {
                sasBuilder.AppendQueryParameter(SasParameters.KeyTenantId, userDelegationKey.SignedTenantId);
            }

            if (userDelegationKey.SignedStartsOn != DateTimeOffset.MinValue)
            {                
                sasBuilder.AppendQueryParameter(SasParameters.KeyStart, WebUtility.UrlEncode(signedStart));
            }

            if (userDelegationKey.SignedExpiresOn != DateTimeOffset.MinValue)
            {
                sasBuilder.AppendQueryParameter(SasParameters.KeyExpiry, WebUtility.UrlEncode(signedExpiry));
            }

            if (!string.IsNullOrWhiteSpace(userDelegationKey.SignedService))
            {
                sasBuilder.AppendQueryParameter(SasParameters.KeyService, userDelegationKey.SignedService);
            }

            if (!string.IsNullOrWhiteSpace(userDelegationKey.SignedVersion))
            {
                sasBuilder.AppendQueryParameter(SasParameters.KeyVersion, userDelegationKey.SignedVersion);
            }

            if (!string.IsNullOrWhiteSpace(Version))
            {
                sasBuilder.AppendQueryParameter(SasParameters.Version, Version);
            }

            if (Protocol != default)
            {
                sasBuilder.AppendQueryParameter(SasParameters.Protocol, Protocol.ToProtocolString());
            }

            if (StartsOn != DateTimeOffset.MinValue)
            {
                sasBuilder.AppendQueryParameter(SasParameters.StartTime, WebUtility.UrlEncode(startTime));
            }

            if (ExpiresOn != DateTimeOffset.MinValue)
            {
                sasBuilder.AppendQueryParameter(SasParameters.ExpiryTime, WebUtility.UrlEncode(expiryTime));
            }

            var ipr = IPRange.ToString();
            if (ipr.Length > 0)
            {
                sasBuilder.AppendQueryParameter(SasParameters.IPRange, ipr);
            }

            if (!string.IsNullOrWhiteSpace(Identifier))
            {
                sasBuilder.AppendQueryParameter(SasParameters.Identifier, Identifier);
            }

            if (!string.IsNullOrWhiteSpace(Resource))
            {
                sasBuilder.AppendQueryParameter(SasParameters.Resource, Resource);
            }

            if (!string.IsNullOrWhiteSpace(Permissions))
            {
                sasBuilder.AppendQueryParameter(SasParameters.Permissions, Permissions);
            }

            if (!string.IsNullOrWhiteSpace(CacheControl))
            {
                sasBuilder.AppendQueryParameter(SasParameters.CacheControl, WebUtility.UrlEncode(CacheControl));
            }

            if (!string.IsNullOrWhiteSpace(ContentDisposition))
            {
                sasBuilder.AppendQueryParameter(SasParameters.ContentDisposition, WebUtility.UrlEncode(ContentDisposition));
            }

            if (!string.IsNullOrWhiteSpace(ContentEncoding))
            {
                sasBuilder.AppendQueryParameter(SasParameters.ContentEncoding, WebUtility.UrlEncode(ContentEncoding));
            }

            if (!string.IsNullOrWhiteSpace(ContentLanguage))
            {
                sasBuilder.AppendQueryParameter(SasParameters.ContentLanguage, WebUtility.UrlEncode(ContentLanguage));
            }

            if (!string.IsNullOrWhiteSpace(ContentType))
            {
                sasBuilder.AppendQueryParameter(SasParameters.ContentType, WebUtility.UrlEncode(ContentType));
            }

            if (!string.IsNullOrWhiteSpace(PreauthorizedAgentObjectId))
            {
                sasBuilder.AppendQueryParameter(SasParameters.PreauthorizedAgentObjectId, WebUtility.UrlEncode(PreauthorizedAgentObjectId));
            }

            if (!string.IsNullOrWhiteSpace(CorrelationId))
            {
                sasBuilder.AppendQueryParameter(SasParameters.CorrelationId, WebUtility.UrlEncode(CorrelationId));
            }

            if (!string.IsNullOrWhiteSpace(signature))
            {
                sasBuilder.AppendQueryParameter(SasParameters.Signature, WebUtility.UrlEncode(signature));
            }

            var sas = sasBuilder.ToString();

            return sas;
        }

        /// <summary>
        /// Ensure the <see cref="BlobSasBuilder"/>'s properties are in a
        /// consistent state.
        /// </summary>
        private async Task EnsureState(IJSRuntime jsRuntime)
        {
            if (HmacSha256 == null)
            {
                HmacSha256 = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./HmacSha256.js");
            }

            if (Identifier == default)
            {
                if (ExpiresOn == default)
                {
                    throw SasMissingData(nameof(ExpiresOn));
                }
                if (string.IsNullOrEmpty(Permissions))
                {
                    throw SasMissingData(nameof(Permissions));
                }
            }

            // Container
            if (string.IsNullOrEmpty(BlobName))
            {
                Resource = SasResource.Container;
            }

            // Blob or Snapshot
            else
            {
                // Blob
                if (string.IsNullOrEmpty(Snapshot) && string.IsNullOrEmpty(BlobVersionId))
                {
                    Resource = SasResource.Blob;
                }
                // Snapshot
                else if (string.IsNullOrEmpty(BlobVersionId))
                {
                    Resource = SasResource.BlobSnapshot;
                }
                // Blob Version
                else
                {
                    Resource = SasResource.BlobVersion;
                }
            }

            Version = DefaultSasVersionInternal;
        }

        /// <summary>
        /// FormatTimesForSASSigning converts a time.Time to a snapshotTimeFormat string suitable for a
        /// SASField's StartTime or ExpiryTime fields. Returns "" if value.IsZero().
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private static string FormatTimesForSasSigning(DateTimeOffset time) =>
            // "yyyy-MM-ddTHH:mm:ssZ"
            (time == new DateTimeOffset()) ? "" : time.ToString(SasTimeFormatSeconds, CultureInfo.InvariantCulture);

        private static InvalidOperationException SasMissingData(string paramName)
            => new InvalidOperationException($"SAS is missing required parameter: {paramName}");

        /// <summary>
        /// Settable internal property to allow different versions in test.
        /// </summary>
        private static string DefaultSasVersionInternal { get; set; } = "2020-08-04";

        /// <summary>
        /// Computes the canonical name for a container or blob resource for SAS signing.
        /// Container: "/blob/account/containername"
        /// Blob: "/blob/account/containername/blobname"
        /// </summary>
        /// <param name="account">The name of the storage account.</param>
        /// <param name="containerName">The name of the container.</param>
        /// <param name="blobName">The name of the blob.</param>
        /// <returns>The canonical resource name.</returns>
        private static string GetCanonicalName(string account, string containerName, string blobName)
            => !String.IsNullOrEmpty(blobName)
               ? $"/blob/{account}/{containerName}/{blobName.Replace("\\", "/")}"
               : $"/blob/{account}/{containerName}";

        #region Implement Asyn Disposable pattern.
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();

            Dispose(disposing: false);
            #pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
            GC.SuppressFinalize(this);
            #pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                (HmacSha256 as IDisposable)?.Dispose();
            }

            HmacSha256 = null;
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (HmacSha256 is not null)
            {
                await HmacSha256.DisposeAsync().ConfigureAwait(false);
            }

            HmacSha256 = null;
        }
        #endregion Implement Disposable
    }
}
