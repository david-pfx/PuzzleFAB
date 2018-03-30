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
using System.Text;
using DOLE;

namespace PuzzLangLib {
  /// <summary>
  /// Action to take after processing rules
  /// </summary>
  internal enum StateAction {
    None, Cancel, Restart, Win, Checkpoint

  }

  /// <summary>
  /// Current state of game after zero or more inputs
  /// </summary>
  class GameState {
    internal Level Level { get { return _level; } }
    internal bool Finished { get; private set; }
    internal int AgainCount { get; set; }
    internal bool Undo { get; private set; }
    internal int MoveNumber { get { return _moveno; } }
    internal IEnumerable<string> Sounds { get { return _rulestate.Sounds; } }

    GameDef _gamedef { get { return _model._gamedef; } }

    GameModel _model;
    Level _level;
    Direction _input = Direction.None;
    List<Mover> _movers = new List<Mover>();
    int _moveno = 0;
    HashSet<RuleGroup> _disabledgrouplookup = new HashSet<RuleGroup>();
    RuleState _rulestate;         // state as rules are evaluated

    public override string ToString() {
      return $"Game<{_moveno},{_input},f={Finished},a={AgainCount},u={Undo}>";
    }

    internal static GameState Create(GameModel model, Level level, int moveno) {
      return new GameState {
        _model = model,             // static data
        _level = level.Clone(),     // initial state of level -- will be updated
        _moveno = moveno,           // just a counter
      };
    }

    // Compute next game state based on input and rules
    // Return null if no new valid state
    internal GameState NextState(Direction input) {
      Logger.WriteLine(3, "NextState {0}", input);
      var nextstate = GameState.Create(_model, _level, _moveno + 1);
      return nextstate.ApplyAll(input);
    }

    // apply input, then rules, then process queued commands and return state to use
    GameState ApplyAll(Direction input) {
      Logger.WriteLine(4, "[ApplyAll {0}]", input);
      ApplyInput(input);
      ApplyRules();
      foreach (var command in _rulestate.Commands) {
        switch (command) {
        case RuleCommand.Again:
          _rulestate.CheckTrigger(SoundTrigger.Again);
          AgainCount++;
          break;
        case RuleCommand.Cancel:
          _rulestate.CheckTrigger(SoundTrigger.Cancel);
          return this;
        case RuleCommand.Checkpoint:
          _model.SetCheckpoint(_rulestate.CheckPoint);
          _rulestate.CheckTrigger(SoundTrigger.Checkpoint);
          break;
        case RuleCommand.Restart:
          _rulestate.CheckTrigger(SoundTrigger.Restart);
          _model.Restart(this);
          return null;
        case RuleCommand.Undo:
          _rulestate.CheckTrigger(SoundTrigger.Undo);
          Undo = true;
          break;
        case RuleCommand.Win:
          _rulestate.CheckTrigger(SoundTrigger.Win);
          Finished = true;
          break;
        default:
          throw Error.Assert("bad command {0}", command);
        }
      }
      Logger.WriteLine(4, "[AA {0}]", this);
      return this;
    }

    // apply input to every player and add to movers list
    void ApplyInput(Direction input) {
      Logger.WriteLine(3, "ApplyInput {0}", input);
      _input = input;
      if (input != Direction.None) {
        foreach (var player in _gamedef.Players)
          foreach (var locator in FindObject(player)) {
            _movers.Add(Mover.Create(player, locator, _input));
            Logger.WriteLine(2, "Mover add {0} to '{1}' at {2}", _input, _gamedef.GetName(player), locator);
          }
      }
    }

