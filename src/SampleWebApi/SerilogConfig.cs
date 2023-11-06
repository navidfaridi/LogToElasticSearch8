using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Elasticsearch;
using Serilog.Sinks.Elasticsearch;

namespace WebApplication1
{
    public class LoggingOptions
    {
        public const string SectionName = "LoggingOptions";
        public string ElasticUri { get; set; } = null!;
        public string ElasticUser { get; set; } = null!;
        public string ElasticPassword { get; set; } = null!;
    }

    public static class ServiceCollectionExtension
    {
        public static void ConfigureSerilog(this WebApplicationBuilder builder, string serviceName, string serviceIndex)
        {
            builder.Services.AddSerilog();
            builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection(nameof(LoggingOptions)));
            builder.Host.UseSerilog();
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .Enrich.WithProperty("Service", serviceName)
                .Enrich.WithProperty("DomainName", Environment.UserDomainName)
                .Enrich.WithProperty("UserName", Environment.UserName)
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithProperty("OSVersion", Environment.OSVersion)
                .WriteTo.Debug()
                .WriteTo.Console()
                .WriteTo.Elasticsearch(builder.Services.configSink(serviceIndex))
                .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
                .ReadFrom.Configuration(builder.Configuration)
                .Filter.ByExcluding(c => c.Properties
                    .Any(p =>
                         p.Value.ToString().Contains("swagger") ||
                         p.Value.ToString().Contains("browserLink") ||
                         p.Value.ToString().Contains("aspnetcore-browser-refresh.js")
                     ))
                .CreateLogger();
        }

        static ElasticsearchSinkOptions configSink(this IServiceCollection services, string serviceIndex)
        {
            var options = services.BuildServiceProvider().GetRequiredService<IOptions<LoggingOptions>>().Value;
            return new ElasticsearchSinkOptions(new Uri(options.ElasticUri))
            {
                AutoRegisterTemplate = true,
                IndexFormat = $"{serviceIndex}-{DateTime.UtcNow:yyyy-MM}",
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                NumberOfShards = 2,
                NumberOfReplicas = 1,
                CustomFormatter = new ElasticsearchJsonFormatter(false, Environment.NewLine),
                ModifyConnectionSettings = x => x.BasicAuthentication(options.ElasticUser, options.ElasticPassword)
            };
        }
    }
}
