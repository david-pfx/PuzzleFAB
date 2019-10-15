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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DOLE;

namespace PuzzLangLib {
  /// <summary>
  /// List of moving objects
  /// </summary>
  class Mover {
    internal int ObjectId;
    internal int CellIndex;
    internal int Layer;
    internal Direction Direction;
    internal RuleGroup RuleGroup;

    // create a mover, which may target an illegal location
    static internal Mover Create(int obj, int cellindex, int layer, Direction dir, RuleGroup group = null, bool rigid = false) {
      if (!GameDef.MoveDirections.Contains(dir)) throw Error.Assert("create {0}", dir);
      return new Mover {
        ObjectId = obj,
        CellIndex = cellindex,
        Layer = layer,
        Direction = dir,
        RuleGroup = group,
      };
    }
    public override string ToString() {
      return $"Mover<{ObjectId},{CellIndex},{Direction}>";
    }
  }

  /// <summary>
  /// Current state of level for access by pattern matching
  /// </summary>
  class LevelState {
    GameDef _gamedef;
    Level _level;
    List<Mover> _movers;    // list of objects that are moving
    HashSet<int>[] _objectmap;
    Dictionary<Locator, int> _changes = new Dictionary<Locator, int>();

    public int MoverCount { get; internal set; } // TODO:
    internal int ChangesCount { get { return _changes.Count; } }

    internal static LevelState Create(GameDef gamedef, Level level, List<Mover> movers) {
      return new LevelState {
        _gamedef = gamedef,
        _level = level,
        _movers = movers,
      }.Init();
    }

    LevelState Init() {
      // one bit array per cell index, spans all levels
      _objectmap = new HashSet<int>[_level.Length];
      for (int x = 0; x < _level.Length; x++) 
        _objectmap[x] = new HashSet<int>(_level.GetObjects(x));
      return this;
    }

    internal Level CloneLevel(string name) {
      return _level.Clone(name);
    }

    internal IEnumerable<int> GetObjects(int cellindex) {
      // note: cannot use object map, wrong ordering
      return _level.GetObjects(cellindex);
    }

    internal IEnumerable<int> GetMovers() {
      return _movers.SelectMany(m => new int[] { m.ObjectId, m.CellIndex, (int)m.Direction });
    }

    internal int? Step(int cellindex, Direction direction) {
      return _level.Step(cellindex, direction);
    }

    internal void SetCell(int cellindex, int layer, int value) {
      //Logger.WriteLine(4, "Setcell {0}:{1}={2} cc={3}", cellindex, layer, value, ChangesCount);
      if (_level[cellindex, layer] != value) {

        // if location already known, either ignore or delete (back the way it was)
        // otherwise add a new change
        var locator = Locator.Create(cellindex, layer);
        if (_changes.ContainsKey(locator)) {
          if (_changes[locator] == value)
            _changes.Remove(locator);
        } else
          _changes[locator] = _level[cellindex, layer];

        // update map
        if (value == 0) _objectmap[cellindex].Remove(_level[cellindex, layer]);
        else _objectmap[cellindex].Add(value);
        _level[cellindex, layer] = value;
      }
    }

    // test whether there is a matching object at this location
    // direction can be specified, None for ignore or Stationary for not moving
    // oper is No, All or Any
    internal bool IsMatch(int cellindex, HashSet<int> objects, Direction direction, MatchOperator oper) {
      // no objects matches no objects (ignore direction and operator)
      if (objects.Count == 0)
        return true;                                        // don't care
      var found = IsInLevel(cellindex, objects, oper);
      if (found && direction != Direction.None)
        found = IsMover(cellindex, objects, direction);
      return found;
    }

    // Find object in level, moving or not
    bool IsInLevel(int cellindex, HashSet<int> objects, MatchOperator oper) {
      if (oper == MatchOperator.All)
        return _objectmap[cellindex].IsSupersetOf(objects);
      var found = _objectmap[cellindex].Overlaps(objects);
      return found == (oper != MatchOperator.No);
    }

    // return cellindex list with objects that are moving this way
    internal IList<int> FindMatches(HashSet<int> objects, Direction direction, MatchOperator oper) {
      if (direction == Direction.None)
        return FindStatics(objects, oper).ToList();
      if (direction == Direction.Stationary)
        return FindStatics(objects, oper)
          .Where(x => !_movers.Any(m => m.CellIndex == x && objects.Contains(m.ObjectId)))
          .ToList();
      return FindMovers(objects, direction, oper).ToList();
    }

