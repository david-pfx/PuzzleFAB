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
using System.IO;
using Pegasus.Common;
using DOLE;
using System.Collections.Generic;
using System.Linq;

namespace PuzzLangLib {
  public class Compiler {
    public TextWriter Out { get; private set; }
    public string SourceName { get; private set; }
    public bool Success { get { return _parser.ErrorCount == 0; } }
    public GameModel Model { get; private set; }
    public IList<Pair<string, string>> Settings = null;

    internal ParseManager _parser;
    bool _parseerror = false;

    public static Compiler Compile(string sourcename, TextReader reader, TextWriter output, 
      IList<Pair<string,string>> settings = null) {
      var ret = new Compiler {
        SourceName = sourcename,
        Out = output,
        Settings = settings,
      };
      ret.Compile(reader);
      if (ret.Success)
        ret.Model = GameModel.Create(output, ret._parser.GameDef, sourcename);
      return ret;
    }

    public string EncodeLevel(Level level) {
      //var level = _engine.LastLevel;
      var sw = new StringWriter();
      var seenlookup = new Dictionary<string, int>();
      var seen = 0;
      var linebreak = 0;
      for (int x = 0; x < level.Width; x++) {
        for (int y = 0; y < level.Height; y++) {
          var objs = new List<string>();
          for (int z = 1; z <= level.Depth; z++) {
            var obj = level[x, y, z];
            if (obj != 0) {
              objs.Add(_parser.SymbolName(obj).ToLower());
            }
          }
          objs.Sort();
          var name = objs.Join(" ");
          if (!seenlookup.ContainsKey(name)) {
            seenlookup[name] = seen++;
            sw.Write(name + ":");
          }
          sw.Write(seenlookup[name] + ",");
          if (++linebreak % level.Width == 0) sw.Write("\n");
        }
      }
      return sw.ToString();
    }

    void Compile(TextReader reader) {
      _parser = ParseManager.Create(SourceName, Out, Settings);
      var program = reader.ReadToEnd() + "\r\n";  // just too hard to parse missing EOL
      try {
        var result = _parser.PegParser.Parse(program);
        var fmt = _parser.ErrorCount > 0 ? "Compiled '{0}' with errors, count: {1} warnings: {2}."
          : _parser.WarningCount > 0 ? "Compiled '{0}' with warnings, count: {2}." : "Compiled '{0}' with no errors.";
        Out.WriteLine(fmt, SourceName, _parser.ErrorCount, _parser.WarningCount);
      } catch (FormatException e) {
        if (e.Data.Contains("cursor")) {
          var state = e.Data["cursor"] as Cursor;
          var offset = Math.Max(0, state.Location - 30);
          var source = program.Substring(offset, state.Location - offset)
            + "(^)" + program.Substring(state.Location);
          Out.WriteLine(source.Replace('\r', '\\').Replace('\n', '\\').Shorten(78));
          Out.WriteLine($"*** '{SourceName}' at {state.Line},{state.Column}: parse error: {e.Message}");
          _parseerror = true;
        } else {
          Out.WriteLine($"*** '{SourceName}': unexpected exception: {e.ToString()}");
          _parseerror = true;
        }
      }
      if (_parseerror)
        ++_parser.ErrorCount;
      else _parser.CheckData();
    }
  }

}
