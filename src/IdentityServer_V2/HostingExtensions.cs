using Duende.IdentityServer;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using IdentityServer_V2;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityServer_V2
{
    internal static class HostingExtensions
    {
        public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
        {
            builder.Services.AddRazorPages();
            var migraionsAssembly = typeof(Program).Assembly.GetName().Name;
            const string connectionString = @"Server=localhost:5432;User Id=postgr;password=postgrespw;Database=IdentityServer";
            var isBuilder = builder.Services.AddIdentityServer(options =>
                {
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;

                    // see https://docs.duendesoftware.com/identityserver/v6/fundamentals/resources/
                    options.EmitStaticAudienceClaim = true;
                })
                .AddConfigurationStore(option =>
                {
                    option.ConfigureDbContext = b => b.UseNpgsql(connectionString,
                        sql => sql.MigrationsAssembly(migraionsAssembly));
                })
                .AddOperationalStore(option =>
                {
                    option.ConfigureDbContext = b => b.UseNpgsql(connectionString,
                        sql => sql.MigrationsAssembly(migraionsAssembly));
                })
                .AddTestUsers(TestUsers.Users);


            // if you want to use server-side sessions: https://blog.duendesoftware.com/posts/20220406_session_management/
            // then enable it
            //isBuilder.AddServerSideSessions();
            //
            // and put some authorization on the admin/management pages
            //builder.Services.AddAuthorization(options =>
            //       options.AddPolicy("admin",
            //           policy => policy.RequireClaim("sub", "1"))
            //   );
            //builder.Services.Configure<RazorPagesOptions>(options =>
            //    options.Conventions.AuthorizeFolder("/ServerSideSessions", "admin"));


            builder.Services.AddAuthentication()
                .AddGoogle(options =>
                {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

                    // register your IdentityServer with Google at https://console.developers.google.com
                    // enable the Google+ API
                    // set the redirect URI to https://localhost:5001/signin-google
                    options.ClientId = "copy client ID from Google here";
                    options.ClientSecret = "copy client secret from Google here";
                });

            return builder.Build();
        }

        public static WebApplication ConfigurePipeline(this WebApplication app)
        {
            app.UseSerilogRequestLogging();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            InitilizeDatabase(app);

            app.UseStaticFiles();
            app.UseRouting();
            app.UseIdentityServer();
            app.UseAuthorization();

            app.MapRazorPages()
                .RequireAuthorization();

            return app;
        }

        private static void InitilizeDatabase(IApplicationBuilder app)
        {
            using(var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>()!.CreateScope())
            {
                serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();
                var context = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
                context.Database.Migrate();

                if (!context.Clients.Any())
                {
                    foreach (var client in Config.Clients)
                    {
                        context.Clients.Add(client.ToEntity());
                    }
                    context.SaveChanges();
                }

                if (!context.IdentityResources.Any())
                {
                    foreach(var resource in Config.IdentityResources)
                    {
                        context.IdentityResources.Add(resource.ToEntity());
                    }
                    context.SaveChanges();
                }

                if(!context.ApiScopes.Any())
                {
                    foreach( var api in Config.ApiScopes)
                    {
                        context.ApiScopes.Add(api.ToEntity());
                    }
                    context.SaveChanges();
                }



            }
        }
    }
}