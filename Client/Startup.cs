using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Logging;
using Microsoft.AspNetCore.Authentication;

namespace toy2.net
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

            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "Cookies";
                    options.DefaultChallengeScheme = "oidc";
                })
                .AddCookie("Cookies")

              .AddOpenIdConnect("oidc", options =>
              {
                  ///options.SignInScheme = "Cookies";
                  options.Authority = "http://localhost:5000";
                  options.RequireHttpsMetadata = false;
                  options.ClientId = "mv10blog.client.mvc";
                  options.ClientSecret = "the_secret";
                  options.ResponseType = "code";
                  
                  options.SaveTokens = true;

                  options.Scope.Add("mv10blog.identity");
                  options.Scope.Add("offline_access");
                  options.ClaimActions.MapJsonKey("website", "website");
              });

            //https://blog.georgekosmidis.net/2019/02/08/identityserver4-asp-dotnet-core-api-and-a-client-with-username-password/
            /*services.AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
            .AddIdentityServerAuthentication(options =>
            {
                options.Authority = "http://localhost:5000";//IdentityServer URL
                options.RequireHttpsMetadata = false;       //False for local addresses, true ofcourse for live scenarios

                options.ApiName = "mv10blog.identity";
                options.ApiSecret = "the_secret";
            });*/

        }



        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            if (env.IsDevelopment())
            {
                IdentityModelEventSource.ShowPII = true;
                app.UseDeveloperExceptionPage();
                //app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            //app.UseHttpsRedirection();
            app.UseStaticFiles();
            //app.UseCookiePolicy();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            //app.UseSession();

            app.UseEndpoints(endpoints =>
            {
                //endpoints.MapRazorPages();
                //endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute()
                    .RequireAuthorization();
            });

            app.UseSpa(spa =>
                  {
          // To learn more about options for serving an Angular SPA from ASP.NET Core,
          // see https://go.microsoft.com/fwlink/?linkid=864501

          /*spa.Options.SourcePath = "ClientApp";*/

                      if (env.IsDevelopment())
                      {
                          spa.UseProxyToSpaDevelopmentServer("http://localhost:8080");
                        //spa.UseAngularCliServer(npmScript: "start");
                    }
                  });

        }
    }
}
