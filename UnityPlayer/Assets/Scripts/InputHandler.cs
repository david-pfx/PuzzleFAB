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
using System.Linq;
using UnityEngine;
using PuzzLangLib;
using DOLE;

public class InputHandler : MonoBehaviour {

  // for use by state machine
  internal MainController _main { get { return MainController.Instance; } }
  internal GameState _gamestate {
    get { return _main.GameState; }
    set { _main.GameState = value; }
  }

  // translate Unity keycodes into string
  static readonly Dictionary<KeyCode, string> _keycodelookup = new Dictionary<KeyCode, string>() {
    { KeyCode.LeftArrow,  "left" },
    { KeyCode.RightArrow, "right" },
    { KeyCode.UpArrow,    "up" },
    { KeyCode.DownArrow,  "down" },
    { KeyCode.X,          "action" },
    { KeyCode.C,          "reaction" },
    { KeyCode.Z,          "undo" },
    { KeyCode.R,          "restart" },
    { KeyCode.Q,          "quit" },
    { KeyCode.S,          "select" },
    { KeyCode.Escape,     "escape" },
    { KeyCode.Space,      "action" },
    { KeyCode.Equals,     "status" },
    { KeyCode.F12,        "status" },
  };

  static readonly Dictionary<string, string> _buttonlookup = new Dictionary<string, string> {
      { "Fire1", "fire1 {0}" },
      { "Fire2", "fire2 {0}" },
      { "Fire3", "fire3 {0}" },
  };
  GameModel _model { get { return _main.Model; } }
  ModelInfo _modelinfo { get { return _main.ModelInfo; } }
  ScriptLoader _scldr { get { return _main.ScriptLoader; } }
  StateMachine _sm;
  bool _busy = false;
  int? _lasthoverindex = null;
  string _buttondrag;
  string _currentinput { get; set; }

  void Start() {
    _sm = new StateMachine(this);
  }

  // main and only input processing
  void Update() {
    if (_busy || _main == null) return;
    // switch off updating that may have been enabled by previous input
    _main.EnableViewUpdate = false;
    // note: hover may set input to be handled on next update, so check for click first
    if (_main.GameState == GameState.Level) {
      if (_model.GameDef.GetSetting(OptionSetting.click, false) && HandleClick()) return;
      if (_model.GameDef.GetSetting(OptionSetting.hover, false) && HandleHover()) return;
    }
    if (Input.GetButtonDown("Fire1")) {
      _gamestate = HandleInput("click");
    } else if (Input.anyKeyDown) {
      foreach (var kvp in _keycodelookup)
        if (Input.GetKeyDown(kvp.Key)) {
          _gamestate = HandleInput(kvp.Value);
          return;
        }
    }
  }

  // coroutine to provide input after a delay, and update status directly
  IEnumerator DeferInput(float defertime, string input) {
    _busy = true;
    yield return new WaitForSeconds(defertime);
    _busy = false;
    _gamestate = HandleInput(input);
  }

  GameState HandleInput(string input) {
    _currentinput = input;
    return _sm.HandleInput(input);
  }

  bool HandleClick() {
    var index = _main.FindCellIndex(Input.mousePosition);
    if (index == null) return false;
    if (_buttondrag != null) {
      Util.Trace(2, "Drag index {0}\nmousepos {1}", index, Input.mousePosition);
      _gamestate = HandleInput(_buttonlookup[_buttondrag].Fmt(index));
      _buttondrag = null;
      return true;
    }
    foreach (var kvp in _buttonlookup)
      if (Input.GetButtonDown(kvp.Key)) {   // check for just pressed
        _gamestate = HandleInput(kvp.Value.Fmt(index));
        return true;
      }
    return false;
  }

  // handle hover based on whether the mouse has moved to a new index
  // else check for button drag and simulate click
  bool HandleHover() {
    var index = _main.FindCellIndex(Input.mousePosition);
    if (index == _lasthoverindex) return false;
    Util.Trace(2, "Hover index {0}\nmousepos {1}", index, Input.mousePosition);
    _gamestate = HandleInput("hover {0}".Fmt(index));
    _lasthoverindex = index;
    foreach (var kvp in _buttonlookup)
      if (Input.GetButton(kvp.Key)) {   // check for held down
        _buttondrag = kvp.Key;
        return true;
      }
    return false;
  }

  ///---------------------------------------------------------------------------
  ///
  /// Handlers are called by the state machine and must return a new state
  /// 

  // handler
  internal GameState AcceptInput(string input = null) {
    if (_model.EndLevel) _main.ClearScript();
    _main.AcceptInput(input ?? _currentinput);
    var newstate = CheckModel();
    if (_model.EndLevel && newstate != GameState.EndLevel) {
      Util.Trace(1, "Endlevel defer {0}", _main.EndInterval);
      StartCoroutine(DeferInput(_main.EndInterval, "tick"));
    } else if (_model.Againing) {
      Util.Trace(1, "Againing defer {0}", _main.AgainInterval);
      StartCoroutine(DeferInput(_main.AgainInterval, "tick"));
    }
    // enable updating for one cycle
    _main.EnableViewUpdate = true;
    return newstate;
  }

