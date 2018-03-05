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
using System.Text.RegularExpressions;
using DOLE;
using PuzzLangLib;
using PuzzLangTest;

namespace PuzzLangMain {
  class Program {
    const string Version = "PuzzLang 1.0b";
    static string _help = "PuzzLang <input path> options\n";
    static TextWriter _output;
    static bool _encodelevel;
    static string _startlevel;
    static string _inputs = null;
    static string _hack = null;
    static string _filter = "";  // default filter
    static string _jconvert = null; // converted files go into this directory
    static string _jctest = null; // converted files from this directory
    static string _gist = null;
    static int _take = 9999;
    static bool _jtarray = false;
    static bool _jtsingle = false;
    static bool _static = false;
    static string _xpath = null;
    static List<Pair<string, string>> _settings = new List<Pair<string, string>>();
    static bool _xdebug = false;

    static readonly Dictionary<string, Action<string>> _options
      = new Dictionary<string, Action<string>>(StringComparer.CurrentCultureIgnoreCase) {
        { "encode",   (a) => { _encodelevel = true; } },
        { "filter",   (a) => { _filter = a; } },
        { "hack",     (a) => { _hack = a; } },
        { "gist",     (a) => { _gist = a ?? "0ebbe5e1f0761a87cc7aeace0d5e4b8e"; } },
        { "inputs",   (a) => { _inputs = a ?? "tick"; } },
        { "jtarray",  (a) => { _jtarray = true; } },
        { "jtsingle", (a) => { _jtsingle = true; } },
        { "jconvert", (a) => { _jconvert = a ?? "jsonconvert"; } },
        { "jctest",   (a) => { _jctest = a ?? "jsonconvert"; } },
        { "level",    (a) => { _startlevel = a; } },
        { "static",   (a) => { _static = true; } },
        { "take",     (a) => { _take = a.SafeIntParse() ?? 9999; } },
        { "debug",    (a) => { _settings.Add(Pair.Create("debug","true")); } },
        { "verbose",  (a) => { _settings.Add(Pair.Create("verbose_logging","true")); } },
        { "again",    (a) => { _settings.Add(Pair.Create("pause_on_again","true")); } },
        { "endlevel", (a) => { _settings.Add(Pair.Create("pause_at_end_level","true")); } },
        { "xdebug",   (a) => { _xdebug = true; } },
      };

    static void Main(string[] args) {
      Logger.Open(0);   // no default logging
      _output = Logger.Out;  // FIX:?
      Logger.WriteLine(Version);
      var options = OptionParser.Create(_options, _help);
      if (!options.Parse(args))
        return;

      // trigger options here to avoid having to change debug conditions
      if (_xdebug) {
        options.Parse(new string[] { "/jta", "testdata.json", "/f=by your side", "/0" });
        //options.Parse(new string[] { "/302", "/input=right,right,right,right" });
        //options.Parse(new string[] { "/jta", "testdata.json", "/f=drop swap", "/0" });
        //options.Parse(new string[] { "/deb", "/input=right,right" });
        //options.Parse(new string[] { @"..\demo\cratopia.txt", "/301", "/input=action,action,right" });
        //options.Parse(new string[] { "SimpleBlockPushing.txt", "/301", "/input=action,right" });
        //options.Parse(new string[] { "/jta", "testdata.json", "/f=whale", "/0" });
        //options.Parse(new string[] { "/jts", "testcase.json", "/3" });
        //Logger.Level = 301;
        //_inputs = "right,right";
        //_take = 1;
        //_jtarray = true;
        //_filter = "explod";
        //_jsonarray = true;
        //_jsonsingle = true;
        //_xpath = "testcase.json";
        //_gist = "0ebbe5e1f0761a87cc7aeace0d5e4b8e";
        //_jsonconvert = true;
        //_dir = true;
        //path = "2048.txt";
        //_encodelevel = true;
        //path = "../demo/modality.txt";
        //path = "../demo/ebony and ivory.txt";
        //path = "../demo/cute train.txt";
        //path = "../demo/atlas shrank.txt";
      }
      if (_jtarray) {
        foreach (var path in options.GetPathnames(_xpath ?? "testdata.json"))
          RunJsonTests(path, ReadFile(path));
      } else if (_jtsingle) {
        foreach (var path in options.GetPathnames(_xpath ?? "testcase.json")
          .Where(p => p.ToLower().Contains(_filter)))
            RunJsonTestCase(path, ReadFile(path));
      } else if (_jconvert != null) {
        foreach (var path in options.GetPathnames(_xpath ?? "testdata.json")
          .Where(p => p.ToLower().Contains(_filter)))
          RunJsonConvert(path, ReadFile(path), _jconvert);
      } else if (_jctest != null) {
        foreach (var file in options.GetPathnames(_xpath ?? "jsonconvert")
          .Where(p => p.ToLower().Contains(_filter)))
            RunJsonConvTest(file);
      } else if (_gist != null) {
        RunGist(_gist);

        //--- static test data cases -----------------------------------------------
      } else if (_static) {
        //if (_xdebug) _filter = "PRBG";
        _output.WriteLine($"Compiling test case: '{_filter}'");
        var testcase = StaticTestData.GetTestCase(_filter, "PRBG with winco",
          "(win):@(rul):[>p|r]->[>p|>r]");
        RunScript(_filter, testcase.Script, _inputs, _startlevel);

        //--- default script -----------------------------------------------
      } else {
        var defaultscript = "testgame.txt";
        //if (_xdebug) Logger.Level = 202;
        //if (_xdebug) _inputs = "right,right,right,down,right";
        //if (_xdebug) _inputs = "left,left";
        //if (_xdebug) _inputs = "right,right";
        //if (_xdebug) defaultscript = "SimpleBlockPushing.txt";
        foreach (var path in options.GetPathnames(defaultscript))   //())
          RunScript(path, ReadFile(path), _inputs, _startlevel);
      }
    }

