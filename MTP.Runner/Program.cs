using System.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;
using MSTest.Acceptance.IntegrationTests.Messages.V100;



namespace Playground;

public class Program
{
    class ExecutionStates
    {
        public const string Passed = "passed";
        public const string Discovered = "discovered";
        public const string Failed = "failed";
        public const string Skipped = "skipped";
        public const string TimedOut = "timed-out";
        public const string Error = "error";
        public const string Cancelled = "cancelled";
        public const string InProgress = "in-progress";
    }

    public static async Task<int> Main(string[] args)
    {
        // Opt-out telemetry
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

        Environment.SetEnvironmentVariable("TESTSERVERMODE", "0");
        var thisAssemblyPath = Assembly.GetEntryAssembly()!.Location;
        var binDir = Path.GetDirectoryName(thisAssemblyPath)!;
        var playgroundRoot = Path.GetFullPath(Path.Combine(binDir, "..", "..", ".."));

        //var testExecutables = new[] {
        //    Path.Combine(playgroundRoot, "..", "VSTest.XUnit.Tests", "bin", "Debug", "net9.0", "VSTest.XUnit.Tests.dll"),
        //    Path.Combine(playgroundRoot, "..", "VSTest.NUnit.Tests", "bin", "Debug", "net9.0", "VSTest.NUnit.Tests.dll"),
        //    Path.Combine(playgroundRoot, "..", "VSTest.Expecto.Tests", "bin", "Debug", "net8.0", "VSTest.Expecto.Tests.dll"),
        //    Path.Combine(playgroundRoot, "..", "MTP.NUnit.Tests", "bin", "Debug", "net9.0", "MTP.NUnit.Tests.dll"),
        //    Path.Combine(playgroundRoot, "..", "MTP.xUnit.Tests", "bin", "Debug", "net9.0", "MTP.xUnit.Tests.dll"),
        //    Path.Combine(playgroundRoot, "..", "MTP.Expecto.Tests", "bin", "Debug", "net8.0", "MTP.Expecto.Tests.dll"),
        //    Path.Combine(playgroundRoot, "..", "MTP.TUnit.Tests", "bin", "Debug", "net8.0", "MTP.TUnit.Tests.dll"),
        //};
        var testExecutable = Path.GetFullPath(Path.Combine(playgroundRoot, "..", "MTP.xUnit.Tests", "bin", "Debug", "net9.0", "MTP.xUnit.Tests.exe"));
        using TestingPlatformClient client = await TestingPlatformClientFactory.StartAsServerAndConnectToTheClientAsync(testExecutable);

        await client.InitializeAsync();
        List<TestNodeUpdate> testNodeUpdates = new();
        ResponseListener discoveryResponse = await client.DiscoverTestsAsync(Guid.NewGuid(), node =>
        {
            testNodeUpdates.AddRange(node);
            return Task.CompletedTask;
        });
        await discoveryResponse.WaitCompletionAsync();

        Console.WriteLine($"Discovery finished: {testNodeUpdates.Count} tests discovered");
        Console.WriteLine(String.Join(Environment.NewLine, testNodeUpdates.Select(n => n.Node.DisplayName)));

        List <TestNodeUpdate> runResults = new();
        ResponseListener runRequest = await client.RunTestsAsync(Guid.NewGuid(), testNodeUpdates.Select(x => x.Node).ToArray(), node =>
        {
            runResults.AddRange(node);
            return Task.CompletedTask;
        });
        await runRequest.WaitCompletionAsync();


        var passedCount = runResults.Where(tn => tn.Node.ExecutionState == ExecutionStates.Passed).Count();
        var failedCount = runResults.Where(tn => tn.Node.ExecutionState == ExecutionStates.Failed).Count();
        var skippedCount = runResults.Where(tn => tn.Node.ExecutionState == ExecutionStates.Skipped).Count();

        Console.WriteLine($"Passed: {passedCount}; Skipped: {skippedCount}; Failed: {failedCount};");
        await client.ExitAsync();

        return 0;

    }
}