    // create object at location 
    internal bool CreateObject(int obj, int cellindex) {
      var layer = _gamedef.GetLayer(obj);
      if (_level[cellindex, layer] == obj) return false;
      ClearLocation(cellindex, layer);
      SetCell(cellindex, layer, obj);
      return true;
    }

    // destroy object at location
    internal bool DestroyObject(int obj, int cellindex) {
      var layer = _gamedef.GetLayer(obj);
      if (_level[cellindex, layer] != obj) return false;
      ClearLocation(cellindex, layer);
      return true;
    }

    // clear out whatever is in the location, return true if was not empty
    bool ClearLocation(int cellindex, int layer) {
      if (_level[cellindex, layer] != 0) {
        SetCell(cellindex, layer, 0);
        //_level[cellindex, layer] = 0;
        var mover = _movers.FirstOrDefault(m => m.CellIndex == cellindex && _gamedef.GetLayer(m.ObjectId) == layer);
        if (mover != null)
          _movers.Remove(mover);
        return true;
      }
      return false;
    }

    // move an object at location in a direction
    // direction None used to cancel existing movement
    internal void MoveObject(int obj, int cellindex, Direction direction, RuleGroup rulegroup) {
      if (!(GameDef.MoveDirections.Contains(direction) || direction == Direction.Stationary))
        throw Error.Assert("move {0}", direction);
      // only move an object if it's here, else add a new mover
      if (_level[cellindex, _gamedef.GetLayer(obj)] == obj) {
        var found = false;

        // first try to update move list
        // do not use rule group in comparison -- if rigid, this may cause a problem
        for (var movex = 0; movex < _movers.Count; ++movex) {
          var mover = _movers[movex];
          if (mover.ObjectId == obj && mover.CellIndex == cellindex) {
            found = true;
            if (_movers[movex].Direction != direction) {
              Logger.WriteLine(2, "Mover now {0} to {1} at {2}", direction, _gamedef.ShowName(obj), cellindex);
              if (direction == Direction.Stationary)
                _movers.RemoveAt(movex);
              else _movers[movex].Direction = direction;
              //_vm_moved = true;    // TODO: only for net change
            }
            break;
          }
        }
        // otherwise add a new mover
        if (!found && direction != Direction.Stationary) {
          Logger.WriteLine(2, "Mover set {0} to {1} at {2}", direction, _gamedef.ShowName(obj), cellindex);
          _movers.Add(Mover.Create(obj, cellindex, _gamedef.GetLayer(obj), direction, rulegroup));
          //_vm_moved = true;    // TODO: only for net change
        }
      }
    }

    // Look for object moving in specific direction, or none
    bool IsMover(int cellindex, HashSet<int> objects, Direction direction) {
      foreach (var move in _movers) {
        if (cellindex == move.CellIndex && objects.Contains(move.ObjectId)) {
          if (direction == Direction.Moving) return true;
          if (direction == Direction.Stationary) return false;
          if (move.Direction == direction) return true;
        }
      }
      // not found, not a mover
      return (direction == Direction.Stationary);
    }

    // find object in level using object map
    IEnumerable<int> FindStatics(HashSet<int> objects, MatchOperator oper) {
      for (var cellindex = 0; cellindex < _level.Length; ++cellindex) {
        if (objects.Count == 0)
          yield return cellindex;
        else if (oper == MatchOperator.All) {
          if (_objectmap[cellindex].IsSupersetOf(objects))
            yield return cellindex;
        } else { // Any or No
          var found = _objectmap[cellindex].Overlaps(objects);
          if (found == (oper != MatchOperator.No))
            yield return cellindex;
        }
      }
    }

    IEnumerable<int> FindStaticsOld(HashSet<int> objects, MatchOperator oper) {
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
        .OrderBy(m => m.CellIndex);
      if (oper == MatchOperator.Any) {
        foreach (var move in moves)
          if (objects.Contains(move.ObjectId))
            yield return move.CellIndex;
      } else {
        foreach (var move in moves)
          if (objects.Contains(move.ObjectId))
            if (objects
              .All(o => moves
                .Any(m => m.ObjectId == move.ObjectId && m.CellIndex == move.CellIndex)))
              yield return move.CellIndex;
      }
    }
  }
}
