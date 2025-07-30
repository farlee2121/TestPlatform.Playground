module NUnit.Sample.Tests

open NUnit.Framework

[<SetUp>]
let Setup () =
    ()

[<Test>]
let Test1 () =
    Assert.Pass()

[<Test>]
let PassWithMessage () =
    Assert.Pass("Success output message")
