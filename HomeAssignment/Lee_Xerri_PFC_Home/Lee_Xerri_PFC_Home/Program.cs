using Lee_Xerri_PFC_Home.Repositories;
using Lee_Xerri_PFC_Home.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using StackExchange.Redis;

namespace Lee_Xerri_PFC_Home
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure authentication
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

            if (builder.Environment.IsDevelopment())
                builder.Configuration.AddUserSecrets<Program>();

            builder.Services.AddSingleton<BucketRepository>();
            builder.Services.AddSingleton<FirestoreRepository>();
            builder.Services.AddSingleton<PubSubService>();
            string connectionRedis = builder.Configuration["Redis"];
            string usernameRedis = builder.Configuration["RedisUsername"];
            string passwordRedis = builder.Configuration["RedisPassword"];

            var options = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                User = usernameRedis,
                Password = passwordRedis,
                EndPoints = { { "redis-10900.c327.europe-west1-2.gce.redns.redis-cloud.com", 10900 }},
            };

            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
                    ConnectionMultiplexer.Connect(options));
            builder.Services.AddSingleton<RedisRepository>();
            builder.Services.AddHttpClient<MailGunService>();

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();

            var app = builder.Build();
            
            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Global error: " + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    throw;
                }
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}