    private static string ReadFile(string path) {
      if (path == "--") {
        if (Console.IsInputRedirected)
          using (var rdr = new StreamReader(Console.OpenStandardInput()))
            return rdr.ReadToEnd();
        _output.WriteLine($"*** Console not redirected!");
        return null;
      }
      using (var rdr = new StreamReader(path))
        return rdr.ReadToEnd();
    }

    static void RunGist(string gist) {
      var regex = new Regex("[0-9a-z]{32}");
      var match = regex.Match(gist);
      if (!match.Success)
        _output.WriteLine("*** invalid gist id: '{0}'", gist);
      else {
        _output.WriteLine("Loading gist: '{0}'", match.Value);
        var script = GistAccess.Load(match.Value);
        if (script == null)
          _output.WriteLine("*** gist not found: '{0}'", gist);
        else RunScript($"Gist: {_gist}", script, _inputs, _startlevel);
      }
    }

    // run a single converted JSON test
    static void RunJsonConvTest(string path) {
      //if (_xdebug) Logger.Level = 202;
      //if (_xdebug) _filter = "035";

      var title = "";
      var finalstate = "";
      var lookup = new Dictionary<string, Action<string>> {
        { "Json test case: ", s=> title = s },
        { "Inputs: ",       s=> _inputs = s },
        { "Target level: ", s=> _startlevel = s },
        { "Final state: ",  s=> finalstate = s.Replace(";", "\n") },
      };
      _output.WriteLine($"Json conv test: '{path}'.");
      var script = File.ReadAllText(path) + "\r\n";

      // scan the script looking for test params
      title = path;
      using (var rdr = new StringReader(script)) {
        var line = rdr.ReadLine();
        while (line != null) {
          var key = lookup.Keys.FirstOrDefault(k => line.Length > k.Length && k == line.Substring(0, k.Length));
          if (key != null) lookup[key](line.Substring(key.Length).Trim());
          line = rdr.ReadLine();
        }
      }
      RunJsonTest(title, script, _inputs, _startlevel, finalstate);
    }

    // expand one piece of JSON into a set of converted test cases in a directory, create if needed
    static void RunJsonConvert(string path, string script, string dir) {
      _output.WriteLine($"Converting JSON: '{path}' to '{dir}'");
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);
      JsonTestData.Setup(script);
      var testdata = JsonTestData.ClassTestCases;
      _output.WriteLine($"json: '{testdata.Count}'");
      var badchars = new HashSet<char>(Path.GetInvalidFileNameChars());
      var counter = 0;
      foreach (var testcase in testdata) {
        var title = new string(testcase.Title.Select(c => badchars.Contains(c) ? '-' : c).ToArray());
        var filename = String.Format("{0:D3}-{1}.txt", ++counter, title);
        var outpath = Path.Combine(dir, filename);
        _output.WriteLine($"title: '{testcase.Title} to {outpath}");
        using (var sw = new StreamWriter(outpath)) 
          sw.Write(JsonTestData.ConvertToScript(testcase));
      }
    }

