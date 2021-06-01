using Azure.Core;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorAzureBlobBrowser
{
    public class AccessTokenProviderTokenCredential : TokenCredential
    {
        private IAccessTokenProvider TokenProvider;

        public AccessTokenProviderTokenCredential(IAccessTokenProvider tokenProvider) => TokenProvider = tokenProvider;

        public override Azure.Core.AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => GetTokenAsync(requestContext, cancellationToken).Result;

        public override async ValueTask<Azure.Core.AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var tokenResult = await TokenProvider.RequestAccessToken(new AccessTokenRequestOptions { Scopes = requestContext.Scopes });

            if (tokenResult.TryGetToken(out var token) && token != null)
            {
                return new Azure.Core.AccessToken(token.Value, token.Expires);
            }
            else
            {
                throw new AccessTokenNotAvailableException(null, tokenResult, requestContext.Scopes);
            }
        }
    }
}
