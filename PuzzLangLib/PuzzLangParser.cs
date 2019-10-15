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
using Pegasus.Common;
using Pegasus.Common.Tracing;
using DOLE;

namespace PuzzLangLib {
  /// <summary>
  /// Extensions to the generated parser
  /// </summary>
  partial class PuzzLangParser {
    ParseManager _manager { get; set; }
    internal int LineNumber { get { return _prevline; } }
    int _lastline = 1; // line number after printing
    int _prevline = 1; // line number before printing (more useful)
    int _lastloc = 0;

    internal static PuzzLangParser Create(TextWriter tw, ParseManager manager) {
      return new PuzzLangParser {
        Tracer = new Tracer { Out = tw },
        _manager = manager,
      };
    }

    string Eol(Cursor state) {
      if (state.Location > _lastloc) {
        _prevline = _lastline;
        var lines = state.Subject.Substring(_lastloc, state.Location - _lastloc).Split('\n');
        foreach (var line in lines.Take(lines.Length - 1)) {
          if (Logger.Level >= 2)
            Logger.WriteLine("{0,4} : {1}", _lastline, line.Trim());
          _lastline++;
        }
        _lastloc = state.Location;
      }
      return null;
    }

    string DefSetting(Cursor cursor, string name, string value) {
      if (!_manager.SettingsParser.Parse(name, value)) ParseError(cursor, $"invalid setting '{name}' value '{value}'");
      Logger.WriteLine(2, $"Setting {name}='{value}'");
      return null;
    }

    string DefObject(Cursor cursor, string ident, string glyph, IList<string> justify, string scale, 
      IList<string> colours, string message, IList<string> gridlines) {
      var clist = colours.Select(c => _manager.ColourParser.ParseColour(c)).ToList();
      var jlist = justify.Select(d => _manager.ParseDirection(d));
      var jx = (jlist.Contains(Direction.Left) ? -1f : 0f) + (jlist.Contains(Direction.Right) ? 1f : 0f);
      var jy = (jlist.Contains(Direction.Down) ? -1f : 0f) + (jlist.Contains(Direction.Up) ? 1f : 0f);
      var jpivot = Pair.Create(jx, jy);
      var nscale = (scale == null) ? 1.0 : scale.SafeDoubleParse();
      if (scale != null && (nscale == null || nscale <= 0))
        ParseError(cursor, $"invalid scale: {scale}");
      if (gridlines.Count == 0) {
        _manager.AddObject(ident, jpivot, (float)nscale.Value, 1, new int[] { clist.First() }, clist.Last(), message);
      } else {
        var grid = ParseGrid(cursor, gridlines, clist);
        _manager.AddObject(ident, jpivot, (float)nscale.Value, gridlines.Count, grid, clist.Last(), message);
      }
      if (glyph != null) _manager.AddObjectDef(glyph, new List<string> { ident }, LogicOperator.None);
      return null;
    }

    List<int> ParseGrid(Cursor cursor, IList<string> gridlines, List<int> clist) {
      if (!(gridlines.All(s => s.Length == gridlines.Count)))
        ParseError(cursor, $"sprite not square");
      var maxchar = '0' + clist.Count - 1;
      var badchar = gridlines.Join("")
        .Where(c => c > maxchar).ToList();
      if (badchar.Count > 0)
        ParseError(cursor, $"sprite char(s) '{badchar.Join()}' not valid");
      var grid = gridlines.SelectMany(s => s.Select(c => c == '.' ? -1 : clist[c - '0'])).ToList();
      return grid;
    }

    //--- predicates for parsing

    bool IsSetting(string value) {
      if (value == null || value.EndsWith("_")) return false;
      return value.SafeEnumParse<OptionSetting>() != null;
    }

    bool IsColour(string value) {
      return _manager.ColourParser.IsColour(value);
    }
    bool IsObject(string value) {
      return _manager.ParseSymbol(value).Kind == SymbolKind.Real;
    }
    bool IsDefined(string value) {
      return _manager.ParseSymbol(value) != null;
    }
    bool IsVariable(string value) {
      return _manager.ParseVariable(value) != null;
    }
    bool IsWinAction(string value) {
      return (value.SafeEnumParse<MatchOperator>() ?? MatchOperator.None) != MatchOperator.None;
    }
    bool IsSoundTrigger(string value) {
      return _manager.ParseTriggerEvent(value) != SoundTrigger.None;
    }
    bool IsRuleCommand(string value) {
      return _manager.ParseCommandName(value) != CommandName.None;
    }
    bool IsRulePrefix(string value) {
      return _manager.ParseRulePrefix(value) != RulePrefix.None;
    }
    bool IsRuleDirection(string value) {
      var dir = _manager.ParseDirection(value);
      return dir != Direction.None && dir <= Direction.Orthogonal;
    }
    bool IsMoveDirection(string value) {
      var dir = _manager.ParseDirection(value);
      return dir != Direction.None; ;
    }
    bool IsJustify(string value) {
      var dir = _manager.ParseDirection(value);
      return dir == Direction.Left || dir == Direction.Right|| dir == Direction.Up || dir == Direction.Down;
    }

    string DefLegend(string ident, IList<string> objects, string andor) {
      var op = (objects.Count == 1) ? LogicOperator.None
        : andor.SafeEnumParse<LogicOperator>() ?? LogicOperator.None;
      if (IsDefined(ident))
        _manager.CompileWarn("'{0}' already defined", ident);
      else {
        _manager.AddObjectDef(ident, objects, op);
      }
      return null;
    }

