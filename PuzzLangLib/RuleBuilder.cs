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
using DOLE;

namespace PuzzLangLib {
  /// <summary>
  /// Manage an iterator over a rule, visiting each subrule and cell
  /// </summary>
  class RuleIterator {
    internal AtomicRule Rule;
    internal int SubRuleIndex;
    internal int CellIndex;

    internal bool HasAction { get { return Rule.HasAction; } }
    internal SubRule Pattern { get { return Rule.Patterns[SubRuleIndex]; } }
    internal SubRule Action { get { return Rule.Actions[SubRuleIndex]; } }
    internal IList<RuleAtom> PatternAtoms { get { return Pattern.Cells[CellIndex]; } }
    internal IList<RuleAtom> ActionAtoms { get { return Action.Cells[CellIndex]; } }

    internal bool Done { get { return SubRuleIndex >= Rule.Patterns.Count; } }
    // for (new; !Done; Step)
    internal void Step() {
      if (++CellIndex >= Pattern.Cells.Count) {
        CellIndex = 0;
        ++SubRuleIndex;
      }
    }
  }

  /// <summary>
  /// Manage the expansion and building of rules
  /// </summary>
  class RuleBuilder {
    // note: direction order compatible with PS
    // translate a rule direction into a set of directions for generated rules
    static readonly Dictionary<Direction, IList<Direction>> _ruledirlookup = new Dictionary<Direction, IList<Direction>> {
      { Direction.None,       new Direction[] { Direction.None } },  // special for single cell no direction
      { Direction.Up,         new Direction[] { Direction.Up } },
      { Direction.Right,      new Direction[] { Direction.Right } },
      { Direction.Down,       new Direction[] { Direction.Down } },
      { Direction.Left,       new Direction[] { Direction.Left } },
      { Direction.Action,     new Direction[] { Direction.Action } },
      { Direction.Reaction,   new Direction[] { Direction.Reaction } },
      { Direction.Horizontal, new Direction[] { Direction.Left,Direction.Right } },
      { Direction.Vertical,   new Direction[] { Direction.Up, Direction.Down } },
      { Direction.Orthogonal, new Direction[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right } },
    };

    // pattern directions that cause rule expansion
    static readonly Dictionary<Direction, Direction[]> _expanddirlookup = new Dictionary<Direction, Direction[]> {
      { Direction.Perpendicular, new Direction[] { Direction.DownArrow_, Direction.UpArrow_} },
      { Direction.Parallel,     new Direction[] { Direction.RightArrow_, Direction.LeftArrow_} },
      { Direction.Horizontal,   new Direction[] { Direction.Left,Direction.Right } },
      { Direction.Vertical,     new Direction[] { Direction.Up, Direction.Down } },
      { Direction.Orthogonal,   new Direction[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right, } },
      { Direction.Moving,       new Direction[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right,
                                                  Direction.Action } },
    };

    // relative directions translate to rotation
    static readonly Dictionary<Direction, int> _rotatebylookup = new Dictionary<Direction, int> {
      { Direction.RightArrow_, 0 },
      { Direction.DownArrow_,  1 },
      { Direction.LeftArrow_,  2 },
      { Direction.UpArrow_,    3 },
    };

    // lookup for single rotation
    static readonly Dictionary<Direction, Direction> _rotatelookup = new Dictionary<Direction, Direction> {
      { Direction.Up,     Direction.Right},
      { Direction.Right,  Direction.Down },
      { Direction.Down,   Direction.Left },
      { Direction.Left,   Direction.Up },
    };

    // specify allowed directions in emitted code 
    static readonly HashSet<Direction> _directionlookup = new HashSet<Direction> {
      Direction.None,       // unspecified movement
      Direction.Up, Direction.Right, Direction.Down, Direction.Left,  // real movement
      Direction.Horizontal, Direction.Vertical,   // engine can check these too
      Direction.Action,     // pseudo-movement
      Direction.Reaction,   // pseudo-movement
      Direction.Moving, Direction.Stationary, // moving or not
      Direction.Random,     // pick a random object
      Direction.RandomDir,  // pick a random direction
    };

    Generator _gen;
    ParseManager _parser;

    internal static RuleBuilder Create(ParseManager parser) {
      return new RuleBuilder {
        _parser = parser,
      };
    }

