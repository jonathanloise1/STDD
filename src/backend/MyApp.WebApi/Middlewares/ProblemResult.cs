using Microsoft.AspNetCore.Mvc;

namespace MyApp.WebApi.Middlewares;

/// <summary>An action result that writes the problem document of operations.md 4.2.</summary>
public sealed class ProblemResult(ProblemDocument document) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context) => ProblemResponse.WriteAsync(context.HttpContext, document);
}
