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
using DOLE;
using System.IO;

// very important state that drives the entire UI
public enum GameState {
  None,
  Select, Intro, Pause, Quit,
  Error, Level, Message, GameOver,
  Status,
}

/// <summary>
/// Implements main functions for getting started, loading and running games
/// Also contains most of the settable items
/// </summary>
public class MainController : MonoBehaviour {
  // settings
  public GameObject BoardPrefab;
  public GameObject TilePrefab;
  public AudioSource AudioSource;
  public AudioClip DefaultSound;
  public string UserPuzzlesDirectory = "Games";
  public string BuiltinPuzzlesDirectory = "Puzzles";
  public TextAsset Gists;
  public string GistFile = "Gists.txt";
  public string StartupScript;
  public Color BackgroundColour = Color.yellow;
  public Color ForegroundColour = Color.blue;
  public float AgainInterval = 0.25f;
  public float RealtimeInterval = 0.25f;
  public float BoardScale = 0.90f;
  public bool VerboseLogging = false;
  public int Noisy = 2;

  internal static MainController Instance { get; private set; }
  internal GameState GameState { get; set; }
  internal string Message { get; private set; }
  internal GameModel Model { get { return _modelinfo.Model; } }
  internal ModelInfo ModelInfo { get { return _modelinfo; } }
  internal GameDef GameDef { get { return _modelinfo.Model.GameDef; } }
  internal BoardView Board { get { return _boardview; } }
  internal ScriptLoader ScriptLoader { get; private set; }
  internal string AppDirectory { get; private set; }
  internal string UserDirectory { get; private set; }

  UiManager _uimx;
  ModelInfo _modelinfo;
  GameObject _board;
  BoardView _boardview;
  string _scriptname;
  float _screentoortho;

  void Awake() {
    Instance = this;
    Util.MaxLoggingLevel = Noisy;
    Assert.raiseExceptions = true;
    DOLE.Logger.Level = Noisy;
  }

  void Start() {
    Util.Trace(1, "Main start '{0}'", Noisy);
    _uimx = FindObjectOfType<UiManager>();
    ScriptLoader = FindObjectOfType<ScriptLoader>();
    AppDirectory = Path.GetDirectoryName(Application.dataPath);
    UserDirectory = Util.Combine(AppDirectory, UserPuzzlesDirectory);
    ScriptLoader.Setup();
    GameState = LoadScript(StartupScript);
  }

  // update our state
  void Update() {
    switch (GameState) {
    case GameState.Intro:
      break;
    case GameState.Level:
      if (_board == null) CreateBoard();
      break;
    case GameState.Status:
      _uimx.MessageText.text = GetStatusInfo();
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

  internal string GetStatusInfo() {
    return "data={0}\napp={1}\nuser={2}\n".Fmt(Application.dataPath, AppDirectory, UserDirectory);
  }

  // accessed on startup or when new script selected
  internal GameState LoadScript(string scriptname = null) {
    if (scriptname == null) {
      scriptname = _scriptname;
      ScriptLoader.ReadScript(scriptname, true);
    } else _scriptname = scriptname;
    Util.Trace(1, "Load script '{0}'", scriptname);
    if (_uimx != null) {
      _uimx.TitleText.text = scriptname;
      _uimx.MessageText.text = "";
      _uimx.StatusText.text = "Loading...";
    }
    return TryCompile(scriptname) ? GameState.Intro : GameState.Error;
  }

  // access by tile view -- must guard against level change
  internal int GetObjectId(Vector2Int cellcoords) {
    if (_board == null) return 0;
    // offset by screen index to handle zoom/flick
    return Model.CurrentLevel[Model.ScreenIndex + cellcoords.x, cellcoords.y];
  }

  internal void PlaySounds(IEnumerable<string> sounds) {
    foreach (var sound in sounds) {
      var clip = _modelinfo.GetClip(sound);
      Util.Trace(1, "Play sound '{0}'", sound);
      AudioSource.PlayOneShot(clip);
    }
  }

  internal GameState RestartScript(bool useindex) {
    DestroyBoard();
    var newindex = useindex ? _uimx.RestartLevelIndex : 0;
    AcceptInput("level {0}".Fmt(newindex));
    return (newindex == 0) ? GameState.Intro : GameState.Level;
  }

  // called when board should be redrawn next time
  internal void ClearScript() {
    DestroyBoard();
  }

  // wrapper to handle log and sounds
  internal void AcceptInput(string input) {
    _modelinfo.AcceptInput(input);
    Message = Model.CurrentMessage;
    PlaySounds(Model.Sounds);
  }

  // use boardview to find a tile give point on screen
  internal int? FindCellIndex(Vector2 screenpoint) {
    var point = (screenpoint - Camera.main.pixelRect.center) * _screentoortho;
    return _boardview.FindCellIndex(point);
  }

  //--- impl

  bool TryCompile(string name) {
    bool ok = false;
    DestroyBoard();
    _modelinfo = new ModelInfo();
    var script = ScriptLoader.ReadScript(name, false);
    if (script == null) Message = "Not yet loaded...";
    else if (!_modelinfo.Compile(name, script))
      Message = _modelinfo.Message;
    else {
      AcceptInput("level 0");
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
    _screentoortho = 2 * camera.orthographicSize / camera.pixelHeight;

    _board = Instantiate(BoardPrefab);
    _boardview = _board.GetComponent<BoardView>();
    _boardview.Position = new Vector3(camerasize.x, camerasize.y);

    // the rectangle of tiles in the level
    var levelsize = new Vector3Int(level.Width, level.Height, level.Depth);
    // the rectangle of tiles to be displayed
    var displaysize = (_modelinfo.ScreenSize == Vector2Int.zero) ? levelsize
      : Vector3Int.Min(levelsize, new Vector3Int(_modelinfo.ScreenSize.x, _modelinfo.ScreenSize.y, level.Depth));
    // the size of each tile in units
    var scale = Math.Min(camerasize.x / displaysize.x, camerasize.y / displaysize.y) * BoardScale;

    _boardview.CreateTiles(displaysize, scale, level.Width, TilePrefab);
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