    // Expand until rules are atomic, make absolute, return list of atomic rules
    internal IList<AtomicRule> BuildRuleList(AtomicRule arule, int groupno) {
      Logger.WriteLine(2, "Build group {0} rule {1}", groupno, arule.RuleId);

      // how many directions do we need?
      var singles = arule.Patterns.All(p => p.Cells.Count == 1)
        && arule.Patterns.All(p => p.Cells[0].All(a => _ruledirlookup.ContainsKey(a.Direction)));
      var ruledirs = (arule.Directions.Count == 0)
        ? _ruledirlookup[singles ? Direction.None : Direction.Orthogonal]
        : arule.Directions.SelectMany(d => _ruledirlookup[d]).Distinct();

      // expand ambiguous directions and symbols into absolute/relative
      var exrules = ExpandRule(arule);

      // convert relative directions into absolute clones
      var absrules = ruledirs
        .SelectMany(d => exrules
          .Select(r => MakeAbsolute(r, d))).ToList();

      return absrules;
    }

    // Convert every relative direction into absolute
    AtomicRule MakeAbsolute(AtomicRule rule, Direction ruledir) {
      var newrule = rule.Clone();
      newrule.Directions = new HashSet<Direction>() { ruledir };
      MakeAbsolute(newrule.Patterns, ruledir);
      if (newrule.HasAction) MakeAbsolute(newrule.Actions, ruledir);
      return newrule;
    }

    void MakeAbsolute(IList<SubRule> subrules, Direction ruledir) {
      foreach (var subrule in subrules) {
        foreach (var atoms in subrule.Cells) {
          for (int i = 0; i < atoms.Count; i++) {
            var newdir = Rotate(atoms[i].Direction, ruledir);
            if (newdir != atoms[i].Direction)
              atoms[i] = atoms[i].Clone(newdir);
          }
        }
      }
    }

    // expand a rule where an occurrence on an action does not match its pattern
    // replacing combination directions and symbols by individuals (absolute or relative)
    // could be fixed by more powerful vm code
    IList<AtomicRule> ExpandRule(AtomicRule oldrule) {
      if (!oldrule.HasAction) return ExpandDirectionSimple(oldrule);

      // check each cell of each subrule
      for (var riter = new RuleIterator { Rule = oldrule }; !riter.Done; riter.Step()) {

        // Do no expansion on random actions
        if (riter.ActionAtoms.Any(a => a.IsRandom)) oldrule.Prefixes.Add(RulePrefix.Final);
        else {

          // check for action atom with ambiguous direction and no matching pattern atom
          var datom = riter.ActionAtoms.FirstOrDefault(a => _expanddirlookup.ContainsKey(a.Direction)
            && !riter.PatternAtoms.Any(p => p.Matches(a) && p.Direction == a.Direction));
          if (datom != null)
            return ExpandDirectionMulti(oldrule, datom.Direction);

          // check for action atom with property symbol and no matching pattern atom
          var satom = riter.ActionAtoms.FirstOrDefault(a => a.Symbol.Kind == SymbolKind.Property
            && !a.IsNegated && !a.IsRandom && !riter.PatternAtoms.Any(p => p.Matches(a)));
          if (satom != null) {
            var newrules = ExpandSymbolMulti(oldrule, satom.Symbol) ?? ExpandPropertyMatcher(riter);
            if (newrules == null) _parser.CompileError("'{0}' in action cannot be matched", satom.Symbol.Name);
            else return newrules;
          }
        }
      }
      return ExpandDirectionSimple(oldrule);
    }

    // look for matching property objects in pattern and action, expand matching pairwise
    IList<AtomicRule> ExpandPropertyMatcher(RuleIterator riter) {
      var patom = riter.PatternAtoms.FirstOrDefault(a => a.Symbol.Kind == SymbolKind.Property
        && !a.IsNegated && !a.IsRandom);
      var aatom = riter.ActionAtoms.FirstOrDefault(a => a.Symbol.Kind == SymbolKind.Property
        && !a.IsNegated && !a.IsRandom);
      if (patom != null && aatom != null && patom.Symbol.ObjectIds.Count == aatom.Symbol.ObjectIds.Count) {
        var newrules = new List<AtomicRule>();
        for (int i = 0; i < patom.Symbol.ObjectIds.Count; i++) {
          var newrule = riter.Rule.Clone();
          newrule.Prefixes.Add(RulePrefix.Final);
          newrule.Patterns[riter.SubRuleIndex].Cells[riter.CellIndex]
            = ReplaceObject(riter.PatternAtoms, patom.Symbol, patom.Symbol.ObjectIds[i]);
          newrule.Actions[riter.SubRuleIndex].Cells[riter.CellIndex]
            = ReplaceObject(riter.ActionAtoms, aatom.Symbol, aatom.Symbol.ObjectIds[i]);
          newrules.Add(newrule);
        }
        return newrules;
      }
      return null;
    }

