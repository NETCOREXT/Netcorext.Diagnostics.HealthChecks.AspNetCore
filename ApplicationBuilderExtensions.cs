using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

public static class ApplicationBuilderExtensions
{
    public const string DEFAULT_ENDPOINT = "/Health";

    public static IApplicationBuilder UseSimpleHealthChecks(this IApplicationBuilder app, Func<IServiceProvider, string?> configure)
    {
        var path = configure?.Invoke(app.ApplicationServices);

        if (string.IsNullOrWhiteSpace(path)) path = DEFAULT_ENDPOINT;

        return UseSimpleHealthChecks(app, path);
    }

    public static IApplicationBuilder UseSimpleHealthChecks(this IApplicationBuilder app, string? path = DEFAULT_ENDPOINT)
    {
        return app.UseHealthChecks(path, new HealthCheckOptions
                                         {
                                             ResponseWriter = async (context, report) =>
                                                              {
                                                                  var items = report.Entries.ToDictionary(pair => pair.Key,
                                                                                                          pair => new
                                                                                                                  {
                                                                                                                      Status = pair.Value.Status.ToString(),
                                                                                                                      Description = string.IsNullOrWhiteSpace(pair.Value.Description) ? null : pair.Value.Description,
                                                                                                                      Duration = pair.Value.Duration.ToString(),
                                                                                                                      Data = pair.Value.Data == null || !pair.Value.Data.Any() ? null : pair.Value.Data,
                                                                                                                      Exception = pair.Value.Exception?.ToString()
                                                                                                                  });

                                                                  using var stream = new MemoryStream();

                                                                  await JsonSerializer.SerializeAsync(stream, items, new JsonSerializerOptions
                                                                                                                     {
                                                                                                                         IgnoreNullValues = true,
                                                                                                                         WriteIndented = false,
                                                                                                                         AllowTrailingCommas = true,
                                                                                                                         PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                                                                                                         DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                                                                                                                     });

                                                                  stream.Seek(0, SeekOrigin.Begin);

                                                                  var body = await new StreamReader(stream).ReadToEndAsync();

                                                                  context.Response.ContentType = "application/json";

                                                                  await context.Response.WriteAsync(body);
                                                              }
                                         });
    }
}