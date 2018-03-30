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
    internal IList<Direction> Directions;             // direction(s) to look for match
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
    internal bool IsNegated { get; private set; }           // if this is a negative test
    internal Direction Direction { get; private set; }   // direction object should be moving 
    internal ObjectSymbol Object { get; private set; }     // object/set to match at location
    internal RuleCommand Command { get; private set; }   // action to take if rule part succeeds

    internal bool IsEllipsis { get { return Object == Ellipsis; } }
    internal bool HasObject { get { return Object.Objects.Count > 0; } }

    // note: object name used here, not tostring()
    public override string ToString() {
      return IsEllipsis ? "..." : Util.JoinNonEmpty(" ", IsNegated ? "no" : "", 
        Direction == Direction.None ? "" : Direction.ToString(),
        Object.Name, Command == RuleCommand.None ? "" : Command.ToString());
    }

    internal static RuleAtom Create(bool isnegated, Direction direction, ObjectSymbol objects, RuleCommand command) {
      return new RuleAtom {
        IsNegated = isnegated, Direction = direction, Object = objects, Command = command,
      };
    }

    // Clone an atom but change the direction
    internal RuleAtom Clone(Direction direction) {
      var rm = this.MemberwiseClone() as RuleAtom;
      rm.Direction = direction;
      return rm;
    }
  }

  /// <summary>
  /// Holds fully expanded rule before compilation
  /// </summary>
  class AtomicRule {
    internal int RuleId;
    internal IList<RulePrefix> Prefixes;
    internal IList<Direction> Directions;
    internal IList<SubRule> Patterns;
    internal IList<SubRule> Actions;
    internal IList<Pair<RuleCommand, string>> Commands;  // .NET 3.5

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
    internal HashSet<int> Objects = new HashSet<int>();

    // true if references the same objects in the same way
    internal bool IsSame(ObjectSymbol other) {
      return Kind == other.Kind && Objects.SetEquals(other.Objects);
    }

    // true if groups reference at least some of the same objects
    internal bool Matches(ObjectSymbol other) {
      return Objects.Overlaps(other.Objects);
    }

    public override string ToString() {
      return String.Format("{0}:{1}({2})", Name, Kind, Objects.Join());
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
    internal GameDef GameDef { get { return _gamedef; } }
    internal SettingsParser SettingsParser { get; private set; }
    internal ColourParser ColourParser { get; private set; }
    internal PuzzLangParser PegParser { get; private set; }

    int _linenumber { get { return PegParser.LineNumber; } }

    string _sourcename;
    TextWriter _out;
    GameDef _gamedef;
    RuleBuilder _rulebuilder;
    Dictionary<string, ObjectSymbol> _symbols = new Dictionary<string, ObjectSymbol>(StringComparer.CurrentCultureIgnoreCase);

    static readonly Dictionary<string, Direction> _arrowlookup =
      new Dictionary<string, Direction>(StringComparer.CurrentCultureIgnoreCase) {
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
      gamedef.Palette = cp.GetPalette();
      var gp = new ParseManager {
        _sourcename = source,
        _out = tw,
        _gamedef = gamedef,
        SettingsParser = new SettingsParser { Game = gamedef, Colour = cp },
        ColourParser = cp,
      };
      if (settings != null)
        foreach (var setting in settings)
          gp.SettingsParser.Parse(setting.Item1, setting.Item2);
      gp.PegParser = PuzzLangParser.Create(tw, gp);
      gp._rulebuilder = RuleBuilder.Create(gp);
      return gp;
    }

    internal string SymbolName(int id) {
      return _gamedef.Objects[id - 1].Name;  // TODO: move into GameDef?
    }

    internal Direction ParseDirection(string value) {
      return (value == null || value.EndsWith("_")) ? Direction.None
        : value.SafeEnumParse<Direction>() ?? _arrowlookup.SafeLookup(value);  // None if not found???
    }

    internal ObjectSymbol ParseSymbol(string name) {
      Logger.Assert(name != null);
      return _symbols.SafeLookup(name);
    }

    // get a set of objects ids to use as a mask
    internal HashSet<int> ParseMask(string name) {
      var sym = ParseSymbol(name);
      Logger.Assert(sym != null, name);
      if (!(sym.Kind == SymbolKind.Alias||sym.Kind == SymbolKind.Mask))
        CompileError("object '{0}' may only use OR", name);
      return sym.Objects;
    }

    // get a set of objects ids to use as a pile
    internal HashSet<int> ParsePile(string name) {
      var sym = ParseSymbol(name);
      Logger.Assert(sym != null, name);
      if (!(sym.Kind == SymbolKind.Alias || sym.Kind == SymbolKind.Pile))
        CompileError("object '{0}' may only use AND", name);
      return sym.Objects;
    }

    internal RulePrefix ParseRulePrefix(string value) {
      if (value == null || value.EndsWith("_")) return RulePrefix.None;
      if (value == "+") return RulePrefix.Plus_;
      return value.SafeEnumParse<RulePrefix>() ?? RulePrefix.None;
    }

    internal RuleCommand ParseRuleCommand(string value) {
      if (value == null || value.EndsWith("_")) return RuleCommand.None;
      return value.SafeEnumParse<RuleCommand>() ?? RuleCommand.None;
    }

    internal SoundTrigger ParseTriggerEvent(string value) {
      if (value == null || value.EndsWith("_")) return SoundTrigger.None;
      return value.SafeEnumParse<SoundTrigger>() ?? SoundTrigger.None;
    }

    // Add a named object to symbol table and object list
    internal int AddObject(string name, int width, IList<int> grid) {
      _symbols[name] = new ObjectSymbol {
        Name = name,
        Kind = SymbolKind.Alias,
        Objects = new HashSet<int> { _gamedef.Objects.Count + 1 },
      };
      _gamedef.Objects.Add(new Object {
        Name = name, Layer = 0, Width = width, Sprite = grid
      });
      DebugLog("Object {0}: '{1}' {2}x{3}", 
        _gamedef.Objects.Count, name, width, grid.Count / width);
      Logger.WriteLine(4, "{0}", grid.Select(c=>String.Format("{0:X}",c)).Join());
      return _gamedef.Objects.Count;
    }

    // Add object group to symbol table
    internal void AddObjectDef(string name, IList<string> others, LogicOperator oper) {
      var sym = CollectObjects(others.Select(o => ParseSymbol(o)).ToList());
      sym.Name = name;

      // if it's a collection of all objects then make a decision
      if (sym.Kind == SymbolKind.Alias && sym.Objects.Count > 1)
        sym.Kind = (oper == LogicOperator.And) ? SymbolKind.Pile : SymbolKind.Mask;
      
      // these are bad, otherwise use whatever we have
      if ((oper == LogicOperator.And && sym.Kind == SymbolKind.Mask)
        || (oper == LogicOperator.Or && sym.Kind == SymbolKind.Pile)) {
        CompileError("incompatible symbols: {0}", others.Join());
      }
      _symbols[name] = sym;
      DebugLog("Define '{0}': {1} <{2}>",
        name, sym.Kind, sym.Objects.Select(o => _gamedef.GetName(o)).Join());
    }

    static readonly SymbolKind[] _symbolkindlookkup = new SymbolKind[] {
      SymbolKind.Alias, SymbolKind.Mask, SymbolKind.Pile, SymbolKind.Mixed
    };

    // collect names objects, check out compatibility
    // returns a partially filled-in symbol -- needs name and final kind
    ObjectSymbol CollectObjects(IList<ObjectSymbol> syms) {
      var kinds = syms.Select(s => s.Kind).ToList();
      if (kinds.Contains(SymbolKind.Mixed)) throw Error.Assert("stored mixed");
      var kindx = (kinds.Contains(SymbolKind.Mask) ? 1 : 0)
        + (kinds.Contains(SymbolKind.Pile) ? 2 : 0);
      var objects = new HashSet<int>(syms.SelectMany(s => s.Objects));
      return new ObjectSymbol {
        Kind = _symbolkindlookkup[kindx],
        Objects = objects,
      };
    }

    internal void AddSound(string trigger, string seed, string objct, IList<string> directions) {
      Logger.WriteLine(2, "Sound {0}: {1} {2} {3}", trigger, seed, objct, directions.Join());
      var trig = ParseTriggerEvent(trigger);
      if (trig <= MaxWithObject_) {
        var obj = ParseMask(objct);
        if (directions != null && directions.Count > 0)
          _gamedef.AddObjectSound(trig, obj, directions.Select(c => ParseDirection(c)), seed);
        else _gamedef.AddObjectSound(trig, obj, null, seed);
      } else _gamedef.AddGameSound(trig, seed);
    }

    // Add a layer and mark objects that belong to it (first layer is 1)
    internal void AddLayer(IList<string> idents) {
      var symbols = CollectObjects(idents.Select(o => ParseSymbol(o)).ToList());
      var layer = ++_gamedef.LayerCount;
      foreach (var obj in symbols.Objects)
        _gamedef.SetObjectLayer(obj, layer);
      Logger.WriteLine(2, "Layer {0}: {1}", layer, idents.Join());
    }

    // add a win condition as a relationship between two masks
    internal void AddWin(string condition, string ident, string other) {
      var winco = condition.SafeEnumParse<MatchOperator>() ?? MatchOperator.None;
      // Any is an (undocumented) synonym for Some
      _gamedef.AddWinCondition(winco == MatchOperator.Any ? MatchOperator.Some : winco,
        ParseMask(ident), (other == null) ? null : ParseMask(other));
      //_gamedef.AddWinCondition(condition.SafeEnumParse<MatchOperator>() ?? MatchOperator.None,
      //  ParseMask(ident), (other == null) ? null : ParseMask(other));
    }

    // update loop state, return true if ok
    internal bool AddLoop(RulePrefix loop) {
      _inloop = !_inloop;
      if (_inloop) ++_loopcounter;
      Logger.WriteLine(2, $"Add Loop '{loop}'");
      return _inloop == (loop == RulePrefix.Startloop_);
    }


    // expand rule and add to game data
    internal void AddRule(int ruleid, IList<RulePrefix> prefixes, IList<Direction> directions,
      IList<SubRule> pattern, IList<SubRule> action, IList<string> commands) {

      // create the base rule with unexpanded directions
      var arule = new AtomicRule {
        RuleId = ruleid,
        Prefixes = prefixes,
        Directions = directions,
        Patterns = pattern,
        Actions = action,
        Commands = commands.Select(c => ParseCommand(c)).ToList(),
      };

      // expanded rules all go in the same group
      var rulegroup = _gamedef.GetRuleGroup(
            arule.Prefixes.Contains(RulePrefix.Late),   // choose rulegroup set
            arule.Prefixes.Contains(RulePrefix.Plus_),  // choose rulegroup
            arule.Prefixes.Contains(RulePrefix.Random), // at runtime choose rule from group
            arule.Prefixes.Contains(RulePrefix.Rigid),  // entire group is rigid
            _inloop ? _loopcounter : 0);
      foreach (var crule in _rulebuilder.Build(arule, rulegroup.Id)) 
        rulegroup.Rules.Add(crule);
    }

    Pair<RuleCommand, string> ParseCommand(string command) {
      // if the command is a message then handle it
      if (command.StartsWith("?")) return Pair.Create(RuleCommand.Message, command.Substring(1));

      // if the command is a sound then it's a sound command
      var sound = ParseTriggerEvent(command);
      if (sound >= MinSound_) {
        var seed = _gamedef.GameSounds.SafeLookup(sound);
        if (seed == null) CompileWarn("sound not defined: {0}", command);
        return Pair.Create(RuleCommand.Sound_, seed ?? "");
      }
      // otherwise it's a normal command
      return Pair.Create(ParseRuleCommand(command), "");
    }

    // create temporary rule subpart info
    internal RuleAtom CreateRuleCell(string noflag, string direction, string objct, string command) {
      return RuleAtom.Create(noflag != null, ParseDirection(direction), 
        (objct == null) ? RuleAtom.Empty : (objct == "...") ? RuleAtom.Ellipsis : ParseSymbol(objct), 
        ParseRuleCommand(command));
    }

    // Parse and add a level
    internal void AddLevel(IList<string> lines) {
      if (_gamedef.Background == null) {
        var background = _symbols.SafeLookup("background");
        if (background == null) {
          CompileError("background not defined");
          _gamedef.Background = new HashSet<int> { 0 };
        } else _gamedef.Background = background.Objects;
      }
      var level = ParseLevel(lines);
      DebugLog("Level {0}: {1}x{2}x{3}", _gamedef.LevelIndex.Count, level.Height, level.Width, level.Depth);
      _gamedef.AddLevel(level);
    }

    internal void AddMessage(string message) {
      DebugLog("Level {0}: '{1}'", _gamedef.Messages.Count, message);
      _gamedef.AddMessage(message);
    }

    // Parse an array of grid lines and depth, converting glyphs into objects in layers
    Level ParseLevel(IList<string> gridlines) {
      var width = gridlines.Max(g => g.Length);
      if (gridlines.Any(l => l.Length != width))
        CompileWarn("short level line(s)");
      var bgobjects = new HashSet<int>();

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
          foreach (var obj in ParsePile(line.Substring(x, 1))) {
            if (_gamedef.Background.Contains(obj))
              bgobjects.Add(obj);
            var layer = _gamedef.GetLayer(obj);
            if (layer != 0) level[x, y, layer] = obj;
          }
        }
      }
      // now fill in the background
      for (int levindex = 0; levindex < level.Length; levindex++) {
        // a tile with no background gets the lowest number we found on the level
        if (!level.GetObjects(levindex).Any(o => bgobjects.Contains(o))) {
          var bgobj = (bgobjects.Count > 0) ? bgobjects.Min() : _gamedef.Background.Min();
          level[levindex, _gamedef.GetLayer(bgobj)] = bgobj;
        }
      }

      return level;
    }

    // Check validity of data
    internal void CheckData() {
      var player = _symbols.SafeLookup("player");
      if (player == null) CompileError("player not defined");
      else _gamedef.Players = ParseMask("player");
      var nolayer = _gamedef.Objects.Where(o => o.Layer == 0);
      if (nolayer.Count() > 0)
        CompileError("not in any layer: {0}", nolayer.Select(o=>o.Name).Join());
      if (_inloop) CompileError("missing endloop");
    }

    internal void DebugLog(string format, params object[] args) {
      if (GameDef.GetSetting(OptionSetting.debug, false))
        _out.WriteLine(String.Format(format, args));
      else Logger.WriteLine(1, "% " + format, args);
    }

    internal void CompileError(string fmt, params object[] args) {
      _out.WriteLine("*** '{0}' at {1}: compile error: {2}", _sourcename, _linenumber, String.Format(fmt, args));
      ErrorCount++;
    }
    internal void CompileWarn(string fmt, params object[] args) {
      _out.WriteLine("*** '{0}' at {1}: compile warning: {2}", _sourcename, _linenumber, String.Format(fmt, args));
      WarningCount++;
    }
  }

}