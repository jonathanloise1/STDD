using MyApp.Infrastructure.Logging;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class ExceptionSummaryTests
{
    [Test]
    public void ExceptionType_IsTheInnermostException_AndTopFrameTheFirstApplicationFrame()
    {
        Exception caught = null!;
        try { Outer(); }
        catch (Exception e) { caught = e; }

        var summary = ExceptionSummary.From(caught);

        Assert.That(summary.ExceptionType, Is.EqualTo("System.ArgumentException"));
        Assert.That(summary.TopFrame, Is.EqualTo("MyApp.UnitTests.Backend.ExceptionSummaryTests.Outer"));
    }

    [Test]
    public void TopFrame_ReducesAsyncStateMachinesAndLambdasToTheSourceMethod()
    {
        Exception? asyncException = null;
        try { ThrowAsync().GetAwaiter().GetResult(); }
        catch (Exception e) { asyncException = e; }
        Assert.That(ExceptionSummary.From(asyncException!).TopFrame, Is.EqualTo("MyApp.UnitTests.Backend.ExceptionSummaryTests.ThrowAsync"));

        Exception? lambdaException = null;
        try { new Action(() => throw new InvalidOperationException()).Invoke(); }
        catch (Exception e) { lambdaException = e; }
        Assert.That(ExceptionSummary.From(lambdaException!).TopFrame,
            Is.EqualTo("MyApp.UnitTests.Backend.ExceptionSummaryTests.TopFrame_ReducesAsyncStateMachinesAndLambdasToTheSourceMethod"));
    }

    [Test]
    public void TopFrame_IsUnknownWhenNoApplicationFrameExists()
    {
        Assert.That(ExceptionSummary.From(new InvalidOperationException("never thrown")).TopFrame, Is.EqualTo("unknown"));
    }

    private static void Outer()
    {
        try { Inner(); }
        catch (Exception e) { throw new InvalidOperationException("wrapped", e); }
    }

    private static void Inner() => throw new ArgumentException("inner");

    private static async Task ThrowAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("async");
    }
}
