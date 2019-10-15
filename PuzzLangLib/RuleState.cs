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
  /// A single match on a rule
  /// </summary>
  internal class RuleMatch {
    internal CompiledRule rule;
    internal IList<int> path;
    internal Dictionary<int,IList<int>> refs;
  }

  /// <summary>
  /// A single command for queued execution
  /// </summary>
  internal struct QueuedCommand {
    internal CommandName commandId;
    internal int objectId;
    internal string text;

    public override string ToString() {
      return $"cmd<{commandId},{objectId},{text}>";
    }
  }

  /// <summary>
  /// State machine driven by the evaluation of a sequence of rules
  /// 
  /// Commands are added to the command list in order for later execution.
  /// </summary>
  class RuleState {
    // list of commands triggered by rules
    internal List<QueuedCommand> CommandList = new List<QueuedCommand>();
    // is there any change from the initial state?
    internal bool HasChanged { get; set; }
    // checkpoint by rule action
    internal Level CheckPoint { get; private set; }
    // true if some command will trigger an exit

    internal int PatternCounter { get; private set; }
    internal int ActionCounter { get; private set; }
    internal int CommandCounter { get; private set; }

    static readonly Direction[] _dirtable = new Direction[] {
      Direction.Up, Direction.Right, Direction.Down, Direction.Left,
    };

    GameDef _gamedef;
    Evaluator _evaluator;
    Random _rng;

    GameModel _model;
    LevelState _levelstate;     // current state of level
    RuleGroup _rulegroup;       // track rigids
    Dictionary<int,IList<int>> _refobjects;   // array of object references

    // these are set or used by VM code
    Trail _vm_trail;            // trail being built
    IList<int> _vm_matchpath;   // match being actioned
    int _vm_pathindex;          // index into match path

    public override string ToString() {
      return $"RuleState<{_levelstate.MoverCount},{_levelstate.ChangesCount},{HasChanged}>";
    }

    // Create new state, with reference to parent level and movers
    internal static RuleState Create(GameModel model, LevelState levelstate, GameDef gamedef, 
                                     Evaluator evaluator, Random rng) {
      return new RuleState {
        _model = model,
        _levelstate = levelstate,
        _gamedef = gamedef,
        _evaluator = evaluator,
        _rng = rng,
      };
    }

    internal IList<int> GetRefObject(int index) {
      return _refobjects[index];
    }

    // find all the matches for the rules in a group
    internal IList<RuleMatch> FindMatches(RuleGroup rulegroup) {
      Logger.WriteLine(3, "Find matches group {0} #{1}", rulegroup.Id, rulegroup.Rules.Count);

      var matches = new List<RuleMatch>();
      // find all possible matches for each rule
      foreach (var rule in rulegroup.Rules) {
        _vm_trail = new Trail();
        _evaluator.Exec(rule.PatternCode, this);
        matches.AddRange(_vm_trail.GetMatches(rule));
      }
      Logger.WriteLine(3, "[FM found {0}]", matches.Count);
      return matches;
    }

    // carry out the actions for this set of matches
    // return true if anything actually was changed
    internal bool DoActions(RuleGroup rulegroup, IList<RuleMatch> matches) {
      Logger.WriteLine(4, "Do actions {0} #{1}", rulegroup.Id, matches.Count);

      _rulegroup = rulegroup;     // needed to track rigid

      // track net changes made by this entire rule group
      // note: cannot use object map because of unstable order
      var objiter = matches.SelectMany(m => m.path.SelectMany(p => _levelstate.GetObjects(p)));
      var objinit = objiter.ToList();
      var moviter = _levelstate.GetMovers();
      var movinit = moviter.ToList();

      //_vm_moved = false;
      foreach (var match in matches) {
        var rule = match.rule;
        // do action if any
        if (!rule.ActionCode.IsEmpty) {
          // check per individual rule to get good verbose logging
          // note the need to flatten in order to compare objects as ints

          _vm_matchpath = match.path;
          _refobjects = match.refs;
          _vm_pathindex = 0;
          _model.VerboseLog("Matched rule {0} {1}", rule.RuleId, rule.RuleDirection);
          _evaluator.Exec(rule.ActionCode, this);
          ActionCounter++;
        }
      }
      // could optimise this with _vm_moved?
      var changed = !objinit.SequenceEqual(objiter) || !movinit.SequenceEqual(moviter);
      Logger.WriteLine(4, "[DA {0}]", changed);
      return changed;
    }

    // carry out commands for this rulegroup
    internal void DoCommands(RuleGroup rulegroup, IList<RuleMatch> matches) {

      var rules = matches.Select(m => m.rule).Distinct();
      Logger.WriteLine(4, "Do commands {0} #{1}", rulegroup.Id, rules.Count());
      foreach (var rule in rules) {
        if (!rule.CommandCode.IsEmpty) {
          _evaluator.Exec(rule.CommandCode, this);
          CommandCounter++;
        }
      }
    }

    // check whether a sound has been triggered
    // NOTE: sound may not happen if some other command takes precedence
    internal void CheckTrigger(SoundTrigger trigger, int obj = 0, Direction direction = Direction.None) {
      Logger.WriteLine(3, "Check trigger {0} {1} {2}", trigger, _gamedef.ShowName(obj), direction);
      var sound = _gamedef.GameSounds.SafeLookup(trigger);
      if (sound == null) {
        var sact = _gamedef.ObjectSounds.FirstOrDefault(s => s.Trigger == trigger && s.ObjectIds.Contains(obj)
          && (s.Directions == null || s.Directions.Contains(direction)));
        if (sact != null) sound = sact.Seed;
      }
      if (sound != null)
        AddCommand(CommandName.Sound_, sound);
    }

    ///-------------------------------------------------------------------------
    /// 
    /// Machine instructions
    /// 

    ///===== Pattern instructions ==============================================
    ///
    /// Check some stuff and update the trail (add or remove locations)
    /// Return false if match failed
    /// 

    // Start of a cell sequence
    internal bool OpStart() {
      return false;
    }

    // Return true if any locations contain objects moving any of these ways
    // if found add this location to every trail (usually just the root)
    internal bool OpFindOM(HashSet<int> objectids, Direction direction, MatchOperator oper) {
      var matches = _levelstate.FindMatches(objectids, direction, oper).ToList();
      if (matches.Count == 0)
        _vm_trail = new Trail();  // Hmmm. Maybe clear all is better?
      //_trail.Clear();
      else {
        // combinatorially add match to all trails
        foreach (var trail in _vm_trail.GetLeaves())
          foreach (var match in matches)
            _vm_trail.Add(trail, match);
      }
      return _vm_trail.IsEmpty;
    }

    // extend trail in given direction, return false if cannot
    internal bool OpStepD(Direction direction) {
      foreach (var trail in _vm_trail.GetLeaves()) {
        var newindex = _levelstate.Step(trail.Location, direction) ?? -1;
        if (newindex == -1) _vm_trail.RemoveLeaf(trail);
        else _vm_trail.Add(trail, newindex);
      }
      return _vm_trail.IsEmpty;
    }

    // Test locations for moving objects
    internal bool OpTestOMN(HashSet<int> objectids, Direction direction, MatchOperator oper) {
      foreach (var trail in _vm_trail.GetLeaves()) {
        if (!_levelstate.IsMatch(trail.Location, objectids, direction, oper))
          _vm_trail.RemoveLeaf(trail);
      }
      return _vm_trail.IsEmpty;
    }

    // Test locations for moving objects using offset into path
    internal bool OpTestXMN(int trailvar, Direction direction, MatchOperator oper) {
      foreach (var trail in _vm_trail.GetLeaves()) {
        var objids = trail.GetRef(~trailvar);
        if (!_levelstate.IsMatch(trail.Location, new HashSet<int>(objids), direction, oper))
          _vm_trail.RemoveLeaf(trail);
      }
      return _vm_trail.IsEmpty;
    }

    // check for matching object, return true on failure to abort
    internal bool OpCheckON(HashSet<int> objectids, MatchOperator oper) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      return !_levelstate.IsMatch(cellindex, objectids, Direction.None, oper);
    }

    // check for matching object, return true on failure to abort
    internal bool OpCheckOMN(HashSet<int> objectids, Direction direction, MatchOperator oper) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      return !_levelstate.IsMatch(cellindex, objectids, direction, oper);
    }

    // Test for matching objects, stepping in given direction
    internal bool OpScanODN(HashSet<int> objectids, Direction step, MatchOperator oper) {
      return OpScanOMDN(objectids, step, Direction.None, oper);
    }

    // Test for moving objects, stepping in given direction
    internal bool OpScanOMDN(HashSet<int> objectids, Direction step, Direction direction, MatchOperator oper) {
      foreach (var trail in _vm_trail.GetLeaves()) {
        var mark = _vm_trail.NodeCount;
        for (var newindex = trail.Location; newindex != -1; newindex = _levelstate.Step(newindex, step) ?? -1) {
          if (_levelstate.IsMatch(newindex, objectids, direction, oper)) {
            _vm_trail.Add(trail, newindex);
          }
        }
        if (mark == _vm_trail.NodeCount) // nothing added, give up on this trail
            _vm_trail.RemoveLeaf(trail);
      }
      return _vm_trail.IsEmpty;
    }

    internal bool OpSetVarVO(int varid, HashSet<int> objectids) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      var objids = objectids.Intersect(_levelstate.GetObjects(cellindex));
      if (objids.Count() != 1) throw Error.Assert("set var");  // TODO: ban AND?
      _refobjects[varid] = objids.ToList(); 
      return false;
    }

    internal bool OpStoreXO(int trailvarid, HashSet<int> objectids) {
      // check every active trail (others already discarded)
      foreach (var trail in _vm_trail.GetLeaves()) {
        var cellindex = trail.Location;
        var objids = objectids.Intersect(_levelstate.GetObjects(cellindex));
        if (objids.Count() != 1) throw Error.Fatal("set trail var");  // TODO: ban AND?
        trail.SetRef(trailvarid, objids.ToList());
      }
      return _vm_trail.IsEmpty;
    }

    internal bool OpTestR(bool result) {
      if (!result) _vm_trail = new Trail();
      return !result;
    }

    ///===== Action instructions ===============================================
    ///
    /// Create or destroy objects; update the move list
    ///

      // set trail index for action
    internal bool OpTrailX(int value) {
      _vm_pathindex = value;
      return false;
    }

    internal bool OpCreateO(IList<int> objectids, Direction direction) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      // random means pick a random object from those provided (instead of all of them)
      if (direction == Direction.Random) {
        var obj = objectids.ToArray()[_rng.Next(objectids.Count)];
        CreateObject(obj, cellindex);
      } else {
        foreach (var obj in objectids) {
          CreateObject(obj, cellindex);
          if (direction != Direction.None && direction != Direction.Stationary)
            _levelstate.MoveObject(obj, cellindex, direction, _rulegroup);
        }
      }
      return false;
    }

    // destroy specific objects if found
    internal bool OpDestroyO(HashSet<int> objectids) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      foreach (var obj in objectids)
        DestroyObject(obj, cellindex);
      return false;
    }

    // apply movement to objects -- may be random
    // Direction.None special: means cancel/ignore
    internal bool OpMoveOM(HashSet<int> objectids, Direction direction) {
      var cellindex = _vm_matchpath[_vm_pathindex];

      // randomdir means pick a random direction from those available
      var dir = (direction == Direction.RandomDir) ? _dirtable[_rng.Next(_dirtable.Length)]
        : direction;
      foreach (var obj in objectids)
        _levelstate.MoveObject(obj, cellindex, dir, _rulegroup);
      return false;
    }

    ///===== Command instructions ==============================================
    ///
    /// Command with arguments

    internal bool OpCommandCSO(CommandName command, string value, int objid) {
      Logger.WriteLine(2, "Command: {0} {1} {2}", command, value, objid);
      AddCommand(command, value, objid);
      return false;
    }

    //===== implementation =====

    // create object at location 
    internal void CreateObject(int obj, int cellindex) {
      if (_levelstate.CreateObject(obj, cellindex)) {
        Logger.WriteLine(2, "Create {0} at {1}", _gamedef.ShowName(obj), cellindex);
        CheckTrigger(SoundTrigger.Create, obj);
      }
    }

    // destroy object at location
    internal void DestroyObject(int obj, int cellindex) {
      if (_levelstate.DestroyObject(obj, cellindex)) {
        Logger.WriteLine(2, "Destroy {0} at {1}", _gamedef.ShowName(obj), cellindex);
        CheckTrigger(SoundTrigger.Destroy, obj);
      }
    }

    // add a command to the queue
    internal void AddCommand(CommandName command, string argument = null, int objid = 0) {
      _model.VerboseLog("Command {0} {1} {2}", command, argument, objid == 0 ? "" : objid.ToString());
      var cmd = new QueuedCommand {
        commandId = command,
        text = argument,
        objectId = objid,
      };
      if (!CommandList.Contains(cmd))         // no duplicates please, first wins
        CommandList.Add(cmd);
    }
  }
}