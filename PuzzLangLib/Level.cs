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
  /// specific location of an object in a level
  /// </summary>
  struct Locator {
    internal int Index;
    internal int Layer;
    internal static Locator Null = Create(-1, -1);
    internal bool IsNull { get { return Index == -1; } }

    static internal Locator Create(int index, int layer) {
      return new Locator { Index = index, Layer = layer };
    }
    public override string ToString() {
      return $"[{Index}:{Layer}]";
    }
  }

  /// <summary>
  /// Layout of current location of objects
  /// Also tracks changes
  /// </summary>
  public class Level {
    public string Name { get; private set; }
    public int Width { get; private set; }
    public int Length { get { return _locations.GetLength(0); } }
    public int Height { get { return Length / Width; } }
    public int Depth { get { return _locations.GetLength(1); } }
    internal int[,] Locations { get { return _locations; } }
    internal int ChangesCount { get { return _changes.Count; } }

    internal int this[int x, int y, int layer] {
      get { return _locations[GetLocation(x, y), layer - 1]; }
      set { SetCell(GetLocation(x, y), layer - 1, value); }
    }
    public int this[int index, int layer] {
      get { return _locations[index, layer - 1]; }
      set { SetCell(index, layer - 1, value); }
    }
    internal int this[Locator locator] {
      get { return _locations[locator.Index, locator.Layer - 1]; }
      set { SetCell(locator.Index, locator.Layer - 1, value); }
    }
    internal int GetLocation(int x, int y) {
      if (!IsLocation(x, y)) throw Error.Assert("out of range");
      return y * Width + x;
    }
    internal bool IsLocation(int x, int y) {
      return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    internal Dictionary<Locator, int> _changes = new Dictionary<Locator, int>();
    int[,] _locations;

    // lookup increment for x, y
    static readonly Dictionary<Direction, Pair<int, int>> _steplookup = new Dictionary<Direction, Pair<int, int>> {
      { Direction.None, Pair.Create(0,0) },
      { Direction.Up, Pair.Create(0,-1) },
      { Direction.Right, Pair.Create(1,0) },
      { Direction.Down, Pair.Create(0,1) },
      { Direction.Left, Pair.Create(-1,0) },
      { Direction.Action, Pair.Create(0,0) },
    };

    //--- ctor
    static internal Level Create(int x, int y, int z, string name) {
      return new Level {
        Name = name,
        _locations = new int[x * y, z],
        Width = x,
      };
    }

    // Create new level as clone of this one
    internal Level Clone(string name = null) {
      return new Level {
        Name = name ?? Name,
        _locations = _locations.Clone() as int[,],
        Width = Width,
      };
    }

    // return list of objects found at this location (for testing and background)
    internal IList<int> GetObjects(int location) {
      return Enumerable.Range(1, Depth).Select(z => this[location, z])
        .Where(v => v != 0)
        .ToList();
    }

    internal bool IsValid(Direction direction) {
      return _steplookup.ContainsKey(direction);
    }

    // Try to step a level index by a direction
    internal int? Step(int levelindex, Direction direction) {
      if (!_steplookup.ContainsKey(direction)) throw Error.Assert("dir: {0}", direction);
      var x = levelindex % Width + _steplookup[direction].Item1;
      var y = levelindex / Width + _steplookup[direction].Item2;
      if (!IsLocation(x, y)) return null;
      return GetLocation(x, y);
    }

    // update contents, keeping a record of change location and initial content
    void SetCell(int index, int z, int value) {
      Logger.WriteLine(4, "Setcell {0}:{1}={2} cc={3}", index, z, value, ChangesCount);
      if (_locations[index, z] != value) {
        var locator = Locator.Create(index, z + 1);
        // if location already known, either ignore or delete (back the way it was)
        // otherwise add a new change
        if (_changes.ContainsKey(locator)) {
          if (_changes[locator] == value)
            _changes.Remove(locator);
        } else
          _changes[locator] = _locations[index, z];
        _locations[index, z] = value;
      }
    }
  }
}
