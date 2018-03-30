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
using DOLE;
using System.IO;

//public enum GameStatus {
//  None, Choose, Error, Intro, Play, Pause, Message, Prompt, Finished
//}

public enum GameState {
  None,
  Select, Intro, Pause, Quit,
  Error, Level, Message, GameOver,
  EndLevel,
}


public class MainController : MonoBehaviour {
  public string InitialScript = "SimpleBlockPushing.txt";
  public GameObject BoardPrefab;
  public GameObject TilePrefab;
  public AudioSource AudioSource;

  internal GameState GameState { get; set; }
  internal GameModel Model { get { return _model; } }
  internal GameDef GameDef { get { return _model.GameDef; } }
  internal ItemManager Items { get { return _itemmx; } }
  internal string CurrentScript { get; set; }
  internal string Message { get; private set; }

  UiManager _uimx;
  ItemManager _itemmx;
  GameModel _model;
  bool boardexists = false;
  List<GameObject> _knownobjects = new List<GameObject>();

  private void Awake() {
    Util.MaxLoggingLevel = 2;
  }

  // Use this for initialization
  void Start() {
    _uimx = FindObjectOfType<UiManager>();
    _itemmx = FindObjectOfType<ItemManager>();
    _itemmx.FindAllScripts();
    GameState = LoadScript(InitialScript);
  }

  // update our state
  void Update() {
    switch (GameState) {
    case GameState.Intro:
    case GameState.EndLevel:
      break;
    case GameState.Level:
      if (!boardexists)
        CreateBoard(_model.LastLevel);
        break;
    case GameState.GameOver: // todo: end level
      if (boardexists) DestroyBoard();
      break;
    case GameState.Quit:
      Application.Quit();
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#endif
      break;
    }
  }

  // access by tile view -- must guard against level change
  internal Sprite GetSprite(Vector2Int levelindex) {
    if (!boardexists) return null;
    var objectid = _model.LastLevel[levelindex.x, levelindex.y];
    var sprite = Items.GetSprite(objectid);
    return sprite;
  }

  internal void PlaySounds(IEnumerable<string> sounds) {
    foreach (var sound in sounds) {
      var clip = _itemmx.GetClip(sound);
      AudioSource.PlayOneShot(clip);
    }
  }

  // accessed on startup or when new script selected
  internal GameState LoadScript(string scriptname) {
    Util.Trace(1, "Load script '{0}'", scriptname);
    _uimx.StatusText.text = "Loading " + scriptname; // FIXME
    CurrentScript = scriptname;
    DestroyBoard();
    _model = _itemmx.Compile(scriptname);
    _itemmx.ShowLog();
    if (_model == null) {
      Message = _itemmx.Message;
      return GameState.Error;
    } else {
      _model.LoadLevel(0);
      return GameState.Intro;
    }
  }

  internal GameState ReloadScript(bool useindex) {
    DestroyBoard();
    var newindex = useindex ? _uimx.RestartLevelIndex : 0;
    _model.LoadLevel(newindex);
    return (newindex == 0) ? GameState.Intro : GameState.Level;
  }

  // accessed when input triggers a new level -- force rebuild of board on next update
  internal void NewLevel() {
    DestroyBoard();
  }

  // create a new board, destroy old first
  void CreateBoard(Level level) {
    DestroyBoard();
    Util.Trace(1, "Create board '{0}' {1} {2}x{3}", 
      GameDef.GetSetting(OptionSetting.title, "unknown"), Model.CurrentLevelIndex, level.Length, level.Depth);
    var camera = Camera.main;
    var camerasize = new Vector2(2 * camera.orthographicSize * camera.aspect, 2 * camera.orthographicSize);

    var board = Instantiate(BoardPrefab);
    var boardview = board.GetComponent<BoardView>();
    boardview.Location = new Vector3(camerasize.x, camerasize.y);
    _knownobjects.Add(board);

    var scale = Math.Min(2 * camera.orthographicSize / level.Height,
      2 * camera.orthographicSize * camera.aspect / level.Width) * 0.95f;
    for (int i = 0; i < level.Length; i++) {
      var x = (i % level.Width) - level.Width / 2.0f + 0.5f;
      var y = (i / level.Width) - level.Height / 2.0f + 0.5f;
      for (int j = 1; j <= level.Depth; j++) {
        var tile = Instantiate(TilePrefab);
        //tile.transform.SetParent(_boardobject.transform, true);
        var tileview = tile.GetComponent<TileView>();
        tileview.Location = new Vector3(scale * x, -scale * y, -j / 10.0f);
        tileview.Size = new Vector2(scale, scale);
        tileview.LevelIndex = new Vector2Int(i, j);
        _knownobjects.Add(tile);
      }
    }
    boardexists = true;
  }

  // destroy board and all objects on it
  void DestroyBoard() {
    if (_knownobjects.Count > 0) {
      Util.Trace(1, "Destroy board count={0}", _knownobjects.Count);
      foreach (var obj in _knownobjects)
        Destroy(obj);
      _knownobjects.Clear();
    }
    boardexists = false;
  }
}
