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
using System.Text;
using System.Threading.Tasks;
using DOLE;
using PuzzLangLib;

namespace PuzzLangTest {
  public class StaticTestCase {
    public string Title;
    public string Script;
    public string Inputs;
    //public List<InputEvent> Inputs;
    public int TargetLevel;
    public double RandomSeed;
    public static StaticTestCase Create(string title, string script, string inputs) {
      return new StaticTestCase { Title = title, Script = script, Inputs = inputs };
    }
  }

  public static class StaticTestData {
    const string Crlf = "\r\n";
    static readonly char[] Colon = new char[] { ':' };

    static public List<string[]> _testcases {
      get {
        return new List<string[]> {
          new string[] { "Simple Block Pushing", _sbp, null, null, "right" },
          new string[] { "PRBG filled", _extraprelude, _template, _game_prbg, "right" },
          new string[] { "PRBGX extended", _extraprelude, _template_ext, _game_prbg_ext, "right" },
        };
      }
    }

    public static IEnumerable<StaticTestCase> TestCases {
      get {
        foreach (var name in TestCaseNames)
          yield return GetTestCase(name);
      }
    }

    public static IList<string> TestCaseNames { get { return _testcases.Select(t => t[0]).ToList(); } }

    public static StaticTestCase GetTestCase(string key, string title = null, string newsubs = null) {
      var testcase = _testcases.Find(t => t[0].Contains(key));
      var subs = (newsubs == null) ? testcase[3] : FixSubs(testcase[3], newsubs);
      var script = (subs == null) ? testcase[1] 
        : MakeScript(title ?? testcase[0], testcase[1], testcase[2], subs);
      return new StaticTestCase {
        Title = title ?? testcase[0],
        Script = script,
        Inputs = testcase[3],
      };
    }

    private static string FixSubs(string subs, string newsubs) {
      var lookup = subs.Split('@').ToDictionary(t => t.Substring(0,5), t => t);
      foreach (var s in newsubs.Split('@').Where(s => s.Length >= 4))
        lookup[s.Substring(0, 5)] = s;
      return lookup.Values.Join("@");
    }

    static string MakeScript(string title, string prelude, string template, string args) {
      var script = String.Format(prelude, title) + template;
      //var game = template.Replace("(pre)", String.Format(prelude, title));
      // split args on ...@(sec)subs@... then 
      foreach (var subs in args.Split('@')) {
        var parts = subs.Split(new char[] { ':' }, 2);
        script = script.Replace(parts[0], parts[1]);
      }
      return script.Replace(";", Crlf);
    }

    static string _extraprelude = "title {0};author puzzlang testing;homepage puzzlang.org;debug;verbose_logging;";

    // Simple game using template substitutions
    static string _game_prbg =
       "(pre):;" +
      "@(obj):Background;black;;PLAYER P;white;;R;RED;;B;BLUE;;G;green;;Y;yellow;;K;Pink;;" +
      "@(leg):. = Background;a = R and K;o = R or G or B or Y;ork = R or K;oyk = Y or K;obk = B or K" +
      "@(col):Background;Y;Player,R,B,G;K;" +
      "@(rul):[ > P | a] -> [ > P | > a];" +
      "@(win):no p;" +
      "@(lev):message starting;.......;.g.P.a.;...R...;.......;";

    // Ditto with extensions
    static string _game_prbg_ext =
       "(pre):;" +
      "@(obj):Background;black;;PLAYER P;white;;R;RED;;B;BLUE;;G;green;;Y;yellow;;K;Pink;;S;purple;text SSS" +
      "@(leg):. = Background;a = R and K;o = R or G or B or Y;ork = R or K;oyk = Y or K" +
      "@(col):Background;Y;Player,R,B,G;K;S;" +
      "@(scr):;" +
      "@(rul):[ > P | a] -> [ > P | > a];" +
      "@(win):no p;" +
      "@(lev):message starting;.......;.g.P.a.;...R...;.......;";

    // packed template allows sections to be substituted
    static string _template =
      "(pre)"+
      ";========;OBJECTS;========;(obj)" +
      ";=======;LEGEND;=======;(leg)" +
      ";=======;SOUNDS;=======;(sou)" +
      ";================;COLLISIONLAYERS;================;(col)" +
      ";======;RULES;======;(rul)" +
      ";==============;WINCONDITIONS;==============;(win)" +
      ";=======;LEVELS;=======;(lev)";
    static string _template_ext =
      "(pre)" +
      ";==;OBJECTS;========;(obj)" +
      ";==;LEGEND;=======;(leg)" +
      ";==;SOUNDS;=======;(sou)" +
      ";==;COLLISIONLAYERS;================;(col)" +
      ";==;SCRIPTS;======;(scr)" +
      ";==;RULES;======;(rul)" +
      ";==;WINCONDITIONS;==============;(win)" +
      ";==;LEVELS;=======;(lev)";

    //--- full game verbatim from https://www.puzzlescript.net/editor.html
    static string _sbp = @"title Simple Block Pushing Game
author Stephen Lavelle
homepage www.puzzlescript.net

========
OBJECTS
========

Background
LIGHTGREEN GREEN
11111
01111
11101
11111
10111


Target
DarkBlue
.....
.000.
.0.0.
.000.
.....

Wall
BROWN DARKBROWN
00010
11111
01000
11111
00010

Player
Black Orange White Blue
.000.
.111.
22222
.333.
.3.3.

Crate
Orange Yellow
00000
0...0
0...0
0...0
00000


=======
LEGEND
=======

. = Background
# = Wall
P = Player
* = Crate
@ = Crate and Target
O = Target


=======
SOUNDS
=======

Crate MOVE 36772507

================
COLLISIONLAYERS
================

Background
Target
Player, Wall, Crate

======
RULES
======

[ >  Player | Crate] -> [  >  Player | > Crate]

==============
WINCONDITIONS
==============

All Target on Crate

=======
LEVELS
=======


####..
#.O#..
#..###
#@P..#
#..*.#
#..###
####..


######
#....#
#.#P.#
#.*@.#
#.O@.#
#....#
######
";
  }
}
