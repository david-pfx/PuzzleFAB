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
    // source name to display in messages
    public string SourceName { get; private set; }
    // settings to use for compile and model
    public IList<Pair<string, string>> Settings = null;
    // output produced during compile
    public TextWriter Out { get; private set; }
    // result of compile, if successful
    public GameModel Model { get; private set; }
    // result of compile
    public bool Success { get { return _parser.ErrorCount == 0; } }
    // explanation, if compile failed
    public string Message { get; private set; }
    // for testing purposes only
    public IList<string> RuleExpansions { get { return _parser.AtomicRules.Select(r=>r.ToString()).ToList(); } }

    internal ParseManager _parser;

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

    // encode level per PuzzleScript format
    public string EncodeLevel(Level level) {
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
              objs.Add(_parser.GetSymbolName(obj).ToLower());
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

    // decode level back to compiler input (but unknown combos will be '?')
    public string DecodeLevel(Level level) {
      var sw = new StringWriter();
      var lookup = _parser.Symbols
        .Where(s => s.Name.Length == 1)
        .ToDictionary(k => k.ObjectIds.OrderBy(i => i).Join(), v => v.Name);
      for (int x = 0; x < level.Length; x++) {
        if (x > 0 && x % level.Width == 0) sw.Write(" ;");
        var objs = level.GetObjects(x).OrderBy(i => i);
        var objsexbg = (objs.Count() == 1) ? objs : objs.Skip(1);
        sw.Write(lookup.SafeLookup(objsexbg.Join()) ?? "?");
      }
      return sw.ToString();
    }

    void Compile(TextReader reader) {
      _parser = ParseManager.Create(SourceName, Out, Settings);
      var program = reader.ReadToEnd() + "\r\n";  // just too hard to parse missing EOL
      try {
        _parser.PegParser.Parse(program);
        _parser.CheckData();
      } catch (FormatException e) {
        if (e.Data.Contains("cursor")) {
          var state = e.Data["cursor"] as Cursor;
          var offset = Math.Max(0, state.Location - 30);
          var source = (program.Substring(offset, state.Location - offset)
            + "(^)" + program.Substring(state.Location))
            .Replace("\r", "").Replace("\n", ";");
          Message = "{0}\n*** '{1}' at {2},{3}: parse error: {4}".Fmt(source.Shorten(78),
            SourceName, state.Line, state.Column, e.Message);
          Out.WriteLine(Message);
          ++_parser.ErrorCount;
        } else {
          Out.WriteLine($"*** '{SourceName}': unexpected exception: {e.ToString()}");
          ++_parser.ErrorCount;
        }
      } catch (DOLEException e) {
        Message = e.Message;
        Out.WriteLine(Message);
        ++_parser.ErrorCount;
      }
      var fmt = _parser.ErrorCount > 0 ? "Compiled '{0}' with errors, count: {1} warnings: {2}."
        : _parser.WarningCount > 0 ? "Compiled '{0}' with warnings, count: {2}." 
        : "Compiled '{0}' with no errors, {3} rules.";
      Out.WriteLine(fmt, SourceName, _parser.ErrorCount, _parser.WarningCount, _parser.RuleCount);
    }
  }

}
