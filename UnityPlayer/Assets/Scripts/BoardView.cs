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
  public Vector3 Location;
  public GameObject TilePrefab;

  GameModel _model { get { return _main.Model; } }
  MainController _main;
  SpriteRenderer _renderer;
  List<GameObject> _knownobjects = new List<GameObject>();

  void Start() {
    _main = FindObjectOfType<MainController>();
    // set size to cover given area
    _renderer = GetComponent<SpriteRenderer>();
    var size = _renderer.sprite.bounds.size;
    var scale = Math.Max(Location.x / size.x, Location.y / size.y);
    transform.localScale = new Vector3(scale, scale, 0);  // FIXME
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
  void SetVisible(bool visible) {
    if (visible != _visible) {
      _renderer.enabled = visible;
      _renderer.color = _main.Items.BackgroundColour;
      _visible = visible;
    }
  }

  internal void CreateTiles(Vector3Int layout, float scale, int levelwidth, GameObject tileprefab) {
    // create same order as level, from top left back
    var origin = new Vector3(-layout.x / 2.0f + 0.5f, -layout.y / 2.0f + 0.5f, 0f) * scale;
    var levelindex = 0;
    for (int y = layout.y - 1; y >= 0; --y) {
      for (int x = 0; x < layout.x; x++) {
        for (int z = 1; z <= layout.z; z++) {
          var tile = Instantiate(tileprefab);
          var tileview = tile.GetComponent<TileView>();
          tileview.Location = new Vector3(origin.x + scale * x, origin.y + scale * y, origin.z - z / 10.0f);
          tileview.Size = new Vector2(scale, scale);
          tileview.LevelIndex = new Vector2Int(levelindex + x, z);
          _knownobjects.Add(tile);
        }
      }
      levelindex += levelwidth;
    }
    Util.Trace(1, ">[CT done {0}]", layout.x * layout.y * layout.z);
  }

  // destroy board and all objects on it
  internal void DestroyTiles() {
    if (_knownobjects.Count > 0) {
      Util.Trace(1, "Destroy board count={0}", _knownobjects.Count);
      foreach (var obj in _knownobjects)
        Destroy(obj);
      _knownobjects.Clear();
    }
  }


}
