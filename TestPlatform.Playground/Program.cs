// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace TestPlatform.Playground;

internal class Program
{
    static void Main()
    {

        var thisAssemblyPath = Assembly.GetEntryAssembly()!.Location;
        var binDir = Path.GetDirectoryName(thisAssemblyPath)!;
        var playgroundRoot = Path.GetFullPath(Path.Combine(binDir, "..", "..", ".."));

        //var console = @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\Extensions\TestPlatform\vstest.console.exe";
        var console = "C:/Program Files/dotnet/sdk/9.0.201/vstest.console.dll";

        var sources = new[] {
            Path.Combine(playgroundRoot, "..", "XUnit.Sample.Tests", "bin", "Debug", "net9.0", "XUnit.Sample.Tests.dll"),
            Path.Combine(playgroundRoot, "..", "NUnit.Sample.Tests", "bin", "Debug", "net9.0", "NUnit.Sample.Tests.dll"),
            Path.Combine(playgroundRoot, "..", "Expecto.Sample.Tests", "bin", "Debug", "net8.0", "Expecto.Sample.Tests.dll"),
        };

        // design mode
        var detailedOutput = true;
        var consoleOptions = new ConsoleParameters
        {
            EnvironmentVariables = EnvironmentVariables.Variables,
            // LogFilePath = Path.Combine(here, "logs", "log.txt"),
            // TraceLevel = TraceLevel.Verbose,
        };
        var options = new TestPlatformOptions
        {
            CollectMetrics = true,
            SkipDefaultAdapters = false
        };
        var r = new VsTestConsoleWrapper(console, consoleOptions);
        var sessionHandler = new TestSessionHandler();
#pragma warning disable CS0618 // Type or member is obsolete
        //// TestSessions
        // r.StartTestSession(sources, sourceSettings, sessionHandler);
#pragma warning restore CS0618 // Type or member is obsolete
        var discoveryHandler = new PlaygroundTestDiscoveryHandler(detailedOutput);
        var sw = Stopwatch.StartNew();
        // Discovery
        r.DiscoverTests(sources, null, options, sessionHandler.TestSessionInfo, discoveryHandler);
        var discoveryDuration = sw.ElapsedMilliseconds;
        Console.WriteLine($"Discovery done in {discoveryDuration} ms");
        sw.Restart();
        // Run with test cases and custom testhost launcher
        //r.RunTestsWithCustomTestHost(discoveryHandler.TestCases, sourceSettings, options, sessionHandler.TestSessionInfo, new TestRunHandler(detailedOutput), new DebuggerTestHostLauncher());
        //// Run with test cases and without custom testhost launcher
        r.RunTests(discoveryHandler.TestCases, null, options, sessionHandler.TestSessionInfo, new TestRunHandler(detailedOutput));
        //// Run with sources and custom testhost launcher and debugging
        //r.RunTestsWithCustomTestHost(sources, sourceSettings, options, sessionHandler.TestSessionInfo, new TestRunHandler(detailedOutput), new DebuggerTestHostLauncher());
        //// Run with sources
        //r.RunTests(sources, sourceSettings, options, sessionHandler.TestSessionInfo, new TestRunHandler(detailedOutput));
        var rd = sw.ElapsedMilliseconds;
        Console.WriteLine($"Discovery: {discoveryDuration} ms, Run: {rd} ms, Total: {discoveryDuration + rd} ms");
        // Console.WriteLine($"Settings:\n{sourceSettings}");
    }

    public class PlaygroundTestDiscoveryHandler : ITestDiscoveryEventsHandler, ITestDiscoveryEventsHandler2
    {
        private int _testCasesCount;
        private readonly bool _detailedOutput;

        public PlaygroundTestDiscoveryHandler(bool detailedOutput)
        {
            _detailedOutput = detailedOutput;
        }

        public List<TestCase> TestCases { get; internal set; } = new List<TestCase>();

        public void HandleDiscoveredTests(IEnumerable<TestCase>? discoveredTestCases)
        {
            if (_detailedOutput)
            {
                Console.WriteLine($"[DISCOVERY.PROGRESS]");
                Console.WriteLine(WriteTests(discoveredTestCases));
            }
            _testCasesCount += discoveredTestCases!.Count();
            if (discoveredTestCases != null) { TestCases.AddRange(discoveredTestCases); }
        }

        public void HandleDiscoveryComplete(long totalTests, IEnumerable<TestCase>? lastChunk, bool isAborted)
        {
            Console.WriteLine($"[DISCOVERY.COMPLETE] aborted? {isAborted}, tests count: {totalTests}");
            if (_detailedOutput)
            {
                Console.WriteLine("Last chunk:");
                Console.WriteLine(WriteTests(lastChunk));
            }
            if (lastChunk != null) { TestCases.AddRange(lastChunk); }
        }

