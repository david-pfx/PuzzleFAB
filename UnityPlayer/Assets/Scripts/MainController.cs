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
using System.Collections;

// very important state that drives the entire UI
public enum GameState {
  None,
  Select, Error, Wait, Info,
  Intro, Pause, Quit,
  Level, EndLevel, Message, GameOver,
}

public enum ReloadMode { Start, Index, Script }

/// <summary>
/// Implements main functions for getting started, loading and running games
/// Also contains most of the settable items
/// </summary>
public class MainController : MonoBehaviour {
  // settings
  public string Version;
  public string UserPuzzlesDirectory = "Games";
  public string BuiltinPuzzlesDirectory = "Puzzles";
  public string StartupScript = "2048";
  public TextAsset Gists;
  public string GistFile = "Gists.txt";
  public float AgainInterval = 0.25f;
  public float EndInterval = 0.50f;
  public float RealtimeInterval = 0.25f;
  public float BoardScale = 0.90f;
  public float SplashTime = 1.0f;
  public Color BackgroundColour = Color.yellow;
  public Color ForegroundColour = Color.blue;
  public GameObject BoardPrefab;
  public GameObject TilePrefab;
  public AudioSource AudioSource;
  public AudioClip DefaultSound;
  public bool VerboseLogging = false;
  public int Noisy;
  public string TestUrl;

  internal static MainController Instance { get; private set; }
  internal GameState GameState { get; set; }
  internal GameModel Model { get { return _modelinfo.Model; } }
  internal ModelInfo ModelInfo { get { return _modelinfo; } }
  internal GameDef GameDef { get { return _modelinfo.Model.GameDef; } }
  internal BoardView Board { get { return _boardview; } }
  internal ScriptLoader ScriptLoader { get; private set; }
  internal string DataDirectory { get; private set; }
  internal string Title { get; private set; }   // for display
  internal string Message { get; private set; }  // for display
  internal string Status { get; private set; }   // for display
  internal bool EnableViewUpdate { get; set; }   // when true, need to update display

  UiManager _uimx;
  ModelInfo _modelinfo;
  GameObject _board;
  BoardView _boardview;
  string _scriptname;
  float _screentoortho;

  void Awake() {
    Instance = this;
    //Assert.raiseExceptions = true;
    EnableViewUpdate = true;
    // setting during testing
#if UNITY_EDITOR
    UserPuzzlesDirectory += ",test puzzles,test puzzles/demo,test puzzles/buggy";
    //TestUrl = @"xx.com?u=http://david-pfx.github.io/PuzzlangWeb/Puzzles/New/2048.txt";
    //TestUrl = @"http://david-pfx.github.io/PuzzlangWeb/index.html?p=Puzzles/New/2048.txt";
    //TestUrl = @"xx.com?w=New/2048.txt";
    //VerboseLogging = true;
    //Noisy = 2;
#endif
    Util.MaxLoggingLevel = Noisy;
    //Version = "1.0.beta.{0:D2}{1}{2:D2}".Fmt(DateTime.Now.Year % 100, (char)('a' + DateTime.Now.Month - 1), DateTime.Now.Day);
  }

void Start() {
    Util.Trace(1, "Main start '{0}'", Noisy);
    _uimx = FindObjectOfType<UiManager>();
    ScriptLoader = FindObjectOfType<ScriptLoader>();
    DataDirectory = Path.GetDirectoryName(Application.dataPath);
    ScriptLoader.Setup();
    var newstate = ScriptLoader.FindScript("startup") ? LoadScript("startup") : LoadScript(StartupScript);
    StartCoroutine(ShowSplash(SplashTime, newstate));
  }

  IEnumerator ShowSplash(float time, GameState newstate) {
    GameState = GameState.None;
    yield return new WaitForSeconds(time);
    GameState = newstate;
  }

  // update our state
  void Update() {
    switch (GameState) {
    case GameState.Wait:
      if (ScriptLoader.ReadScript(_scriptname) != null)
        GameState = TryCompile(_scriptname);
      break;
    case GameState.Intro:
      break;
    case GameState.Level:
      if (_board == null) CreateBoard();
      break;
    case GameState.EndLevel:
      Message = "You win!";
      break;
    case GameState.Info:
      Message = GetStatusInfo();
      break;
    case GameState.GameOver: // todo: end level
      DestroyBoard();
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
    return "data={0}\npersist={1}\nurl={2}"
      .Fmt(Application.dataPath, Application.persistentDataPath, Application.absoluteURL);
  }

  // load and compile script if possible, without or without reload, setting State
  internal GameState LoadScript(string scriptname, bool reload = false) {
    if (scriptname != null) _scriptname = scriptname;
    Util.Trace(1, "Load script '{0}'", _scriptname);
    Title = _scriptname;
    Message = "Please wait...";
    return ScriptLoader.FindScript(_scriptname, reload) ? GameState.Wait : GameState.Error;
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

  internal GameState RestartScript(ReloadMode mode) {
    DestroyBoard();
    if (mode == ReloadMode.Script) return LoadScript(null, true);
    var newindex = (mode == ReloadMode.Index) ? _uimx.RestartLevelIndex : 0;
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
    Status = Model.Status ?? "Level {0}".Fmt(Model.CurrentLevelIndex);

    PlaySounds(Model.Sounds);
  }

  // use boardview to find a tile give point on screen
  internal int? FindCellIndex(Vector2 screenpoint) {
    var point = (screenpoint - Camera.main.pixelRect.center) * _screentoortho;
    return _boardview.FindCellIndex(point);
  }

  //--- impl

  GameState TryCompile(string name) {
    DestroyBoard();
    _modelinfo = new ModelInfo();
    Title = name;
    var script = ScriptLoader.ReadScript(name);
    if (script == null) Message = "Script not loaded";
    else if (script.StartsWith("Error")) Message = script;
    else if (!_modelinfo.Compile(name, script)) Message = _modelinfo.Message;
    else {
      Title = GameDef.GetSetting(OptionSetting.title, "unknown");
      AcceptInput("level 0");
      return GameState.Intro;
    }
    return GameState.Error;
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

    // the size of the board including surroundings
    var boardsize = new Vector3(camerasize.x, camerasize.y) * BoardScale;
    // the rectangle of tiles in the level
    var levelsize = new Vector3Int(level.Width, level.Height, level.Depth);
    // the rectangle of tiles to be displayed
    var displaysize = (_modelinfo.ScreenSize == Vector2Int.zero) ? levelsize
      : Vector3Int.Min(levelsize, new Vector3Int(_modelinfo.ScreenSize.x, _modelinfo.ScreenSize.y, level.Depth));
    // the size of each tile in units
    var scale = Math.Min(boardsize.x / displaysize.x, boardsize.y / displaysize.y);

    _board = Instantiate(BoardPrefab);
    _boardview = _board.GetComponent<BoardView>();
    _boardview.Setup(boardsize);
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
