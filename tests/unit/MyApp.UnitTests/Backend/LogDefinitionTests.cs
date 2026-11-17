using System.Reflection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Logging;
using MyApp.Domain.Entities;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class LogDefinitionTests
{
    [Test]
    public void TaskCreateNotAllowed_CarriesTheKeyTheLevelAndTheStructuredFields()
    {
        var logger = new CapturingLogger<object>();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        logger.TaskCreateNotAllowed(OrganizationRole.Viewer, userId, orgId);

        var entry = logger.Entries.Single();
        Assert.That(entry.LogKey, Is.EqualTo(TasksLog.Keys.TaskCreateNotAllowed));
        Assert.That(entry.EventId.Id, Is.EqualTo(1));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry["UserId"], Is.EqualTo(userId));
        Assert.That(entry["OrganizationId"], Is.EqualTo(orgId));
        Assert.That(entry["Role"], Is.EqualTo(OrganizationRole.Viewer));
        Assert.That(entry["{OriginalFormat}"], Is.EqualTo(
            "Task not created: the role {Role} of user {UserId} in organization {OrganizationId} does not allow it"));
        Assert.That(entry.Message, Does.StartWith("Task not created:").And.Contain(userId.ToString()));
    }

    [Test]
    public void KeyConstant_IsTheKeyInPascalCaseOfTheMethodName()
    {
        var methods = typeof(TasksLog).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(TasksLog).Namespace && t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<LoggerMessageAttribute>() is not null)
            .ToList();

        Assert.That(methods, Is.Not.Empty);
        foreach (var method in methods)
        {
            var key = method.GetCustomAttribute<LoggerMessageAttribute>()!.EventName!;
            var pascal = string.Concat(key.Split('.', '_').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
            Assert.That(method.Name, Is.EqualTo(pascal), key);
        }
    }

    [Test]
    public void EventIds_AreUniqueWithinEachDefinitionClass()
    {
        foreach (var type in new[] { typeof(TasksLog), typeof(PlatformLog) })
        {
            var ids = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Select(m => m.GetCustomAttribute<LoggerMessageAttribute>()?.EventId)
                .Where(id => id is not null)
                .ToList();
            Assert.That(ids, Is.Unique, type.Name);
        }
    }
}
