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
  /// A single match on a fule
  /// </summary>
  internal class RuleMatch {
    internal CompiledRule Rule;
    internal IList<int> MatchPath;
  }

  /// <summary>
  /// State machine driven by the evaluation of a sequence of rules
  /// </summary>
  class RuleState {
    // list of sounds triggered by rules
    internal HashSet<string> Sounds = new HashSet<string>();
    // message triggered by rules (only one can survive)
    internal string Message { get; set; }
    // list of commands triggered by rules
    internal HashSet<RuleCommand> Commands = new HashSet<RuleCommand>();
    // is there any change from the initial state?
    internal bool HasChanged { get; set; }
    // checkpoint by rule action
    internal Level CheckPoint { get; private set; }
    // true if some command will trigger an exit
    public bool Exit {
      get { return Commands.Contains(RuleCommand.Cancel) 
                || Commands.Contains(RuleCommand.Restart) 
                || Commands.Contains(RuleCommand.Win); }
    }

    internal int PatternCounter { get; private set; }
    internal int ActionCounter { get; private set; }
    internal int CommandCounter { get; private set; }

    static readonly Direction[] _dirtable = new Direction[] {
      Direction.Up, Direction.Right, Direction.Down, Direction.Left,
    };

    GameDef _gamedef { get { return _model._gamedef; } }
    Evaluator _evaluator { get { return _model._evaluator; } }

    GameModel _model;
    Level _level;           // current state of level
    List<Mover> _movers;    // list of objects that are moving, with location and direction
    RuleGroup _rulegroup;   // track rigids

    // these are set or used by VM code
    Trail _vm_trail;            // trail being build
    IList<int> _vm_matchpath;   // match being actioned
    int _vm_pathindex;          // index into match path
    //bool _vm_moved;             // true if something moved

    public override string ToString() {
      return $"RuleState<{_movers.Count},{_level.ChangesCount},{HasChanged}>";
    }

    // Create new state, with reference to parent level and movers
    internal static RuleState Create(GameModel model, Level level, List<Mover> movers) {
      return new RuleState {
        _model = model,
        _level = level,
        _movers = movers,
      };
    }

    // find all the matches for the rules in a group
    internal IList<RuleMatch> FindMatches(RuleGroup rulegroup) {
      Logger.WriteLine(3, "Find matches group {0} #{1}", rulegroup.Id, rulegroup.Rules.Count);

      var matches = new List<RuleMatch>();
      // find all possible matches for each rule
      foreach (var rule in rulegroup.Rules) {
        _vm_trail = new Trail();
        _evaluator.Exec(rule, rule.PatternCode, this);
        _vm_trail.Prepare();
        foreach (var path in _vm_trail.Paths)
          matches.Add(new RuleMatch { Rule = rule, MatchPath = path });
      }
      Logger.WriteLine(3, "[FM found {0}]", matches.Count);
      return matches;
    }

    // carry out the actions for this set of matches
    // return true if anything actually was changed
    internal bool DoActions(RuleGroup rulegroup, IList<RuleMatch> matches) {
      Logger.WriteLine(4, "Do actions {0} #{1}", rulegroup.Id, matches.Count);

      _rulegroup = rulegroup;     // needed to track rigid
      if (matches.Count == 0) return false;

      // track net changes made by this entire rule group
      var objiter = matches.SelectMany(m=>m.MatchPath
        .SelectMany(p => _level.GetObjects(p)));
      var objinit = objiter.ToList();
      var moviter = _movers.SelectMany(m => new int[] { m.ObjectId, m.Locator.Index, (int)m.Direction });
      var movinit = moviter.ToList();

      //_vm_moved = false;
      foreach (var match in matches) {
        var rule = match.Rule;
        // do action if any
        if (!rule.ActionCode.IsEmpty) {
          // check per individual rule to get good verbose logging
          // note the need to flatten in order to compare objects as ints

          _vm_matchpath = match.MatchPath;
          _vm_pathindex = 0;
          _model.VerboseLog("Matched rule {0} {1}", rule.RuleId, rule.RuleDirection);
          _evaluator.Exec(rule, rule.ActionCode, this);
          ActionCounter++;
        }
        // do command if any
        if (!rule.CommandCode.IsEmpty) {
          _evaluator.Exec(rule, rule.CommandCode, this);
          CommandCounter++;
        }
      }
      // could optimise this with _vm_moved?
      var changed = !objinit.SequenceEqual(objiter) || !movinit.SequenceEqual(moviter);
      Logger.WriteLine(4, "[DA {0}]", changed);
      return changed;
    }

    // check whether a sound has been triggered
    internal void CheckTrigger(SoundTrigger trigger, int obj = 0, Direction direction = Direction.None) {
      Logger.WriteLine(3, "Check trigger {0} {1} {2}", trigger, _gamedef.ShowName(obj), direction);
      var sound = _gamedef.GameSounds.SafeLookup(trigger);
      if (sound == null) {
        var sact = _gamedef.ObjectSounds.FirstOrDefault(s => s.Trigger == trigger && s.ObjectIds.Contains(obj)
          && (s.Directions == null || s.Directions.Contains(direction)));
        if (sact != null) sound = sact.Seed;
      }
      if (sound != null)
        AddSound(sound);
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

    // return true if any locations contain any of the specified objects and not moving
    internal bool OpFindO(HashSet<int> objects, MatchOperator oper) {
      return OpFindOM(objects, Direction.None, oper);
    }

    // Return true if any locations contain objects moving any of these ways
    // if found add this location to every trail (usually just the root)
    internal bool OpFindOM(HashSet<int> objects, Direction direction, MatchOperator oper) {
      var matches = FindMatches(objects, direction, oper).ToList(); 
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
        var newindex = _level.Step(trail.Location, direction) ?? -1;
        if (newindex == -1) _vm_trail.RemoveLeaf(trail);
        else _vm_trail.Add(trail, newindex);
      }
      return _vm_trail.IsEmpty;
    }

    // Test locations for matching objects 
    internal bool OpTestON(HashSet<int> objects, MatchOperator oper) {
      return OpTestOMN(objects, Direction.None, oper);
    }

    // Test locations for moving objects
    internal bool OpTestOMN(HashSet<int> objects, Direction direction, MatchOperator oper) {
      foreach (var trail in _vm_trail.GetLeaves()) {
        if (!IsMatch(trail.Location, objects, direction, oper))
          _vm_trail.RemoveLeaf(trail);
      }
      return _vm_trail.IsEmpty;
    }

    // check for matching object, return true on failure to abort
    internal bool OpCheckON(HashSet<int> objects, MatchOperator oper) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      return !IsMatch(cellindex, objects, Direction.None, oper);
    }

    // check for matching object, return true on failure to abort
    internal bool OpCheckOMN(HashSet<int> objects, Direction direction, MatchOperator oper) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      return !IsMatch(cellindex, objects, direction, oper);
    }

    // Test for matching objects, stepping in given direction
    internal bool OpScanODN(HashSet<int> objects, Direction step, MatchOperator oper) {
      return OpScanOMDN(objects, step, Direction.None, oper);
    }

    // Test for moving objects, stepping in given direction
    internal bool OpScanOMDN(HashSet<int> objects, Direction step, Direction direction, MatchOperator oper) {
      foreach (var trail in _vm_trail.GetLeaves()) {
        var mark = _vm_trail.NodeCount;
        for (var newindex = trail.Location; newindex != -1; newindex = _level.Step(newindex, step) ?? -1) {
          if (IsMatch(newindex, objects, direction, oper)) {
            _vm_trail.Add(trail, newindex);
          }
        }
        if (mark == _vm_trail.NodeCount) // nothing added, give up on this trail
            _vm_trail.RemoveLeaf(trail);
      }
      return _vm_trail.IsEmpty;
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

    internal bool OpCreateO(HashSet<int> objects, Direction direction) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      // random means pick a random object from those provided (instead of all of them)
      if (direction == Direction.Random) {
        var obj = objects.ToArray()[_model.Rng.Next(objects.Count)];
        CreateObject(obj, cellindex);
      } else {
        foreach (var obj in objects) {
          CreateObject(obj, cellindex);
          MoveObject(obj, cellindex, direction);
        }
      }
      return false;
    }

    // destroy specific objects if found
    internal bool OpDestroyO(HashSet<int> objects) {
      var cellindex = _vm_matchpath[_vm_pathindex];
      foreach (var obj in objects)
        DestroyObject(obj, cellindex);
      return false;
    }

    // apply movement to objects -- may be random
    // Direction.None special: means cancel/ignore
    internal bool OpMoveOM(HashSet<int> objects, Direction direction) {
      var cellindex = _vm_matchpath[_vm_pathindex];

      // randomdir means pick a random direction from those available
      var dir = (direction == Direction.Random) ? Direction.None
        : (direction == Direction.Randomdir) ? _dirtable[_model.Rng.Next(_dirtable.Length)]
        : direction;
      foreach (var obj in objects)
        MoveObject(obj, cellindex, dir);
      return false;
    }

    ///===== Command instructions ==============================================
    ///
    /// Sound and message have arguments, queued for later execution
    /// Command does not; some commands act immediately, some are queued

    internal bool OpSoundT(string seed) {
      AddSound(seed);
      return false;
    }

    internal bool OpMessageT(string text) {
      Logger.WriteLine(2, "Message: {0}", text);
      AddMessage(text);
      return false;
    }

    internal bool OpCommandC(RuleCommand command) {
      Logger.WriteLine(2, "Command: {0}", command);
      AddCommand(command);
      if (command == RuleCommand.Checkpoint)
        CheckPoint = _level.Clone("checkpoint");
      return false;
    }

    //===== implementation =====

    // create object at location 
    void CreateObject(int obj, int cellindex) {
      var layer = _gamedef.GetLayer(obj);
      if (_level[cellindex, layer] != obj) {
        Logger.WriteLine(2, "Create {0} at {1}", _gamedef.ShowName(obj), cellindex);
        ClearLocation(cellindex, layer);
        _level[cellindex, layer] = obj;
        CheckTrigger(SoundTrigger.Create, obj);
      }
    }

    // destroy object at location
    void DestroyObject(int obj, int cellindex) {
      var layer = _model._gamedef.GetLayer(obj);
      if (_level[cellindex, layer] == obj) {
        Logger.WriteLine(2, "Destroy {0} at {1}", _gamedef.ShowName(obj), cellindex);
        ClearLocation(cellindex, layer);
        CheckTrigger(SoundTrigger.Destroy, obj);
      }
    }

    // move an object at location in a direction
    // direction None used to cancel existing movement
    void MoveObject(int obj, int cellindex, Direction direction) {
      if (!(GameDef.MoveDirections.Contains(direction) || direction == Direction.None))
        throw Error.Assert("move {0}", direction);
      // only move an object if it's here, else add a new mover
      var locator = Locator.Create(cellindex, _gamedef.GetLayer(obj));
      if (_level[locator] == obj) {
        var found = false;

        // first try to update move list
        // do not use rule group in comparison -- if rigid, this may cause a problem
        for (var movex = 0; movex < _movers.Count; ++movex) {
          var mover = _movers[movex];
          if (mover.ObjectId == obj && mover.Locator.Index == cellindex) {
            found = true;
            if (_movers[movex].Direction != direction) {
              Logger.WriteLine(2, "Mover now {0} to {1} at {2}", direction, _gamedef.ShowName(obj), locator);
              if (direction == Direction.None)
                _movers.RemoveAt(movex);
              else _movers[movex].Direction = direction;
              //_vm_moved = true;    // TODO: only for net change
            }
            break;
          }
        }
        // otherwise add a new mover
        if (!found && direction != Direction.None) {
          Logger.WriteLine(2, "Mover set {0} to {1} at {2}", direction, _gamedef.ShowName(obj), locator);
          _movers.Add(Mover.Create(obj, locator, direction, _rulegroup));
          //_vm_moved = true;    // TODO: only for net change
        }
      }
    }

    // add a sound to the queue
    void AddSound(string sound) {
      if (!Sounds.Contains(sound)) {
        _model.VerboseLog("Sound {0}", sound);
        Sounds.Add(sound);
      }
    }

    // add a message to the queue
    private void AddMessage(string text) {
      if (Message == null) {  // only the first survives !?
        _model.VerboseLog("Message {0}", text);
        Message = text;
      }
    }

    // add a command to the queue
    private void AddCommand(RuleCommand command) {
      if (!Commands.Contains(command)) {
        _model.VerboseLog("Command {0}", command);
        Commands.Add(command);
      }
    }

    // clear out whatever is in the location, return true if was not empty
    bool ClearLocation(int cellindex, int layer) {
      if (_level[cellindex, layer] != 0) {
        _level[cellindex, layer] = 0;
        var mover = _movers.FirstOrDefault(m => m.Locator.Index == cellindex && m.Locator.Layer == layer);
        if (mover != null)
          _movers.Remove(mover);
        return true;
      }
      return false;
    }

    // test whether there is a matching object at this location
    // direction can be specified, None for ignore or Stationary for not moving
    // oper is No, All or Any
    bool IsMatch(int cellindex, HashSet<int> objects, Direction direction, MatchOperator oper) {
      // no objects matches no objects (ignore direction and operator)
      if (objects.Count == 0)
        return true;                                        // don't care
      var found = IsInLevel(cellindex, objects, oper);
      if (found && direction != Direction.None)
        found = IsMover(cellindex, objects, direction);
      return found;
    }

    // Look for object moving in specific direction, or none
    bool IsMover(int cellindex, HashSet<int> objects, Direction direction) {
      foreach (var move in _movers) { 
        if (cellindex == move.Locator.Index && objects.Contains(move.ObjectId)) {
          if (direction == Direction.Moving) return true;
          if (direction == Direction.Stationary) return false;
          if (move.Direction == direction) return true;
        }
      }
      // not found, not a mover
      return (direction == Direction.Stationary);
    }

    // Find object in level, moving or not
    bool IsInLevel(int cellindex, HashSet<int> objects, MatchOperator oper) {
      if (oper == MatchOperator.All)
        return objects.All(o => _level[cellindex, _gamedef.GetLayer(o)] == o);
      else return objects.Any(o => _level[cellindex, _gamedef.GetLayer(o)] == o) == (oper != MatchOperator.No);
    }

    IList<int> FindMatches(HashSet<int> objects, Direction direction, MatchOperator oper) {
      if (direction == Direction.None)
        return FindStatics(objects, oper).ToList();
      if (direction == Direction.Stationary)
        return FindStatics(objects, oper)
          .Where(x => !_movers.Any(m => m.Locator.Index == x && objects.Contains(m.ObjectId)))
          .ToList();
      return FindMovers(objects, direction, oper).ToList();
    }

    IEnumerable<int> FindStatics(HashSet<int> objects, MatchOperator oper) {
      for (var cellindex = 0; cellindex < _level.Length; ++cellindex) {
        if (objects.Count == 0)
          yield return cellindex;
        else if (oper == MatchOperator.All) {
          if (objects.All(o => _level[cellindex, _gamedef.GetLayer(o)] == o))
            yield return cellindex;
        } else {
          var found = objects.Any(o => _level[cellindex, _gamedef.GetLayer(o)] == o);
          if (found == (oper != MatchOperator.No))
            yield return cellindex;
        }
      }
    }

    IEnumerable<int> FindMovers(HashSet<int> objects, Direction direction, MatchOperator oper) {
      if (!(oper == MatchOperator.Any || oper == MatchOperator.All)) throw Error.Assert("oper {0}", oper);
      if (!(direction == Direction.Moving || GameDef.MoveDirections.Contains(direction))) throw Error.Assert("dir {0}", direction);
      var moves = _movers
        .Where(m => direction == Direction.Moving || direction == m.Direction)
        .OrderBy(m => m.Locator.Index);
      if (oper == MatchOperator.Any) {
        foreach (var move in moves)
          if (objects.Contains(move.ObjectId))
            yield return move.Locator.Index;
      } else {
        foreach (var move in moves)
          if (objects.Contains(move.ObjectId))
            if (objects
              .All(o => moves
                .Any(m => m.ObjectId == move.ObjectId && m.Locator.Index == move.Locator.Index)))
              yield return move.Locator.Index;
      }
    }


  }
}