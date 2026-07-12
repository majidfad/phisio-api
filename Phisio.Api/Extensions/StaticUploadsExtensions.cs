using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.FileProviders;

namespace Phisio.Api.Extensions;

public static class StaticUploadsExtensions
{
    public static WebApplication UseStaticUploads(this WebApplication app)
    {
        var uploadsPath = UploadsPath.ResolvePhysicalPath(app.Environment.ContentRootPath);
        Directory.CreateDirectory(UploadsPath.ResolveExercisesPhysicalPath(app.Environment.ContentRootPath));

        app.Use(RejectUploadPathTraversal);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsPath),
            RequestPath = UploadsPath.RequestPath,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl = "public,max-age=86400";
            },
        });

        app.Lifetime.ApplicationStarted.Register(() => LogSampleUploadUrl(app, uploadsPath));

        return app;
    }

    private static Task RejectUploadPathTraversal(HttpContext context, RequestDelegate next)
    {
        var requestPath = context.Request.Path.Value;

        if (requestPath is not null
            && requestPath.StartsWith(UploadsPath.RequestPath, StringComparison.OrdinalIgnoreCase)
            && UploadsPath.ContainsPathTraversal(requestPath))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }

        return next(context);
    }

    private static void LogSampleUploadUrl(WebApplication app, string uploadsPath)
    {
        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("StaticUploads");

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var baseUrl = addresses?
            .FirstOrDefault(address => address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? addresses?.FirstOrDefault()
            ?? "http://localhost:5111";

        var sampleUrl = UploadsPath.BuildSampleUrl(baseUrl);
        var sampleFilePath = Path.Combine(
            uploadsPath,
            UploadsPath.ExercisesFolderName,
            UploadsPath.SampleFileName);

        logger.LogInformation("Static uploads physical path: {UploadsPath}", uploadsPath);
        logger.LogInformation(
            "Static uploads sample URL (place file at uploads/exercises/{SampleFileName}): {SampleUrl}",
            UploadsPath.SampleFileName,
            sampleUrl);

        if (!File.Exists(sampleFilePath))
        {
            logger.LogInformation(
                "Sample upload file not found yet at {SampleFilePath}",
                sampleFilePath);
        }
    }
}
