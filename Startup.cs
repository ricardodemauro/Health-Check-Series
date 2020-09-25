using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson.IO;

namespace WebAppHealthChecks
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            //adding health check services to container
            services.AddHealthChecks()
                .AddMongoDb(mongodbConnectionString: _configuration.GetConnectionString("DefaultMongo"),
                    name: "mongo",
                    failureStatus: HealthStatus.Unhealthy); //adding MongoDb Health Check

            services.AddHealthChecksUI(opt =>
            {
                opt.SetEvaluationTimeInSeconds(15);
                opt.MaximumHistoryEntriesPerEndpoint(60);
                opt.SetHeaderText("Health Check UIIIII");
                opt.SetApiMaxActiveRequests(1);
                opt.DisableDatabaseMigrations();

                opt.AddHealthCheckEndpoint("endpoint1", "/healthz");
            })
            .AddInMemoryStorage();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/healthcheck");
                endpoints.MapHealthChecks("/hc",
                   new HealthCheckOptions
                   {
                       ResponseWriter = async (context, report) =>
                       {
                           var result = JsonSerializer.Serialize(
                               new
                               {
                                   status = report.Status.ToString(),
                                   errors = report.Entries.Select(e => new { key = e.Key, value = Enum.GetName(typeof(HealthStatus), e.Value.Status) })
                               });

                           context.Response.ContentType = MediaTypeNames.Application.Json;

                           await context.Response.WriteAsync(result);
                       }
                   });

                endpoints.MapHealthChecks("/healthz", new HealthCheckOptions
                {
                    Predicate = _ => true,
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });

                endpoints.MapHealthChecksUI();

                endpoints.MapGet("/", async context => await context.Response.WriteAsync("Hello World!"));
            });
        }
    }
}