    // expand ambiguous directions in rule pattern known to have no matching action
    // replacing by individuals (absolute or relative)
    // abs could be fixed by more powerful vm code
    IList<AtomicRule> ExpandDirectionSimple(AtomicRule oldrule) {
      for (var riter = new RuleIterator { Rule = oldrule }; !riter.Done; riter.Step()) {
        // look for any ambiguous unmatched pattern directions, just one each time
        var patom = riter.PatternAtoms.FirstOrDefault(a => _expanddirlookup.ContainsKey(a.Direction));
        if (patom != null)
          return ExpandDirectionSingle(riter, patom.Direction);
      }
      return new List<AtomicRule> { oldrule };
    }

    // limited substitution of pattern direction
    IList<AtomicRule> ExpandDirectionSingle(RuleIterator riter, Direction olddir) {
      var newrules = new List<AtomicRule>();
      foreach (var newdir in _expanddirlookup[olddir]) {
        var newrule = riter.Rule.Clone();
        newrule.Patterns[riter.SubRuleIndex].Cells[riter.CellIndex] = ReplaceDirection(riter.PatternAtoms, olddir, newdir);
        if (riter.HasAction)
          newrule.Actions[riter.SubRuleIndex].Cells[riter.CellIndex] = ReplaceDirection(riter.ActionAtoms, olddir, newdir);
        newrules.AddRange(ExpandRule(newrule));
      }
      return newrules;
    }

    // expand an OR symbol throughout a rule
    // error if more than one in pattern
    IList<AtomicRule> ExpandSymbolMulti(AtomicRule oldrule, ObjectSymbol oldsym) {
      var pcells = oldrule.Patterns.SelectMany(p => p.Cells
        .SelectMany(c => c.Where(a => a.Symbol == oldsym && !a.IsNegated)));
      if (pcells.Count() != 1) return null;

      // expand one rule per object id
      var newrules = new List<AtomicRule>();
      foreach (var newobj in oldsym.ObjectIds) {
        var newrule = oldrule.Clone();
        for (var riter = new RuleIterator { Rule = oldrule }; !riter.Done; riter.Step()) {
          newrule.Patterns[riter.SubRuleIndex].Cells[riter.CellIndex] = ReplaceObject(riter.PatternAtoms, oldsym, newobj);
          newrule.Actions[riter.SubRuleIndex].Cells[riter.CellIndex] = ReplaceObject(riter.ActionAtoms, oldsym, newobj);
        }
        newrules.AddRange(ExpandRule(newrule));
      }
      return newrules;
    }

    IList<RuleAtom> ReplaceObject(IList<RuleAtom> cell, ObjectSymbol oldsym, int newobj) {
      IList<RuleAtom> newcell = null;
      for (var i = 0; i < cell.Count; i++) {
        if (cell[i].Symbol == oldsym && !cell[i].IsNegated) {
          if (newcell == null) newcell = new List<RuleAtom>(cell); // BUG: could lose atoms
          newcell[i] = cell[i].Clone(_parser.GetSymbol(newobj));
        }
      }
      return newcell ?? cell;
    }

    // expand an ambiguous direction throughout a rule
    // error if more than one in pattern
    IList<AtomicRule> ExpandDirectionMulti(AtomicRule oldrule, Direction olddir) {
      var pcells = oldrule.Patterns.SelectMany(p => p.Cells
        .SelectMany(c => c.Where(a => a.Direction == olddir)));
      if (pcells.Count() != 1)
        _parser.CompileError("'{0}' in action cannot be matched", olddir);

      // expand one rule per single direction
      var newrules = new List<AtomicRule>();
      foreach (var newdir in _expanddirlookup[olddir]) {
        var newrule = oldrule.Clone();
        for (var riter = new RuleIterator { Rule = oldrule }; !riter.Done; riter.Step()) {
          newrule.Patterns[riter.SubRuleIndex].Cells[riter.CellIndex] = ReplaceDirection(riter.PatternAtoms, olddir, newdir);
          newrule.Actions[riter.SubRuleIndex].Cells[riter.CellIndex] = ReplaceDirection(riter.ActionAtoms, olddir, newdir);
        }
        newrules.AddRange(ExpandRule(newrule));
      }
      return newrules;
    }

