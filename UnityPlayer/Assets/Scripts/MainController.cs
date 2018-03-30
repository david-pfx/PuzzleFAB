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
using UnityEngine.Assertions;
using PuzzLangLib;

// very important state that drives the entire UI
public enum GameState {
  None,
  Select, Intro, Pause, Quit,
  Error, Level, Message, GameOver,
  Gist,
}

/// <summary>
/// Implements main functions for getting started, loading and running games
/// </summary>
public class MainController : MonoBehaviour {
  public GameObject BoardPrefab;
  public GameObject TilePrefab;
  public AudioSource AudioSource;
  public float BoardScale = 0.90f;
  public int Noisy = 2;

  internal GameState GameState { get; set; }
  internal GameModel Model { get { return _modelinfo.Model; } }
  internal GameModelInfo ModelInfo { get { return _modelinfo; } }
  internal GameDef GameDef { get { return _modelinfo.Model.GameDef; } }
  internal ItemManager Items { get { return _itemmx; } }
  internal string Message { get; private set; }

  UiManager _uimx;
  ItemManager _itemmx;
  GameModelInfo _modelinfo;
  GameObject _board;
  BoardView _boardview;

  private void Awake() {
    Util.MaxLoggingLevel = Noisy;
    Assert.raiseExceptions = true;
    DOLE.Logger.Level = Noisy;
  }

  void Start() {
    _uimx = FindObjectOfType<UiManager>();
    _itemmx = FindObjectOfType<ItemManager>();
    _itemmx.FindAllScripts();
    GameState = LoadScript("<startup>");
  }

  // update our state
  void Update() {
    switch (GameState) {
    case GameState.Intro:
      break;
    case GameState.Level:
      if (_board == null) CreateBoard();
      break;
    case GameState.GameOver: // todo: end level
      if (_board != null) DestroyBoard();
      break;
    case GameState.Quit:
      Application.Quit();
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#endif
      break;
    }
  }

  // accessed on startup or when new script selected
  internal GameState LoadScript(string scriptname) {
    Util.Trace(1, "Load script '{0}'", scriptname);
    if (_uimx != null) {
      _uimx.TitleText.text = scriptname;
      _uimx.MessageText.text = "";
      _uimx.StatusText.text = "Loading...";
    }
    return TryCompile(scriptname) ? GameState.Intro : GameState.Error;
  }

  // access by tile view -- must guard against level change
  internal Sprite GetSprite(Vector2Int levelindex) {
    if (_board == null) return null;
    // offset by screen index to handle zoom/flick
    var objectid = Model.CurrentLevel[Model.ScreenIndex + levelindex.x, levelindex.y];
    var sprite = _modelinfo.GetSprite(objectid);
    return sprite;
  }

  internal void PlaySounds(IEnumerable<string> sounds) {
    foreach (var sound in sounds) {
      var clip = _modelinfo.GetClip(sound);
      Util.Trace(1, "Play sound '{0}'", sound);
      AudioSource.PlayOneShot(clip);
    }
  }

  internal GameState ReloadScript(bool useindex) {
    DestroyBoard();
    var newindex = useindex ? _uimx.RestartLevelIndex : 0;
    LoadLevel(newindex);
    return (newindex == 0) ? GameState.Intro : GameState.Level;
  }

  // called when board should be redrawn next time
  internal void ClearScript() {
    DestroyBoard();
  }

  // wrapper to handle log and sounds
  internal void AcceptInput(string input) {
    _modelinfo.AcceptInputs(input);
    Message = Model.CurrentMessage;
    PlaySounds(Model.Sounds);
  }

  // wrapper to handle log and sounds
  void LoadLevel(int levelindex) {
    _modelinfo.LoadLevel(levelindex);
    Message = Model.CurrentMessage;
    PlaySounds(Model.Sounds);
  }

  //--- impl

  bool TryCompile(string name) {
    bool ok = false;
    DestroyBoard();
    _modelinfo = new GameModelInfo(_itemmx);
    var script = _itemmx.ReadScript(name, false);
    if (script == null) Message = "Not yet loaded...";
    else if (!_modelinfo.Compile(name, script))
      Message = _modelinfo.Message;
    else { 
      LoadLevel(0);
      ok = true;
    }
    return ok;
  }

  // create a new board, destroy old first
  void CreateBoard() {
    Debug.Assert(Model.InLevel);
    var level = Model.CurrentLevel;
    Util.Trace(1, "Create board '{0}' level {1} size {2}x{3}", 
      GameDef.GetSetting(OptionSetting.title, "unknown"), Model.CurrentLevelIndex, level.Length, level.Depth);
    var camera = Camera.main;
    var camerasize = new Vector2(2 * camera.orthographicSize * camera.aspect, 2 * camera.orthographicSize);

    _board = Instantiate(BoardPrefab);
    _boardview = _board.GetComponent<BoardView>();
    _boardview.Location = new Vector3(camerasize.x, camerasize.y);

    var levelsize = new Vector3Int(level.Width, level.Height, level.Depth);
    var layout = (_modelinfo.ScreenSize == Vector2Int.zero) ? levelsize
      : Vector3Int.Min(levelsize, new Vector3Int(_modelinfo.ScreenSize.x, _modelinfo.ScreenSize.y, level.Depth));
    var scale = Math.Min(2 * camera.orthographicSize / layout.y,
      2 * camera.orthographicSize * camera.aspect / layout.x) * BoardScale;
    _boardview.CreateTiles(layout, scale, level.Width, TilePrefab);
  }

  // destroy board and all objects on it
  void DestroyBoard() {
    if (_board != null) {
      _boardview.DestroyTiles();
      Destroy(_board);
      _board = null;
    }
  }
}