    // add sound trigger
    string DefSound(string objct, string trigger, IList<string> directions, string seed) {
      _manager.AddSound(trigger, seed, objct, directions);
      return null;
    }
    string DefCollision(Cursor cursor, IList<string> idents) {
      _manager.AddLayer(idents);
      return null;
    }
    string Loop(Cursor state, string loop) {
      if (!_manager.AddLoop(loop.ToLower() == "startloop" ? RulePrefix.Startloop_ : RulePrefix.Endloop_))
        ParseError(state, $"nesting error: {loop}");
      return null;
    }

    // define a rule composes of patterns, actions and commands
    string DefRule(Cursor state, IList<SubRule> pattern, IList<SubRule> action, IList<CommandCall> commands) {

      if (action.Count == 0) {
        if (action == null) ParseError(state, "rule missing action or command");
      } else {
        var ok = (pattern.Count == action.Count)
          && Enumerable.Range(0, pattern.Count).All(x => pattern[x].Cells.Count == action[x].Cells.Count);
        if (!ok) ParseError(state, "rule pattern and action size mismatch");
      }

      // rule directions parsed on first subrule; others left as future enhancement
      if (pattern.Skip(1).Any(p => p.Prefixes.Any() || p.Directions.Any()) || action.Any(p => p.Prefixes.Any() || p.Directions.Any()))
        _manager.CompileWarn("subrule prefixes or directions ignored");

      // just use the prefix and directions from first pattern subrule
      _manager.AddRule(LineNumber, pattern.First().Prefixes, pattern.First().Directions, pattern, action, commands);
      return null;
    }

    // define a rule sequence (pattern or action) as a prefix and a list of cells
    // prefix includes both rule modifiers and directions -- separate them out
    SubRule DefSubRule(IList<string> idents, IList<IList<RuleAtom>> cells) {
      var prefixes = idents
        .Where(i=>IsRulePrefix(i))
        .Select(p=>_manager.ParseRulePrefix(p));
      var directions = idents
        .Where(i=>IsRuleDirection(i))
        .Select(p=>_manager.ParseDirection(p));
      return new SubRule {
        Prefixes = prefixes.ToList(),
        Directions = directions.ToList(),
        Cells = cells,
      };
    }

    // define a cell as a special value
    IList<RuleAtom> DefRuleAtomList(string arg) {
      return new List<RuleAtom> {
        _manager.CreateRuleCell(null, null, null, arg, null)
      };
    }
    
    // define a cell as a list of matches
    RuleAtom DefRuleAtom(string noflag, string direction, string prefix, string ident, ScriptFunctionCall call) {
      return _manager.CreateRuleCell(noflag, direction, prefix, ident, call);
    }

    // define a command call
    CommandCall DefCommandCall(Cursor cursor, string ident, ScriptFunctionCall call, string argument) {
      var cc = _manager.CreateCommandCall(ident, call, argument);
      if (cc == null)
        ParseError(cursor, "bad command: {0}", ident);
      return cc;
    }

    CommandCall DefCommandScript(ScriptFunctionCall call) {
      return _manager.CreateCommandCall(call);
    }

    string DefWinCondition(string condition, string ident, string other, ScriptFunctionCall call) {
      _manager.AddWin(condition, ident, other, call);
      Logger.WriteLine(2, $">Win {condition},{ident},{other},{call}");
      return null;
    }

    // parse and define a game level
    string DefLevel(Cursor cursor, IList<string> lines) {
      var bad = lines.Join("").Where(c => !IsDefined(new String(c, 1))).ToList();
      if (bad.Count > 0)
        ParseError(cursor, $"level char '{bad.Join("")}' not valid");
      _manager.AddLevel(lines);
      return null;
    }

    // parse and define a message
    string DefMessage(string message) {
      _manager.AddMessage(message);
      return null;
    }

    // parse and define a message
    string DefScript(Cursor cursor, IList<string> lines) {
      _manager.AddScript(lines);
      return null;
    }

    // create script call, but leave parsing until later
    ScriptFunctionCall DefScriptCall(string ident, IList<string> args) {
      return _manager.GameDef.Scripts.CreateFunctionCall(_manager, ident, args);
    }

    void ParseError(Cursor cursor, string format, params string[] args) {
      throw ExceptionHelper(cursor, state => String.Format(format, args));
    }

    T Single<T>(IList<T> list) where T : class {
      return list.FirstOrDefault();
    }

    List<T> ListOf<T>(params T[] args) {
      return args.ToList();
    }
  }

  /// <summary>
  /// 
  /// </summary>
  internal class Tracer : ITracer {
    internal TextWriter Out { get; set; }
    internal bool Enabled { get; set; }

    public void TraceCacheHit<T>(string ruleName, Cursor cursor, CacheKey cacheKey, IParseResult<T> parseResult) {
    }

    public void TraceCacheMiss(string ruleName, Cursor cursor, CacheKey cacheKey) {
    }

    public void TraceInfo(string ruleName, Cursor cursor, string info) {
      if (Enabled) Out.WriteLine($"Info rule: {ruleName} info: {info}");
    }

    public void TraceRuleEnter(string ruleName, Cursor cursor) {
      if (Enabled) Out.WriteLine($"Enter rule: {ruleName}");
    }

    public void TraceRuleExit<T>(string ruleName, Cursor cursor, IParseResult<T> parseResult) {
      if (Enabled) Out.WriteLine($"Exit rule: {ruleName} result: {parseResult}");
    }
  }
}
