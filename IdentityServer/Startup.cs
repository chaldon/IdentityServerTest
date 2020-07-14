using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IdentityServer4;
using IdentityServer4.Stores;
//using Microsoft.ApplicationInsights.Extensibility;

namespace IdentityServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            var builder = services.AddIdentityServer()
                .AddInMemoryClients(ConfigureIdentityServer.GetClients())
                .AddInMemoryIdentityResources(ConfigureIdentityServer.GetIdentityResources())
                .AddInMemoryApiScopes(ConfigureIdentityServer.ApiScopes())
                .AddInMemoryApiResources(ConfigureIdentityServer.GetApiResources())
                .AddProfileService<UserProfileService>();

            builder.AddDeveloperSigningCredential();

            services.AddSingleton<IUserStore, UserStore>();

            services.AddTransient<IPersistedGrantStore, PersistedGrantStore>();

            services.AddAuthentication()
                .AddFacebook("Facebook", options =>
                {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

                    options.AppId = Configuration["Authentication:Facebook:AppId"];
                    options.AppSecret = Configuration["Authentication:Facebook:AppSecret"];
                })
                /*.AddGoogle("Google", options =>
                {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                    options.ClientId = "434483408261-55tc8n0cs4ff1fe21ea8df2o443v2iuc.apps.googleusercontent.com";
                    options.ClientSecret = "3gcoTrEDPPJ0ukn_aYYT6PWo";
                })*/
            ;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
             // https://github.com/IdentityServer/IdentityServer4/blob/main/samples/Quickstarts/3_AspNetCoreAndApis/src/IdentityServer/Startup.cs
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.UseIdentityServer();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
