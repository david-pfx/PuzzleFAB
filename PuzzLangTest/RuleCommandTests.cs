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
  public class RuleCommandTests : TestCommon {

    [TestMethod]
    public void CommandExit() {
      var template =
         "(rul):[>p|r]->restart;" +
               "[>p|b]->cancel;" +
               "[>p|g]->checkpoint;" +
               "[>p|y]->win;" +
        "@(lev):.......;.b.p.r.;.y...g.;.......;;" +
                "...;";
      var tests = new string[,] {
        // undo
        { "",                                     "7; right; 1  14 1  12 1  13 1  end" },
        { "right",                                "7; right; 1  14 1  1 12  13 1  end" },
        { "right,undo",                           "7; right; 1  14 1  12 1  13 1  end" },
        { "right,undo,undo",                      "7; right; 1  14 1  12 1  13 1  end" },
        { "left",                                 "7; right; 1  14 12 1  1  13 1  end" },
        { "left,right",                           "7; right; 1  14 1  12 1  13 1  end" },
        { "left,right,undo",                      "7; right; 1  14 12 1  1  13 1  end" },
        { "left,right,undo,undo",                 "7; right; 1  14 1  12 1  13 1  end" },
        { "left,right,undo,undo,undo",            "7; right; 1  14 1  12 1  13 1  end" },

        // restart
        { "",                         "7; right; 1  14 1  12 1  13 1  end" },
        { "right",                    "7; right; 1  14 1  1 12  13 1  end" },
        { "right,right",              "7; right; 1  14 1  12 1  13 1  end" },
        { "right,right,right",        "7; right; 1  14 1  1 12  13 1  end" },

        // cancel
        { "left",                     "7; right; 1  14 12 1  1  13 1  end" },
        { "left,left",                "7; right; 1  14 12 1  1  13 1  end" },
        { "left,left,left",           "7; right; 1  14 12 1  1  13 1  end" },

        // checkpoint
        { "",                              "14; right; 1  16 1  1  1  15 1  end" },
        { "down",                          "14; right; 1  16 1  12 1  15 1  end" },
        { "down,right",                    "14; right; 1  16 1  1  12 15 1  end" },
        { "down,right,right",              "14; right; 1  16 1  1  12 15 1  end" }, // triggers checkpoint
        { "down,right,right,left",         "14; right; 1  16 1  12 1  15 1  end" },
        { "down,right,right,left,restart", "14; right; 1  16 1  1  12 15 1  end" },
        { "down,right,right,left,restart", "14; right; 1  16 1  1  12 15 1  end" },

        // win
        { "",                              "14; right; 1  16 1  1  1  15 1  end" },
        { "down",                          "14; right; 1  16 1  12 1  15 1  end" },
        { "down,left",                     "14; right; 1  16 12 1  1  15 1  end" },
        { "down,left,left",                "0; right; 1  1  1 end" },

      };

      DoTests("PRBG", "", template, tests, DoTestLocationValue);
    }

    [TestMethod]
    public void CommandMessage() {
      var template =
         "(rul):[right p | r ] -> message msg 1;" +
               "[up    p | r ] -> message msg 2;" +
               "[up    p | r ] -> cancel;" +
               "[down  p | r ] -> message msg 3;" +
               "[down  p | r ] -> message msg 4;" +
               "[down  p | r ] -> cancel;" +
        "@(lev):..P.R..;" +
        "@(win):;";
      var tests = new string[,] {
        { "",                         "ok;MSG=null" },
        { "right",                    "ok;MSG=null" },
        { "right,action",             "ok;MSG=null" },
        { "right,right",              "msg;MSG=msg 1" },
        { "right,right,action",       "ok;MSG=null" },
        { "right,right,right",        "ok;MSG=null" },
        { "right,right,action,right", "msg;MSG=msg 1" },
        
        { "right,up",                 "msg;MSG=msg 2" },
        { "right,up,action",          "ok;MSG=null" },
        { "right,up,up",              "ok;MSG=null" },
        { "right,up,action,up",       "msg;MSG=msg 2" },

        { "right,down",               "msg;MSG=msg 3" },
        { "right,down,action",        "msg;MSG=msg 4" },
        { "right,down,action,action", "ok;MSG=null" },
      };

      DoTests("PRBG", "", template, tests, DoTestModel);
    }

    [TestMethod]
    public void CommandStatus() {
      var template =
        "@(rul):right [ up    p ] -> [ p ];" +
               "right [ right p ] -> [ p ] status;" +
               "right [ down  p ] -> [ p ] status one;" +
               "right [ left  p ] -> [ p ] status two;" +
        "@(lev):..P..;;..P..;" +
        "@(win):;";
      var tests = new string[,] {
        { "",                         "ok;STA=null" },
        { "up",                       "ok;STA=null" },
        { "right",                    "ok;STA=null" },
        { "down",                     "ok;STA=one" },
        { "left",                     "ok;STA=two" },
        { "left,action",              "ok;STA=two" },   // status persists if not changed
        { "left,level 1",             "ok;STA=null" },  // status reset on new level
        { "left,down",                "ok;STA=one" },   // status overwritten
        { "left,right",               "ok;STA=null" },  // status overwritten
      };

      DoTests("PRBG", "", template, tests, DoTestModel);
    }
  }
}