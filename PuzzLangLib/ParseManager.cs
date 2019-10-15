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
using DOLE;

namespace PuzzLangLib {

  /// <summary>
  /// A subrule is an ordered list of cells (disjoint subpatterns)
  /// </summary>
  class SubRule {
    internal IList<RulePrefix> Prefixes;              // rule prefixes
    internal IList<Direction> Directions;             // rule direction(s)
    internal IList<IList<RuleAtom>> Cells;  // sub-parts to match or act on

    public override string ToString() {
      var ss = Cells == null ? " " : Cells.Select(s => s.Join(" ")).Join(" | ");
      return String.Format("[ {0} ]", ss);
    }

    internal SubRule Clone() {
      return new SubRule {
        Cells = Cells.Select(c => new List<RuleAtom>(c)).Cast<IList<RuleAtom>>().ToList(),
      };
    }
  }

  /// <summary>
  /// The smallest atomic unit of a pattern or action
  /// </summary>
  class RuleAtom {
    internal static readonly ObjectSymbol Ellipsis = new ObjectSymbol();
    internal static readonly ObjectSymbol Empty = new ObjectSymbol();
    internal bool IsNegated { get; private set; }          // if this is a negative test
    internal Direction Direction { get; private set; }     // direction object should be moving 
    internal ObjectSymbol Symbol { get; private set; }     // object/set to match at location
    internal ObjectSymbol Variable { get; private set; }   // variable set with result of match
    internal ScriptFunctionCall Call { get; private set; }         // link to script call

    internal bool IsEllipsis { get { return Symbol == Ellipsis; } }
    internal bool IsRandom { get { return Direction == Direction.Random; } }
    internal bool HasObject { get { return Symbol.ObjectIds.Count > 0; } }
    internal bool HasVariable { get { return Variable != null; } }
    internal bool IsFunction { get { return Call != null; } }

    // a matching atom has the same symbol, negation and random, not necessarily the same direction
    internal bool Matches(RuleAtom other) {
      return other.Symbol == Symbol && other.IsNegated == IsNegated
        && other.IsEllipsis == IsEllipsis && other.IsRandom == IsRandom;
    }

    // note: object name used here, not tostring()
    public override string ToString() {
      if (IsEllipsis) return "...";
      return Util.JoinNonEmpty(" ", IsNegated ? "no" : "",
        Direction == Direction.None ? "" : Direction.ToString(), 
        Variable == null ? Symbol.Name : Variable.Name + ":" + Symbol.Name);
    }

    internal static RuleAtom Create(bool isnegated, Direction direction, ObjectSymbol objects, 
                                    ObjectSymbol variable, ScriptFunctionCall call) {
      return new RuleAtom {
        IsNegated = isnegated,
        Direction = direction,
        Symbol = objects,
        Variable = variable,
        Call = call,
      };
    }

    // Clone an atom but change the direction
    internal RuleAtom Clone(Direction direction) {
      var rm = this.MemberwiseClone() as RuleAtom;
      rm.Direction = direction;
      return rm;
    }

    // Clone an atom but change the symbol
    internal RuleAtom Clone(ObjectSymbol symbol) {
      var rm = this.MemberwiseClone() as RuleAtom;
      rm.Symbol = symbol;
      return rm;
    }
  }

  /// <summary>
  /// Holds fully expanded rule before compilation
  /// </summary>
  class AtomicRule {
    internal int RuleId;
    internal HashSet<RulePrefix> Prefixes;
    internal HashSet<Direction> Directions;
    internal IList<SubRule> Patterns;
    internal IList<SubRule> Actions;
    internal IList<CommandCall> Commands;

    public bool IsFinal { get { return Prefixes.Contains(RulePrefix.Final); } }
    public bool HasAction { get { return Actions.Count > 0; } }
    public bool HasCommand { get { return Commands.Count > 0; } }

    public override string ToString() {
      return String.Format("Rule {0}: {1} {2} {3} -> {4} {5}",
        RuleId, Prefixes.Join(), Directions.Join(), Patterns.Join(" "), Actions.Join(" "), Commands.Join());
    }

    // partial deep clone -- use with care!
    internal AtomicRule Clone() {
      var newrule = this.MemberwiseClone() as AtomicRule;
      newrule.Patterns = Patterns.Select(p => p.Clone()).ToList();
      newrule.Actions = Actions.Select(p => p.Clone()).ToList();
      return newrule;
    }
  }

