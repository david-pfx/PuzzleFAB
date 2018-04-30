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
using UnityEngine;
using PuzzLangLib;

public class BoardView : MonoBehaviour {
  public GameObject TilePrefab;

  GameModel _model { get { return _main.Model; } }
  MainController _main { get { return MainController.Instance; } }
  Vector2 _boardsize;
  SpriteRenderer _renderer;
  List<GameObject> _tiles = new List<GameObject>();

  internal void Setup(Vector2 boardsize) {
    _boardsize = boardsize;
  }

  void Start() {
    // set size to cover given area
    _renderer = GetComponent<SpriteRenderer>();
    var ssize = _renderer.sprite.bounds.size;
    _renderer.transform.localScale = new Vector3(_boardsize.x / ssize.x, _boardsize.y / ssize.y, 0);
  }

  // update our state
  void Update() {
    switch (_main.GameState) {
    case GameState.Level:
      SetVisible(true);
      break;
    default:
      SetVisible(false);
      break;
    }
  }

  bool _visible = false;
  TileView _lasttileview = null;

  void SetVisible(bool visible) {
    if (visible != _visible) {
      _renderer.enabled = visible;
      _renderer.color = _main.BackgroundColour;
      _visible = visible;
    }
  }

  // find the coords of cell containing a point
  internal int? FindCellIndex(Vector2 point) {
    if (_lasttileview != null && _lasttileview.Rect.Contains(point))
      return _lasttileview.CellIndex.x;
    foreach (var tile in _tiles) {
      var tileview = tile.GetComponent<TileView>();
      if (tileview.Rect.Contains(point)) {
        _lasttileview = tileview;
        return tileview.CellIndex.x;
      }
    }
    _lasttileview = null;
    return null;
  }

internal void CreateTiles(Vector3Int layout, float scale, int levelwidth, GameObject tileprefab) {
    // create same order as level, from top left back
    var origin = new Vector3(-layout.x / 2.0f + 0.5f, -layout.y / 2.0f + 0.5f, 0f) * scale;
    var cellindex = 0;
    for (int y = layout.y - 1; y >= 0; --y) {
      for (int x = 0; x < layout.x; x++) {
        for (int z = 1; z <= layout.z; z++) {
          var tile = Instantiate(tileprefab);
          var tileview = tile.GetComponent<TileView>();
          tileview.Setup(new Vector3(origin.x + scale * x, origin.y + scale * y, origin.z - z / 10.0f),
            new Vector2(scale, scale), 
            new Vector2Int(cellindex + x, z));
          _tiles.Add(tile);
        }
      }
      cellindex += levelwidth;
    }
    Util.Trace(2, ">[CT done {0}]", layout.x * layout.y * layout.z);
  }

  // destroy board and all objects on it
  internal void DestroyTiles() {
    if (_tiles.Count > 0) {
      Util.Trace(1, "Destroy board count={0}", _tiles.Count);
      foreach (var obj in _tiles)
        Destroy(obj);
      _tiles.Clear();
    }
  }


}
