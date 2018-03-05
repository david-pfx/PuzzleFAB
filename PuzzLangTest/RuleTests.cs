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
  public class RuleTests {
    [ClassInitialize]
    static public void ClassSetup(TestContext context) {
    }
    [TestMethod]
    public void BasicPush() {
      var setup =
         "(rul):[> p|r]->[> p|> r];" +
               "[< p|r]->[< p|< r];" +
               "[^ p|r]->[< p|> r];" +
               "[v p|r]->[  p|> r];" +
        "@(lev):..P.R..;" +
        "@(win):;";
      var tests = new string[,] {
        { "restart",                          "0; right; 1 1  12 1  13 1  1 end" },
        { "restart,up",                       "0; right; 1 1  12 1  13 1  1 end" },
        { "restart,down",                     "0; right; 1 1  12 1  13 1  1 end" },
        { "restart,left",                     "0; right; 1 12 1  1  13 1  1 end" },
        { "restart,right",                    "0; right; 1 1  1  12 13 1  1 end" },
        { "restart,right,right",              "0; right; 1 1  1  1  12 13 1 end" },
        { "restart,right,right,right",        "0; right; 1 1  1  1  1  12 13 end" },
        { "restart,right,right,right,right",  "0; right; 1 1  1  1  1  12 13 end" },
        { "restart,right,left",               "0; right; 1 1  12 13 1  1  1 end" },
        { "restart,right,left,left",          "0; right; 1 12 13 1  1  1  1 end" },
        { "restart,right,up",                 "0; right; 1 1  12 1  1  13  1 end" },
        { "restart,right,up,up",              "0; right; 1 1  12 1  1  13  1 end" },
        { "restart,right,down",               "0; right; 1 1  1  12 1  13  1 end" },
        { "restart,right,down,down",          "0; right; 1 1  1  12 1  13  1 end" },
      };

      var testcase = StaticTestData.GetTestCase("PRBG", "PRBG bare", setup);
      var engine = SetupEngine(testcase);
      for (int i = 0; i < tests.GetLength(0); i++) {
        DoTest(engine, tests[i, 0], tests[i, 1]);
      }
    }

    [TestMethod]
    public void BasicMatch() {
      var template = 
        "(rul):[p|{0}]->[p|];" +
              "[p|a]->[|];" +  // compile but never match
        "@(lev):.....;.....;R.P.R;.....;..R.." +
        "@(win):";
      var variations = "R;O";
      var tests = new string[,] {
        { "restart",                          "10; right; 13 1  12 1  13 end" },
        { "restart,right",                    "10; right; 13 1  1  12 13 end" },
        { "restart,right,right",              "10; right; 13 1  1  1  12 end" },
        { "restart,right,left",               "10; right; 13 1  12 1  1 end" },
        { "restart,left",                     "10; right; 13 12 1  1  13 end" },
        { "restart,left,left",                "10; right; 12 1  1  1  13 end" },
        { "restart,left,right",               "10; right; 1  1  12 1  13 end" },
      };

      DoTests("", variations, template, tests);
    }

    [TestMethod]
    public void MatchStationary() {
      var template =
         "(rul):[> p|r]->[> p|> r];" +
               "[p|stationary r]->[p|]" +
        "@(lev):..R..;.....;R.P.R;.....;..R.." +
        "@(win):";
      var tests = new string[,] {
        { "restart",                          "10; right; 13 1  12 1  13 end" },
        { "restart,right",                    "10; right; 13 1  1  12 13 end" },
        { "restart,right,right",              "10; right; 13 1  1  12 13 end" },
        { "restart,right,right,left",         "10; right; 13 1  12 1  1  end" },
        { "restart,left",                     "10; right; 13 12 1  1  13 end" },
        { "restart,left,left",                "10; right; 13 12 1  1  13 end" },
        { "restart,left,left,right",          "10; right; 1  1  12 1  13 end" },
      };

      DoTests("", "", template, tests);
    }

    // test some weird characters
    [TestMethod]
    public void BasicLegend() {
      var setup = 
         "(rul):[> p|r]->[> p|> r];" +
        "@(leg):. = Background;a = R and K;o = R or G or B or Y;" +
               "[ = r; | = b; ] = g;" +
        "@(lev):.P.[|].;" +
        "@(win):;";
      var tests = new string[,] {
        { "restart",                          "0; right; 1  12 1  13 14 15 1 end" },
      };

      var testcase = StaticTestData.GetTestCase("PRBG", "PRBG bare", setup);
      var engine = SetupEngine(testcase);
      for (int i = 0; i < tests.GetLength(0); i++) {
        DoTest(engine, tests[i, 0], tests[i, 1]);
      }
    }

    [TestMethod]
    public void BasicEllipsis() {
      var template = 
         "(rul):[>p|...|r]->[>p|...|>r]" +
        "@(lev):..RP.R.." +
        "@(win):";
      var tests = new string[,] {
        { "restart",                          "0; right; 1  1  13 12 1  13  1 1 end" },
        { "restart,up",                       "0; right; 1  1  13 12 1  13  1 1 end" },
        { "restart,down",                     "0; right; 1  1  13 12 1  13  1 1 end" },
        { "restart,right",                    "0; right; 1  1  13 1  12 1  13 1 end" },
        { "restart,right,right",              "0; right; 1  1  13 1  1  12  1 13 end" },
        { "restart,right,right,right",        "0; right; 1  1  13 1  1  1  12 13 end" },
        { "restart,right,right,right,right",  "0; right; 1  1  13 1  1  1  12 13 end" },
        { "restart,left",                     "0; right; 1  13 12 1  1  13  1 1 end" },
        { "restart,left,left",                "0; right; 13 12 1  1  1  13  1 1 end" },
        { "restart,left,left,left",           "0; right; 13 12 1  1  1  13  1 1 end" },
      };

      DoTests("", "", template, tests);
    }

    [TestMethod]
    public void BasicWin() {
      var template = 
         "(rul):[>p|r]->[>p|>r]" +
        "@(lev):..P.Y.." +
        "@(win):{0}";
      var variations = 
        "all p on y; some p on y; all y on p; some y on p; "+
        "all p on o; some p on o; all o on p; some o on p";
      var tests = new string[,] {
        { "restart",                          "0; right; 1 1 12 1 16 1 1 end" },
        { "restart,right",                    "0; right; 1 1 1 12 16 1 1 end" },
        { "restart,right,right",              "0; right; over" },
      };

      DoTests("", variations, template, tests);
    }

    [TestMethod]
    public void EllipsisFill() {
      var template =
         "(rul):[>p|...|]->[>p|...|r]" +
        "@(lev):P...;....;...." +
        "@(win):";
      var tests = new string[,] {
        { "restart",                          "0; right; 12 1  1  1  end" },
        { "restart,up",                       "0; right; 12 1  1  1  end" },
        { "restart,down",                     "0; right; 12 1  1  1  end" },
        { "restart,left",                     "0; right; 12 1  1  1  end" },
        { "restart,right",                    "0; right; 12 13 13 13 end" },
        { "restart",                          "0; down;  12 1  1  end" },
        { "restart,up",                       "0; down;  12 1  1  end" },
        { "restart,left",                     "0; down;  12 1  1  end" },
        { "restart,right",                    "0; down;  12 1  1  end" },
        { "restart,down",                     "0; down;  12 13 13 end" },
      };

      DoTests("", "", template, tests);
    }

    [TestMethod]
    public void BasicLoop() {
      var template =
         "(rul):right[>p|o]->[>p|>o];" +
                "startloop;" +
                "right[>r|r]->[>r|>r];" +
                "right[>r|r]->[>r|>r];" +
                "right[>b|b]->[>b|>b];" +
                "right[>r|b]->[>r|>b];" +
                "right[>b|r]->[>b|>r];" +
                "endloop;" +
        "@(lev):..PBRR..";
      var tests = new string[,] {
        { "restart",                          "0; right; 1 1 12 14 13 13 1 1 end" },
        { "restart,right",                    "0; right; 1 1 1 12 14 13 13 1 end" },
        { "restart,right,right",              "0; right; 1 1 1 1 12 14 13 13 end" },
        { "restart,right,right,right",        "0; right; 1 1 1 1 12 14 13 13 end" },
      };

      DoTests("", "", template, tests);
    }

    [TestMethod]
    public void EllipsisDrag() {
      var template =
         "(rul): {0} [ p | ... | | r ]->[ p | ... | r | ];" +
        "@(lev):.P.......R.";
      var variations = ";late";
      var tests = new string[,] {
        { "restart",                          "0; right; 1 12 1  1 1 1 1 1 1 13 1 end" },
        { "restart,action",                   "0; right; 1 12 13 1 1 1 1 1 1 1 1 end" },
      };

      DoTests("", variations, template, tests);
    }

    [TestMethod]
    public void SegmentedRule() {
      var template =
         "(rul):[up p][r]->[up p][];" +
               "[right p][b]->[right p][]; " +
        "@(lev):.....;.p...;..rr.;..bb.";
      var tests = new string[,] {
        { "restart",                          "10; right; 1 1 13 13 1 end" },
        { "restart,right",                    "10; right; 1 1 13 13 1 end" },
        { "restart,left",                     "10; right; 1 1 13 13 1 end" },
        { "restart,up",                       "10; right; 1 1 1  1  1 end" },
        { "restart,up,up",                    "10; right; 1 1 1  1  1 end" },
        { "restart",                          "15; right; 1 1 14 14 1 end" },
        { "restart,right",                    "15; right; 1 1 1  1  1 end" },
        { "restart,right,right",              "15; right; 1 1 1  1  1 end" },
        { "restart,left",                     "15; right; 1 1 14 14 1 end" },
        { "restart,up",                       "15; right; 1 1 14 14 1 end" },
      };

      DoTests("", "", template, tests);
    }

    [TestMethod]
    public void RandomRule() {
      var template =
         "(rul):random [p|r]->[>p|>r];" +
               "random [>p|g]->[>p|randomdir g];" +
               "random [>p|b]->[>p|random o];" +
        "@(lev):..r..;.bpg.;";
      var tests = new string[,] {
        { "restart",                          "0; right; 1 1  13 1  1 end" },
        { "restart",                          "5; right; 1 14 12 15 1 end" },
        { "restart,up",                       "0; right; 1 1" },  // TODO:test something!!!
        { "restart,down",                     "0; right; 1 1" },
        { "restart,left",                     "0; right; 1 1" },
        { "restart,right",                    "0; right; 1 1" },
      };

      DoTests("", "", template, tests);
    }

    [TestMethod]
    public void ParaPerpOrtho() {
      var template =
         "(rul):[parallel p|r]->[>p|>r];" +
               "[perpendicular p|g]->[>p|>g];" +
               "[orthogonal p|b]->[>p|>b];" +
        "@(lev):.......;.b.p.r.;.y...g.;.......";
      var tests = new string[,] {
        { "restart",                          "7; right; 1  14 1  12 1  13 1  end" },
        { "restart",                         "14; right; 1  16 1  1  1  15 1  end" },

        { "restart,right",                    "7; right; 1  14 1  1  12 13 1 end" },
        { "restart,right,right",              "7; right; 1  14 1  1  1  12 13 end" },
        { "restart,right,left",               "7; right; 1  14 1  1  1  12 13 end" },
        { "restart,right,up",                 "7; right; 1  14 1  1  1  13 1 end" },
        { "restart,right,down",               "7; right; 1  14 1  1  1  13 1 end" },

        { "restart,left",                     "7; right; 1  14 12 1  1  13 1  end" },
        { "restart,left,left",                "7; right; 14 12 1  1  1  13 1  end" },
        { "restart,left,left",                "7; right; 14 12 1  1  1  13 1  end" },
        { "restart,left,up",                  "7; right; 14 12 1  1  1  13 1  end" },
        { "restart,left,down",                "7; right; 14 12 1  1  1  13 1  end" },

        { "restart",                          "14; right; 1  16 1  1  1  15 1  end" },
        { "restart,down",                     "14; right; 1  16 1  12 1  15 1  end" },
        { "restart,down,right",               "14; right; 1  16 1  1  12 15 1  end" },
        { "restart,down,right,right",         "14; right; 1  16 1  1  12 15 1  end" },
        { "restart,down,right,left",          "14; right; 1  16 1  12 1  15 1  end" },
        { "restart,down,right,up",            "14; right; 1  16 1  1  1  12 15  end" },
        { "restart,down,right,down",          "14; right; 1  16 1  1  1  12 15  end" },
      };

      DoTests("", "", template, tests);
    }

    [TestMethod]
    public void Commands() {
      var template =
         "(rul):[>p|r]->restart;" +
               "[>p|b]->cancel;" +
               "[>p|g]->checkpoint;" +
               "[>p|y]->win;" +
        "@(lev):.......;.b.p.r.;.y...g.;.......;;" +
                "...;";
      var tests = new string[,] {
        // undo
        { "reset",                                      "7; right; 1  14 1  12 1  13 1  end" },
        { "reset,right",                                "7; right; 1  14 1  1 12  13 1  end" },
        { "reset,right,undo",                           "7; right; 1  14 1  12 1  13 1  end" },
        { "reset,right,undo,undo",                      "7; right; 1  14 1  12 1  13 1  end" },
        { "reset,left",                                 "7; right; 1  14 12 1  1  13 1  end" },
        { "reset,left,right",                           "7; right; 1  14 1  12 1  13 1  end" },
        { "reset,left,right,undo",                      "7; right; 1  14 12 1  1  13 1  end" },
        { "reset,left,right,undo,undo",                 "7; right; 1  14 1  12 1  13 1  end" },
        { "reset,left,right,undo,undo,undo",            "7; right; 1  14 1  12 1  13 1  end" },

        // restart
        { "reset",                          "7; right; 1  14 1  12 1  13 1  end" },
        { "reset,right",                    "7; right; 1  14 1  1 12  13 1  end" },
        { "reset,right,right",              "7; right; 1  14 1  12 1  13 1  end" },
        { "reset,right,right,right",        "7; right; 1  14 1  1 12  13 1  end" },

        // cancel
        { "reset,left",                     "7; right; 1  14 12 1  1  13 1  end" },
        { "reset,left,left",                "7; right; 1  14 12 1  1  13 1  end" },
        { "reset,left,left,left",           "7; right; 1  14 12 1  1  13 1  end" },

        // checkpoint
        { "reset",                               "14; right; 1  16 1  1  1  15 1  end" },
        { "reset,down",                          "14; right; 1  16 1  12 1  15 1  end" },
        { "reset,down,right",                    "14; right; 1  16 1  1  12 15 1  end" },
        { "reset,down,right,right",              "14; right; 1  16 1  1  12 15 1  end" }, // triggers checkpoint
        { "reset,down,right,right,left",         "14; right; 1  16 1  12 1  15 1  end" },
        { "reset,down,right,right,left,restart", "14; right; 1  16 1  1  12 15 1  end" },
        { "reset,down,right,right,left,restart", "14; right; 1  16 1  1  12 15 1  end" },

        // win
        { "reset",                               "14; right; 1  16 1  1  1  15 1  end" },
        { "reset,down",                          "14; right; 1  16 1  12 1  15 1  end" },
        { "reset,down,left",                     "14; right; 1  16 12 1  1  15 1  end" },
        { "reset,down,left,left",                "0; right; 1  1  1 end" },

      };

      DoTests("", "", template, tests);
    }

    [TestMethod]
    public void Singles() {
      var template =
        "(rul):[ up p ]->[ right r ];" +
              //"[ right p ]->[ > r ];" +
              "[ down p ]->[ r ];" +
              //"[ left p ]->[ < r ];" +
              "[ action p ]->[ ];" +
        "@(lev):.P..;" +
        "@(win):";
      var tests = new string[,] {
        { "reset",                                      "0; right; 1  12 1  1 end" },
        { "reset,up",                                   "0; right; 1  1  13 1 end" },
        //{ "reset,right",                                "0; right; 1  13 1  1 end" },
        { "reset,down",                                 "0; right; 1  13 1  1 end" },
        //{ "reset,left",                                 "0; right; 1  1  13 1 end" },
        { "reset,action",                               "0; right; 1  1  1  1 end" },
      };

      DoTests("Singles", "", template, tests);
    }

    [TestMethod]
    public void Rigid() {
      var template =
        "(rul):rigid [ > p | r] -> [ > p | > r ];" +
               "+ rigid[moving r | r]-> [moving r | moving r];" +
        "@(lev):..r.g.;" +
               "P.r...;" +
               ".rr...;" +
               "......;" +
        "@(win):";
      var tests = new string[,] {
        { "reset",                              "6; right; 12 1  13  1 1  1 end" },
        { "reset,right",                        "6; right; 1  12 13  1 1  1 end" },
        { "reset,right,right",                  "6; right; 1  1  12 13 1  1 end" },
        { "reset,right,right,right",            "6; right; 1  1  12 13 1  1 end" },
        { "reset,right,right,right,down",       "6; right; 1  1  1  13 1  1 end" },
        { "reset,right,right,right,down,right", "6; right; 1  1  1  1  13 1 end" },

        { "reset",                              "12; right; 1 13 13  1  1  1 end" },
        { "reset,right",                        "12; right; 1 13 13  1  1  1 end" },
        { "reset,right,right",                  "12; right; 1 1  13  13 1  1 end" },
        { "reset,right,right,right",            "12; right; 1 1  13  13 1  1 end" },
        { "reset,right,right,right,down",       "12; right; 1 1  12  13 1  1 end" },
        { "reset,right,right,right,down,right", "12; right; 1 1  1   12 13 1 end" },
      };

      DoTests("Singles", "", template, tests);
    }

    // left just pushes, right sends blue to edge
    // note: tested without pause on again
    [TestMethod]
    public void CommandAgain() {
      var template =
        "(rul):right [ > p | r]-> [p | b] ;" +
              "left  [ > p | r]-> [p | g] ;" +
              "left  [ g ]-> [ > g ]      ;" +
              "right [ b ]-> [ > b ] again;" +
        "@(lev):....r.p.r....;" +
        "@(win):";
      var tests = new string[,] {
        { "reset",                  "0; right; 1 1 1 1  13 1  12 1  13 1  1  1  1  end" },
        { "reset,left",             "0; right; 1 1 1 1  13 12 1  1  13 1  1  1  1  end" },
        { "reset,left,left",        "0; right; 1 1 1 15 1  12 1  1  13 1  1  1  1  end" },
        { "reset,right",            "0; right; 1 1 1 1  13 1  1  12 13 1  1  1  1  end" },
        { "reset,right,right",      "0; right; 1 1 1 1  13 1  1  12 1  1  1  1  14 end" },
      };

      DoTests("", "", template, tests);
    }

    // input ignored, sucks r left then kills it
    [TestMethod]
    public void CommandLate() {
      var template =
        "(rul):{0}[ p | r ] -> [ p | ] again      ;" +
              "{0}[ p | | r ] -> [ p | r | ] again;" +
        "@(lev):..p.r..;" +
        "@(win):";
      var tests = new string[,] {
        { "reset",          "0; right; 1 1 12 1  13 1 1  end" },
        { "reset,tick",     "0; right; 1 1 12 1  1  1 1 end" },
      };

      DoTests("", ";late", template, tests);
    }

    [TestMethod]
    public void MatchChange() {
      var template =
        "(rul):{0}[right p | r]-> [right p | right r] again;" +
              "{0}[right o]-> [right r]                    ;" +
        "@(lev):..p.r.y.;" +
        "@(win):any r on y;";
      var tests = new string[,] {
        { "reset",                          "0; right; 1 1 12 1  13 1  16 1  end" },
        { "reset,right",                    "0; right; 1 1 1  12 13 1  16 1  end" },
        { "reset,right,right",              "0; right; 1 1 1  1  12 13 16 1  end" },
        { "reset,right,right",              "0; right; 1 1 1  1  12 13 16 1  end" },
        { "reset,right,right,right",        "0; right; over" },
      };

      DoTests("", "", template, tests);
    }

    [TestMethod]
    public void StartupRules() {
      var template =
        "(pre):run_rules_on_level_start;" +
        "@(rul):[ r | ]-> [ b | ]      ;" +
               "[> p | b ]-> [> p | > b];" +
        "@(lev):.p.r.;" +
        "@(win):;";
      var tests = new string[,] {
        { "",                               "0; right; 1 12 1  14 1  end" },
        { "reset",                          "0; right; 1 12 1  14 1  end" },
        { "reset,right",                    "0; right; 1 1 12  14 1  end" },
        { "reset,right,restart",            "0; right; 1 12 1  14 1  end" },
        { "reset,right,right",              "0; right; 1 1  1  12 14 end" },
        { "reset,right,right,restart",      "0; right; 1 12 1  14 1  end" },
      };

      DoTests("", "", template, tests);
    }

    void DoTests(string title, string variations, string template, string[,] tests, [CallerMemberName] string method = "") {
      foreach (var variation in variations.Split(';')) {
        var setup = String.Format(template, variation);
        var testcase = StaticTestData.GetTestCase("PRBG", title + ":" + method, setup);
        var engine = SetupEngine(testcase);
        for (int i = 0; i < tests.GetLength(0); i++) {
          DoTest(engine, tests[i, 0], tests[i, 1], variation);
        }
      }
    }

    void DoTest(GameModel engine, string inputs, string tests, string variation = "") {
      var msg = $"[{variation}][{inputs}] [{tests}]";
      engine.AcceptInputs(inputs);
      var ts = tests.Split(';');
      var location = ts[0].SafeIntParse();
      var direction = ts[1].Trim();
      var objs = ts[2].Trim().Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
      for (int i = 0; i < objs.Length; i++) {
        var actual = (!engine.Ok) ? "error"
          : (engine.EndLevel) ? "done"
          : (engine.GameOver) ? "over"
          : (location == null) ? "end"
          : engine.GetObjects(location.Value).OrderBy(v => v).Join("");
        Assert.AreEqual(objs[i], actual, msg);
        location = engine.Step(location ?? 0, direction);
      }
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