  /// <summary>
  /// Grouping of objects created by AND or OR or combinations thereof
  /// </summary>
  class ObjectSymbol {
    internal string Name;
    internal SymbolKind Kind;
    internal IList<int> ObjectIds = new List<int>();

    public bool IsObject { get { return Kind != SymbolKind.None; }}

    // true if references the same objects in the same way
    // CHECK: same ids in different sequence will fail
    internal bool Matches(ObjectSymbol other) {
      return ObjectIds.SequenceEqual(other.ObjectIds);
    }

    public override string ToString() {
      return String.Format("{0}:{1}({2})", Name, Kind, ObjectIds.Join());
    }

    internal void SetVarRef(int varid) {
      if (Kind != SymbolKind.RefObject) throw Error.Assert($"ref {varid}");
      ObjectIds = new List<int> { varid };
    }
  }

  /// <summary>
  /// Implements parser support with access to game data, colour, etc
  /// </summary>
  internal class ParseManager {
    const SoundTrigger MaxWithObject_ = SoundTrigger.Cantmove;
    const SoundTrigger MinSound_ = SoundTrigger.Sfx0;

    public int ErrorCount { get; set; }
    public int WarningCount { get; private set; }
    public int RuleCount { get { return AtomicRules.Count; } }
    internal GameDef GameDef { get { return _gamedef; } }
    internal SettingsParser SettingsParser { get; private set; }
    internal ColourParser ColourParser { get; private set; }
    internal PuzzLangParser PegParser { get; private set; }
    // access to expanded rules for testing
    internal IList<AtomicRule> AtomicRules { get { return _atomicrules; } }
    internal IEnumerable<ObjectSymbol> Symbols { get { return _symbols.Values; } }

    int _linenumber { get { return PegParser.LineNumber; } }

    string _sourcename;
    TextWriter _out;
    GameDef _gamedef;
    RuleBuilder _rulebuilder;
    Dictionary<string, ObjectSymbol> _symbols = new Dictionary<string, ObjectSymbol>(StringComparer.OrdinalIgnoreCase);
    List<AtomicRule> _atomicrules = new List<AtomicRule>();

    static readonly Dictionary<string, Direction> _arrowlookup =
      new Dictionary<string, Direction>(StringComparer.OrdinalIgnoreCase) {
      {  ">", Direction.RightArrow_ },
      {  "<", Direction.LeftArrow_ },
      {  "^", Direction.UpArrow_ },
      {  "v", Direction.DownArrow_ },
    };

    bool _inloop = false;
    int _loopcounter;

    static internal ParseManager Create(string source, TextWriter tw, IList<Pair<string,string>> settings = null) {
      var cp = new ColourParser();
      var gamedef = new GameDef();
      gamedef.Scripts = ScriptManager.Create(gamedef);
      gamedef.Palette = cp.GetPalette();
      var gp = new ParseManager {
        _sourcename = source,
        _out = tw,
        _gamedef = gamedef,
        SettingsParser = new SettingsParser { GameDef = gamedef, Colour = cp },
        ColourParser = cp,
      };
      if (settings != null)
        foreach (var setting in settings)
          gp.SettingsParser.Parse(setting.Item1, setting.Item2);
      gp.PegParser = PuzzLangParser.Create(tw, gp);
      gp._rulebuilder = RuleBuilder.Create(gp);
      return gp;
    }

    internal string GetSymbolName(int id) {
      return _gamedef.PuzzleObjects[id - 1].Name;  // TODO: move into GameDef?
    }

    internal ObjectSymbol GetSymbol(int id) {
      return _symbols[GetSymbolName(id)];
    }

    internal Direction ParseDirection(string value) {
      return (value == null || value.EndsWith("_")) ? Direction.None
        : value.SafeEnumParse<Direction>() ?? _arrowlookup.SafeLookup(value);  // None if not found???
    }

    internal ObjectSymbol ParseSymbol(string name) {
      return (name == null) ? null : _symbols.SafeLookup(name);
    }

    // get a set of objects ids defined by OR to use as a property
    internal IList<int> ParseProperty(string name) {
      var sym = ParseSymbol(name);
      Logger.Assert(sym != null, name);
      if (!(sym.Kind == SymbolKind.Real || sym.Kind == SymbolKind.Property))
        CompileError("object '{0}' may only use OR", name);
      return sym.ObjectIds;
    }