    // create a new rule state and apply the rules to them
    // returned state command says what to do next
    void ApplyRules() {
      Logger.WriteLine(3, "ApplyRules early={0} late={1}", _gamedef.EarlyRules.Count, _gamedef.LateRules.Count);

      _disabledgrouplookup.Clear();
      var originalmovers = _movers;
      var originallevel = _level;

      // apply early rules and moves, loop until no rigid failure
      _model.DebugWriteline("Try rules {0}", _gamedef.EarlyRules.Select(r=>r.Id).Join());
      while (true) {
        _movers = new List<Mover>(originalmovers);
        _level = originallevel.Clone();
        _rulestate = RuleState.Create(_model, _level, _movers);

        ApplyRuleGroups(_gamedef.EarlyRules);
        if (_rulestate.Exit) return;

        // make the moves
        var disablecount = _disabledgrouplookup.Count;
        ApplyMoves();
        if (disablecount == _disabledgrouplookup.Count) break;
      }

      // apply late rules
      if (_gamedef.LateRules.Count > 0) {
        _model.DebugWriteline("Try late rules {0}", _gamedef.LateRules.Select(r => r.Id).Join());
        ApplyRuleGroups(_gamedef.LateRules);
        if (_rulestate.Exit) return;
      }

      // figure out command to return
      var haschanged = _level.ChangesCount > 0;
      if (TestWinConditions()) {
        _model.DebugWriteline("Win conditions satisfied.");
        _rulestate.Commands.Add(RuleCommand.Win);
      } else if (!haschanged && _rulestate.Commands.Contains(RuleCommand.Again)) {
        _model.DebugWriteline("Again cancelled, no effect.");
        _rulestate.Commands.Remove(RuleCommand.Again);
      }

      Logger.WriteLine(3, "[ARR chg={0} rs={1}]", haschanged, _rulestate);
    }

    // apply a group of rules, single or nested in startloop/endloop
    // set rulestate has changed as needed
    void ApplyRuleGroups(IList<RuleGroup> groups) {
      Logger.WriteLine(4, "ApplyRuleGroups {0}", groups.Count);

      // work through each rule group or loop group in source code order
      for (var groupno = 0; groupno < groups.Count; ++groupno) {
        var group = groups[groupno];
        var loop = group.Loop;  // id for loop, 0 if not

        // if not in a group loop just do it
        if (loop == 0) {
          if (ApplyRuleGroup(group))
            _rulestate.HasChanged = true;

        // if in a group loop, repeat the whole set until no change
        } else {
          Logger.WriteLine(3, "Start loop {0}", loop);
          var loopchange = false;
          var loopstart = groupno;
          for (var retry = 0; ; ++retry) {
            if (retry > 100) throw Error.Fatal("too many rule loops: <{0}>", group);

            // do all the rule groups in the loop block
            var groupchange = false;
            var nextloop = loop;
            for (groupno = loopstart; nextloop == loop;) {
              group = groups[groupno];
              if (ApplyRuleGroup(group))
                groupchange = true;
              nextloop = (++groupno < groups.Count) ? groups[groupno].Loop : 0;
            }
            loopchange |= groupchange;
            if (!groupchange) break;
          }
          _rulestate.HasChanged |= loopchange;
        }
      }
      Logger.WriteLine(4, "[ARGS {0}]", _rulestate.HasChanged);
    }

    // apply a rule group, return true if there was any change
    // loop until no further changes
    bool ApplyRuleGroup(RuleGroup group) {
      Logger.WriteLine(4, "ApplyRuleGroup {0} {1}", _rulestate, group);

      // skip this group -- it failed rigid previously
      if (_disabledgrouplookup.Contains(group)) return false;

      // rule group is needed to track rigid
      _rulestate.RuleGroup = group;
      var groupchange = false;

      // Apply a single rule chosen at random 
      // TODO: pick from available matches
      if (group.IsRandom) { 
        var n = _model.Rng.Next(group.Rules.Count);
        groupchange = _rulestate.Apply(group.Rules[n]);

      // Apply all the rules in the group
      // loop until no more changes, returnt true if there were any
      } else {
        for (var retry = 0; ; ++retry) {
          if (retry > 200) throw Error.Fatal("too many rule group retries: <{0}>", group);
          var rulechange = false;
          foreach (var rule in group.Rules)
            if (_rulestate.Apply(rule))
              rulechange = true;
          groupchange |= rulechange;
          if (!rulechange) break;
        }
      }
      Logger.WriteLine(4, "[ARG {0}]", groupchange);
      return groupchange;
    }

