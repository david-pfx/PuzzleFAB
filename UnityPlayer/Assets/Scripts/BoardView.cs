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

  MainController _main;
  SpriteRenderer _renderer;

  // Use this for initialization
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
    case GameState.EndLevel:
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

}
