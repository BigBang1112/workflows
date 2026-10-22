using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fixture.Tests;

[TestClass]
public class CalculatorTests
{
    [TestMethod]
    public void Add_ReturnsSum()
    {
        Assert.AreEqual(5, Calculator.Add(2, 3));
    }
}
