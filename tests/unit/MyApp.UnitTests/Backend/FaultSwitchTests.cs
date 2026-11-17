extern alias WebApi;

using System.Data.Common;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyApp.Application.Logging;
using NUnit.Framework;
using WebApi::MyApp.WebApi.Filters;
using WebApi::MyApp.WebApi.Middlewares;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class FaultSwitchTests
{
    private static FaultSwitch Switch(string environment, bool? enabled)
    {
        var values = new Dictionary<string, string?>();
        if (enabled is not null) values[FaultSwitch.EnabledKey] = enabled.ToString();
        return new FaultSwitch(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new TestHostEnvironment(environment));
    }

    private static HttpContext Request(string? header)
    {
        var context = new DefaultHttpContext();
        if (header is not null) context.Request.Headers[FaultSwitch.HeaderName] = header;
        return context;
    }

    [Test]
    public void IsOffByDefault()
    {
        var faults = Switch("Development", enabled: null);

        Assert.That(faults.Enabled, Is.False);
        Assert.That(faults.IsRequested(Request(TasksLog.Keys.TaskCreateUnavailable), TasksLog.Keys.TaskCreateUnavailable), Is.False);
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    [TestCase("Test")]
    public void IsOffOutsideDevelopment_EvenWhenTurnedOnAndRequested(string environment)
    {
        var faults = Switch(environment, enabled: true);

        Assert.That(faults.Enabled, Is.False);
        Assert.That(faults.IsRequested(Request(TasksLog.Keys.TaskCreateUnavailable), TasksLog.Keys.TaskCreateUnavailable), Is.False);
    }

    [Test]
    public void InDevelopmentWhenTurnedOn_OnlyTheNamedOperationIsRequested()
    {
        var faults = Switch("Development", enabled: true);
        var request = Request($"{TasksLog.Keys.TaskListUnavailable}, {TasksLog.Keys.TaskCreateUnavailable}");

        Assert.That(faults.IsRequested(request, TasksLog.Keys.TaskCreateUnavailable), Is.True);
        Assert.That(faults.IsRequested(request, TasksLog.Keys.TaskListUnavailable), Is.True);
        Assert.That(faults.IsRequested(request, DashboardLog.Keys.DashKpiUnavailable), Is.False);
        Assert.That(faults.IsRequested(Request(null), TasksLog.Keys.TaskCreateUnavailable), Is.False);
    }

    private static (ServiceUnavailableFilter Filter, CapturingLoggerFactory Logs) Filter(string key, FaultSwitch faults, string environment = "Development")
    {
        var logs = new CapturingLoggerFactory();
        return (new ServiceUnavailableFilter(key, faults, logs, new TestHostEnvironment(environment)), logs);
    }

    private static ActionExecutingContext Executing(HttpContext http) =>
        new(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], new Dictionary<string, object?>(), new object());

    [Test]
    public async Task Filter_WithTheSwitchOn_AnswersUnavailableAndWritesTheKeyedEntry_WithoutRunningTheAction()
    {
        var key = TasksLog.Keys.TaskCreateUnavailable;
        var (filter, logs) = Filter(key, Switch("Development", true));
        var context = Executing(Request(key));
        var ran = false;

        await filter.OnActionExecutionAsync(context, () => { ran = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.That(ran, Is.False);
        Assert.That(context.Result, Is.TypeOf<ProblemResult>());
        var entry = logs.Logger.Entries.Single();
        Assert.That(entry.LogKey, Is.EqualTo(key));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
    }

    [Test]
    public async Task Filter_OutsideDevelopment_IgnoresTheHeaderAndRunsTheAction()
    {
        var key = TasksLog.Keys.TaskCreateUnavailable;
        var (filter, logs) = Filter(key, Switch("Production", true), "Production");
        var http = Request(key);
        var context = Executing(http);
        var ran = false;
        var executed = new ActionExecutedContext(context, [], new object());

        await filter.OnActionExecutionAsync(context, () => { ran = true; return Task.FromResult(executed); });

        Assert.That(ran, Is.True);
        Assert.That(context.Result, Is.Null);
        Assert.That(logs.Logger.Entries, Is.Empty);
    }

    [Test]
    public async Task Filter_CatchesADatabaseFailureOfTheAction()
    {
        var key = DashboardLog.Keys.DashKpiUnavailable;
        var (filter, logs) = Filter(key, Switch("Production", null), "Production");
        var context = Executing(Request(null));
        var executed = new ActionExecutedContext(context, [], new object()) { Exception = new InvalidOperationException("wrapped", new FakeDbException()) };

        await filter.OnActionExecutionAsync(context, () => Task.FromResult(executed));

        Assert.That(executed.ExceptionHandled, Is.True);
        Assert.That(executed.Result, Is.TypeOf<ProblemResult>());
        Assert.That(logs.Logger.Entries.Single().LogKey, Is.EqualTo(key));
    }

    [Test]
    public async Task Filter_LeavesOtherFailuresToTheMiddleware()
    {
        var (filter, logs) = Filter(TasksLog.Keys.TaskListUnavailable, Switch("Production", null), "Production");
        var context = Executing(Request(null));
        var executed = new ActionExecutedContext(context, [], new object()) { Exception = new KeyNotFoundException() };

        await filter.OnActionExecutionAsync(context, () => Task.FromResult(executed));

        Assert.That(executed.ExceptionHandled, Is.False);
        Assert.That(logs.Logger.Entries, Is.Empty);
    }

    private sealed class FakeDbException : DbException;

    [Test]
    public void EveryUnavailableKey_HasOneEmitter_AndEveryActionAttributeUsesAKnownKey()
    {
        var keys = typeof(TasksLog).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(TasksLog).Namespace && t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            // The emitters of the filter take the exception and its summary; a [ClientReported] key has no emitter (the browser reports it)
            .Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual([typeof(ILogger), typeof(Exception), typeof(string), typeof(string)]))
            .Select(m => m.GetCustomAttribute<LoggerMessageAttribute>()?.EventName)
            .Where(k => k is not null && !k.StartsWith("platform.", StringComparison.Ordinal))
            .ToList();

        Assert.That(keys, Has.Count.EqualTo(13), "12 unavailable keys of the areas and help.download.failed");
        var logger = new CapturingLogger<object>();
        foreach (var key in keys)
        {
            Assert.That(UnavailableOutcomes.Emit(logger, key!, new Exception("x"), "T", "F"), Is.True, key);
            Assert.That(logger.Entries.Last().LogKey, Is.EqualTo(key));
            Assert.That(logger.Entries.Last().Level, Is.EqualTo(LogLevel.Error), key);
        }

        var used = typeof(ServiceUnavailableAttribute).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(m => m.GetCustomAttribute<ServiceUnavailableAttribute>()?.Key)
            .Where(k => k is not null)
            .ToList();

        Assert.That(used, Is.Not.Empty);
        Assert.That(used.Distinct(), Is.SubsetOf(keys));
        Assert.That(used.Distinct().Count(), Is.EqualTo(used.Count - 1), "only the team key is used by two actions");
    }
}
