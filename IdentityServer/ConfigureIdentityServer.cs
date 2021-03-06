using System;
using System.Collections.Generic;
using IdentityServer4;
using IdentityServer4.Models;

namespace IdentityServer
{
    public class ConfigureIdentityServer
    {
        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Email(),
                new IdentityResources.Profile(),
                new IdentityResources.Phone(),
                new IdentityResources.Address(),

                new IdentityResource(
                    name: "mv10blog.identity",
                    displayName: "MV10 Blog User Profile",
                    userClaims: new[] { "mv10_accounttype" })
            };
        }

        // scopes define the API resources in your system
        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
            {
                new ApiResource("api1", "API1")
            };
        }

        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>
            {
                new Client
        {
            ClientId = "client",
            ClientSecrets = { new Secret("the_secret".Sha256()) },

            AllowedGrantTypes = GrantTypes.ClientCredentials,
            // scopes that client has access to
            AllowedScopes = { "api1" }
        },
                new Client
                {
                    ClientId = "mv10blog.client.mvc",
                    ClientName = "McGuireV10.com.mvc",
                    ClientUri = "https://localhost:5002",
                    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                    ClientSecrets = {new Secret("the_secret".Sha256())},
                    AllowRememberConsent = true,
                    AllowOfflineAccess = true,
                    RedirectUris = { "http://localhost:5002/signin-oidc","https://localhost:5003/signin-oidc"}, // after login
                    PostLogoutRedirectUris = { "https://localhost:5002/signout-callback-oidc"}, // after logout
                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Email,
                        //IdentityServerConstants.StandardScopes.Phone,
                        //IdentityServerConstants.StandardScopes.Address,
                        "mv10blog.identity"
                    },

                }
            };
        }

        public static IEnumerable<ApiScope> ApiScopes()
        {
            return new List<ApiScope>
        {
            new ApiScope("api1", "My API")
        };
        }
    }
}