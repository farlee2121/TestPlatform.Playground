# TestPlatform.PlayGround

This code demonstrates how to run both [VSTest](https://github.com/microsoft/vstest) and the newer [Microsoft Testing Platform](https://github.com/microsoft/testfx) in server mode.

The goal is to solve several issues with the Ionide test explorer: 
- [consistent test discovery with other test explorers](https://github.com/ionide/ionide-vscode-fsharp/pull/2000)
- [Support for projects using the new Microsoft Testing Platform](https://github.com/ionide/ionide-vscode-fsharp/issues/2069#issuecomment-2739533739)
- Rely on the standard test adapters for test code location, and removing that responsibility from FS AutoComplete

This project specifically isolates examples from each test platform to test our ability to use the platform for test discovery and execution in Ionide.
- [Original VSTest Playground](https://github.com/microsoft/vstest/tree/main/playground/TestPlatform.Playground)
  - This became [VSTest.Runner](./VSTest.Runner) in this project
- [Original Microsoft Testing Platform Playground](https://github.com/microsoft/testfx/tree/main/samples/Playground)
  - This became [MTP.Runner](./MTP.Runner) in this project 

## Conclusions

VSTest still seems like the right move for now. It can run any older versions of test projects and any projects with VSTestBridge for dual VSTest and MTP support.
It also has an official [C# wrapper for the JSON protocol](https://www.nuget.org/packages/Microsoft.TestPlatform.TranslationLayer).  Granted, it can't run MTP-only projects like TUnit.

MTP is does not have an official wrapper for its JSON protocol and does not support running VSTest-based projects.
It also doesn't have great documentation yet and was difficult to figure out server mode.