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
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DOLE;
using PuzzLangLib;
using System.Runtime.CompilerServices;

namespace PuzzLangTest {
  [TestClass]
  public class NonRuleTests {
    [ClassInitialize]
    static public void ClassSetup(TestContext context) {
    }

    [TestMethod]
    public void BasicColour() {
      var setup =
        "(pre):background_color green;" +
        "@(win):;";
      var testcase = StaticTestData.GetTestCase("PRBG", "PRBG bare", setup);
      var engine = SetupEngine(testcase);
      // assume default palette
      Assert.AreEqual(0xbe2633, engine.GameDef.GetRGB(8));
      Assert.AreEqual(0x44891a, engine.GameDef.GetRGB(16));
      Assert.AreEqual(0x44891a, engine.GameDef.GetColour(OptionSetting.background_color, 0));
    }

    GameModel SetupEngine(StaticTestCase testcase) {
      var game = new StringReader(testcase.Script);
      var compiler = Compiler.Compile(testcase.Title, game, Console.Out);
      Assert.IsTrue(compiler.Success, testcase.Title);
      compiler.Model.LoadLevel(0);
      return compiler.Model;
    }
  }
}