    // apply the movers list to the level
    // check each failed move, abort if not allowed
    void ApplyMoves() {
      Logger.WriteLine(3, "ApplyMoves movers={0} changes={1}", _movers.Count, _level.ChangesCount);

      // 1.Remove illegals and duplicates (later in list)
      foreach (var mover in _movers.ToArray()) {
        var newloc = Destination(mover);
        if (mover.Direction == Direction.None || newloc.IsNull) {
          if (NoFailRigid(mover)) return;
          _movers.Remove(mover);
        } else {
          var dups = _movers.Where(m => Destination(m).Equals(newloc));
          foreach (var dup in dups.Skip(1).ToArray()) {  // avoid modifying collection
            if (NoFailRigid(mover)) return;
            _model.DebugWriteline("Duplicate move {0} to {1}", mover.Object, mover.Locator.Index);
            _movers.Remove(dup);
          }
        }
        if (_movers.Where(m => m.Locator.Equals(mover.Locator)).Count() > 1) throw Error.Assert("dup mover obj");
      }

      // 2. Remove all movers from level
      foreach (var mover in _movers)
        _level[mover.Locator] = 0;

      // 3. Remove blocked movers from list, restore to level
      // 4. Repeat step 3 until none
      while (true) {
        var blocked = _movers.Where(m => _level[Destination(m)] != 0).ToArray();
        if (blocked.Count() == 0) break;
        foreach (var mover in blocked) {
          _level[mover.Locator] = mover.Object;
          if (NoFailRigid(mover)) return;
          _model.DebugWriteline("Conflicting move {0} to {1}", mover.Object, mover.Locator.Index);
          _movers.Remove(mover);
        }
      }
      // 4.Insert movers into level at new location, count it
      if (_movers.Count > 0)
        _model.DebugWriteline("Make moves: {0}", _movers.Select(m=>_gamedef.GetName(m.Object)).Join());
      foreach (var mover in _movers) {
        _level[Destination(mover)] = mover.Object;
        _rulestate.CheckTrigger(mover.Direction == Direction.Action ? SoundTrigger.Action : SoundTrigger.Move, 
          mover.Object, mover.Direction);
      }
      Logger.WriteLine(4, "[AM {0},{1}]", _movers.Count, _level.ChangesCount);
    }

    // if rigid add to disabled list and return true
    bool NoFailRigid(Mover mover) {
      var rulegroup = mover.RuleGroup;
      _rulestate.CheckTrigger(SoundTrigger.Cantmove, mover.Object, mover.Direction);
      if (rulegroup == null || !rulegroup.IsRigid) return false;
      _model.DebugWriteline("Rule group {0} disabled", rulegroup.Id);
      _disabledgrouplookup.Add(rulegroup);
      return true;
    }

    // return true if all win conditions are satisfied, and there is at least one!
    bool TestWinConditions() {
      return _gamedef.WinConditions.Count() > 0
        && _gamedef.WinConditions.All(w => TestWinCondition(w));
    }

    // columns used by lookup
    static MatchOperator[] _wincocol = new MatchOperator[] { MatchOperator.All, MatchOperator.Some, MatchOperator.No };
    // Win Condition Lookup
    static bool?[,] _wincolookup = new bool?[,] {
      // All   Some    No | All-on Some-on No-on
      { null,  null,  null,  null,  null,  null  },  // object not found
      { null,  true,  false, false, null,  null  },  // object found, no target
      { null,  null,  null,  null,  true,  false },  // object and target found
      { false, false, true,  true,  false, true  },  // on exit (no nulls)
    };

    // return true if this win condition is satisfied
    bool TestWinCondition(WinCondition winco) {
      var lookupcol = Array.IndexOf(_wincocol, winco.Action) + (winco.Others != null ? 3 : 0);
      bool? result = null;
      for (int index = 0; index < _level.Length; index++) {
        var objfound = winco.Objects.Where(o => ObjectFound(o, index)).Count() > 0;
        if (winco.Others == null) result = _wincolookup[!objfound ? 0 : 1, lookupcol];
        else {
          var targfound = winco.Others.Where(o => ObjectFound(o, index)).Count() > 0;
          result = _wincolookup[!objfound ? 0 : !targfound ? 1 : 2, lookupcol];
        }
        if (result != null) return result.Value;
      }
      return _wincolookup[3, lookupcol].Value;
    }

    Locator Destination(Mover mover) {
      var newindex = _level.Step(mover.Locator.Index, mover.Direction) ?? -1;
      return (newindex == -1) ? Locator.Null
        : Locator.Create(newindex, mover.Locator.Layer);
    }

    // Find the locations of an object
    List<Locator> FindObject(int objct) {
      var layer = _gamedef.GetLayer(objct);
      return Enumerable.Range(0, _level.Length)
        .Where(x => _level[x, layer] == objct)
        .Select(x => Locator.Create(x, layer)).ToList();
    }

    bool ObjectFound(int objct, int location) {
      return _level[location, _gamedef.GetLayer(objct)] == objct;
    }
  }
}
