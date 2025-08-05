namespace VSTest.Runner.FSharp

open System
open System.Globalization
open Microsoft.TestPlatform.VsTestConsole.TranslationLayer
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities
open Microsoft.VisualStudio.TestPlatform.Common.Filtering
open System.Diagnostics

module Program =
    type TestProjectDll = string 

    module private Display =
        let WriteTests(testCases: TestCase seq) : string =
            match testCases |> defaultIfNull [] |> List.ofSeq with
            | [] -> "\t<empty>"
            | testCases -> 
                "\t" 
                + (testCases |> List.map (fun r -> r.Source + " " + r.DisplayName) |> String.concat "\n\t")

        let WriteSources(sources: string seq) : string =
            match sources |> defaultIfNull [] |> List.ofSeq with
            | [] -> "\t<empty>"
            | sources -> 
                "\t" + (sources  |> String.concat "\n\t")

    type private TestDiscoveryHandler (detailedOutput: bool) =

        member val private _testCasesCount : int = 0 with get,set
        member val DiscoveredTests : TestCase ResizeArray = ResizeArray() with get, set

        //interface ITestDiscoveryEventsHandler with 
        //    member this.HandleDiscoveredTests (discoveredTestCases: System.Collections.Generic.IEnumerable<TestCase>): unit = 
        //        if (not << isNull) discoveredTestCases then 
        //            this.DiscoveredTests.AddRange(discoveredTestCases) 
               
        //    member this.HandleDiscoveryComplete (_totalTests: int64, lastChunk: System.Collections.Generic.IEnumerable<TestCase>, _isAborted: bool): unit = 
        //        if (not << isNull) lastChunk then 
        //            this.DiscoveredTests.AddRange(lastChunk)
            
        //    member this.HandleLogMessage (_level: TestMessageLevel, _message: string): unit = 
        //        ()

        //    member this.HandleRawMessage (_rawMessage: string): unit = 
        //        ()
        

        interface ITestDiscoveryEventsHandler2 with
            member this.HandleDiscoveredTests (discoveredTestCases: System.Collections.Generic.IEnumerable<TestCase>): unit = 
                if (detailedOutput) then
                    Console.WriteLine($"[DISCOVERY.PROGRESS]")
                    Console.WriteLine(Display.WriteTests(discoveredTestCases))
            
                if (not << isNull) discoveredTestCases then 
                    this.DiscoveredTests.AddRange(discoveredTestCases) 

            member this.HandleDiscoveryComplete (discoveryCompleteEventArgs: DiscoveryCompleteEventArgs, lastChunk: System.Collections.Generic.IEnumerable<TestCase>): unit = 
                Console.WriteLine($"[DISCOVERY.COMPLETE] aborted? {discoveryCompleteEventArgs.IsAborted}, tests count: {discoveryCompleteEventArgs.TotalCount}, discovered count: {this._testCasesCount}");
                if (detailedOutput) then
                    Console.WriteLine($"Last Chunk:")
                    Console.WriteLine(Display.WriteTests(lastChunk))

                Console.WriteLine("Fully discovered:")
                Console.WriteLine(Display.WriteSources(discoveryCompleteEventArgs.FullyDiscoveredSources))
                Console.WriteLine("Partially discovered:");
                Console.WriteLine(Display.WriteSources(discoveryCompleteEventArgs.PartiallyDiscoveredSources));
                Console.WriteLine("Skipped discovery:");
                Console.WriteLine(Display.WriteSources(discoveryCompleteEventArgs.SkippedDiscoveredSources));
                Console.WriteLine("Not discovered:");
                Console.WriteLine(Display.WriteSources(discoveryCompleteEventArgs.NotDiscoveredSources));

                if (not << isNull) lastChunk then 
                    this.DiscoveredTests.AddRange(lastChunk)

            member this.HandleLogMessage (level: TestMessageLevel, message: string): unit = 
                Console.WriteLine($"[DISCOVERY.{level.ToString().ToUpper(CultureInfo.InvariantCulture)}] {message}");

            member this.HandleRawMessage (rawMessage: string): unit = 
                Console.WriteLine($"[DISCOVERY.MESSAGE] {rawMessage}");

    type TestSessionHandler()=
        member val TestSessionInfo: TestSessionInfo | null  = null with get, set
        interface ITestSessionEventsHandler with
            member this.HandleLogMessage (level: TestMessageLevel, message: string): unit = 
                ()

            member this.HandleRawMessage (rawMessage: string): unit = 
                ()

            member this.HandleStartTestSessionComplete (eventArgs: StartTestSessionCompleteEventArgs): unit = 
                this.TestSessionInfo <- if eventArgs |> isNull then null else eventArgs.TestSessionInfo

            member this.HandleStopTestSessionComplete (eventArgs: StopTestSessionCompleteEventArgs): unit = 
                ()

    type TestRunHandler(_detailedOutput: bool) = 
        member val TestResults : TestResult ResizeArray = ResizeArray() with get,set

        interface ITestRunEventsHandler with
            member _.HandleLogMessage (level: TestMessageLevel, message: string): unit = 
                Console.WriteLine($"[{level.ToString().ToUpper(CultureInfo.InvariantCulture)}]: {message}");
                

            member _.HandleRawMessage (rawMessage: string): unit = 
                if (_detailedOutput) then
                    Console.WriteLine($"[RUN.MESSAGE]: {rawMessage}");
            

            member this.HandleTestRunComplete (testRunCompleteArgs: TestRunCompleteEventArgs, lastChunkArgs: TestRunChangedEventArgs, _runContextAttachments: System.Collections.Generic.ICollection<AttachmentSet>, _executorUris: System.Collections.Generic.ICollection<string>): unit = 
                Console.WriteLine($"[RUN.COMPLETE]: err: {testRunCompleteArgs.Error}, lastChunk:")

                if ((not << isNull) testRunCompleteArgs.TestRunStatistics && (not << isNull) testRunCompleteArgs.TestRunStatistics.Stats) then
                    let outcomeDisplay = testRunCompleteArgs.TestRunStatistics.Stats |> Seq.map (fun outcome -> $"{outcome.Key}: {outcome.Value}") |> String.concat "; " 
                    Console.WriteLine(outcomeDisplay);    
                
                if((not << isNull) lastChunkArgs && (not << isNull) lastChunkArgs.NewTestResults) then
                    this.TestResults.AddRange(lastChunkArgs.NewTestResults)
                    if (_detailedOutput) then 
                        Console.WriteLine(Display.WriteTests(lastChunkArgs.NewTestResults |> Seq.map _.TestCase));
                

            member this.HandleTestRunStatsChange (testRunChangedArgs: TestRunChangedEventArgs): unit = 
                if((not << isNull) testRunChangedArgs && (not << isNull) testRunChangedArgs.NewTestResults) then
                    this.TestResults.AddRange(testRunChangedArgs.NewTestResults)

                    if (_detailedOutput) then 
                        Console.WriteLine($"[RUN.PROGRESS]");
                        Console.WriteLine(Display.WriteTests(testRunChangedArgs.NewTestResults |> Seq.map _.TestCase));

            member _.LaunchProcessWithDebuggerAttached (_testProcessStartInfo: TestProcessStartInfo): int = 
                raise (System.NotImplementedException())

    open System
    open System.IO
    [<EntryPoint>]
    let main argv = 
        let playgroundRoot = __SOURCE_DIRECTORY__
        let sources = [
            Path.Combine(playgroundRoot, "..", "VSTest.XUnit.Tests", "bin", "Debug", "net9.0", "VSTest.XUnit.Tests.dll")
            //Path.Combine(playgroundRoot, "..", "VSTest.NUnit.Tests", "bin", "Debug", "net9.0", "VSTest.NUnit.Tests.dll")
            //Path.Combine(playgroundRoot, "..", "VSTest.Expecto.Tests", "bin", "Debug", "net8.0", "VSTest.Expecto.Tests.dll")
            //Path.Combine(playgroundRoot, "..", "MTP.NUnit.Tests", "bin", "Debug", "net9.0", "MTP.NUnit.Tests.dll")
            //Path.Combine(playgroundRoot, "..", "MTP.xUnit.Tests", "bin", "Debug", "net9.0", "MTP.xUnit.Tests.dll")
            //Path.Combine(playgroundRoot, "..", "MTP.MSTest.Tests", "bin", "Debug", "net9.0", "MTP.MSTest.Tests.dll")
            //Path.Combine(playgroundRoot, "..", "MTP.Expecto.Tests", "bin", "Debug", "net8.0", "MTP.Expecto.Tests.dll")
            //// TUnit not discovered
            //Path.Combine(playgroundRoot, "..", "MTP.TUnit.Tests", "bin", "Debug", "net8.0", "MTP.TUnit.Tests.dll")
        ]
        let vstestPath = "C:/Program Files/dotnet/sdk/9.0.104/vstest.console.dll"
        let consoleParams = ConsoleParameters()
        consoleParams.EnvironmentVariables <- [
              "VSTEST_CONNECTION_TIMEOUT", "999"
              "VSTEST_DEBUG_NOBP", "1"
              "VSTEST_RUNNER_DEBUG_ATTACHVS", "0"
              "VSTEST_HOST_DEBUG_ATTACHVS", "0"
              "VSTEST_DATACOLLECTOR_DEBUG_ATTACHVS", "0"
        ] |> dict |> System.Collections.Generic.Dictionary<string, string>
        consoleParams.LogFilePath <- $"""C:/temp/ionide-test/vstest-{System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm")}.txt"""
        // consoleParams.InheritEnvironmentVariables <- false

        let detailedOutput = true
        let vstest = new VsTestConsoleWrapper(vstestPath, consoleParams)
        let discoveryHandler = TestDiscoveryHandler(detailedOutput)
        let options = TestPlatformOptions()
        options.CollectMetrics <- true
        options.SkipDefaultAdapters <- false
        let filterOptions = FilterOptions()
        //filterOptions.FilterRegEx <- @"^[^\s\(]+"
        options.FilterOptions <- filterOptions
        //let filterExpression = "XUnitTests"
        let filterExpression = "(FullyQualifiedName~XUnitTests)"
        //let filterExpression = "Added"
        //let filterExpression = "DisplayName~XUnitTests"
        options.TestCaseFilter <- filterExpression

        let sessionHandler = TestSessionHandler()

        
        let sw = Stopwatch.StartNew()
        vstest.DiscoverTests(sources, null, options, sessionHandler.TestSessionInfo, discoveryHandler)
        let discovered = discoveryHandler.DiscoveredTests
        
        let discoveryDuration = sw.ElapsedMilliseconds
        Console.WriteLine($"Discovery done in {discoveryDuration} ms")
        discovered |> Seq.map _.FullyQualifiedName |> String.concat Environment.NewLine |> printfn "Discovered: \n\n%s"

        sw.Restart()

        let runHandler = TestRunHandler(detailedOutput)
        vstest.RunTests(sources, null, options, sessionHandler.TestSessionInfo, runHandler)
        let testResults = runHandler.TestResults

        let parsedFilter = TestCaseFilterExpression(FilterExpressionWrapper(filterExpression))
        let doesMatchFilter (t: TestCase) : bool=
            parsedFilter.MatchTestCase(t, fun _ -> t.FullyQualifiedName)
        let filtered = testResults |> Seq.filter (_.TestCase >> doesMatchFilter)
        Console.WriteLine($"Match filter: {filtered |> Seq.length} ")
        Console.WriteLine($"COMPLETED: Ran {testResults.Count} tests")
        0