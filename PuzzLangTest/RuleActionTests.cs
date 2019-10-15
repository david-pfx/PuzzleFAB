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
  public class RuleActionTests : TestCommon {

    [TestMethod]
    public void PushBasic() {
      var template =
         "(rul):[> p|r]->[> p|> r];" +
               "[< p|r]->[< p|< r];" +
               "[^ p|r]->[< p|> r];" +
               "[v p|r]->[  p|> r];" +
        "@(lev):..P.R..;" +
        "@(win):;";
      var tests = new string[,] {
        { "",                         "0; right; 1 1  12 1  13 1  1 end" },
        { "action",                   "0; right; 1 1  12 1  13 1  1 end" },
        { "hover",                    "0; right; 1 1  12 1  13 1  1 end" },
        { "fire1",                    "0; right; 1 1  12 1  13 1  1 end" },
        { "fire2",                    "0; right; 1 1  12 1  13 1  1 end" },
        { "fire3",                    "0; right; 1 1  12 1  13 1  1 end" },
        { "up",                       "0; right; 1 1  12 1  13 1  1 end" },
        { "down",                     "0; right; 1 1  12 1  13 1  1 end" },
        { "left",                     "0; right; 1 12 1  1  13 1  1 end" },
        { "right",                    "0; right; 1 1  1  12 13 1  1 end" },
        { "right,right",              "0; right; 1 1  1  1  12 13 1 end" },
        { "right,right,right",        "0; right; 1 1  1  1  1  12 13 end" },
        { "right,right,right,right",  "0; right; 1 1  1  1  1  12 13 end" },
        { "right,left",               "0; right; 1 1  12 13 1  1  1 end" },
        { "right,left,left",          "0; right; 1 12 13 1  1  1  1 end" },
        { "right,up",                 "0; right; 1 1  12 1  1  13  1 end" },
        { "right,up,up",              "0; right; 1 1  12 1  1  13  1 end" },
        { "right,down",               "0; right; 1 1  1  12 1  13  1 end" },
        { "right,down,down",          "0; right; 1 1  1  12 1  13  1 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void MatchBasic() {
      var template = 
        "(rul):[p|{0}]->[p|];" +
              "[p|a]->[|];" +  // compile but never match
        "@(lev):.....;.....;R.P.R;.....;..R.." +
        "@(win):";
      var variations = "R;O";
      var tests = new string[,] {
        { "",                         "10; right; 13 1  12 1  13 end" },
        { "right",                    "10; right; 13 1  1  12 13 end" },
        { "right,right",              "10; right; 13 1  1  1  12 end" },
        { "right,left",               "10; right; 13 1  12 1  1 end" },
        { "left",                     "10; right; 13 12 1  1  13 end" },
        { "left,left",                "10; right; 12 1  1  1  13 end" },
        { "left,right",               "10; right; 1  1  12 1  13 end" },
      };

      DoTests("PRBG", variations, template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void MatchChange() {
      var template =
        "(rul):{0}[right p | r]-> [right p | right r] again;" +
              "{0}[right o]-> [right r]                    ;" +
        "@(lev):..p.r.y.;" +
        "@(win):any r on y;";
      var tests = new string[,] {
        { "",                         "0; right; 1 1 12 1  13 1  16 1  end" },
        { "right",                    "0; right; 1 1 1  12 13 1  16 1  end" },
        { "right,right",              "0; right; 1 1 1  1  12 13 16 1  end" },
        { "right,right",              "0; right; 1 1 1  1  12 13 16 1  end" },
        { "right,right,right",        "0; right; over" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void MatchStationary() {
      var template =
         "(rul):[> p | r]->[> p | > r];" +
               "[ p | stationary r]->[ p | b ];" +
               "[stationary r]->[ g ];" +
        "@(lev):.R.PR..;"+
        "@(win):";
      var tests = new string[,] {
        { "",                         "0; right; 1 13 1  12 13 1  1 end" },
        { "up",                       "0; right; 1 15 1  12 14 1  1 end" },
        { "right",                    "0; right; 1 15 1  1  12 13 1 end" },
        { "right,up",                 "0; right; 1 15 1  1  12 14 1 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void MatchSegmentedRule() {
      var template =
         "(rul):[up p][r]->[up p][];" +
               "[right p][b]->[right p][]; " +
        "@(lev):.....;.p...;..rr.;..bb.";
      var tests = new string[,] {
        { "",                         "10; right; 1 1 13 13 1 end" },
        { "right",                    "10; right; 1 1 13 13 1 end" },
        { "left",                     "10; right; 1 1 13 13 1 end" },
        { "up",                       "10; right; 1 1 1  1  1 end" },
        { "up,up",                    "10; right; 1 1 1  1  1 end" },
        { "",                         "15; right; 1 1 14 14 1 end" },
        { "right",                    "15; right; 1 1 1  1  1 end" },
        { "right,right",              "15; right; 1 1 1  1  1 end" },
        { "left",                     "15; right; 1 1 14 14 1 end" },
        { "up",                       "15; right; 1 1 14 14 1 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void MatchSingleton() {
      var template =
        "(rul):[ up p ]->[ right r ];" +
              //"[ right p ]->[ > r ];" +
              "[ down p ]->[ r ];" +
              //"[ left p ]->[ < r ];" +
              "[ action p ]->[ ];" +
        "@(lev):.P..;" +
        "@(win):";
      var tests = new string[,] {
        { "",                                     "0; right; 1  12 1  1 end" },
        { "up",                                   "0; right; 1  1  13 1 end" },
        //{ "right",                                "0; right; 1  13 1  1 end" },
        { "down",                                 "0; right; 1  13 1  1 end" },
        //{ "left",                                 "0; right; 1  1  13 1 end" },
        { "action",                               "0; right; 1  1  1  1 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void MatchAmbiguous() {
      var template =
         "(rul):[parallel p|r]->[>p|>r];" +
               "[perpendicular p|g]->[>p|>g];" +
               "[orthogonal p|b]->[>p|>b];" +
        "@(lev):.......;.b.p.r.;.y...g.;.......";
      var tests = new string[,] {
        { "",                         "7; right; 1  14 1  12 1  13 1  end" },
        { "",                        "14; right; 1  16 1  1  1  15 1  end" },

        { "right",                    "7; right; 1  14 1  1  12 13 1 end" },
        { "right,right",              "7; right; 1  14 1  1  1  12 13 end" },
        { "right,left",               "7; right; 1  14 1  1  1  12 13 end" },
        { "right,up",                 "7; right; 1  14 1  1  1  13 1 end" },
        { "right,down",               "7; right; 1  14 1  1  1  13 1 end" },

        { "left",                     "7; right; 1  14 12 1  1  13 1  end" },
        { "left,left",                "7; right; 14 12 1  1  1  13 1  end" },
        { "left,left",                "7; right; 14 12 1  1  1  13 1  end" },
        { "left,up",                  "7; right; 14 12 1  1  1  13 1  end" },
        { "left,down",                "7; right; 14 12 1  1  1  13 1  end" },

        { "",                         "14; right; 1  16 1  1  1  15 1  end" },
        { "down",                     "14; right; 1  16 1  12 1  15 1  end" },
        { "down,right",               "14; right; 1  16 1  1  12 15 1  end" },
        { "down,right,right",         "14; right; 1  16 1  1  12 15 1  end" },
        { "down,right,left",          "14; right; 1  16 1  12 1  15 1  end" },
        { "down,right,up",            "14; right; 1  16 1  1  1  12 15  end" },
        { "down,right,down",          "14; right; 1  16 1  1  1  12 15  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // Test issue: Pattern action object matching must be exact, not overlap ???
    [TestMethod]
    public void ActionNetMove() {
      var template =
        "(rul):[> p | r]-> [> p | > r];"  +
              "late [ p oyk ]-> [ p y k ];" +
        "@(lev):..k.p.y..;";
      var tests = new string[,] {
        { "",                         "0; right; 1  1  17   1 12 1  16   1  1  end" },
        { "right",                    "0; right; 1  1  17   1  1 12 16   1  1  end" },
        { "right,right",              "0; right; 1  1  17   1  1 1  1267 1  1  end" },
        { "left",                     "0; right; 1  1  17   12 1 1  16   1  1  end" },
        { "left,left",                "0; right; 1  1  1267 1  1 1  16   1  1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // Test issue: same property both sides no action
    [TestMethod]
    public void ActionOrSimple() {
      var template =
        "(rul):[ > p oyk ]-> [ p oyk ];" +
        "@(lev):..k.p.y..;";
      var tests = new string[,] {
        { "",                         "0; right; 1  1  17  1 12 1  16  1  1  end" },
        { "right",                    "0; right; 1  1  17  1  1 12 16  1  1  end" },
        { "right,right",              "0; right; 1  1  17  1  1 1  126 1  1  end" },
        { "right,right,right",        "0; right; 1  1  17  1  1 1  126 1  1  end" },
        { "left",                     "0; right; 1  1  17  12 1 1  16  1  1  end" },
        { "left,left",                "0; right; 1  1  127 1  1 1  16  1  1  end" },
        { "left,left,left",           "0; right; 1  1  127 1  1 1  16  1  1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // Test issue: 'no property' on the RHS should clear objects
    [TestMethod]
    public void ActionNoOr() {
      var template =
        "(rul):[ > p | ]-> [ > p | no oyk ];" +
        "@(lev):..k.p.y..;";
      var tests = new string[,] {
        { "",                         "0; right; 1  1  17  1 12 1  16  1  1  end" },
        { "right",                    "0; right; 1  1  17  1  1 12 16  1  1  end" },
        { "right,right",              "0; right; 1  1  17  1  1 1  12  1  1  end" },
        { "right,right,right",        "0; right; 1  1  17  1  1 1  1   12 1  end" },
        { "left",                     "0; right; 1  1  17  12 1 1  16  1  1  end" },
        { "left,left",                "0; right; 1  1  12  1  1 1  16  1  1  end" },
        { "left,left,left",           "0; right; 1  12 1   1  1 1  16  1  1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // Test issue: Pattern ambiguous movement should match same anywhere in action
    // see also laserblock
    [TestMethod]
    public void ActionAmbiMulti() {
      var template =
        "(rul):[ horizontal p | oyk ]-> [ horizontal p | horizontal oyk ];" +
        "@(lev):...k.p.y...;";
      var tests = new string[,] {
        { "",                         "0; right; 1  1  1  17  1  12 1  16  1  1  1 end" },
        { "right",                    "0; right; 1  1  1  17  1  1  12 16  1  1  1 end" },
        { "right,right",              "0; right; 1  1  1  17  1  1  1  12  16  1  1 end" },
        { "right,right,right",        "0; right; 1  1  1  17  1  1  1  1   12 16 1 end" },
        { "left",                     "0; right; 1  1  1  17  12 1  1  16  1  1  1  end" },
        { "left,left",                "0; right; 1  1 17  12  1  1  1  16  1  1  1  end" },
        { "left,left,left",           "0; right; 1 17 12  1   1  1  1  16  1  1  1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // Test issue: Pattern object should match same anywhere in action
    [TestMethod]
    public void ActionOrMulti() {
      var template =
        "(rul):[ > p oyk | | ]-> [ p | oyk | oyk ];" +
        "@(lev):...k.p.y...;";
      var tests = new string[,] {
        { "",                         "0; right; 1  1  1  17  1  12 1  16  1  1  1 end" },
        { "right",                    "0; right; 1  1  1  17  1  1  12 16  1  1  1 end" },
        { "right,right",              "0; right; 1  1  1  17  1  1  1  126 1  1  1 end" },
        { "right,right,right",        "0; right; 1  1  1  17  1  1  1  12  16 16 1 end" },
        { "left",                     "0; right; 1  1  1  17  12 1  1  16  1  1  1  end" },
        { "left,left",                "0; right; 1  1  1  127 1  1  1  16  1  1  1  end" },
        { "left,left,left",           "0; right; 1 17 17  12  1  1  1  16  1  1  1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void MatchEllipsisSimple() {
      var template = 
         "(rul):[>p|...|r]->[>p|...|>r]" +
        "@(lev):..RP.R.." +
        "@(win):";
      var tests = new string[,] {
        { "",                         "0; right; 1  1  13 12 1  13  1 1 end" },
        { "up",                       "0; right; 1  1  13 12 1  13  1 1 end" },
        { "down",                     "0; right; 1  1  13 12 1  13  1 1 end" },
        { "right",                    "0; right; 1  1  13 1  12 1  13 1 end" },
        { "right,right",              "0; right; 1  1  13 1  1  12  1 13 end" },
        { "right,right,right",        "0; right; 1  1  13 1  1  1  12 13 end" },
        { "right,right,right,right",  "0; right; 1  1  13 1  1  1  12 13 end" },
        { "left",                     "0; right; 1  13 12 1  1  13  1 1 end" },
        { "left,left",                "0; right; 13 12 1  1  1  13  1 1 end" },
        { "left,left,left",           "0; right; 13 12 1  1  1  13  1 1 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // ellipsis matches only once???
    [TestMethod]
    public void ActionEllipsisNoDup() {
      var template =
         "(rul):[ > p | ... | r ] -> [ p | ... | b ]" +
        "@(lev):...P.RR.." +
        "@(win):";
      var tests = new string[,] {
        { "",                         "0; right; 1  1  1 12 1  13  13  1 1 end" },
        { "up",                       "0; right; 1  1  1 12 1  13  13  1 1 end" },
        { "down",                     "0; right; 1  1  1 12 1  13  13  1 1 end" },
        { "right",                    "0; right; 1  1  1 12 1  14  13  1 1 end" },
        { "right,right",              "0; right; 1  1  1 12 1  14  14  1 1 end" },
        { "right,right,right",        "0; right; 1  1  1 1  12 14  14  1 1 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // use ellipsis to fill a row
    [TestMethod]
    public void ActionEllipsisFill() {
      var template =
         "(rul):[ > p | ... | ] -> [ > p | ... | r ]" +
        "@(lev):P...;....;...." +
        "@(win):";
      var tests = new string[,] {
        { "",                         "0; right; 12 1  1  1  end" },
        { "up",                       "0; right; 12 1  1  1  end" },
        { "down",                     "0; right; 12 1  1  1  end" },
        { "left",                     "0; right; 12 1  1  1  end" },
        { "right",                    "0; right; 12 13 13 13 end" },
        { "",                         "0; down;  12 1  1  end" },
        { "up",                       "0; down;  12 1  1  end" },
        { "left",                     "0; down;  12 1  1  end" },
        { "right",                    "0; down;  12 1  1  end" },
        { "down",                     "0; down;  12 13 13 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // use ellipsis to drag from far to near
    [TestMethod]
    public void ActionEllipsisDrag() {
      var template =
         "(rul): {0} [ p | ... | | r ]->[ p | ... | r | ];" +
        "@(lev):.P.......R.";
      var variations = ";late";
      var tests = new string[,] {
        { "",                         "0; right; 1 12 1  1 1 1 1 1 1 13 1 end" },
        { "action",                   "0; right; 1 12 13 1 1 1 1 1 1 1 1 end" },
      };

      DoTests("PRBG", variations, template, tests, DoTestLocationValue);
    }

    // Test issue: match on property but create object
    [TestMethod]
    public void ActionOrCreate() {
      var template =
         "(obj):Background;black;;PR;white;;PL;grey;;R;RED;;B;BLUE;;G;green;;Y;yellow;;K;Pink;;" +
        "@(leg):. = Background;PLAYER=PR or PL;P = PR;" +
        "@(rul):[ left player  ] -> [ left PL ];" +
               "[ right player ] -> [ right PR ];" +
        "@(lev):..p..;" +
        "@(win):no player;";
      ;
      var tests = new string[,] {
        { "",                         "0; right; 1  1  12 1  1  end" },
        { "right",                    "0; right; 1  1  1  12 1  end" },
        { "right,left",               "0; right; 1  1  13 1  1  end" },
        { "right,left,left",          "0; right; 1  13 1  1  1  end" },
        { "left",                     "0; right; 1  13 1  1  1  end" },
        { "left,left",                "0; right; 13 1  1  1  1  end" },
        { "left,right",               "0; right; 1  1  12 1  1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // Test issue: new feature property map
    [TestMethod]
    public void ActionPropertyMap() {
      var template = 
         "(leg):. = Background;rbg = r or b or g;bgr = b or g or r;" +
        "@(rul):[ > p | rbg ]-> [ > p | > bgr ];" +
        "@(lev):..bpr..;" +
        "@(win):no player;";
      ;
      var tests = new string[,] {
        { "",                         "0; right; 1  1  14  12 13  1   1  end" },
        { "right",                    "0; right; 1  1  14  1  12  14  1  end" },
        { "right,right",              "0; right; 1  1  14  1  1   12  15 end" },
        { "right,right,right",        "0; right; 1  1  14  1  1   12  13 end" },
        { "left",                     "0; right; 1  15 12  1  13  1  1   end" },
        { "left,left",                "0; right; 13 12  1  1  13  1  1   end" },
        { "left,left,left",           "0; right; 14 12  1  1  13  1  1   end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void RandomRule() {
      var template =
         "(rul):random [p|r]->[>p|>r];" +
               "random [>p|g]->[>p|randomdir g];" +   // hit green moves random
               "random [>p|b]->[>p|random o];" +      // hit blue spawns R or G or B or Y
        "@(lev):..r..;.bpg.;";
      var tests = new string[,] {
        { "",                         "0; right; 1 1  13 1  1 end" },
        { "",                         "5; right; 1 14 12 15 1 end" },
        { "up",                       "0; right; 1 1" },  // TODO:test something!!!
        { "down",                     "0; right; 1 1" },
        { "left",                     "0; right; 1 1" },
        { "right",                    "0; right; 1 1" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void RandomAction() {
      var template =
         "(rul):[ o ] -> [ random o ];" +
        "@(lev):..r..;.bpg.;";
      var tests = new string[,] {
        { "",                         "0; right; 1 1  13 1  1 end" },
        { "",                         "5; right; 1 14 12 15 1 end" },
        { "up",                       "0; right; 1 1" },  // TODO:test something!!!
        { "down",                     "0; right; 1 1" },
        { "left",                     "0; right; 1 1" },
        { "right",                    "0; right; 1 1" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // issue: Rule after loop is skipped
    // issue: Empty pattern start should match everything
    [TestMethod]
    public void LoopBasic() {
      var template =
         "(rul):right [ > p | o ] -> [ > p | > o ];" +
               "startloop;" +
               "right [ > r | b ] -> [ > r | > b ];" +
               "right [ > b | r ] -> [ > b | > r ];" +
               "endloop;" +
               "right [ | > p ] -> [ g | > p ];" +  //
        "@(lev):..PBRBR..";
      var tests = new string[,] {
        { "",                         "0; right; 1 1  12  14  13 14 13 1 1 end" },
        { "right",                    "0; right; 1 15 1   12  14 13 14 13 1 end" },
        { "right,right",              "0; right; 1 15 15  1   12 14 13 14 13 end" },
        { "right,right,right",        "0; right; 1 15 15  15  12 14 13 14 13 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void RigidGroup() {
      var template =
        "(rul):rigid [ > p | r] -> [ > p | > r ];" +
               "+ rigid[moving r | r]-> [moving r | moving r];" +
        "@(lev):..r.g.;" +
               "P.r...;" +
               ".rr...;" +
               "......;" +
        "@(win):";
      var tests = new string[,] {
        { "",                             "6; right; 12 1  13  1 1  1 end" },
        { "right",                        "6; right; 1  12 13  1 1  1 end" },
        { "right,right",                  "6; right; 1  1  12 13 1  1 end" },
        { "right,right,right",            "6; right; 1  1  12 13 1  1 end" },
        { "right,right,right,down",       "6; right; 1  1  1  13 1  1 end" },
        { "right,right,right,down,right", "6; right; 1  1  1  1  13 1 end" },

        { "",                             "12; right; 1 13 13  1  1  1 end" },
        { "right",                        "12; right; 1 13 13  1  1  1 end" },
        { "right,right",                  "12; right; 1 1  13  13 1  1 end" },
        { "right,right,right",            "12; right; 1 1  13  13 1  1 end" },
        { "right,right,right,down",       "12; right; 1 1  12  13 1  1 end" },
        { "right,right,right,down,right", "12; right; 1 1  1   12 13 1 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // left just pushes, right sends blue to edge
    // TODO: test with pause on again
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
        { "",                 "0; right; 1 1 1 1  13 1  12 1  13 1  1  1  1  end" },
        { "left",             "0; right; 1 1 1 1  13 12 1  1  13 1  1  1  1  end" },
        { "left,left",        "0; right; 1 1 1 15 1  12 1  1  13 1  1  1  1  end" },
        { "right",            "0; right; 1 1 1 1  13 1  1  12 13 1  1  1  1  end" },
        { "right,right",      "0; right; 1 1 1 1  13 1  1  12 1  1  1  1  14 end" },
        { "right,right,undo", "0; right; 1 1 1 1  13 1  1  12 13 1  1  1  1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
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
        { "",         "0; right; 1 1 12 1  13 1 1 end" },
        { "action",   "0; right; 1 1 12 1  1  1 1 end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    // test some weird characters
    [TestMethod]
    public void LegendOddSymbols() {
      var template =
         "(rul):[> p|r]->[> p|> r];" +
        "@(leg):. = Background;a = R and K;o = R or G or B or Y;" +
               "[ = r; | = b; ] = g;" +
        "@(lev):.P.[|].;" +
        "@(win):;";
      var tests = new string[,] {
        { "",                         "0; right; 1  12 1  13 14 15 1 end" },
      };

      for (int i = 0; i < tests.GetLength(0); i++) {
        DoTests("PRBG", "", template, tests, DoTestLocationValue);
      }
    }

    [TestMethod]
    public void WinBasic() {
      var template =
         "(rul):[>p|r]->[>p|>r]" +
        "@(lev):..P.Y.." +
        "@(win):{0}";
      var variations =
        "all p on y; some p on y; all y on p; some y on p; " +
        "all p on o; some p on o; all o on p; some o on p";
      var tests = new string[,] {
        { "",                         "0; right; 1 1 12 1 16 1 1 end" },
        { "right",                    "0; right; 1 1 1 12 16 1 1 end" },
        { "right,right",              "0; right; over" },
      };

      DoTests("PRBG", variations, template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void SettingRunRules() {
      var template =
        "(pre):run_rules_on_level_start;" +
        "@(rul):[ r | ]-> [ b | ]      ;" +
               "[> p | b ]-> [> p | > b];" +
        "@(lev):.p.r.;" +
        "@(win):;";
      var tests = new string[,] {
        { "",                         "0; right; 1 12 1  14 1  end" },
        { "",                         "0; right; 1 12 1  14 1  end" },
        { "right",                    "0; right; 1 1 12  14 1  end" },
        { "right,restart",            "0; right; 1 12 1  14 1  end" },
        { "right,right",              "0; right; 1 1  1  12 14 end" },
        { "right,right,restart",      "0; right; 1 12 1  14 1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void SettingPlayerMove() {
      var template =
        "(pre):require_player_movement;" +
        "@(rul):[ b ]-> [ right b ];" +
        "@(lev):.b....p.r.;" +
        "@(win):;";
      var tests = new string[,] {
        { "",                         "0; right; 1 14 1  1 1 1 12 1  13 1  end" },
        // test that non-player movement still happens
        { "tick",                     "0; right; 1 1 14  1 1 1 12 1  13 1  end" },  // non-player ok
        //{ "tick",                     "0; right; 1 14 1  1 1 1 12 1  13 1  end" },
        { "right",                    "0; right; 1 1  14 1 1 1 1 12  13 1  end" },  // move ok
        { "right",                    "0; right; 1 1  14 1 1 1 1 12  13 1  end" },  // cancelled
        { "right,up",                 "0; right; 1 1  14 1 1 1 1 12  13 1  end" },  // cancelled
        { "right,left",               "0; right; 1 1  1 14 1 1 12 1  13 1  end" },  // move ok
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void InputClick() {
      var template =
        "(pre):hover;" +
        "@(leg):. = Background;clickable = b or g;" +
        "@(rul):[ left b ]-> [ r ];" +
               "[ right b ]-> [ g ];" +
               "[ hover b ]-> [ y ];" +
        "@(lev):..b..g..;" +
        "@(win):;";
      var tests = new string[,] {
        { "",                         "0; right; 1 1 14  1 1  15 1 1  end" },
        { "tick",                     "0; right; 1 1 14  1 1  15 1 1  end" },
        { "left 0",                   "0; right; 1 1 14  1 1  15 1 1  end" },
        { "left 1",                   "0; right; 1 1 14  1 1  15 1 1  end" },
        { "left 2",                   "0; right; 1 1 13  1 1  15 1 1  end" },
        { "right 0",                  "0; right; 1 1 14  1 1  15 1 1  end" },
        { "right 1",                  "0; right; 1 1 14  1 1  15 1 1  end" },
        { "right 2",                  "0; right; 1 1 15  1 1  15 1 1  end" },
        { "hover 0",                  "0; right; 1 1 14  1 1  15 1 1  end" },
        { "hover 1",                  "0; right; 1 1 14  1 1  15 1 1  end" },
        { "hover 2",                  "0; right; 1 1 16  1 1  15 1 1  end" },
        { "fire1 2",                  "0; right; 1 1 14  1 1  15 1 1  end" },
        { "fire2 2",                  "0; right; 1 1 14  1 1  15 1 1  end" },
        { "fire3 2",                  "0; right; 1 1 14  1 1  15 1 1  end" },
        { "action 2",                 "0; right; 1 1 14  1 1  15 1 1  end" },
      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }
  }
}