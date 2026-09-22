using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Middleware;

namespace SEF_Project.Api.Tests;

public class GlobalExceptionHandlerTests
{
    private static async Task<(int StatusCode, string Body)> HandleAsync(
        Exception exception)
    {
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        Assert.True(handled);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body)
            .ReadToEndAsync();

        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task InvalidOperationException_ShouldMapTo409Conflict()
    {
        var (statusCode, body) = await HandleAsync(
            new InvalidOperationException("Insufficient stock."));

        Assert.Equal(409, statusCode);
        Assert.Contains("Conflict", body);
        Assert.Contains("Insufficient stock.", body);
    }

    [Fact]
    public async Task ArgumentException_ShouldMapTo400BadRequest()
    {
        var (statusCode, body) = await HandleAsync(
            new ArgumentException("Invalid product variant."));

        Assert.Equal(400, statusCode);
        Assert.Contains("Bad Request", body);
        Assert.Contains("Invalid product variant.", body);
    }

    [Fact]
    public async Task UnauthorizedAccessException_ShouldMapTo401()
    {
        var (statusCode, _) = await HandleAsync(
            new UnauthorizedAccessException("Access denied."));

        Assert.Equal(401, statusCode);
    }

    [Fact]
    public async Task UnhandledException_ShouldMapTo500_WithoutLeakingDetails()
    {
        var (statusCode, body) = await HandleAsync(
            new Exception("SECRET internal failure detail"));

        Assert.Equal(500, statusCode);
        Assert.Contains("An unexpected error occurred.", body);
        Assert.DoesNotContain("SECRET", body);
    }
}
