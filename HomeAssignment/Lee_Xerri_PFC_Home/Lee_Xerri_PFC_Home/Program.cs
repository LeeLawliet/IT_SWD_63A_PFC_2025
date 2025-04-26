using Lee_Xerri_PFC_Home.Repositories;
using Lee_Xerri_PFC_Home.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using StackExchange.Redis;
using Google.Cloud.SecretManager.V1;

namespace Lee_Xerri_PFC_Home
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(int.Parse(port));
            });

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();
            
            var secretManager = SecretManagerServiceClient.Create();
            string projectId = "pftchome-457811";

            async Task<string> GetSecret(string secretId)
            {
                var name = new SecretVersionName(projectId, secretId, "latest");
                var result = await secretManager.AccessSecretVersionAsync(name);
                return result.Payload.Data.ToStringUtf8();
            }

            var clientId = await GetSecret("Authentication-Google-ClientId");
            var clientSecret = await GetSecret("Authentication-Google-ClientSecret");
            var redisPassword = await GetSecret("RedisPassword");
            var redisUsername = await GetSecret("RedisUsername");
            var mailGunDomain = await GetSecret("MailGunDomain");
            var mailGunApiKey = await GetSecret("MailGunApiKey");

            builder.Configuration["Authentication:Google:ClientId"] = clientId;
            builder.Configuration["Authentication:Google:ClientSecret"] = clientSecret;
            builder.Configuration["RedisUsername"] = redisUsername;
            builder.Configuration["RedisPassword"] = redisPassword;
            builder.Configuration["MailGunDomain"] = mailGunDomain;
            builder.Configuration["MailGunApiKey"] = mailGunApiKey;

            // Configure Authentication
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
                })
                .AddCookie()
                .AddGoogle(options =>
                {
                    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
                    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
                    options.CallbackPath = "/signin-google";
                    options.UsePkce = true;

                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        context.Response.Redirect(context.RedirectUri.Replace("http://", "https://"));
                        return Task.CompletedTask;
                    };
                });

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

            var optionsRedis = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                User = builder.Configuration["RedisUsername"],
                Password = builder.Configuration["RedisPassword"],
                EndPoints = { { "redis-10900.c327.europe-west1-2.gce.redns.redis-cloud.com", 10900 } }
            };

            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(optionsRedis));
            builder.Services.AddSingleton<RedisRepository>();
            builder.Services.AddSingleton<BucketRepository>();
            builder.Services.AddSingleton<FirestoreRepository>();
            builder.Services.AddSingleton<PubSubService>();
            builder.Services.AddHttpClient<MailGunService>();

            var app = builder.Build();

            // Usual Middleware
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            await app.RunAsync();
        }
    }
}