  // handler
  internal GameState CheckModel() {
    return !_model.Ok ? GameState.Error
      : _model.GameOver ? GameState.GameOver
      : _model.EndLevel? GameState.EndLevel
      : _model.InMessage ? GameState.Message
      : _model.InLevel ? GameState.Level
      : GameState.None;
  }

  // handler for level increment
  internal GameState ChangeLevel(int increment) {
    var sel = FindObjectOfType<UiManager>();
    var newlevel = sel.RestartLevelIndex + increment;
    if (newlevel >= 0 && newlevel < _model.GameDef.LevelCount)
      sel.RestartLevelIndex = newlevel;
    return _gamestate;
  }

  // handler for selection
  internal GameState MakeSelection() {
    var sel = FindObjectOfType<ScriptSelectionView>().Selection;
    return (sel != null) ? _main.LoadScript(sel) : _gamestate;
  }

  internal GameState SelectNewPage(int v) {
    var sel = FindObjectOfType<ScriptSelectionView>();
    sel.SelectionPageNo += v;
    return _gamestate;
  }
}

////////////////////////////////////////////////////////////////////////////////
/// <summary>
/// State machine class and types
/// </summary>
////////////////////////////////////////////////////////////////////////////////

// represent a transition between states and some action
class Transition {
  internal GameState State;
  internal string Input;
  internal Func<InputHandler,GameState> OnAction;
  
  internal Transition(GameState state, string input, Func<InputHandler,GameState> onaction) {
    State = state; Input = input; OnAction = onaction; 
  }
}

// manage state transition on input event
class StateMachine {
  Transition[] _table = new Transition[] {
    // from this state with this input: do something and go to that state
    new Transition(GameState.Intro, "action,click", t => t.CheckModel()),
    new Transition(GameState.Intro, "select", t => GameState.Select),
    new Transition(GameState.Intro, "quit", t => GameState.Quit),
    new Transition(GameState.Intro, "status", t => GameState.Info),
    new Transition(GameState.Level, "left,right,up,down,action,reaction,undo,restart,tick,hover,fire1,fire2,fire3",
      t => t.AcceptInput()),
    new Transition(GameState.Level, "escape", t => GameState.Pause),
    new Transition(GameState.EndLevel, "escape", t => GameState.Pause),
    new Transition(GameState.EndLevel, "action,click", t => t.AcceptInput("tick")),
    new Transition(GameState.EndLevel, "restart", 
      t => { t._main.RestartScript(ReloadMode.Index); return t.CheckModel(); } ),
    new Transition(GameState.Message, "action,click", t => t.AcceptInput("action")),
    new Transition(GameState.Message, "escape", t => GameState.Pause),
    new Transition(GameState.Pause, "action,click", t => t.CheckModel()),
    new Transition(GameState.Pause, "restart", t => { t._main.RestartScript(ReloadMode.Index); return t.CheckModel(); } ),
    new Transition(GameState.Pause, "select", t => GameState.Select),
    new Transition(GameState.Pause, "quit", t => GameState.Quit),
    new Transition(GameState.Pause, "up", t => t.ChangeLevel(1)),
    new Transition(GameState.Pause, "down", t => t.ChangeLevel(-1)),
    new Transition(GameState.Pause, "status", t => GameState.Info),
    new Transition(GameState.Error, "restart", t => t._main.LoadScript(null, true) ),
    new Transition(GameState.Error, "select", t => GameState.Select),
    new Transition(GameState.Error, "quit", t => GameState.Quit),
    new Transition(GameState.Wait, "select", t => GameState.Select),
    new Transition(GameState.Wait, "quit", t => GameState.Quit),
    new Transition(GameState.GameOver, "action,click", t => t._main.RestartScript(ReloadMode.Start)),
    new Transition(GameState.GameOver, "select", t => GameState.Select),
    new Transition(GameState.GameOver, "escape", t => GameState.Pause),
    new Transition(GameState.GameOver, "quit", t => GameState.Quit),
    new Transition(GameState.Select, "action", t =>t.MakeSelection()),
    new Transition(GameState.Select, "left", t => t.SelectNewPage(-1)),
    new Transition(GameState.Select, "right", t => t.SelectNewPage(1)),
    new Transition(GameState.Select, "escape", t => GameState.Pause),
    new Transition(GameState.Info, "escape", t => GameState.Pause),
  };

  InputHandler  _handler ;

  internal StateMachine(InputHandler handler) {
    _handler = handler;
  }

  // handle an action and return true
  // if not recognised return false
  internal GameState HandleInput(string input) {
    var infirst = input.Split(' ')[0];
    var trans = _table.FirstOrDefault(t => t.State == _handler._gamestate && t.Input.Contains(infirst));
    if (trans == null) return _handler._gamestate;
    Util.Trace(2, ">input {0}: {1}", input, _handler._gamestate);
    var newstate = trans.OnAction(_handler);
    Util.Trace(2, ">newstate {0}", newstate);
    return newstate;
  }
}

