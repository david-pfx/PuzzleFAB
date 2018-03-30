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
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DOLE;
using PuzzLangLib;

namespace PuzzLangTest {
  public class JsonTestCase {
    public string Title;
    public string Script;
    public List<InputEvent> Inputs;
    public string FinalState;
    public int TargetLevel;
    public double RandomSeed;
  }

  public static class JsonTestData {
    public static List<string[]> StringTestCases { get {
        if (_stringtestcases == null) PopulateStrings();
        return _stringtestcases;
      } }
    public static List<JsonTestCase> ClassTestCases { get {
        if (_classtestcases == null) PopulateClass();
        return _classtestcases;
      } }

    static List<JsonTestCase> _classtestcases;
    static List<string[]> _stringtestcases;
    static string _data;

    public static void Setup(TextReader reader) {
      _data = reader.ReadToEnd();
    }

    public static void Setup(string data) {
      _data = data;
    }

    public static string ConvertToScript(JsonTestCase testcase) {
      var sw = new StringWriter();
      sw.WriteLine("(--- Script generated from Json test case");
      sw.WriteLine("Json test case: {0}", testcase.Title);
      sw.WriteLine("Inputs: {0}", testcase.Inputs.Join());
      sw.WriteLine("Final state: {0}", testcase.FinalState.Replace("\n", ";"));
      sw.WriteLine("Target level: {0}", testcase.TargetLevel);
      sw.WriteLine("Random seed: {0}", testcase.RandomSeed);
      sw.WriteLine("---)");
      sw.WriteLine("debug");
      sw.WriteLine("verbose_logging");
      sw.WriteLine(testcase.Script.Replace("\n", "\r\n"));
      sw.WriteLine("(eof)");
      return sw.ToString();
    }

    // codes used by PuzzleScript tests.js
    static Dictionary<string, InputEvent> _inputlookup = new Dictionary<string, InputEvent> {
      { "0", InputEvent.Up },
      { "1", InputEvent.Left},
      { "2", InputEvent.Down },
      { "3", InputEvent.Right },
      { "4", InputEvent.Action },
      { "tick", InputEvent.Tick },
      { "undo", InputEvent.Undo },
      { "restart", InputEvent.Restart },
    };

    // populate object list from what Json gives us
    static void PopulateClass() {
      var jsondata = JsonConvert.DeserializeObject<IList<IList<object>>>(_data);
      _classtestcases = new List<JsonTestCase>();
      foreach (var item in jsondata) {
        var tokens = (item[1] as JArray).Children().ToList();
        var inputs = (tokens[1] as JArray)
          .Select(t => _inputlookup.SafeLookup(t.Value<string>()))
          .ToList();
        var testcase = new JsonTestCase() {
          Title = item[0] as string,
          Script = tokens[0].Value<string>(),
          Inputs = inputs,
          FinalState = tokens[2].Value<string>(),
          TargetLevel= tokens.Count > 3 ? tokens[3].Value<int>() : 0,
          RandomSeed = tokens.Count > 4 ? tokens[4].Value<double>() : 0,
        };
        _classtestcases.Add(testcase);
      }
    }

    // populate object list from what Json gives us
    static void PopulateStrings() {
      var jsondata = JsonConvert.DeserializeObject<IList<IList<object>>>(_data);
      _stringtestcases = new List<string[]>();
      foreach (var item in jsondata) {
        var tokens = (item[1] as JArray).Children().ToList();
        var inputs = (tokens[1] as JArray)
          .Select(t => _inputlookup.SafeLookup(t.Value<string>()))
          .ToList();
        var testcase = new string[] {
          item[0] as string,
          tokens[0].Value<string>(),
          inputs.Join(),
          tokens[2].Value<string>(),
          tokens.Count >3 ? tokens[3].Value<string>() : "0",
          tokens.Count >4 ? tokens[4].Value<string>() : "0",
        };
        _stringtestcases.Add(testcase);
      }
    }

    public static void SetupSingle(string script) {
      var tokens = JArray.Parse(script);
      var inputs = (tokens[1] as JArray)
        .Select(t => _inputlookup.SafeLookup(t.Value<string>()))
        .ToList();
      var testcase = new JsonTestCase() {
        Title = "",
        Script = tokens[0].Value<string>(),
        Inputs = inputs,
        FinalState = tokens[2].Value<string>(),
        TargetLevel = tokens.Count > 3 ? tokens[3].Value<int>() : 0,
        RandomSeed = tokens.Count > 4 ? tokens[4].Value<double>() : 0,
      };
      _classtestcases = new List<JsonTestCase> { testcase };
    }
  }
}
