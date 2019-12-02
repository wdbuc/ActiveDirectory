using System.Collections.Generic;
using System.Threading.Tasks;
using ActiveDirectory.Entities;
using ActiveDirectory.Extensions;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json.Linq;

namespace ActiveDirectory
{
    public class Startup
    {
        private IConfiguration Configuration { get; set; }

        private readonly AppSettings settings;

        private const string ServiceName = "Active Directory";

        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
              .SetBasePath(env.ContentRootPath)
              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
              .AddEnvironmentVariables();

            Configuration = builder.Build();

            //Extract the AppSettings information from the appsettings config.
            settings = new AppSettings();
            Configuration.GetSection(nameof(AppSettings)).Bind(settings);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(settings); //AppSettings type
            services.AddSingleton<Store>();

            var _ = (settings.Domains.Empty()) ?
                    services.AddSingleton<IAdRepository>(new AdRepository()) :
                    services.AddSingleton<IAdRepository>(new AdRepository(settings.Domains));

            services.AddCarter(options =>
            {
                options.OpenApi = GetOpenApiOptions(settings);
            });

            //HealthChecks
            services.AddHealthChecks();

            services.AddMemoryCache();
        }

        public void Configure(IApplicationBuilder app, AppSettings appSettings)
        {
            app.UseRouting();

            app.UseSwaggerUI(opt =>
            {
                opt.RoutePrefix = appSettings.RouteDefinition.RoutePrefix;
                opt.SwaggerEndpoint(appSettings.RouteDefinition.SwaggerEndpoint, ServiceName);
            });

            app.UseHealthChecks("/healthcheck", new HealthCheckOptions()
            {
                ResponseWriter = WriteResponse
            });

            app.UseEndpoints(builder => builder.MapCarter());
        }

        private OpenApiOptions GetOpenApiOptions(AppSettings settings) =>
        new OpenApiOptions()
        {
            DocumentTitle = ServiceName,
            ServerUrls = settings.Addresses,
            Securities = new Dictionary<string, OpenApiSecurity>()
        };

        private static Task WriteResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var json = new JObject(
                        new JProperty("statusCode", report.Status),
                        new JProperty("status", report.Status.ToString()),
                        new JProperty("timelapsed", report.TotalDuration)
                );

            return context.Response.WriteAsync(json.ToString(Newtonsoft.Json.Formatting.Indented));
        }
    }
}
