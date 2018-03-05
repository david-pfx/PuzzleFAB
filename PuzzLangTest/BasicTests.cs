/// Puzzlang is a pattern matching language for abstract games and puzzles. See http://www.polyomino.com/puzzlang.
///
/// Copyright © Polyomino Games 2018. All rights reserved.
/// 
/// This is free software. You are free to use it, modify it and/or 
/// distribute it as set out in the licence at http://www.polyomino.com/licence.
/// You should have received a copy of the licence with the software.
/// 
/// This software is distributed in the hope that it will be useful, but with
/// absolutely no warranty, express or implied. See the licence for details.
/// 
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PuzzLangLib;

namespace PuzzLangTest {
  [TestClass]
  public class BasicTests {
    [ClassInitialize]
    static public void ClassSetup(TestContext context) {
      // disable for now -- test framework weird results
      //var path ="testdata.json";
      //JsonTestData.Setup(new StreamReader(path));
    }
    [TestMethod]
    public void SingleCase() {
      var data = StaticTestData.GetTestCase("Simple");
      var game = new StringReader(data.Script);
      var compiler = Compiler.Compile("unknown", game, Console.Out);
      Assert.IsTrue(compiler.Success, data.Title);
      compiler.Model.Execute("1", data.Inputs);
      Assert.IsTrue(compiler.Success, data.Title);
    }

    [DataRow("bad data row")]
    [DataTestMethod]
    public void BadDataRow(string arg) {
      var game = new StringReader(arg);
      var compiler = Compiler.Compile("unknown", game, Console.Out);
      Assert.IsFalse(compiler.Success);
    }

    [TestMethod]
    [DynamicData (nameof(SomeBadMethod))]
    public void BadMethod(string arg) {
      var game = new StringReader(arg);
      var compiler = Compiler.Compile("unknown", game, Console.Out);
      Assert.IsFalse(compiler.Success);
    }

    // CHECK: spits out quantities of text (>449)
#if GRIEF_NEEDED
    [TestMethod]
    [DynamicData(nameof(TestCasesArray))]
    public void StaticCases(string title, string script, string inputs) {
      var game = new StringReader(script);
      var compiler = Compiler.Compile("unknown", game, Console.Out);
      Assert.IsTrue(compiler.Success, title);
      var engine = Engine.Execute(compiler, "1", inputs);
      Assert.IsTrue(compiler.Success, title);
    }
#endif
    // wrapper as dictated by dynamic data
    public static IEnumerable<string[]> TestCasesArray {
      get {
        foreach (var testcase in StaticTestData.TestCases) { 
          yield return new string[] { testcase.Title, testcase.Script, testcase.Inputs };
        }
      }
    }

    // CHECK: hopeless error display with massive icons
#if GRIEF_NEEDED
    [TestMethod]
    [DynamicData(nameof(JsonTestCases))]
    public void JsonCases(string title, string script, string inputs, string expected, string level, string seed) {
      var game = new StringReader(script);
      var compiler = Compiler.Compile(game, Console.Out);
      Assert.IsTrue(compiler.Success, title);
      var engine = Engine.Execute(compiler, 1, inputs);
      Assert.IsTrue(compiler.Success, title);
    }
#endif

    // sources of test data
    // this is what works. Method does not (!?)
    static IEnumerable<object[]> SomeBadMethod {
      get {
        yield return new object[] { "asdf" };
      }
    }

    static IEnumerable<object[]> JsonTestCases {
      get {
        foreach (var testcase in JsonTestData.StringTestCases)
          yield return testcase;
      }
    }

    static IEnumerable<object[]> DemoTestCases {
      get {
        yield return new object[] { "asdf" };
      }
    }

  }
}
