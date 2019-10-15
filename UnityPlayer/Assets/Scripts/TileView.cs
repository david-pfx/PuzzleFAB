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
using PuzzLangLib;

public class TileView : MonoBehaviour {
  public GameObject TileText;
  public Vector2Int CellIndex;
  public int ObjectId = 0;

  MainController _main { get { return MainController.Instance; } }
  ModelInfo _modelinfo { get { return _main.ModelInfo; } }
  GameModel _model { get { return _modelinfo.Model; } }

  // puzzle cell index to display
  // rectangle for hit testing
  internal Rect Rect;

  enum DisplayMode { None, Disable, Sprite, Text };
  SpriteRenderer _renderer;
  TextMesh _textmesh;
  Vector3 _position;
  Vector2 _size;
  DisplayMode _mode = DisplayMode.None;
  bool _visible = false;

  internal void Setup(Vector3 position, Vector2 size, Vector2Int cellindex) {
    _position = position;
    _size = size;
    CellIndex = cellindex;
  }
  
  // publics need to be set before this gets called
  void Start() {
    _renderer = GetComponent<SpriteRenderer>();
    _textmesh = TileText.GetComponent<TextMesh>();
    transform.localPosition = _position;
    Rect = new Rect(_position.x - _size.x / 2, _position.y - _size.y / 2, _size.x, _size.y);
    SetSprite(true);
    SetVisible(false);
  }

  // update our state
  void Update() {
    switch (_main.GameState) {
    case GameState.Level:
    case GameState.EndLevel:
      SetSprite();
      SetVisible(true);
      break;
    default:
      SetVisible(false);
      break;
    }
  }

  // control visibility
  void SetVisible(bool visible) {
    if (visible != _visible) {
      _textmesh.gameObject.SetActive(visible && _mode == DisplayMode.Text);
      _renderer.enabled = (visible && _mode == DisplayMode.Sprite);
      _visible = visible;
    }
  }

  // use model to find the object to display, or not
  void SetSprite(bool force = false) {
    if (!force && !_main.EnableViewUpdate) return;
    var objid = _main.GetObjectId(CellIndex);
    var puzzobj = (objid == 0) ? null : _model.GetObject(objid);
    _renderer.sprite = _modelinfo.GetSprite(objid);

    // Display either text or sprite -- cannot fix render order for both!
    if (puzzobj != null && puzzobj.Text != null) {
      _textmesh.text = puzzobj.Text;
      _textmesh.color = _modelinfo.ColorFromIndex(puzzobj.TextColour);
      var scale = _size.y;
      transform.localScale = new Vector3(scale, scale, 1) * puzzobj.Scale;
      _mode = DisplayMode.Text;
    } else if (_renderer.sprite != null) {
      if (CellIndex.x == _main.Model.CurrentLevel.Length - 1) Util.Trace(2, ">SetSprite {0}", CellIndex);
      var cursize = _renderer.sprite.bounds.size;
      var scale = Math.Max(_size.x / cursize.x, _size.y / cursize.y);
      transform.localScale = new Vector3(scale, scale, 0) * puzzobj.Scale;
      _mode = DisplayMode.Sprite;
    } else {
      _mode = DisplayMode.Disable;
    }
    ObjectId = objid;  // no repeat unless object changes
    _visible = false;   // trigger update
  }
}
