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
  /// State machine driven by the evaluation of a sequence of rules
  /// </summary>
  class RuleState {
    // list of sounds to play
    internal HashSet<string> Sounds = new HashSet<string>();
    // list of messages to show
    internal List<string> Messages = new List<string>();
    // list of commands to execute
    internal HashSet<RuleCommand> Commands = new HashSet<RuleCommand>();
    // set to current rule group to track rigids
    internal RuleGroup RuleGroup { get; set; }
    // is there any change from the initial state?
    internal bool HasChanged { get; set; }
    // checkpoint by rule action
    internal Level CheckPoint { get { return _checkpoint; } }
    // true if some command will trigger an exit
    public bool Exit {
      get { return Commands.Contains(RuleCommand.Cancel) 
                || Commands.Contains(RuleCommand.Restart) 
                || Commands.Contains(RuleCommand.Win); }
    }

    static readonly Direction[] _dirtable = new Direction[] {
      Direction.Up, Direction.Right, Direction.Down, Direction.Left,
    };

    GameDef _gamedef { get { return _model._gamedef; } }
    Evaluator _evaluator { get { return _model._evaluator; } }
    GameModel _model;
    Level _level;           // current state of level
    List<Mover> _movers;    // list of objects that are moving, with location and direction
    Level _checkpoint;
    Trail _trail;
    int _trailindex;
    bool _moved;            // set by VM code

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

    // apply a single atomic rule, pattern and action
    // success means some action made a net change
    internal bool Apply(CompiledRule rule) {
      Logger.WriteLine(4, "Apply {0}", rule);
      _moved = false;   // will get set by VM code
      var changed = false;
      _trail = new Trail();

      // find all possible matches
      _evaluator.Exec(rule, rule.PatternCode, this);

      if (!_trail.IsEmpty && !rule.ActionCode.IsEmpty) {
        _trail.Prepare();
        var sequence = _trail.Paths.SelectMany(p => p.SelectMany(x => _level.GetObjects(x)));
        var initial = sequence.ToList();
        while (_trail.Next()) {
          _trailindex = 0;
          Logger.WriteLine(3, "Match rule {0} trail={1}", rule.RuleId, _trail.Current.Join());
          _evaluator.Exec(rule, rule.ActionCode, this);
        }
        changed = !initial.SequenceEqual(sequence);
        if (changed || _moved)
          _model.DebugWriteline("Matched rule {0} trail={1}", rule.RuleId, _trail.Current.Join());
      }

      // the rule matched, do command if any
      if (_trail.NodeCount > 0 && !rule.CommandCode.IsEmpty)
        _evaluator.Exec(rule, rule.CommandCode, this);
      Logger.WriteLine(4, "[A {0}]", changed || _moved);
      return changed || _moved;
    }

    internal void CheckTrigger(SoundTrigger trigger, int obj = 0, Direction direction = Direction.None) {
      Logger.WriteLine(3, "Check trigger {0} {1} {2}", trigger, obj, direction);
      var sound = _gamedef.GameSounds.SafeLookup(trigger);
      if (sound == null) {
        var sact = _gamedef.ObjectSounds.FirstOrDefault(s => s.Trigger == trigger && s.Object.Contains(obj)
          && (s.Directions == null || s.Directions.Contains(direction)));
        if (sact != null) sound = sact.Seed;
      }
      AddSound(sound);
    }

    internal void AddSound(string sound, string trigger = null) {
      if (sound != null && !Sounds.Contains(sound)) {
        Sounds.Add(sound);
        _model.DebugWriteline("Sound {0} {1}", sound, trigger ?? "");
      }
    }

    private void AddCommand(RuleCommand command) {
      if (!Commands.Contains(command)) {
        Commands.Add(command);
        _model.DebugWriteline("Command {0}", command);
      }
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
      var matches = Enumerable.Range(0, _level.Length)
        .Where(x => IsMatch(x, objects, direction, oper)).ToList();
      if (matches.Count == 0)
        _trail = new Trail();  // Hmmm. Maybe clear all is better?
      //_trail.Clear();
      else {
        // combinatorially add match to all trails
        foreach (var trail in _trail.GetLeaves())
          foreach (var match in matches)
            _trail.Add(trail, match);
      }
      return _trail.IsEmpty;
    }

    // extend trail in given direction, return false if cannot
    internal bool OpStepD(Direction direction) {
      foreach (var trail in _trail.GetLeaves()) {
        var newindex = _level.Step(trail.Location, direction) ?? -1;
        if (newindex == -1) _trail.RemoveLeaf(trail);
        else _trail.Add(trail, newindex);
      }
      return _trail.IsEmpty;
    }

    // Test locations for matching objects 
    internal bool OpTestON(HashSet<int> objects, MatchOperator oper) {
      return OpTestOMN(objects, Direction.None, oper);
    }

    // Test locations for moving objects
    internal bool OpTestOMN(HashSet<int> objects, Direction direction, MatchOperator oper) {
      foreach (var trail in _trail.GetLeaves()) {
        if (!IsMatch(trail.Location, objects, direction, oper))
          _trail.RemoveLeaf(trail);
      }
      return _trail.IsEmpty;
    }

    // Test for matching objects, stepping in given direction
    internal bool OpScanODN(HashSet<int> objects, Direction step, MatchOperator oper) {
      return OpScanOMDN(objects, step, Direction.None, oper);
    }

    // Test for moving objects, stepping in given direction
    internal bool OpScanOMDN(HashSet<int> objects, Direction step, Direction direction, MatchOperator oper) {
      foreach (var trail in _trail.GetLeaves()) {
        var mark = _trail.NodeCount;
        for (var newindex = trail.Location; newindex != -1; newindex = _level.Step(newindex, step) ?? -1) {
          if (IsMatch(newindex, objects, direction, oper))
            _trail.Add(trail, newindex);
        }
        if (mark == _trail.NodeCount) // nothing added, give up on this trail
            _trail.RemoveLeaf(trail);
      }
      return _trail.IsEmpty;
    }

    ///===== Action instructions ===============================================
    ///
    /// Create or destroy objects; update the move list
    ///

    // set trail index for action
    internal bool OpTrailX(int value) {
      _trailindex = value;
      return false;
    }

    internal bool OpCreateO(HashSet<int> objects) {
      var levindex = _trail.Current[_trailindex];
      foreach (var obj in objects) {
        var layer = _gamedef.GetLayer(obj);
        if (_level[levindex, layer] != obj) {
          Logger.WriteLine(3, "Create '{0}' at {1}:{2} [q{3}]", _gamedef.GetName(obj), levindex, layer, _level.ChangesCount);
          ClearLocation(levindex, layer);
          _level[levindex, layer] = obj;
          CheckTrigger(SoundTrigger.Create, obj);
        }
      }
      return false;
    }

    // destroy specific objects if found
    internal bool OpDestroyO(HashSet<int> objects) {
      var levindex = _trail.Current[_trailindex];
      foreach (var obj in objects) {
        var layer = _model._gamedef.GetLayer(obj);
        if (_level[levindex, layer] == obj) {
          Logger.WriteLine(3, "Destroy '{0}' at {1}:{2} [q{3}]",
            _gamedef.GetName(obj), levindex, layer, _level.ChangesCount);
          ClearLocation(levindex, layer);
          CheckTrigger(SoundTrigger.Destroy, obj);
        }
      }
      return false;
    }

    // apply movement to objects -- may be random
    // Direction.None special: means cancel/ignore
    internal bool OpMoveOM(HashSet<int> objects, Direction direction) {

      // random means pick a random object from those provided (instead of all of them)
      var objs = (direction == Direction.Random)
        ? new HashSet<int> { objects.ToArray()[_model.Rng.Next(objects.Count)] }
        : objects;

      // randomdir means pick a random direction from those available
      var dir = (direction == Direction.Random) ? Direction.None
        : (direction == Direction.Randomdir) ? _dirtable[_model.Rng.Next(_dirtable.Length)]
        : direction;

      if (!_level.IsValid(dir)) throw Error.Assert("invalid move dir: {0}", dir);

      // add/remove/update movement of any of these objects at this location
      var levindex = _trail.Current[_trailindex];
      foreach (var obj in objs) {

        // only move an object if it's here
        var locator = Locator.Create(levindex, _gamedef.GetLayer(obj));
        if (_level[locator] == obj) { 
          var found = false;
          
          // first try to update move list
          for (var movex = 0; movex < _movers.Count; ++movex) {
            var mover = _movers[movex];
            if (mover.Object == obj && mover.Locator.Index == levindex) {
              found = true;
              if (_movers[movex].Direction != dir) {
                Logger.WriteLine(3, "Mover update {0} to '{1}' at {2}", dir, _gamedef.GetName(obj), locator);
                if (dir == Direction.None)
                  _movers.RemoveAt(movex);
                else _movers[movex].Direction = dir;
                _moved = true;    // TODO: only for net change
              }
              break;
            }
          }
          // otherwise add a new mover
          if (!found && dir != Direction.None) {
            Logger.WriteLine(3, "Mover add {0} to '{1}' at {2}", dir, _gamedef.GetName(obj), locator);
            _movers.Add(Mover.Create(obj, locator, dir, RuleGroup));
            _moved = true;    // TODO: only for net change
          }
        }
      }
      return false;
    }

    ///===== Command instructions ==============================================
    ///
    /// Sound and message have arguments, queued for later execution
    /// Command does not; some commands act immediately, some are queued

    internal bool OpSoundT(string seed) {
      Logger.WriteLine(4, "Sound: {0}", seed);
      AddSound(seed);
      return false;
    }

    internal bool OpMessageT(string text) {
      Logger.WriteLine(4, "Message: {0}", text);
      Messages.Add(text);
      return false;
    }

    internal bool OpCommandC(RuleCommand command) {
      Logger.WriteLine(4, "Command: {0}", command);
      AddCommand(command);
      if (command == RuleCommand.Checkpoint)
        _checkpoint = _level.Clone("checkpoint");
      return false;
    }

    //===== implementation =====

    // clear out whatever is in the location, return true if was not empty
    bool ClearLocation(int levindex, int layer) {
      if (_level[levindex, layer] != 0) {
        _level[levindex, layer] = 0;
        var mover = _movers.FirstOrDefault(m => m.Locator.Index == levindex && m.Locator.Layer == layer);
        if (mover != null)
          _movers.Remove(mover);
        return true;
      }
      return false;
    }

    // test whether there is a matching object at this location
    // direction can be specified, None for ignore or Stationary for not moving
    // oper is No, All or Any
    bool IsMatch(int levindex, HashSet<int> objects, Direction direction, MatchOperator oper) {
      // no objects are always found but never moving
      if (objects.Count == 0)
        return (oper == MatchOperator.No) ? true : (direction == Direction.None); // dubious
      var found = IsInLevel(levindex, objects, oper);
      if (found && direction != Direction.None)
        found = IsMover(levindex, objects, direction);
      return found;
    }

    // Look for object moving in specific direction, or none
    bool IsMover(int levindex, HashSet<int> objects, Direction direction) {
      foreach (var move in _movers) { 
        if (levindex == move.Locator.Index && objects.Contains(move.Object)) {
          if (direction == Direction.Moving) return true;
          if (direction == Direction.Stationary) return false;
          if (move.Direction == direction) return true;
        }
      }
      // not found, not a mover
      return (direction == Direction.Stationary);
    }

    // Find object in level, moving or not
    bool IsInLevel(int levindex, HashSet<int> objects, MatchOperator oper) {
      if (oper == MatchOperator.All)
        return objects.All(o => _level[levindex, _gamedef.GetLayer(o)] == o);
      else return objects.Any(o => _level[levindex, _gamedef.GetLayer(o)] == o) == (oper != MatchOperator.No);
    }
  }
}