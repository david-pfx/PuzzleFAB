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
      { Direction.Perpendicular, new Direction[] { Direction.UpArrow_, Direction.DownArrow_ } },
      { Direction.Parallel,     new Direction[] { Direction.LeftArrow_, Direction.RightArrow_ } },
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
    internal IList<CompiledRule> Build(AtomicRule arule, int groupno) {
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

      // compile and return
      return absrules.Select(r => CompileRule(r)).ToList();
    }

    // compile a rule
    CompiledRule CompileRule(AtomicRule rule) {
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
      //var changed = !(newrule.Directions.Count == 1 && newrule.Directions[0] == ruledir);
      //if (changed) 
      //  newrule.Directions = new List<Direction> { ruledir };
      MakeAbsolute(newrule.Patterns, ruledir);
      if (newrule.HasAction) MakeAbsolute(newrule.Actions, ruledir);
      return newrule;
    }

    void MakeAbsolute(IList<SubRule> subrules, Direction ruledir) {
      foreach (var subrule in subrules) {
        foreach (var cell in subrule.Cells) {
          for (int i = 0; i < cell.Count; i++) {
            var newdir = Rotate(cell[i].Direction, ruledir);
            if (newdir != cell[i].Direction)
              cell[i] = cell[i].Clone(newdir);
          }
        }
      }
    }

    // expand a rule replacing combination directions by individuals (absolute or relative)
    // each rule sequence processed separately, one substitution for each combo in the pattern
    // one subst in pattern => may cause N subst in action, but actions do not multiply
    IList<AtomicRule> ExpandRule(AtomicRule oldrule) {
      // check each subrule
      for (var isubrule = 0; isubrule < oldrule.Patterns.Count; isubrule++) {
        var pattern = oldrule.Patterns[isubrule];
        var action = (oldrule.HasAction) ? oldrule.Actions[isubrule] : null;

        // find indexes for atoms containing replaceable directions
        var icells = Enumerable.Range(0, pattern.Cells.Count)
          .Where(x => pattern.Cells[x]
            .Any(a => _expanddirlookup.ContainsKey(a.Direction))).ToList();

        // if found, clone the rule and perform the substitutions
        if (icells.Count > 0) {
          var icell = icells.First();
          var newrules = new List<AtomicRule>();
          var olddir = pattern.Cells[icell]
            .Select(r => r.Direction)
            .First(d => _expanddirlookup.ContainsKey(d));

          // clone it, fix pattern and action (if any)
          foreach (var newdir in _expanddirlookup[olddir]) {
            var newrule = oldrule.Clone();
            newrule.Patterns[isubrule].Cells[icell] = ReplaceDirection(pattern.Cells[icell], olddir, newdir);
            // if there was only one match replace all, else just do the matching cell
            if (oldrule.HasAction) {
              if (icells.Count > 1)
                newrule.Actions[isubrule].Cells[icell] = ReplaceDirection(action.Cells[icell], olddir, newdir);
              else {
                for (int i = 0; i < action.Cells.Count; i++)
                  newrule.Actions[isubrule].Cells[i] = ReplaceDirection(action.Cells[i], olddir, newdir);
              }
              // Expand each rule recursively ready for return
              // BUG: expanded rule may still contain expandable directions in action
              newrules.AddRange(ExpandRule(newrule));
            }
          }
          return newrules;
        }
      }
      // there was nothing to do
      return new List<AtomicRule> { oldrule };
    }

    private IList<RuleAtom> ReplaceDirection(IList<RuleAtom> seq, Direction olddir, Direction newdir) {
      IList<RuleAtom> newseq = null;
      for (var i = 0; i < seq.Count; i++) {
        if (seq[i].Direction == olddir) {
          if (newseq == null) newseq = new List<RuleAtom>(seq);
          newseq[i] = seq[i].Clone(newdir);
        }
      }
      return newseq ?? seq;
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
        var pic = patterns[i].Cells;
        // each separate cell in pattern [ | cell | ]
        // check pattern still matches
        for (int j = 0; j < pic.Count; j++) {
          if (pic[j].Any(a => a.HasObject)) {
            EmitTrail(trailindex + j);
            foreach (var atom in pic[j])
              EmitCheck(atom);
          }
        }
        // make changes (but always skip ellipsis)
        for (int j = 0; j < pic.Count; j++) {
          if (pic[j].Any(a => !a.IsEllipsis)) {
            EmitTrail(trailindex + j);
            CompileAction(ruledir, pic[j], actions[i].Cells[j]);
          }
        }
        trailindex += pic.Count;
      }
      if (Logger.Level >= 3) _gen.Decode("action", Logger.Out);
      return _gen.Code;
    }

    // Compile code for changing objects or movements
    // arguments are unordered lists of pattern atoms for corresponding cells
    void CompileAction(Direction ruledir, IList<RuleAtom> patterns, IList<RuleAtom> actions) {

      // handle patterns that match and those that do not
      foreach (var pattern in patterns)
        CompileActionMatchups(pattern, actions.FirstOrDefault(a => a.Symbol.Matches(pattern.Symbol)), ruledir);
      // handle actions that do not match
      foreach (var action in actions.Where(a => !patterns.Any(p => p.Symbol.Matches(a.Symbol))))
        CompileActionMatchups(null, action, ruledir);
    }

    void CompileActionMatchups(RuleAtom patom, RuleAtom aatom, Direction ruledir) {
      var pobj = patom?.Symbol.ObjectIds;
      var aobj = aatom?.Symbol.ObjectIds;
      var haspobj = !(patom == null || patom.IsNegated || pobj.Count == 0);
      var hasaobj = !(aatom == null || aatom.IsNegated || aobj.Count == 0);

      if (!haspobj && !hasaobj) {

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
        : cell.Symbol.Kind == SymbolKind.Pile ? MatchOperator.All 
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