    // get a set of objects ids defined by AND to use as an aggregate
    internal IList<int> ParseAggregate(string name) {
      var sym = ParseSymbol(name);
      Logger.Assert(sym != null, name);
      if (!(sym.Kind == SymbolKind.Real || sym.Kind == SymbolKind.Aggregate))
        CompileError("object '{0}' may only use AND", name);
      return sym.ObjectIds;
    }

    // define or return a variable, null if cannot
    internal ObjectSymbol ParseVariable(string name) {
      var sym = _symbols.SafeLookup(name);
      if (sym == null) {
        _symbols[name] = sym = new ObjectSymbol {
          Name = name,
          Kind = SymbolKind.RefObject,
          ObjectIds = new List<int>(),   // must get added later
        };
      }
      return sym.Kind == SymbolKind.RefObject ? sym : null;
    }

    internal RulePrefix ParseRulePrefix(string value) {
      if (value == null || value.EndsWith("_")) return RulePrefix.None;
      if (value == "+") return RulePrefix.Plus_;
      return value.SafeEnumParse<RulePrefix>() ?? RulePrefix.None;
    }

    internal CommandName ParseCommandName(string value) {
      if (value == null || value.EndsWith("_")) return CommandName.None;
      return value.SafeEnumParse<CommandName>() ?? CommandName.None;
    }

    internal SoundTrigger ParseTriggerEvent(string value) {
      if (value == null || value.EndsWith("_")) return SoundTrigger.None;
      return value.SafeEnumParse<SoundTrigger>() ?? SoundTrigger.None;
    }

    internal ScriptFunctionCall ParseScriptCall(ScriptFunctionCall call) {
      call.CheckParse();
      return call;
    }

    // Add a named sprite object to symbol table and object list
    internal int AddObject(string name, Pair<float,float> pivot, float scale, int width, IList<int> grid,
      int textcolour, string text) {
      _symbols[name] = new ObjectSymbol {
        Name = name,
        Kind = SymbolKind.Real,
        ObjectIds = new List<int> { _gamedef.PuzzleObjects.Count + 1 },
      };
      _gamedef.PuzzleObjects.Add(new PuzzleObject {
        Name = name,
        Layer = 0,      // set later
        Pivot = pivot,
        Scale = scale,
        Width = width,
        Sprite = grid,
        TextColour = textcolour,
        Text = text,
      });
      DebugLog("Sprite Object {0}: '{1}' {2}x{3} {4} {5} '{6}'",
        _gamedef.PuzzleObjects.Count, name, width, grid.Count / width, scale, pivot, text);
      Logger.WriteLine(4, "{0}", grid.Select(c => String.Format("{0:X}", c)).Join());
      return _gamedef.PuzzleObjects.Count;
    }

    // Add object group to symbol table
    internal void AddObjectDef(string name, IList<string> others, LogicOperator oper) {
      var sym = CollectObjects(others.Select(o => ParseSymbol(o)).ToList());
      sym.Name = name;

      // if it's a collection of all objects then make a decision
      if (sym.Kind == SymbolKind.Real && sym.ObjectIds.Count > 1)
        sym.Kind = (oper == LogicOperator.And) ? SymbolKind.Aggregate : SymbolKind.Property;
      
      // these are bad, otherwise use whatever we have
      if ((oper == LogicOperator.And && sym.Kind != SymbolKind.Aggregate)
        || (oper == LogicOperator.Or && sym.Kind != SymbolKind.Property)) {
        CompileError("incompatible symbols: {0}", others.Join());
      }
      _symbols[name] = sym;
      var decode = name.All(c => (int)c <= 255) ? ""
        : " (" + name.Select(c => ((int)c).ToString("X")).Join() + ")";
      DebugLog("Define '{0}'{1}: {2} <{3}>",
        name, decode, sym.Kind, sym.ObjectIds.Select(o => _gamedef.ShowName(o)).Join());
    }

    // collect named objects, check out compatibility
    // returns a partially filled-in symbol -- needs name and final kind
    ObjectSymbol CollectObjects(IEnumerable<ObjectSymbol> syms) {
      var kinds = syms.Select(s => s.Kind).Distinct();
      if (kinds.Contains(SymbolKind.Mixed)) throw Error.Assert("stored mixed");
      var kind = (kinds.Count() == 1) ? kinds.First()
        : (kinds.Count() == 2 && kinds.Contains(SymbolKind.Real)) ? kinds.First(k => k != SymbolKind.Real)
        : SymbolKind.Mixed;
      var objects = syms.SelectMany(s => s.ObjectIds);
      return new ObjectSymbol {
        Kind = kind,
        ObjectIds = objects.ToList(),
      };
    }