        public void HandleDiscoveryComplete(DiscoveryCompleteEventArgs discoveryCompleteEventArgs, IEnumerable<TestCase>? lastChunk)
        {
            Console.WriteLine($"[DISCOVERY.COMPLETE] aborted? {discoveryCompleteEventArgs.IsAborted}, tests count: {discoveryCompleteEventArgs.TotalCount}, discovered count: {_testCasesCount}");
            if (_detailedOutput)
            {
                Console.WriteLine("Last chunk:");
                Console.WriteLine(WriteTests(lastChunk));
            }
            Console.WriteLine("Fully discovered:");
            Console.WriteLine(WriteSources(discoveryCompleteEventArgs.FullyDiscoveredSources));
            Console.WriteLine("Partially discovered:");
            Console.WriteLine(WriteSources(discoveryCompleteEventArgs.PartiallyDiscoveredSources));
            Console.WriteLine("Skipped discovery:");
            Console.WriteLine(WriteSources(discoveryCompleteEventArgs.SkippedDiscoveredSources));
            Console.WriteLine("Not discovered:");
            Console.WriteLine(WriteSources(discoveryCompleteEventArgs.NotDiscoveredSources));
            if (lastChunk != null) { TestCases.AddRange(lastChunk); }
        }

        public void HandleLogMessage(TestMessageLevel level, string? message)
        {
            Console.WriteLine($"[DISCOVERY.{level.ToString().ToUpper(CultureInfo.InvariantCulture)}] {message}");
        }

        public void HandleRawMessage(string rawMessage)
        {
            Console.WriteLine($"[DISCOVERY.MESSAGE] {rawMessage}");
        }

        private static string WriteTests(IEnumerable<TestCase>? testCases)
            => testCases?.Any() == true
                ? "\t" + string.Join("\n\t", testCases!.Select(r => r.Source + " " + r.DisplayName))
                : "\t<empty>";

        private static string WriteSources(IEnumerable<string>? sources)
            => sources?.Any() == true
                ? "\t" + string.Join("\n\t", sources)
                : "\t<empty>";
    }

    public class TestRunHandler : ITestRunEventsHandler
    {
        private readonly bool _detailedOutput;

        public TestRunHandler(bool detailedOutput)
        {
            _detailedOutput = detailedOutput;
        }

        public void HandleLogMessage(TestMessageLevel level, string? message)
        {
            Console.WriteLine($"[{level.ToString().ToUpper(CultureInfo.InvariantCulture)}]: {message}");
        }

        public void HandleRawMessage(string rawMessage)
        {
            if (_detailedOutput)
            {
                Console.WriteLine($"[RUN.MESSAGE]: {rawMessage}");
            }
        }

        public void HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs? lastChunkArgs, ICollection<AttachmentSet>? runContextAttachments, ICollection<string>? executorUris)
        {
            Console.WriteLine($"[RUN.COMPLETE]: err: {testRunCompleteArgs.Error}, lastChunk:");
            var stats = testRunCompleteArgs.TestRunStatistics?.Stats;
            if (stats != null)
            {
                var outcomeDisplay = String.Join("; ", stats.Select(outcome => $"{outcome.Key}: {outcome.Value}"));
                Console.WriteLine(outcomeDisplay);    
            }
            if (_detailedOutput)
            {
                Console.WriteLine(WriteTests(lastChunkArgs?.NewTestResults));
            }
        }

        public void HandleTestRunStatsChange(TestRunChangedEventArgs? testRunChangedArgs)
        {
            if (_detailedOutput)
            {
                Console.WriteLine($"[RUN.PROGRESS]");
                Console.WriteLine(WriteTests(testRunChangedArgs?.NewTestResults));
            }
        }

        public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo)
        {
            throw new NotImplementedException();
        }

        private static string WriteTests(IEnumerable<TestResult>? testResults)
            => WriteTests(testResults?.Select(t => t.TestCase));

        private static string WriteTests(IEnumerable<TestCase>? testCases)
            => testCases?.Any() == true
                ? "\t" + string.Join("\n\t", testCases.Select(r => r.DisplayName))
                : "\t<empty>";
    }

    internal class DebuggerTestHostLauncher : ITestHostLauncher2
    {
        public bool IsDebug => true;

        public bool AttachDebuggerToProcess(int pid)
        {
            return true;
        }

        public bool AttachDebuggerToProcess(int pid, CancellationToken cancellationToken)
        {
            return true;
        }

        public int LaunchTestHost(TestProcessStartInfo defaultTestHostStartInfo)
        {
            return 1;
        }

        public int LaunchTestHost(TestProcessStartInfo defaultTestHostStartInfo, CancellationToken cancellationToken)
        {
            return 1;
        }
    }
}

internal class TestSessionHandler : ITestSessionEventsHandler
{
    public TestSessionHandler() { }
    public TestSessionInfo? TestSessionInfo { get; private set; }

    public void HandleLogMessage(TestMessageLevel level, string? message)
    {

    }

    public void HandleRawMessage(string rawMessage)
    {

    }

    public void HandleStartTestSessionComplete(StartTestSessionCompleteEventArgs? eventArgs)
    {
        TestSessionInfo = eventArgs?.TestSessionInfo;
    }

    public void HandleStopTestSessionComplete(StopTestSessionCompleteEventArgs? eventArgs)
    {

    }
}