    // run all the tests in one piece of JSON 
    static void RunJsonTests(string sourcename, string json) {
      //if (_xdebug) _filter = "test testing";
      //if (_xdebug) Logger.Level = 200;
      //if (_xdebug) Logger.Level = 302;
      _output.WriteLine($"Run JSON test: '{sourcename}'");

      JsonTestData.Setup(json);
      var testdata = JsonTestData.ClassTestCases;
      _output.WriteLine($"json test count: {testdata.Count}");
      var testcases = testdata
        .Where(t => t.Title.ToLower().Contains(_filter))
        .Take(_take);
      var counter = 0;
      foreach (var testcase in testcases)
        RunJsonTest(testcase.Title, testcase.Script, testcase.Inputs.Join(), testcase.TargetLevel.ToString(),
          testcase.FinalState, ++counter, testcase.RandomSeed);
    }

    // run the testcase in one piece of JSON 
    static void RunJsonTestCase(string sourcename, string json) {
      //if (_xdebug) _inputs = "";
      //if (_xdebug) Logger.Level = 3;
      //if (_xdebug) Logger.Level = 302;
      _output.WriteLine($"Run JSON test: '{sourcename}'");

      JsonTestData.SetupSingle(json);
      var testdata = JsonTestData.ClassTestCases;
      _output.WriteLine($"json test count: {testdata.Count}");
      var testcases = testdata
        .Take(_take);
      var counter = 0;
      foreach (var testcase in testcases)
        RunJsonTest(testcase.Title, testcase.Script, testcase.Inputs.Join(), testcase.TargetLevel.ToString(),
          testcase.FinalState, ++counter, testcase.RandomSeed);
    }

    static void RunJsonTest(string title, string script, string inputs, string startlevel, string finalstate,
      int counter = 0, double randomseed = 0) {
      _output.WriteLine("\n{0}: '{1}' inputs:{2} target:{3}",
        counter, title, inputs?.SplitTrim().Count, startlevel);

      var skipset = new HashSet<int> { 62, 74, 87 };

      var compiler = Compiler.Compile(title, new StringReader(script), Console.Out, _settings);
      if (compiler.Success) {
        if (skipset.Contains(counter)) Logger.WriteLine(">Skipped!");
        else {
          Logger.Pop();
          compiler.Model.Execute(startlevel, inputs);
          var last = compiler.EncodeLevel(compiler.Model.LastLevel);
          var matched = (last == finalstate);
          if (matched) Logger.WriteLine(">Passed.");
          else {
            Logger.WriteLine(">'{0}' error: final state MISMATCH!", title);
            var first = compiler.EncodeLevel(compiler.Model.FirstLevel);
            if (Logger.Level >= 2) compiler.Model.ShowLevel("json");
            Logger.WriteLine(2, "First:\n{0}", first);
            Logger.WriteLine(2, "Last:\n{0}", last);
            Logger.WriteLine(2, "Expected:\n{0}", finalstate);
          }
          _output.WriteLine(">>>Result {0}",
            Util.JoinNonEmpty(";", counter, title, inputs.SplitTrim().Count, startlevel, randomseed,
            compiler.Model.Ok ? "ok" : "not ok", matched ? "match" : "no match"));
        }
      }
    }

    static void RunScript(string sourcename, string script, string inputs = "", string startlevel = "0") {
      _output.WriteLine($"Compiling script {sourcename}");
      var reader = new StringReader(script);
      var compiler = Compiler.Compile(sourcename, reader, Console.Out, _settings);
      if (compiler.Success) {
        var model = compiler.Model;
        Logger.Write(3, "Colours: fg={0:x} bg={1:x}",
          model.GameDef.GetColour(OptionSetting.text_color, 0xffffff),
          model.GameDef.GetColour(OptionSetting.background_color, 0));
        Logger.Pop();
        Logger.WriteLine(1, $"Running '{sourcename}' level={startlevel} inputs={inputs}");
        model.Execute(startlevel, inputs);
        Logger.WriteLine(3, "Sounds: '{0}'", model.Sounds.Join());
        if (_encodelevel) {
          var last = compiler.EncodeLevel(compiler.Model.LastLevel);
          _output.WriteLine("Encoded: {0}", last);
        }
      }
    }
  }
}