    internal void AddSound(string trigger, string seed, string objct, IList<string> directions) {
      Logger.WriteLine(2, "Sound {0}: {1} {2} {3}", trigger, seed, objct, directions.Join());
      var trig = ParseTriggerEvent(trigger);
      if (trig <= MaxWithObject_) {
        var obj = ParseProperty(objct);
        if (directions != null && directions.Count > 0)
          _gamedef.AddObjectSound(trig, obj, directions.Select(c => ParseDirection(c)), seed);
        else _gamedef.AddObjectSound(trig, obj, null, seed);
      } else _gamedef.AddGameSound(trig, seed);
    }

    // Add a layer and mark objects that belong to it (first layer is 1)
    internal void AddLayer(IList<string> idents) {
      var symbols = CollectObjects(idents.Select(o => ParseSymbol(o)).ToList());
      var layer = ++_gamedef.LayerCount;
      foreach (var obj in symbols.ObjectIds)
        _gamedef.SetObjectLayer(obj, layer);
      Logger.WriteLine(2, "Layer {0}: {1}", layer, idents.Join());
    }

    // add a win condition as a relationship between two masks
    internal void AddWin(string condition, string ident, string other, ScriptFunctionCall call) {
      var winco = condition.SafeEnumParse<MatchOperator>() ?? MatchOperator.None;
      // Any is an (undocumented) synonym for Some
      _gamedef.AddWinCondition(winco == MatchOperator.Any ? MatchOperator.Some : winco,
        ParseProperty(ident), 
        (other == null) ? null : ParseProperty(other), 
        (call == null) ? null : ParseScriptCall(call));
    }

    // update loop state, return true if ok
    internal bool AddLoop(RulePrefix loop) {
      _inloop = !_inloop;
      if (_inloop) ++_loopcounter;
      Logger.WriteLine(2, $"Add Loop '{loop}'");
      return _inloop == (loop == RulePrefix.Startloop_);
    }


    // expand rule and add compiled rule to game data
    internal void AddRule(int ruleid, IList<RulePrefix> prefixes, IList<Direction> directions,
      IList<SubRule> pattern, IList<SubRule> action, IList<CommandCall> commands) {

      // create the base rule with unexpanded directions
      var arule = new AtomicRule {
        RuleId = ruleid,
        Prefixes = new HashSet<RulePrefix>(prefixes),
        Directions = new HashSet<Direction>(directions),
        Patterns = pattern,
        Actions = action,
        Commands = commands,
      };

      // expand rules, all going in the same group, then compile them
      var rulegroup = _gamedef.GetRuleGroup(arule.Prefixes, _inloop ? _loopcounter : 0);
      foreach (var prule in _rulebuilder.BuildRuleList(arule, rulegroup.Id)) {
        _atomicrules.Add(prule);
        rulegroup.Rules.Add(_rulebuilder.CompileRule(prule));
        rulegroup.IsFinal |= prule.IsFinal;       // propagate retry suppression to group
      }
    }

    internal CommandCall CreateCommandCall(string ident, ScriptFunctionCall call, string argument) {
      // if the command is a sound then it's a sound command
      var sound = ParseTriggerEvent(ident);
      if (sound >= MinSound_) {
        var seed = _gamedef.GameSounds.SafeLookup(sound);
        if (seed == null) CompileWarn("sound not defined: {0}", ident);
        return CommandCall.Create(CommandName.Sound_, seed, call);
      }
      // is it a normal command?
      var command = ParseCommandName(ident);
      if (command != CommandName.None)
        return CommandCall.Create(command, argument, call);
      var sym = ParseSymbol(ident);
      if (sym != null)
        return CommandCall.Create(CommandName.Text_, argument, call, sym);
      return null;
    }

    internal CommandCall CreateCommandCall(ScriptFunctionCall call) {
      return new CommandCall {
        funcCall = ParseScriptCall(call),
      };
    }

    // create temporary rule subpart info
    internal RuleAtom CreateRuleCell(string noflag, string direction, string prefix, string ident, ScriptFunctionCall call) {
      return RuleAtom.Create(noflag != null, ParseDirection(direction),
        (ident == null) ? RuleAtom.Empty :
        (ident == "...") ? RuleAtom.Ellipsis : ParseSymbol(ident),
        prefix == null ? null : ParseVariable(prefix),
        call == null ? null : ParseScriptCall(call));
    }

