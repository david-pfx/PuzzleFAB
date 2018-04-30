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
  public class RuleTranslateTests {

    [TestMethod]
    public void TranslateSingleton() {
      var template = "(rul):{0};";
      var tests = new string[,] {
        {
          "[ p ] ->[ ]",
          "[ p ] ->[ ]"
        },
        { // BUG: s/b no direction
          "down [ p ] ->[ ]",
          "down [ p ] ->[ ]",
          //"[ p ] ->[ ]",
        },
      };

      DoTests("PRBG", template, tests.GetLength(0), i => tests[i, 1], i => tests[i, 0]);
    }

    [TestMethod]
    public void TranslateRuleDirection() {
      var template = "(rul):{0};";
      var tests = new string[,] {
        {
          "[ p | ] -> [ | ]",
          "up     [ p | ] -> [ | ];" +
          "down   [ p | ] -> [ | ];"+
          "left   [ p | ] -> [ | ];"+
          "right  [ p | ] -> [ | ]",
        },
        {
          "[ no p | ] -> [ | ]",
          "up     [ no p | ] -> [ | ]; " +
          "down   [ no p | ] -> [ | ];"+
          "left   [ no p | ] -> [ | ];"+
          "right  [ no p | ] -> [ | ]",
        },
        {
          "[ > p | ] -> [ | ]",
          "up     [ up    p | ] -> [ | ];" +
          "down   [ down  p | ] -> [ | ];"+
          "left   [ left  p | ] -> [ | ];"+
          "right  [ right p | ] -> [ | ]",
        },
        {
          "[ up p | ] -> [ | ]",
          "up     [ up p | ] -> [ | ];" +
          "down   [ up p | ] -> [ | ];"+
          "left   [ up p | ] -> [ | ];"+
          "right  [ up p | ] -> [ | ]",
        },
      };

      DoTests("PRBG", template, tests.GetLength(0), i => tests[i, 1], i => tests[i, 0]);
    }

    [TestMethod]
    public void TranslateSingleDirection() {
      var template = "(rul):{0};";
      var tests = new string[,] {
        {
          "down[ p | ] -> [ | ]",
          "down[ p | ] -> [ | ]"
        },
        {
          "down[ no p | ] -> [ | ]",
          "down[ no p | ] -> [ | ]"
        },
        {
          "down[ up p | ] -> [ | ]",
          "down[ up p | ] -> [ | ]"
        },
        {
          "down[ stationary p | ] -> [ | ]",
          "down[ stationary p | ] -> [ | ]"
        },
        {
          "down[ > p | ] -> [ | ]",
          "down[ down p | ] -> [ | ]"
        },
        {
          "down[ horizontal p | ] -> [ | ]",
          "down[ left p | ] -> [ | ];"+
          "down[ right p | ] -> [ | ]"
        },
        {
          "down[ vertical p | ] -> [ | ]",
          "down[ up p | ] -> [ | ];"+
          "down[ down p | ] -> [ | ]"
        },
        {
          "down[ parallel p | ] -> [ | ]",
          "down[ down p | ] -> [ | ];"+
          "down[ up p | ]   -> [ | ]"
        },
        {
          "down[ perpendicular p | ] -> [ | ]",
          "down[ left p | ]   -> [ | ];"+
          "down[ right p | ]  -> [ | ]"
        },
        {
          "down[ orthogonal p | ] -> [ | ]",
          "down[ up p | ]     -> [ | ];"+
          "down[ down p | ]   -> [ | ];" +
          "down[ left p | ]   -> [ | ];"+
          "down[ right p | ]  -> [ | ]"
        },
        {
          "down[ moving p | ] -> [ | ]",
          "down[ up p | ]     -> [ | ];"+
          "down[ down p | ]   -> [ | ];" +
          "down[ left p | ]   -> [ | ];"+
          "down[ right p | ]  -> [ | ];"+
          "down[ action p | ] -> [ | ]"
        },
      };
      DoTests("PRBG", template, tests.GetLength(0), i => tests[i, 1], i => tests[i, 0]);
    }

    [TestMethod]
    public void TranslateDoubleDir() {
      var template = "(rul):{0};";
      var tests = new string[,] {
        {
          "down[ up p | right r ] -> [ | ]",
          "down[ up p | right r ] -> [ | ]"
        },
        {
          "down[ stationary p | right r ] -> [ | ]",
          "down[ stationary p | right r ] -> [ | ]"
        },
        {
          "down[ > p | right r ] -> [ | ]",
          "down[ down p | right r ]   -> [ | ]"
        },
        {
          "down[ horizontal p | right r ] -> [ | ]",
          "down[ left p | right r ]   -> [ | ];"+
          "down[ right p | right r ]  -> [ | ]"
        },
        {
          "down[ vertical p | right r ] -> [ | ]",
          "down[ up p | right r ]     -> [ | ];"+
          "down[ down p | right r ]   -> [ | ]"
        },
        {
          "down[ parallel p | right r ] -> [ | ]",
          "down[ down p | right r ]   -> [ | ];"+
          "down[ up p | right r ]     -> [ | ]"
        },
        {
          "down[ perpendicular p | right r ] -> [ | ]",
          "down[ left p | right r ]   -> [ | ];"+
          "down[ right p | right r ]  -> [ | ]"
        },
        {
          "down[ orthogonal p | right r ] -> [ | ]",
          "down[ up p | right r ]     -> [ | ];"+
          "down[ down p | right r ]   -> [ | ];" +
          "down[ left p | right r ]   -> [ | ];"+
          "down[ right p | right r ]  -> [ | ]"
        },
        {
          "down[ moving p | right r ] -> [ | ]",
          "down[ up p | right r ]     -> [ | ];"+
          "down[ down p | right r ]   -> [ | ];" +
          "down[ left p | right r ]   -> [ | ];"+
          "down[ right p | right r ]  -> [ | ];"+
          "down[ action p | right r ] -> [ | ]"
        },
        {
          "down[ horizontal p | horizontal r ] -> [ | ]",
          "down[ left p | left r ]    -> [ | ];"+
          "down[ left p | right r ]   -> [ | ];"+
          "down[ right p | left r ]   -> [ | ];"+
          "down[ right p | right r ]  -> [ | ]",
        },
      };
      DoTests("PRBG", template, tests.GetLength(0), i => tests[i, 1], i => tests[i, 0]);
    }

    [TestMethod]
    public void TranslateMoveActionSingle() {
      var template = "(rul):{0};";
      var tests = new string[,] {
        {
          "down [ left p | ] -> [ p | ]",
          "down [ left p | ] -> [ p | ]",
        },
        {
          "down [ left p | ] -> [ left p | ]",
          "down [ left p | ] -> [ left p | ]",
        },
        {
          "down [ ^ p | ] -> [ ^ p | ]",
          "down [ right p | ] -> [ right p | ]",
        },
        {
          "down [ stationary p | ] -> [ stationary p | ]",
          "down [ stationary p | ] -> [ stationary p | ]",
        },
        {
          "down [ horizontal p | ] -> [ horizontal p | ]",
          "down [ left p | ]  -> [ left p | ];" +
          "down [ right p | ] -> [ right p | ]",
        },
        {
          "down [ perpendicular p | ] -> [ perpendicular p | ]",
          "down [ left p | ]  -> [ left p | ];" +
          "down [ right p | ] -> [ right p | ]",
        },
      };

      DoTests("PRBG", template, tests.GetLength(0), i => tests[i, 1], i => tests[i, 0]);
    }

    [TestMethod]
    public void TranslateMoveActionDouble() {
      var template = "(rul):{0};";
      var tests = new string[,] {
        {
          "down [ left p | down r ] -> [ p | down r ]",
          "down [ left p | down r ] -> [ p | down r ]",
        },
        {
          "down [ left p | down r ] -> [ left p | down r ]",
          "down [ left p | down r ] -> [ left p | down r ]",
        },
        {
          "down [ ^ p | < r ] -> [ ^ p | < r ]",
          "down [ right p | up r ] -> [ right p | up r ]",
        },
        {
          "down [ stationary p | stationary  r ] -> [ stationary p | stationary  r ]",
          "down [ stationary p | stationary  r ] -> [ stationary p | stationary  r ]",
        },
        {
          "down [ horizontal p | horizontal r ] -> [ horizontal p | horizontal r ]",
          "down [ left p  | left r ] -> [ left p  | left r ];" +
          "down [ left p  | right r ] -> [ left p  | right r ];" +
          "down [ right p | left r ] -> [ right p | left r ];" +
          "down [ right p | right r ] -> [ right p | right r ]",
        },
        {
          "down [ perpendicular p | perpendicular r ] -> [ perpendicular p | perpendicular r ]",
          "down [ left p  | left r ]  -> [ left  p | left r ];" +
          "down [ left p  | right r ] -> [ left  p | right r ];" +
          "down [ right p | left r ]  -> [ right p | left r ];" +
          "down [ right p | right r ] -> [ right p | right r ]",
        },
        {
          "down [ horizontal p | perpendicular r ] -> [ horizontal p | perpendicular r ]",
          "down [ left p  | left r ]  -> [ left p  | left r ];" +
          "down [ left p  | right r ] -> [ left p  | right r ];" +
          "down [ right p | left r ]  -> [ right p | left r ];" +
          "down [ right p | right r ] -> [ right p | right r ]",
        },
      };

      DoTests("PRBG", template, tests.GetLength(0), i => tests[i, 1], i => tests[i, 0]);
    }

    [TestMethod]
    public void TranslateOr() {
      var template = "(rul):{0};";
      var tests = new string[,] {
        {
          "down [ ork ] -> [ ]",
          "down [ ork ] -> [ ]",
        },
        {
          "down [ ork | ork ] -> [ | ]",
          "down [ ork | ork ] -> [ | ]",
        },
        {
          "down [ ork | ] -> [ | ork ]",
          "down [ r | ] -> [ | r ];" +
          "down [ k | ] -> [ | k ]",
        },
        {
          "down [ ork | ork ] -> [ ork | ork ]",    // note: PS is x4
          "down [ ork | ork ] -> [ ork | ork ]",
        },
        {
          "down [ > ork | ork ] -> [ ork | ork ]",
          "down [ down ork | ork ] -> [ ork | ork ]",
        },
        {
          "down [ moving ork | ork ] -> [ moving ork | moving ork ]",
          "down [ up     ork | ork ] -> [ up     ork | up     ork ];" +
          "down [ down   ork | ork ] -> [ down   ork | down   ork ];" +
          "down [ left   ork | ork ] -> [ left   ork | left   ork ];" +
          "down [ right  ork | ork ] -> [ right  ork | right  ork ];" +
          "down [ action ork | ork ] -> [ action ork | action ork ]",
        },
        {
          "down [ > p | ork | no ork ] -> [ | p | ork]",
          "down [ down p | r | no ork ] -> [ | p | r ];" +
          "down [ down p | k | no ork ] -> [ | p | k ]",
        },
      };

      DoTests("PRBG", template, tests.GetLength(0), i => tests[i, 1], i => tests[i, 0]);
    }

    [TestMethod]
    public void TranslateX() {
      var template = "(rul):{0};";
      var tests = new string[,] {
      };

      DoTests("PRBG", template, tests.GetLength(0), i => tests[i, 1], i => tests[i, 0]);
    }

    void DoTests(string testbase, string template, int count, Func<int,string> expecteds, Func<int, string> actuals, 
      [CallerMemberName] string method = "") {
      for (int i = 0; i < count; i++) {
        var nexp = expecteds(i).Split(';').Length;
        var tcexp = StaticTestData.GetTestCase(testbase, testbase + ":" + method, template.Fmt(expecteds(i)));
        var exp = GetRuleExpansions(tcexp);
        var tcact = StaticTestData.GetTestCase(testbase, testbase + ":" + method, template.Fmt(actuals(i)));
        var act = GetRuleExpansions(tcact);
        Assert.AreEqual(nexp, exp.Count, actuals(i));
        Assert.AreEqual(nexp, act.Count, actuals(i));
        for (var j = 0; j < nexp; j++)
          Assert.AreEqual(exp[j].After(":"), act[j].After(":"), actuals(i));
      }
    }

    IList<string> GetRuleExpansions(StaticTestCase testcase) {
      var game = new StringReader(testcase.Script);
      var compiler = Compiler.Compile(testcase.Title, game, Console.Out);
      Assert.IsTrue(compiler.Success, compiler.Message);
      return compiler.RuleExpansions;
    }
  }
}