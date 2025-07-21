namespace VSTest.Runner.FSharp

open System
open System.Globalization
open Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
open Microsoft.VisualStudio.TestPlatform.ObjectModel;
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

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

 
        
    let discoverTests (sources: TestProjectDll list) : TestCase ResizeArray = 
        // let vstestPath = @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
        // let vstestPath = "C:/Program Files/dotnet/sdk/9.0.300/vstest.console.dll"
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

        let vstest = new VsTestConsoleWrapper(vstestPath, consoleParams)
        let discoveryHandler = TestDiscoveryHandler(true)
        let options = TestPlatformOptions()
        options.CollectMetrics <- true
        options.SkipDefaultAdapters <- false

        let sessionHandler = TestSessionHandler()

        
        vstest.DiscoverTests(sources, null, options, sessionHandler.TestSessionInfo, discoveryHandler)
        discoveryHandler.DiscoveredTests
        

    open System
    open System.IO
    [<EntryPoint>]
    let main argv = 
        let playgroundRoot = __SOURCE_DIRECTORY__
        let sources = [
            Path.Combine(playgroundRoot, "..", "VSTest.XUnit.Tests", "bin", "Debug", "net9.0", "VSTest.XUnit.Tests.dll")
            //Path.Combine(playgroundRoot, "..", "VSTest.NUnit.Tests", "bin", "Debug", "net9.0", "VSTest.NUnit.Tests.dll"),
            //Path.Combine(playgroundRoot, "..", "VSTest.Expecto.Tests", "bin", "Debug", "net8.0", "VSTest.Expecto.Tests.dll"),
            //Path.Combine(playgroundRoot, "..", "MTP.NUnit.Tests", "bin", "Debug", "net9.0", "MTP.NUnit.Tests.dll"),
            //Path.Combine(playgroundRoot, "..", "MTP.xUnit.Tests", "bin", "Debug", "net9.0", "MTP.xUnit.Tests.dll"),
            //Path.Combine(playgroundRoot, "..", "MTP.MSTest.Tests", "bin", "Debug", "net9.0", "MTP.MSTest.Tests.dll"),
            //Path.Combine(playgroundRoot, "..", "MTP.Expecto.Tests", "bin", "Debug", "net8.0", "MTP.Expecto.Tests.dll"),
            //// TUnit not discovered
            //Path.Combine(playgroundRoot, "..", "MTP.TUnit.Tests", "bin", "Debug", "net8.0", "MTP.TUnit.Tests.dll"),
        
            // Path.Combine(sourceDir, "SampleTestProjects/VSTest.XUnit.Tests/bin/Debug/net8.0/VSTest.XUnit.Tests.dll")
            //"X:/source/dotnet/TestPlatform.Playground/VSTest.XUnit.Tests/bin/Debug/net9.0/VSTest.XUnit.Tests.dll"
        ]

        let discovered = discoverTests sources
        
        discovered |> Seq.map _.FullyQualifiedName |> String.concat Environment.NewLine |> printfn "Discovered: \n\n%s"
        0