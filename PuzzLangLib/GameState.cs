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
  internal enum StateResult {
    None, Cancel, Restart, Undo, Finished, Againing,
    Success,
  }

  /// <summary>
  /// Current state of game after zero or more inputs
  /// </summary>
  class GameState {
    // status of this game state
    internal StateResult Result { get { return _result; } }
    internal Level Level { get { return _level; } }
    internal bool IsAgaining { get { return _result == StateResult.Againing; } }
    internal bool IsFinished { get { return _result == StateResult.Finished; } }
    internal int AgainCount { get; set; }
    internal int MoveNumber { get { return _moveno; } }
    internal IEnumerable<string> Sounds { get { return _rulestate.Sounds; } }
    internal string Message { get { return _rulestate.Message; } }
    internal IList<int> PlayerIndexes { get { return _playerindexes; } }
    internal int ScreenIndex { get; private set; }

    internal int LostRuleCounter { get; private set; }
    internal int EarlyRuleCounter { get; private set; }
    internal int TotalRuleCounter { get; private set; }
    internal int MoveCounter { get; private set; }
    internal int ActionCounter { get; private set; }
    internal int CommandCounter { get; private set; }

    GameDef _gamedef { get { return _model._gamedef; } }

    static readonly Dictionary<RuleCommand, StateResult> _commandresultlookup = new Dictionary<RuleCommand, StateResult>() {
      { RuleCommand.Cancel, StateResult.Cancel    },
      { RuleCommand.Win,    StateResult.Finished  },
      { RuleCommand.Restart,StateResult.Restart   },
      { RuleCommand.Undo,   StateResult.Undo      },
      { RuleCommand.Again,  StateResult.Againing  },
    };

    // columns used by lookup
    static MatchOperator[] _wincocol = new MatchOperator[] {
      MatchOperator.All, MatchOperator.Some, MatchOperator.No
    };

    // Win Condition Lookup
    static bool?[,] _wincolookup = new bool?[,] {
      // All   Some    No | All-on Some-on No-on
      { null,  null,  null,  null,  null,  null  },  // object not found
      { null,  true,  false, false, null,  null  },  // object found, no target
      { null,  null,  null,  null,  true,  false },  // object and target found
      { false, false, true,  true,  false, true  },  // on exit (no nulls)
    };

    StateResult _result;
    GameModel _model;
    Level _level;
    Direction _input = Direction.None;
    List<Mover> _movers = new List<Mover>();
    int _moveno = 0;
    HashSet<RuleGroup> _disabledgrouplookup = new HashSet<RuleGroup>();
    RuleState _rulestate;         // state as rules are evaluated
    List<int> _playerindexes;

    public override string ToString() {
      return $"Game<{_moveno},{_input},{_result},a={AgainCount}>";
    }

    internal static GameState Create(GameModel model, Level level, int moveno) {
      var gs = new GameState {
        _model = model,             // static data
        _level = level.Clone(),     // initial state of level -- will be updated
        _moveno = moveno,           // just a counter
      };
      gs._playerindexes = gs.FindPlayers();
      gs.ScreenIndex = gs.CalcScreenIndex();
      return gs;
    }

    // Create new game state and apply input to it
    internal GameState NextState(Direction input, int? intparam) {
      Logger.WriteLine(3, "NextState {0} {1}", input, intparam);
      var nextstate = GameState.Create(_model, _level, _moveno + 1);
      return nextstate.ApplyAll(input, intparam);
    }

    // apply input, then rules, then process queued commands and return state to use
    GameState ApplyAll(Direction input, int? intparam) {
      var now1 = DateTime.Now;
      _input = input;
      if (intparam != null) ApplyClick(input, intparam.Value);
      else ApplyInput(input);
      ApplyRules();
      _playerindexes = FindPlayers();
      ScreenIndex = CalcScreenIndex();

      // check commands in order of priority
      _result = StateResult.Success;
      foreach (var command in _commandresultlookup.Keys) {
        if (_rulestate.Commands.Contains(command)) {
          _result = _commandresultlookup[command];
          break;
        }
      }
      // handle checkpoint, gets discarded by any other command
      if (_rulestate.Commands.Contains(RuleCommand.Checkpoint)) {
        _model.SetCheckpoint(_rulestate.CheckPoint);
        _rulestate.CheckTrigger(SoundTrigger.Checkpoint);
      }
      var now2 = DateTime.Now;
      Logger.WriteLine(4, "[AA {0}]", this);
      return this;
    }

    // apply input to any clickables found at this location
    void ApplyClick(Direction input, int cellindex) {
      if (!GameDef.MoveDirections.Contains(input)) return;

      Logger.WriteLine(3, "ApplyClick {0} {1}", input, cellindex);
      foreach (var obj in _level.GetObjects(cellindex)) { 
        if (_gamedef.Clickables.Contains(obj)) {
          var locator = Locator.Create(cellindex, _gamedef.GetLayer(obj));
          _movers.Add(Mover.Create(obj, locator, _input));
          Logger.WriteLine(2, "Mover add {0} to {1} at {2}", _input, _gamedef.GetName(obj), locator);
        }
      }
    }

    // apply input to every player and add to movers list
    void ApplyInput(Direction input) {
      if (!GameDef.MoveDirections.Contains(input)) return;
      //if (_gamedef.Players == null) return;
      Logger.WriteLine(3, "ApplyInput {0}", input);
      foreach (var player in _gamedef.Players)
        foreach (var locator in FindObject(player)) {
          _movers.Add(Mover.Create(player, locator, _input));
          Logger.WriteLine(2, "Mover add {0} to {1} at {2}", _input, _gamedef.GetName(player), locator);
        }
    }

    // find the location of each player as an ordered list
    List<int> FindPlayers() {
      return _gamedef.Players
        .SelectMany(p => FindObject(p)
        .Select(l => l.Index))
        .ToList();
    }

    // calculate origin for screen as an index into the level
    int CalcScreenIndex() {
      var flick = _gamedef.GetSetting(OptionSetting.flickscreen, (Pair<int, int>)null);
      var zoom = _gamedef.GetSetting(OptionSetting.zoomscreen, (Pair<int, int>)null);
      if ((flick == null && zoom == null) || PlayerIndexes.Count == 0) return 0;
      var pindex = PlayerIndexes[0];
      var width = _level.Width;
      var height = _level.Height;
      if (flick != null) {
        var x = flick.Item1 * (pindex % width / flick.Item1);
        var y = flick.Item2 * (pindex / width / flick.Item2);
        return width * y + x;
      } else {
        var x = Math.Max(0, Math.Min(width - zoom.Item1, pindex % width - zoom.Item1 / 2));
        var y = Math.Max(0, Math.Min(height - zoom.Item2, pindex / width - zoom.Item2 / 2));
        return width * y + x;
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
      _model.VerboseLog("Try {0} rules(s)", _gamedef.EarlyRules.Count);
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
        LostRuleCounter += _rulestate.PatternCounter;
      }
      EarlyRuleCounter += _rulestate.PatternCounter;

      // apply late rules
      if (_gamedef.LateRules.Count > 0) {
        _model.VerboseLog("Try {0} late rule(s)", _gamedef.LateRules.Count);
        ApplyRuleGroups(_gamedef.LateRules);
        if (_rulestate.Exit) return;
      }
      TotalRuleCounter += _rulestate.PatternCounter;
      ActionCounter += _rulestate.ActionCounter;
      CommandCounter += _rulestate.CommandCounter;

      // figure out command to return
      var haschanged = _level.ChangesCount > 0;
      if (TestWinConditions()) {
        _model.VerboseLog("Win conditions satisfied.");
        _rulestate.Commands.Add(RuleCommand.Win);
      } else if (!haschanged && _rulestate.Commands.Contains(RuleCommand.Again)) {
        _model.VerboseLog("Again cancelled, no effect.");
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
      // loop until no more changes, return true if there were any
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
            _model.VerboseLog("Blocked duplicate move {0} to {1}", _gamedef.GetName(mover.ObjectId), mover.Locator.Index);
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
          _level[mover.Locator] = mover.ObjectId;
          if (NoFailRigid(mover)) return;
          Logger.WriteLine(3, "Blocked  move {0} to {1}", _gamedef.GetName(mover.ObjectId), mover.Locator.Index);
          _movers.Remove(mover);
        }
      }
      // 4.Insert movers into level at new location, count it
      if (_movers.Count > 0)
        _model.VerboseLog("Make moves: {0}", _movers.Select(m=>_gamedef.GetName(m.ObjectId)).Join());
      MoveCounter += _movers.Count;
      foreach (var mover in _movers) {
        _level[Destination(mover)] = mover.ObjectId;
        _rulestate.CheckTrigger(mover.Direction == Direction.Action ? SoundTrigger.Action : SoundTrigger.Move, 
          mover.ObjectId, mover.Direction);
      }
      Logger.WriteLine(4, "[AM {0},{1}]", _movers.Count, _level.ChangesCount);
    }

    // if rigid add to disabled list and return true
    bool NoFailRigid(Mover mover) {
      var rulegroup = mover.RuleGroup;
      _rulestate.CheckTrigger(SoundTrigger.Cantmove, mover.ObjectId, mover.Direction);
      if (rulegroup == null || !rulegroup.IsRigid) return false;
      _model.VerboseLog("Rule group {0} disabled", rulegroup.Id);
      _disabledgrouplookup.Add(rulegroup);
      return true;
    }

    // return true if all win conditions are satisfied, and there is at least one!
    bool TestWinConditions() {
      return _gamedef.WinConditions.Count() > 0
        && _gamedef.WinConditions.All(w => TestWinCondition(w));
    }

    // return true if this win condition is satisfied
    bool TestWinCondition(WinCondition winco) {
      var lookupcol = Array.IndexOf(_wincocol, winco.Action) + (winco.Others != null ? 3 : 0);
      bool? result = null;
      for (int index = 0; index < _level.Length; index++) {
        var objfound = winco.ObjectIds.Where(o => ObjectFound(o, index)).Count() > 0;
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
    List<Locator> FindObject(int obj) {
      var layer = _gamedef.GetLayer(obj);
      return Enumerable.Range(0, _level.Length)
        .Where(x => _level[x, layer] == obj)
        .Select(x => Locator.Create(x, layer)).ToList();
    }

    bool ObjectFound(int objct, int location) {
      return _level[location, _gamedef.GetLayer(objct)] == objct;
    }
  }
}
