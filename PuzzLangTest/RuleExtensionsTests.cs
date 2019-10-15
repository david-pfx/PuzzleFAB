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
  /// <summary>
  /// Test language extensions
  /// </summary>
  [TestClass]
  public class RuleExtensionsTests : TestCommon {

    [TestMethod]
    public void RefObjectPattern() {
      var template =
        "@(rul):right [ up       p | xx:o |    |        ] -> [ p | | | ];" +
               "right [ right    p | xx:o | xx |        ] -> [ p | | | ];" +
               "right [ down     p | xx:o | xx | xx     ] -> [ p | | | ];" +
               "right [ left     p | xx:o | xx | yy:o   ] -> [ p | | | ];" +
               "right [ action   p | xx:o | xx | yy:obk ] -> [ p | | | ];" +
               "right [ reaction p | xx:o | xx | yy:obk | yy ] -> [ p | | | | ];" +
        "@(lev):..prrbb..;" +
        "@(win):";
      var tests = new string[,] {
        { "",                         "..prrbb.." },
        { "up",                       "..p.rbb.." },
        { "right",                    "..p..bb.." },
        { "down",                     "..prrbb.." },
        { "left",                     "..p...b.." },
        { "action",                   "..p...b.." },
        { "reaction",                 "..p......" },
      };

      DoTests("PRBG", "", template, tests, DoTestInputDecodeLevel);
    }

    [TestMethod]
    public void RefObjectAction() {
      var template =
        "(rul):right [ > p | xx:o | | ] -> [ > p | > xx | | ^ xx ];" +
        "@(lev):..pr... ;;" +
               "....... ;..pr... ;;" +
        "@(win):";
      var tests = new string[,] {
        { "",                         "..pr..." },
        { "right",                    "...prr." },
        { "level 1",                  "....... ;..pr..." },
        { "level 1,right",            ".....r. ;...pr.." },
      };

      DoTests("PRBG", "", template, tests, DoTestInputDecodeLevel);
    }

    [TestMethod]
    public void CommandSetObject() {
      var template =
         "(rul):right [ > p | r ] -> [ p | r ] s RRR;" +
               "left  [ > p | r ] -> [ p | r ] s LLL;" +
        "@(lev):.rpr.." +
        "@(win):";
      var tests = new string[,] {
        { "",                         "s=SSS" },
        { "right",                    "s=RRR" },
        { "left",                     "s=LLL" },
        { "right,restart",            "s=SSS" },
        { "right,undo",               "s=SSS" },
        { "right,restart,left",       "s=LLL" },
        { "right,restart,left,undo",  "s=SSS" },
      };

      DoTests("PRBGX", "", template, tests, DoTestInputObjectText);
    }

    [TestMethod]
    public void CommandLevel() {
      var template =
        "@(rul):right [ up    p ] -> [ p ] level 0;" +
               "right [ right p ] -> [ p ] level 1;" +
               "right [ down  p ] -> [ p ] level 2;" +
               "right [ left  p ] -> [ p ] level 3;" +
        "@(lev):.rpr.;;.kkk.;;.yyy.;;" +
        "@(win):";
      var tests = new string[,] {
        { "",                 ".rpr." },
        { "up",               ".rpr." },
        { "right",            ".kkk." },
        { "down",             ".yyy." },
        { "left",             ".rpr." },
      };

      DoTests("PRBGX", "", template, tests, DoTestInputDecodeLevel);
    }

    [TestMethod]
    public void CommandPriority() {
      var template =
        "@(rul):right [ right p ] -> [ right p ];" +
               "right [ down  p ] -> [ right p ] cancel;" +
               "right [ left  p ] -> [ right p ] restart;" +
               "right [ up    p ] -> [ right p ] undo;" +
               "right [ action p ] -> [ right p ] cancel;" +   // cancel takes priority
               "right [ action p ] -> [ right p ] restart;" +
               "right [ reaction p ] -> [ right p ] restart;" +   // restart takes priority
               "right [ reaction p ] -> [ right p ] cancel;" +
        "@(lev):..p..;" +
        "@(win):";
      var tests = new string[,] {
        { "",                 "..p.." },
        { "right",            "...p." },
        { "right,right",      "....p" },
        { "down",             "..p.." },
        { "right,down",       "...p." },
        { "right,right,down", "....p" },
        { "left",             "..p.." },
        { "right,left",       "..p.." },
        { "right,right,left", "..p.." },
        { "up",               "..p.." },
        { "right,up",         "..p.." },
        { "right,right,up",   "...p." },
        
        { "right,action",        "...p." },
        { "right,reaction",      "..p.." },
      };

      DoTests("PRBGX", "", template, tests, DoTestInputDecodeLevel);
    }

    [TestMethod]
    public void FunctionPattern() {
      var template =
        @"(scr):function f1();return 77;end;" +
               "function f2(arg);return arg;end;" +
        "@(rul):right [ up    p | r &{ f1() } ] -> [ p | r ] s UUU;" +
               "right [ right p | r &{ f2() } ] -> [ p | r ] s RRR;" +
               "right [ down  p | r &{ f2(1) } ] -> [ p | r ] s DDD;" +
               "right [ left  p | r &{ f1(x) } ] -> [ p | r ] s LLL;" +
        "@(lev):.rpr.." +
        "@(win):";
      var tests = new string[,] {
        { "",           "s=SSS" },
        { "up",         "s=UUU" },
        { "right",      "s=SSS" },
        { "down",       "s=DDD" },
        { "left",       "s=LLL" },
        { "action",     "s=SSS" },
        { "reaction",   "s=SSS" },
      };

      DoTests("PRBGX", "", template, tests, DoTestInputObjectText);
    }

    [TestMethod]
    public void FunctionVariables() {
      var template =
        @"(scr):state.xx = 333;" +
               "function f1();return state.xx;end;" +
               "function f2(nn);state.xx = state.xx + nn;end;" +
        "@(rul):right [ up    p ] -> [ p ]                        s &{ state.xx };" +
               "right [ right p ] -> [ p ]                        s &{ f1() };" +
               "right [ down  p ] -> [ p &{ f2(1) } ]             s &{ f1() };" +
               "right [ left  p ] -> [ p &{ f2(1) } &{ f2(1) } ]  s &{ f1() };" +
        "@(lev):.p." +
        "@(win):";
      var tests = new string[,] {
        { "",                "s=SSS" },
        //{ "up",              "s=333" },
        { "right",           "s=333" },
        { "down",            "s=334" },
        { "left",            "s=335" },
        { "down,undo",       "s=SSS" },
        //{ "down,down,undo",  "s=333" },   // TBD: fails
      };

      DoTests("PRBGX", "", template, tests, DoTestInputObjectText);
    }

    [TestMethod]
    public void FunctionAction() {
      var template =
        @"(scr):function f1();return 7;end;" +
               "function f2(arg);return arg;end;" +
        "@(rul):right [ up    p | r ] -> [ p | r ];" +
               "right [ right p | r ] -> [ p | &{ f1() } ];" +
               "right [ down  p | r ] -> [ p | &{ f2() } ];" +
               "right [ left  p | r ] -> [ p | &{ f2(b) } ];" +
        "@(lev):.rpr.." +
        "@(win):";
      var tests = new string[,] {
        { "",           ".rpr.." },
        { "up",         ".rpr.." },
        { "right",      ".rpk.." },
        { "down",       ".rp..." },
        { "left",       ".rpb.." },
        { "action",     ".rpr.." },
        { "reaction",   ".rpr.." },
      };

      DoTests("PRBGX", "", template, tests, DoTestInputDecodeLevel);
    }

    [TestMethod]
    public void FunctionCommand() {
      var template =
        @"(scr):function f1();return 777;end;" +
               "function f2(arg);return arg;end;" +
        "@(rul):right [ up    p | r ] -> s &{ f1() };" +
               "right [ right p | r ] -> s &{ f2() };" +
               "right [ down  p | r ] -> s &{ f2(888) };" +
               "right [ left  p | r ] -> s &{ f2(k) };" +
               "right [ action   p | r ] -> &{ f2() } s AAA;" +
               "right [ reaction p | r ] -> &{ f2(1) } s BBB;" +
        "@(lev):.rpr.." +
        "@(win):";
      var tests = new string[,] {
        { "",           "s=SSS" },
        { "up",         "s=777" },
        { "right",      "s=" },
        { "down",       "s=888" },
        { "left",       "s=" },
        // TODO: insert calls to function
        //{ "action",     "s=SSS" },
        //{ "reaction",   "s=BBB" },
      };

      DoTests("PRBGX", "", template, tests, DoTestInputObjectText);
    }

    [TestMethod]
    public void FunctionWinCondition() {
      var template =
        @"(scr):function f1();return 777;end;" +
               "function f2(arg);return arg;end;" +
        "@(rul):right [ up    p    | r ] -> [ p | r ];" +
               "right [ right p    | r ] -> [ p | b ];" +
               "right [ down  p    | r ] -> [ p | g ];" +
               "right [ left  p    | r ] -> [ p | y ];" +
               "right [ action   p | r ] -> [ p | k ];" +
               "right [ reaction p | r ] -> [ p | r ];" +
        "@(win):some r;" +
               "some b &{ f1() };" +
               "some g &{ f2() };" +
               "some y &{ f2(1) };" +
               "some b &{ f1(xx) };" +
               "some k &{ f1(k) };" +
        "@(lev):..pr.. ;";
      var tests = new string[,] {
        { "",           "over;;" },
        { "up",         "over;;" },
        { "right",      "over;;" },
        { "down",       "ok;;" },
        { "left",       "over;;" },
        { "action",     "over;;" },
        { "reaction",   "ok;;" },
      };

      DoTests("PRBGX", "", template, tests, DoTestLocationValue);
    }
  }
}
