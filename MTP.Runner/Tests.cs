#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

namespace Playground;

[TestClass]
public class TestClass
{
    [TestMethod]
    [DynamicData(nameof(Data))]
    public void Test3(int a, int b)
    {
    }

    public static IEnumerable<(int A, int B)> Data
    {
        get
        {
            yield return (1, 2);
            yield return (3, 4);
        }
    }
}