    // Parse and add a level
    internal void AddLevel(IList<string> lines) {
      if (_gamedef.Background == null) {
        var background = _symbols.SafeLookup("background");
        if (background == null) {
          CompileError("background not defined");
          _gamedef.Background = new List<int> { 0 };
        } else _gamedef.Background = background.ObjectIds;
      }
      var level = ParseLevel(lines);
      DebugLog("Level {0}: {1}x{2}x{3}", _gamedef.LevelIndex.Count, level.Height, level.Width, level.Depth);
      _gamedef.AddLevel(level);
    }

    internal void AddMessage(string message) {
      DebugLog("Message {0}: '{1}'", _gamedef.Messages.Count, message);
      _gamedef.AddMessage(message);
    }

    internal void AddScript(IList<string> lines) {
      DebugLog("Script: '{0}'", lines.FirstOrDefault());
      _gamedef.Scripts.AddScript(this, lines);
    }

    // Parse an array of grid lines and depth, converting glyphs into objects in layers
    Level ParseLevel(IList<string> gridlines) {
      var width = gridlines.Max(g => g.Length);
      if (gridlines.Any(l => l.Length != width))
        CompileWarn("short level line(s)");
      var bgobjects = new List<int>();

      // create empty level of required size
      var level = Level.Create(width, gridlines.Count, _gamedef.LayerCount,
        (_gamedef.Levels.Count + 1).ToString());

      // parse each line of the grid
      for (int y = 0; y < gridlines.Count; y++) {
        var line = gridlines[y];
        if (line.Length < width) 
          line += new string(line.Last(), width - line.Length);

        // parse each column (which must be a glyph object or pile)
        // track any background objects seen
        for (int x = 0; x < line.Length; x++) {
          foreach (var obj in ParseAggregate(line.Substring(x, 1))) {
            if (_gamedef.Background.Contains(obj))
              bgobjects.Add(obj);
            var layer = _gamedef.GetLayer(obj);
            if (layer != 0) level[x, y, layer] = obj;
          }
        }
      }
      // now fill in the background
      for (int cellindex = 0; cellindex < level.Length; cellindex++) {
        // a tile with no background gets the lowest number found on the level
        if (!level.GetObjects(cellindex).Any(o => bgobjects.Contains(o))) {
          var bgobj = (bgobjects.Count > 0) ? bgobjects.Min() : _gamedef.Background.Min();
          level[cellindex, _gamedef.GetLayer(bgobj)] = bgobj;
        }
      }
      return level;
    }

    // Check validity of data
    internal void CheckData() {
      var player = _symbols.SafeLookup("player");
      var clickable = _symbols.SafeLookup("clickable");
      if (player == null && clickable == null) CompileError("must define player or clickable");
      if (player != null) {
        _gamedef.Players = ParseProperty("player");
        if (!_gamedef.BoolSettings.ContainsKey(OptionSetting.arrows))
          _gamedef.BoolSettings[OptionSetting.arrows] = true;
        if (!_gamedef.BoolSettings.ContainsKey(OptionSetting.action))
          _gamedef.BoolSettings[OptionSetting.action] = true;
      }
      if (clickable != null) {
        _gamedef.Clickables = ParseProperty("clickable");
        if (!_gamedef.BoolSettings.ContainsKey(OptionSetting.click))
          _gamedef.BoolSettings[OptionSetting.click] = true;
      }
      var nolayer = _gamedef.PuzzleObjects.Where(o => o.Layer == 0);
      if (nolayer.Count() > 0)
        CompileError("not in any layer: {0}", nolayer.Select(o=>o.Name).Distinct().Join());
      if (_inloop) CompileError("missing endloop");
    }

    internal void DebugLog(string format, params object[] args) {
      if (GameDef.GetSetting(OptionSetting.debug, false))
        _out.WriteLine(String.Format(format, args));
      else Logger.WriteLine(1, "% " + format, args);
    }

    internal void CompileError(string fmt, params object[] args) {
      ErrorCount++;
      throw new DOLEException("*** '{0}' at {1}: compile error: {2}".Fmt(_sourcename, _linenumber, String.Format(fmt, args)));
    }
    internal void CompileWarn(string fmt, params object[] args) {
      _out.WriteLine("*** '{0}' at {1}: compile warning: {2}", _sourcename, _linenumber, String.Format(fmt, args));
      WarningCount++;
    }

  }

}