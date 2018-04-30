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
      Direction.Moving, Direction.Stationary, // moving or not
      Direction.Random,     // pick a random object
      Direction.Randomdir,  // pick a random direction
    };

    Generator _gen;
    ParseManager _parser;

    internal static RuleBuilder Create(ParseManager parser) {
      return new RuleBuilder {
        _parser = parser,
      };
    }

    // Expand until rules are atomic, make absolute, return list of compiled rules
    internal IList<AtomicRule> PrepareRule(AtomicRule arule, int groupno) {
      Logger.WriteLine(2, "Build group {0} rule {1}", groupno, arule.RuleId);

      // how many directions do we need?
      var singles = arule.Patterns.All(p => p.Cells.Count == 1)
        && arule.Patterns.All(p => p.Cells[0].All(a => _ruledirlookup.ContainsKey(a.Direction)));
      var ruledirs = (arule.Directions.Count == 0)
        ? _ruledirlookup[singles ? Direction.None : Direction.Orthogonal]
        : arule.Directions.SelectMany(d => _ruledirlookup[d]).Distinct();

      // expand paraperportho into relative directions
      var exrules = ExpandRule(arule);

      // convert relative directions into absolute clones
      var absrules = ruledirs
        .SelectMany(d => exrules
          .Select(r => MakeAbsolute(r, d))).ToList();

      return absrules;
    }

    // compile a rule
    internal CompiledRule CompileRule(AtomicRule rule) {
      _parser.DebugLog("{0}", rule);
      if (rule.Directions.Count != 1) throw Error.Assert("direction count");
      for (int i = 0; i < rule.Patterns.Count; i++) {
        // TODO: check singleton rules here
      }
      var crule = new CompiledRule {
        RuleId = rule.RuleId,
        RuleDirection = rule.Directions[0],
        PatternCode = CompilePattern(rule.Directions[0], rule.Patterns),
        ActionCode = (rule.HasAction)
          ? CompileActions(rule.Directions[0], rule.Patterns, rule.Actions)
          : RuleCode.Empty,
        CommandCode = (rule.HasCommand)
          ? CompileCommand(rule.Commands)
          : RuleCode.Empty,
      };
      return crule;
    }

    // Convert every relative direction into absolute
    AtomicRule MakeAbsolute(AtomicRule rule, Direction ruledir) {
      var newrule = rule.Clone();
      newrule.Directions = new List<Direction> { ruledir };
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

      if (!oldrule.HasAction) return ExpandPatternRule(oldrule);

      // check each subrule, look for a replaceable direction
      for (var isubrule = 0; isubrule < oldrule.Patterns.Count; isubrule++) {
        var pattern = oldrule.Patterns[isubrule];
        for (var icellp = 0; icellp < pattern.Cells.Count; ++icellp) {
          var patoms = pattern.Cells[icellp];
          var action = oldrule.Actions[isubrule];
          var aatoms = action.Cells[icellp];

          // check for action atom with ambiguous direction and no matching pattern atom
          var datom = aatoms.FirstOrDefault(a => _expanddirlookup.ContainsKey(a.Direction));
          if (datom != null && !patoms.Any(a => a.Symbol == datom.Symbol && a.Direction == datom.Direction))
            return ExpandDirection(oldrule, datom.Direction);

          // check for action atom with property symbol and no matching pattern atom
          var satom = aatoms.FirstOrDefault(a => a.Symbol.Kind == SymbolKind.Property
            && !a.IsNegated && a.Direction != Direction.Random);
          if (satom != null && !patoms.Any(a => a.Symbol == satom.Symbol && !a.IsNegated))
            return ExpandSymbol(oldrule, satom.Symbol);
        }
      }
      return ExpandPatternRule(oldrule);
    }

    // expand a rule pattern and optionally the matching action
    // replacing combination directions and symbols by individuals (absolute or relative)
    // could be fixed by more powerful vm code
    IList<AtomicRule> ExpandPatternRule(AtomicRule oldrule) {

      // check each subrule, look for a replaceable direction
      for (var isubrule = 0; isubrule < oldrule.Patterns.Count; isubrule++) {
        var pattern = oldrule.Patterns[isubrule];
        for (var icellp = 0; icellp < pattern.Cells.Count; ++icellp) {
          var patoms = pattern.Cells[icellp];

          // look for any ambiguous unmatched pattern directions, just one each time
          var patom = patoms.FirstOrDefault(a => _expanddirlookup.ContainsKey(a.Direction));
          if (patom != null)
            return ExpandDirection(oldrule, patom.Direction, isubrule, icellp);
        }
      }
      return new List<AtomicRule> { oldrule };
    }

    // expand an OR symbol throughout a rule
    // error if more than one in pattern
    IList<AtomicRule> ExpandSymbol(AtomicRule oldrule, ObjectSymbol oldsym) {
      var pcells = oldrule.Patterns.SelectMany(p => p.Cells
        .SelectMany(c => c.Where(a => a.Symbol == oldsym && !a.IsNegated)));
      if (pcells.Count() != 1)
        _parser.CompileError("'{0}' in action cannot be matched", oldsym.Name);
      var newrules = new List<AtomicRule>();
      foreach (var newsym in oldsym.ObjectIds) {
        var newrule = oldrule.Clone();
        for (var isubrule = 0; isubrule < oldrule.Patterns.Count; isubrule++) {
          var pattern = oldrule.Patterns[isubrule];
          var action = oldrule.Actions[isubrule];
          for (var icellp = 0; icellp < pattern.Cells.Count; ++icellp) {
            var pcell = pattern.Cells[icellp];
            for (var iatom = 0; iatom < pcell.Count; ++iatom) {
              newrule.Patterns[isubrule].Cells[icellp] = ReplaceObject(pattern.Cells[icellp], oldsym, newsym);
              newrule.Actions[isubrule].Cells[icellp] = ReplaceObject(action.Cells[icellp], oldsym, newsym);
            }
          }
        }
        newrules.AddRange(ExpandRule(newrule));
      }
      return newrules;
    }

    private IList<RuleAtom> ReplaceObject(IList<RuleAtom> cell, ObjectSymbol oldsym, int newobj) {
      IList<RuleAtom> newcell = null;
      for (var i = 0; i < cell.Count; i++) {
        if (cell[i].Symbol == oldsym  && !cell[i].IsNegated) {
          if (newcell == null) newcell = new List<RuleAtom>(cell);
          newcell[i] = cell[i].Clone(_parser.GetSymbol(newobj));
        }
      }
      return newcell ?? cell;
    }

    // expand an ambiguous direction throughout a rule
    // error if more than one in pattern
    IList<AtomicRule> ExpandDirection(AtomicRule oldrule, Direction olddir) {
      var pcells = oldrule.Patterns.SelectMany(p => p.Cells
        .SelectMany(c => c.Where(a => a.Direction == olddir)));
      if (pcells.Count() != 1)
        _parser.CompileError("'{0}' in action cannot be matched", olddir);
      var newrules = new List<AtomicRule>();
      foreach (var newdir in _expanddirlookup[olddir]) {
        var newrule = oldrule.Clone();
        for (var isubrule = 0; isubrule < oldrule.Patterns.Count; isubrule++) {
          var pattern = oldrule.Patterns[isubrule];
          var action = oldrule.Actions[isubrule];
          for (var icellp = 0; icellp < pattern.Cells.Count; ++icellp) {
            var pcell = pattern.Cells[icellp];
            for (var iatom = 0; iatom < pcell.Count; ++iatom) {
              newrule.Patterns[isubrule].Cells[icellp] = ReplaceDirection(pattern.Cells[icellp], olddir, newdir);
              newrule.Actions[isubrule].Cells[icellp] = ReplaceDirection(action.Cells[icellp], olddir, newdir);
            }
          }
        }
        newrules.AddRange(ExpandRule(newrule));
      }
      return newrules;
    }

    // limited substitution of pattern direction
    IList<AtomicRule> ExpandDirection(AtomicRule oldrule, Direction olddir, int isubrule, int icell) {
      var pattern = oldrule.Patterns[isubrule];
      var newrules = new List<AtomicRule>();
      foreach (var newdir in _expanddirlookup[olddir]) {
        var newrule = oldrule.Clone();
        newrule.Patterns[isubrule].Cells[icell] = ReplaceDirection(pattern.Cells[icell], olddir, newdir);
        if (oldrule.HasAction) {
          var action = oldrule.Actions[isubrule];
          newrule.Actions[isubrule].Cells[icell] = ReplaceDirection(action.Cells[icell], olddir, newdir);
        }
        newrules.AddRange(ExpandRule(newrule));
      }
      return newrules;
    }

    private IList<RuleAtom> ReplaceDirection(IList<RuleAtom> cell, Direction olddir, Direction newdir) {
      IList<RuleAtom> newcell = null;
      for (var i = 0; i < cell.Count; i++) {
        if (cell[i].Direction == olddir) {
          if (newcell == null) newcell = new List<RuleAtom>(cell);
          newcell[i] = cell[i].Clone(newdir);
        }
      }
      return newcell ?? cell;
    }

    // Generate code for pattern rule in given direction
    RuleCode CompilePattern(Direction ruledir, IList<SubRule> patterns) {
      _gen = new Generator();
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

          // each object named in the cell [ | player crate | ]
          for (int atomx = 0; atomx < cell.Count; atomx++) {
            var atom = cell[atomx];
            if (cellx == 0 && atomx == 0) EmitFind(atom);
            else if (ellipsis && atomx == 0) EmitScan(atom, ruledir);
            else EmitTest(atom);
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
            foreach (var atom in picells[j])
              EmitCheck(atom);
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

      // handle patterns that match and those that do not
      foreach (var patom in patoms)
        CompileActionMatchups(patom, aatoms.FirstOrDefault(a => a.Symbol.Matches(patom.Symbol)), ruledir);
      // handle actions that do not match
      if (aatoms.Any(a => a.Direction == Direction.Random))
        CompileRandomAction(aatoms.Where(a => !patoms.Any(p => p.Symbol.Matches(a.Symbol))));
      else foreach (var aatom in aatoms.Where(a => !patoms.Any(p => p.Symbol.Matches(a.Symbol))))
          CompileActionMatchups(null, aatom, ruledir);
    }

    // emit code for the 'random' special case
    void CompileRandomAction(IEnumerable<RuleAtom> aatoms) {
      var objids = aatoms.SelectMany(a => a.Symbol.ObjectIds);
      EmitCreate(new HashSet<int>(objids), Direction.Random);
    }

    // emit code for a 'best available' match of two atoms; either can be null or negated
    void CompileActionMatchups(RuleAtom patom, RuleAtom aatom, Direction ruledir) {
      var pobj = patom?.Symbol.ObjectIds;
      var aobj = aatom?.Symbol.ObjectIds;
      var haspobj = !(patom == null || patom.IsNegated || pobj.Count == 0);
      var hasaobj = !(aatom == null || aatom.IsNegated || aobj.Count == 0);

      if (aatom != null && aatom.IsNegated) {
        if (!(patom != null && patom.IsNegated)) {
          EmitDestroy(aobj); // TODO: test
        }
      } else if (!haspobj && !hasaobj) {

      // pattern only
      } else if (haspobj && !hasaobj) {
        EmitDestroy(pobj);

      // action only
      } else if (!haspobj && hasaobj) {
        EmitCreate(aobj, aatom.Direction);

      // same objects
      } else if (pobj.SetEquals(aobj)) {
        if (patom.Direction != aatom.Direction)
          EmitSetMove(pobj, aatom.Direction);

      // different objects, create/move can only be resolved at runtime
      } else {
        EmitDestroy(pobj.Minus(aobj));
        EmitCreate(aobj, aatom.Direction);
      }
    }

    // Compile code for performing actions, if triggered by match
    RuleCode CompileCommand(IList<Pair<RuleCommand,string>> commands) {
      _gen = new Generator();
      foreach (var command in commands) {
        if (command.Item1 == RuleCommand.Message)
          EmitMessage(command.Item2);
        else if (command.Item1 == RuleCommand.Sound_)
          EmitSound(command.Item2);
        else EmitCommand(command.Item1);
      }
      if (Logger.Level >= 3) _gen.Decode("command", Logger.Out);
      return _gen.Code;
    }

    // Rotate a relative object direction according to the rule direction
    private Direction Rotate(Direction objdir, Direction ruledir) {

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

    private void EmitStepNext(Direction direction) {
      _gen.Emit(Opcodes.StepD);
      _gen.Emit(direction);
    }

    private void EmitTrail(int index) {
      _gen.Emit(Opcodes.TrailX);
      _gen.Emit(index);
    }

    private void EmitCreate(HashSet<int> objects, Direction direction = Direction.None) {
      if (objects.Count == 0) return;
      _gen.Emit(Opcodes.CreateO);
      _gen.Emit(objects);
      _gen.Emit(direction);
    }

    private void EmitDestroy(HashSet<int> objects) {
      if (objects.Count == 0) return;
      _gen.Emit(Opcodes.DestroyO);
      _gen.Emit(objects);
    }

    void EmitFind(RuleAtom cell) {
      if (cell.Direction == Direction.None) {
        _gen.Emit(Opcodes.FindO);
        _gen.Emit(cell.Symbol.ObjectIds);
        _gen.Emit(GetOper(cell));
      } else {
        _gen.Emit(Opcodes.FindOM);
        _gen.Emit(cell.Symbol.ObjectIds);
        _gen.Emit(cell.Direction);
        _gen.Emit(GetOper(cell));
      }
    }

    private void EmitTest(RuleAtom cell) {
      if (cell.Direction == Direction.None) {
        _gen.Emit(Opcodes.TestON);
        _gen.Emit(cell.Symbol.ObjectIds);
        _gen.Emit(GetOper(cell));
      } else {
        _gen.Emit(Opcodes.TestOMN);
        _gen.Emit(cell.Symbol.ObjectIds);
        _gen.Emit(cell.Direction);
        _gen.Emit(GetOper(cell));
      }
    }

    private void EmitCheck(RuleAtom cell) {
      if (cell.Direction == Direction.None) {
        _gen.Emit(Opcodes.CheckON);
        _gen.Emit(cell.Symbol.ObjectIds);
        _gen.Emit(GetOper(cell));
      } else {
        _gen.Emit(Opcodes.CheckOMN);
        _gen.Emit(cell.Symbol.ObjectIds);
        _gen.Emit(cell.Direction);
        _gen.Emit(GetOper(cell));
      }
    }

    private void EmitScan(RuleAtom cell, Direction ruledir) {
      if (cell.Direction == Direction.None) {
        _gen.Emit(Opcodes.ScanODN);
        _gen.Emit(cell.Symbol.ObjectIds);
        _gen.Emit(ruledir);
        _gen.Emit(GetOper(cell));
      } else {
        _gen.Emit(Opcodes.ScanOMDN);
        _gen.Emit(cell.Symbol.ObjectIds);
        _gen.Emit(ruledir);
        _gen.Emit(cell.Direction);
        _gen.Emit(GetOper(cell));
      }
    }

    private void EmitSetMove(HashSet<int> objects, Direction direction) {
      _gen.Emit(Opcodes.MoveOM);
      _gen.Emit(objects);
      _gen.Emit(direction);
    }

    private void EmitSound(string seed) {
      _gen.Emit(Opcodes.SoundT);
      _gen.Emit(seed);
    }

    private void EmitCommand(RuleCommand command) {
      _gen.Emit(Opcodes.CommandC);
      _gen.Emit((int)command);
    }

    private void EmitMessage(string text) {
      _gen.Emit(Opcodes.MessageT);
      _gen.Emit(text);
    }

  }
}