    IList<RuleAtom> ReplaceDirection(IList<RuleAtom> cell, Direction olddir, Direction newdir) {
      IList<RuleAtom> newcell = null;
      for (var i = 0; i < cell.Count; i++) {
        if (cell[i].Direction == olddir) {
          if (newcell == null) newcell = new List<RuleAtom>(cell);
          newcell[i] = cell[i].Clone(newdir);
        }
      }
      return newcell ?? cell;
    }

    //==========================================================================
    //
    // compile a rule
    internal CompiledRule CompileRule(AtomicRule rule) {
      _parser.DebugLog("{0}", rule);
      if (rule.Directions.Count != 1) throw Error.Assert("direction count");
      for (int i = 0; i < rule.Patterns.Count; i++) {
        // TODO: check singleton rules here
      }
      var crule = new CompiledRule {
        RuleId = rule.RuleId,
        RuleDirection = rule.Directions.First(),
        PatternCode = CompilePattern(rule.Directions.First(), rule.Patterns),
        ActionCode = (rule.HasAction)
          ? CompileActions(rule.Directions.First(), rule.Patterns, rule.Actions)
          : RuleCode.Empty,
        CommandCode = (rule.HasCommand)
          ? CompileCommand(rule.Commands)
          : RuleCode.Empty,
      };
      return crule;
    }

    // Generate code for pattern rule in given direction
    RuleCode CompilePattern(Direction ruledir, IList<SubRule> patterns) {
      _gen = new Generator();
      var trailvar = 0;
      // each separate pattern: [ | | | ]
      foreach (var subrule in patterns) {

        // each separate cell in pattern [ | cell | ]
        for (int cellx = 0; cellx < subrule.Cells.Count; cellx++) {
          var cell = subrule.Cells[cellx];
          if (cellx == 0) EmitStart();
          else EmitStepNext(ruledir);
          var ellipsis = false;
          if (cell.Count == 1 && cell[0].IsEllipsis) {
            ellipsis = true;
            cell = subrule.Cells[++cellx];
          }

          for (int atomx = 0; atomx < cell.Count; atomx++) {
            var atom = cell[atomx];
            if (cellx == 0 && atomx == 0) EmitFind(atom);
            else if (ellipsis && atomx == 0) EmitScan(atom, ruledir);
            else if (atom.Symbol.Kind == SymbolKind.RefObject) EmitTestRef(atom);
            else EmitTest(atom);
            if (atom.HasVariable) {  // TODO: non-trail vars?
              atom.Variable.SetVarRef(trailvar);
              EmitStoreRef(trailvar, atom.Symbol.ObjectIds);
              trailvar++;
            }
            if (atom.Call != null) {
              atom.Call.Emit(_gen);
              _gen.Emit(Opcodes.TestR);
            }
          }
        }
      }
      if (Logger.Level >= 3) _gen.Decode("pattern", Logger.Out);
      return _gen.Code;
    }

    RuleCode CompileActions(Direction ruledir, IList<SubRule> patterns, IList<SubRule> actions) {
      _gen = new Generator();
      // each separate pattern: [ | | | ]
      var trailindex = 0;
      for (int i = 0; i < patterns.Count; i++) {
        var picells = patterns[i].Cells;
        // each separate cell in pattern [ | cell | ]
        // check pattern still matches
        for (int j = 0; j < picells.Count; j++) {
          if (picells[j].Any(a => a.HasObject)) {
            EmitTrail(trailindex + j);
            foreach (var atom in picells[j]) {
              EmitCheck(atom);
            }
          }
        }
        // make changes (but always skip ellipsis)
        for (int j = 0; j < picells.Count; j++) {
          if (picells[j].Any(a => !a.IsEllipsis)) {
            EmitTrail(trailindex + j);
            CompileAction(ruledir, picells[j], actions[i].Cells[j]);
          }
        }
        trailindex += picells.Count;
      }
      if (Logger.Level >= 3) _gen.Decode("action", Logger.Out);
      return _gen.Code;
    }

