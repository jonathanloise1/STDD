using NUnit.Framework;

namespace MyApp.IntegrationTests.Infrastructure;

/// <summary>Assertions on the error response of operations.md 4.2.</summary>
public static class ApiAssert
{
    /// <summary>
    /// The response is a problem document with this status and, when given, this <c>code</c> (the log key of
    /// a keyed outcome) and <c>detail</c> (the message the user sees).
    /// </summary>
    public static void IsProblem(ApiResponse response, int status, string? code = null, string? detail = null)
    {
        Assert.That(response.Status, Is.EqualTo(status), $"Status; body: {response.Body}");
        Assert.That(response.ContentType, Is.EqualTo("application/problem+json"));

        var problem = response.Problem;
        Assert.That(problem.Type, Is.EqualTo("about:blank"));
        Assert.That(problem.Status, Is.EqualTo(status));
        Assert.That(problem.Title, Is.Not.Empty);
        Assert.That(problem.TraceId, Does.Match("^[0-9a-f]{32}$"), "traceId is 32 lower-case hex");

        if (code is not null)
            Assert.That(problem.Code, Is.EqualTo(code), "code");
        else
            Assert.That(problem.Code, Is.Null, "an outcome with no key has no code");

        if (detail is not null)
            Assert.That(problem.Detail, Is.EqualTo(detail), "detail");
    }

    /// <summary>The response is a success with this status.</summary>
    public static void IsSuccess(ApiResponse response, int status = 200) =>
        Assert.That(response.Status, Is.EqualTo(status), $"Status; body: {response.Body}");
}
