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
using UnityEngine;

public class TileView : MonoBehaviour {
  public Vector2Int LevelIndex;
  public Vector3 Location;
  public Vector2 Size;

  MainController _main;
  SpriteRenderer _renderer;
  Sprite _sprite = null;

  // publics need to be set before this gets called
  void Start() {
    _main = FindObjectOfType<MainController>();
    _renderer = GetComponent<SpriteRenderer>();
    transform.localPosition = Location;
    SetSprite(true);
  }

  // update our state
  void Update() {
    switch (_main.GameState) {
    case GameState.Level:
      SetSprite();
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
      _visible = visible;
    }
  }

  void SetSprite(bool force = false) {
    var sprite = _main.GetSprite(LevelIndex);
    if (force || sprite != _sprite) {
      _renderer.sprite = sprite;
      if (sprite != null) {
        if (LevelIndex.x == _main.Model.CurrentLevel.Length - 1) Util.Trace(2, ">SetSprite {0}", LevelIndex);
        var cursize = sprite.bounds.size;
        var scale = Math.Max(Size.x / cursize.x, Size.y / cursize.y);
        transform.localScale = new Vector3(scale, scale, 0);
      }
      _sprite = sprite;
    }
    SetVisible(sprite != null);
  }

}