    // Compile code for changing objects or movements
    // arguments are unordered lists of pattern atoms for corresponding cells
    void CompileAction(Direction ruledir, IList<RuleAtom> patoms, IList<RuleAtom> aatoms) {

      // handle random action (matches ignored)
      if (aatoms.Any(a => a.IsRandom))
        CompileRandomAction(aatoms);
      else {
        // handle actions that match and those that do not
        foreach (var patom in patoms.Where(p => p.Symbol.IsObject)) {
          var aatom = aatoms.FirstOrDefault(a => a.Symbol == patom.Symbol);
          if (aatom == null)
            CompilePatternOnly(patom);
          else CompilePatternAction(patom, aatom);
        }
        foreach (var aatom in aatoms
            .Where(a => a.IsFunction || (a.Symbol.IsObject && !patoms.Any(p => p.Symbol == a.Symbol))))
          CompileActionOnly(aatom);
      }
    }

    // pattern has no match in the action
    void CompilePatternOnly(RuleAtom patom) {
      if (!patom.IsNegated && patom.Symbol.IsObject)
        EmitDestroy(patom.Symbol);
    }

    // action has no match in the pattern: refobject and scriptcall always come here
    void CompileActionOnly(RuleAtom aatom) {
      if (aatom.IsFunction) {
        EmitCreateByFunction(aatom.Call, aatom.Direction);
      } else if (aatom.Symbol.Kind == SymbolKind.RefObject) {
        EmitCreateByRef(aatom.Symbol, aatom.Direction);
      } else {
        if (aatom.IsNegated)
          EmitDestroy(aatom.Symbol);
        else EmitCreate(aatom.Symbol, aatom.Direction);
      }
    }

    // matching pattern and action depend on NO or direction
    void CompilePatternAction(RuleAtom patom, RuleAtom aatom) {
      if (patom.IsNegated) CompileActionOnly(aatom);
      else if (aatom.IsNegated)
        EmitDestroy(aatom.Symbol);  // TEST: ->[no x] deletes x
      else if (patom.Direction != aatom.Direction) {
        var newdir = (aatom.Direction == Direction.None) ? Direction.Stationary : aatom.Direction;
        if (!(GameDef.MoveDirections.Contains(newdir) || newdir == Direction.Stationary || newdir == Direction.RandomDir))
          throw Error.Assert("move {0}", newdir);
        EmitSetMove(patom.Symbol, newdir);
      }
    }

    // emit code for the 'random' special case
    void CompileRandomAction(IEnumerable<RuleAtom> aatoms) {
      EmitCreateRandom(aatoms.Select(a => a.Symbol));
    }


    // Compile code for performing actions, if triggered by match
    RuleCode CompileCommand(IList<CommandCall> commands) {
      _gen = new Generator();
      foreach (var command in commands) {
        EmitCommand(command.commandId, command.text, command.symbol, command.funcCall);
      }
      if (Logger.Level >= 3) _gen.Decode("command", Logger.Out);
      return _gen.Code;
    }

    // Rotate a relative object direction according to the rule direction
    Direction Rotate(Direction objdir, Direction ruledir) {

      // if the rule has no direction or the object direction is not relative, use as is
      if (!_rotatebylookup.ContainsKey(objdir)) return objdir;
      if (ruledir == Direction.None) {
        _parser.CompileError("relative direction '{0}' not allowed here", objdir);
        return objdir;
      }

      // use the object direction to rotate the rule direction and return that instead
      var dir = ruledir;
      for (var rot = _rotatebylookup[objdir]; rot > 0; --rot)
        dir = _rotatelookup[dir];
      return dir;
    }

    ///-------------------------------------------------------------------------
    ///
    /// Emit fragments
    ///

    MatchOperator GetOper(RuleAtom cell) {
      return cell.IsNegated ? MatchOperator.No
        : cell.Symbol.Kind == SymbolKind.Aggregate ? MatchOperator.All
        : MatchOperator.Any;
    }

    void EmitStart() {
      _gen.Emit(Opcodes.Start);
    }

