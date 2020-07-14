# IdentityServerTest project

## IdentityServer 4 using .NET Core 3 without Entity Framework

The code mostly followed to excellent instructions from the blog entry [IdentityServer4 Without Entity Framework](https://mcguirev10.com/2018/01/02/identityserver4-without-entity-framework.html)
However necesseary changes was done to update API of UserStore, UserProfileService and PersistedGrantStore - mostly reflect switching to async version of methods.

Other changes are:

* switch to PostgreSQL database. The database creation script is in Client/identity.pgsql file.
* Facebook used for external authentification.
* The protected url is [http://localhost:5002/weatherforecast](https://localhost:500/weatherforecast)
* The IdentityServer run on [http://localhost:5000/Account/Login](http://localhost:5000/Account/Login)
* Basic test configuration for SPA added to the client code

It's easy to revert code back to SQL Server version, just replace connections and commands to SQL Server version and replace text of SQL commads to version from the original blog entry.
