﻿using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

public static class ApplicationBuilderExtensions
{
    private const string DEFAULT_ENDPOINT = "/healthz";

    private static readonly JsonSerializerOptions JsonOptions = new()
                                                                {
                                                                    PropertyNameCaseInsensitive = true,
                                                                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                                                                    WriteIndented = false,
                                                                    AllowTrailingCommas = true,
                                                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                                                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                                                                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                                                                };

    [Obsolete("Use UseDefaultHealthChecks instead.")]
    public static IApplicationBuilder UseSimpleHealthChecks(this IApplicationBuilder app, Func<IServiceProvider, string>? configure)
    {
        var path = configure?.Invoke(app.ApplicationServices);

        if (string.IsNullOrWhiteSpace(path)) path = DEFAULT_ENDPOINT;

        return UseSimpleHealthChecks(app, path);
    }

    [Obsolete("Use UseDefaultHealthChecks instead.")]
    public static IApplicationBuilder UseSimpleHealthChecks(this IApplicationBuilder app, string path = DEFAULT_ENDPOINT) => UseDefaultHealthChecks(app, path);

    public static IApplicationBuilder UseDefaultHealthChecks(this IApplicationBuilder app, Func<IServiceProvider, string>? configure)
    {
        var path = configure?.Invoke(app.ApplicationServices);

        if (string.IsNullOrWhiteSpace(path))
            path = DEFAULT_ENDPOINT;

        return UseDefaultHealthChecks(app, path, DEFAULT_ENDPOINT);
    }

    public static IApplicationBuilder UseDefaultHealthChecks(this IApplicationBuilder app, Func<IServiceProvider, (string GatewayEndpoint, string LocalEndpoint)>? configure)
    {
        var paths = configure?.Invoke(app.ApplicationServices);

        if (paths == null)
            paths = (DEFAULT_ENDPOINT, DEFAULT_ENDPOINT);

        return UseDefaultHealthChecks(app, paths.Value.GatewayEndpoint, paths.Value.LocalEndpoint);
    }


    public static IApplicationBuilder UseDefaultHealthChecks(this IApplicationBuilder app, string gatewayEndpoint, string localEndpoint = DEFAULT_ENDPOINT)
    {
        var endpoint = (IEndpointRouteBuilder)app;
        var options = new HealthCheckOptions
                      {
                          ResponseWriter = async (context, report) =>
                                           {
                                               if (!context.Request.Query.TryGetValue("detail", out _))
                                               {
                                                    var status = report.Status.ToString();

                                                    context.Response.ContentType = "text/plain";

                                                    await context.Response.WriteAsync(status);

                                                    return;
                                               }

                                               var items = report.Entries.ToDictionary(pair => pair.Key,
                                                                                       pair => new
                                                                                               {
                                                                                                   Status = pair.Value.Status.ToString(),
                                                                                                   Description = string.IsNullOrWhiteSpace(pair.Value.Description) ? null : pair.Value.Description,
                                                                                                   Duration = pair.Value.Duration.ToString(),
                                                                                                   Data = !pair.Value.Data.Any() ? null : pair.Value.Data,
                                                                                                   Exception = pair.Value.Exception?.ToString()
                                                                                               });

                                               using var stream = new MemoryStream();

                                               await JsonSerializer.SerializeAsync(stream, items, JsonOptions);

                                               stream.Seek(0, SeekOrigin.Begin);

                                               var body = await new StreamReader(stream).ReadToEndAsync();

                                               context.Response.ContentType = "application/json";

                                               await context.Response.WriteAsync(body);
                                           }
                      };


        var pipeline = endpoint.CreateApplicationBuilder()
                                .UseMiddleware<HealthCheckMiddleware>(Options.Options.Create(options))
                                .Build();

        endpoint.MapGet(gatewayEndpoint, pipeline);

        app.UseWhen(context => context.Request.Path.StartsWithSegments(localEndpoint, StringComparison.CurrentCultureIgnoreCase), cfg => cfg.UseMiddleware<HealthCheckMiddleware>(Options.Options.Create(options)));

        return app;
    }
}