    void EmitStepNext(Direction direction) {
      _gen.Emit(Opcodes.StepD);
      _gen.Emit(direction);
    }

    void EmitTrail(int index) {
      _gen.Emit(Opcodes.TrailX);
      _gen.Emit(index);
    }

    void EmitCreate(ObjectSymbol objsym, Direction direction = Direction.None) {
      if (!objsym.IsObject || objsym.ObjectIds.Count() == 0) throw Error.Assert(objsym.Name);
      _gen.Emit(Opcodes.CreateO);
      _gen.Emit(objsym);
      _gen.Emit(direction);
    }

    void EmitCreateRandom(IEnumerable<ObjectSymbol> objsyms) {
      _gen.Emit(Opcodes.CreateO);
      _gen.Emit(objsyms.SelectMany(s => s.ObjectIds));
      _gen.Emit(Direction.Random);
    }

    void EmitDestroy(ObjectSymbol objsym) {
      if (!objsym.IsObject || objsym.ObjectIds.Count() == 0) throw Error.Assert(objsym.Name);
      _gen.Emit(Opcodes.DestroyO);
      _gen.Emit(objsym);
    }

    void EmitCreateByFunction(ScriptFunctionCall function, Direction direction) {
      function.Emit(_gen);
      _gen.Emit(Opcodes.CreateO);
      _gen.Emit(-99);  // special to trigger function result lookup
      _gen.Emit(direction);
    }

    void EmitCreateByRef(ObjectSymbol objsym, Direction direction) {
      if (objsym.ObjectIds.Count() != 1) throw Error.Assert(objsym.Name);
      _gen.Emit(Opcodes.CreateO);
      _gen.Emit(~objsym.ObjectIds.First());  // special to trigger variable lookup
      _gen.Emit(direction);
    }

    void EmitFind(RuleAtom cell) {
      _gen.Emit(Opcodes.FindOM);
      _gen.Emit(cell.Symbol);
      _gen.Emit(cell.Direction);
      _gen.Emit(GetOper(cell));
    }

    void EmitTest(RuleAtom cell) {
      _gen.Emit(Opcodes.TestOMN);
      _gen.Emit(cell.Symbol);
      _gen.Emit(cell.Direction);
      _gen.Emit(GetOper(cell));
    }

    void EmitTestRef(RuleAtom cell) {
      _gen.Emit(Opcodes.TestXMN);
      _gen.Emit(cell.Symbol);
      _gen.Emit(cell.Direction);
      _gen.Emit(GetOper(cell));
    }

    void EmitCheck(RuleAtom cell) {
      _gen.Emit(Opcodes.CheckOMN);
      _gen.Emit(cell.Symbol);
      _gen.Emit(cell.Direction);
      _gen.Emit(GetOper(cell));
    }

    void EmitScan(RuleAtom cell, Direction ruledir) {
      _gen.Emit(Opcodes.ScanOMDN);
      _gen.Emit(cell.Symbol);
      _gen.Emit(ruledir);
      _gen.Emit(cell.Direction);
      _gen.Emit(GetOper(cell));
    }

    void EmitSetMove(ObjectSymbol objsym, Direction direction) {
      _gen.Emit(Opcodes.MoveOM);
      _gen.Emit(objsym);
      _gen.Emit(direction);
    }

    void EmitStoreRef(int trailvarid, IList<int> objectids) {
      if (objectids.Count == 0) throw Error.Assert("trail");
      _gen.Emit(Opcodes.StoreXO);
      _gen.Emit(trailvarid);
      _gen.Emit(objectids);
    }

    void EmitCommand(CommandName command, string argument, ObjectSymbol objsym, ScriptFunctionCall call) {
      if (call != null)
        call.Emit(_gen);
      if (objsym != null) {
        _gen.Emit(Opcodes.CommandCSO);
        _gen.Emit((int)command);
        if (call == null) _gen.Emit(argument);
        else _gen.Emit(-99);
        _gen.Emit(objsym);
      } else if (argument != null) {
        _gen.Emit(Opcodes.CommandCS);
        _gen.Emit((int)command);
        if (call == null) _gen.Emit(argument);
        else _gen.Emit(-99);
      } else {
        _gen.Emit(Opcodes.CommandC);
        _gen.Emit((int)command);
      }
    }
  }
}
