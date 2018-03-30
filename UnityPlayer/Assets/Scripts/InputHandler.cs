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

public class InputHandler : MonoBehaviour {
  internal GameState State {
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
    { KeyCode.Z,          "undo" },
    { KeyCode.R,          "restart" },
    { KeyCode.Q,          "quit" },
    { KeyCode.S,          "select" },
    { KeyCode.Escape,     "escape" },
    { KeyCode.Space,      "action" },
  };

  internal MainController _main;

  GameModel _model { get { return _main.Model; } }
  ItemManager _itemmx { get { return _main.Items; } }

  StateMachine _sm;
  bool _busy = false;

  void Start() {
    _main = FindObjectOfType<MainController>();
    _sm = new StateMachine(this);
  }

  // main and only input processing
  void Update() {
    if (!_busy) {
      if (_model.EndLevel) {
        var newstate = _sm.HandleInput("tick");
        StartCoroutine(SetState(0.5f, newstate));
        _busy = true;
      }  else if (_model.Againing) {
        var newstate = _sm.HandleInput("tick");
        StartCoroutine(SetState(_main.Items.AgainInterval, newstate));
        _busy = true;
      } else if (Input.anyKeyDown) {
        foreach (var kvp in _keycodelookup)
          if (Input.GetKeyDown(kvp.Key)) {
            var newstate = _sm.HandleInput(kvp.Value);
            StartCoroutine(SetState(0.1f, newstate));
            _busy = true;
            break;
          }
      }
    }
  }

  IEnumerator SetState(float defertime, GameState state) {
    yield return new WaitForSeconds(defertime);
    State = state;
    _busy = false;
  }

  // handler
  internal GameState AcceptInput() {
    _model.AcceptInputs(_sm.CurrentInput);
    _itemmx.ShowLog();
    _main.PlaySounds(_model.Sounds);
    return CheckModel();
  }

  // handler
  internal GameState CheckModel() {
    return !_model.Ok ? GameState.Error
      : _model.GameOver ? GameState.GameOver
      : _model.InLevel ? GameState.Level
      : _model.EndLevel ? GameState.EndLevel
      : _model.InMessage ? GameState.Message
      : GameState.None;
  }

  // handler
  internal GameState NextLevel() {
    _model.AcceptInputs("tick");
    _itemmx.ShowLog();
    _main.PlaySounds(_model.Sounds);
    _main.NewLevel();
    return CheckModel();
  }

  // handler
  internal string GetSelection() {
    var sel = FindObjectOfType<ScriptSelectionView>();
    return sel.Selection;
  }

  // handler for level increment
  internal GameState ChangeLevel(int increment) {
    var sel = FindObjectOfType<UiManager>();
    var newlevel = sel.RestartLevelIndex + increment;
    if (newlevel >= 0 && newlevel < _model.GameDef.LevelCount)
      sel.RestartLevelIndex = newlevel;
    return State;
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
  // Allow caller to access
  internal string CurrentInput { get; private set; }

  Transition[] _table = new Transition[] {
    // from this state with this input: do something and go to that state
    new Transition(GameState.Intro, "action", t => t.CheckModel()),
    new Transition(GameState.Intro, "select", t => GameState.Select),
    new Transition(GameState.Intro, "quit", t => GameState.Quit),
    new Transition(GameState.Level, "left,right,up,down,action,undo,restart,tick", t => t.AcceptInput()),
    new Transition(GameState.Level, "escape", t => GameState.Pause),
    new Transition(GameState.EndLevel, "tick", t => t.NextLevel()),
    new Transition(GameState.Message, "action", t => t.AcceptInput()),
    new Transition(GameState.Message, "escape", t => GameState.Pause),
    new Transition(GameState.Pause, "action", t => t.CheckModel()),
    new Transition(GameState.Pause, "restart", t => { t._main.ReloadScript(true); return t.CheckModel(); } ),
    new Transition(GameState.Pause, "select", t => GameState.Select),
    new Transition(GameState.Pause, "quit", t => GameState.Quit),
    new Transition(GameState.Pause, "up", t => t.ChangeLevel(1)),
    new Transition(GameState.Pause, "down", t => t.ChangeLevel(-1)),
    new Transition(GameState.Error, "quit", t => GameState.Quit),
    new Transition(GameState.Error, "select", t => GameState.Select),
    new Transition(GameState.GameOver, "action", t => t._main.ReloadScript(false)),
    new Transition(GameState.GameOver, "select", t => GameState.Select),
    new Transition(GameState.GameOver, "escape", t => GameState.Pause),
    new Transition(GameState.GameOver, "quit", t => GameState.Quit),
    new Transition(GameState.Select, "action", t =>t._main.LoadScript(t.GetSelection())),
    new Transition(GameState.Select, "escape", t => GameState.Pause),
  };

  InputHandler  _handler ;

  internal StateMachine(InputHandler handler) {
    _handler = handler;
  }

  // handle an action and return true
  // if not recognised return false
  internal GameState HandleInput(string input) {
    var trans = _table.FirstOrDefault(t => t.State == _handler.State && t.Input.Contains(input));
    if (trans == null) return _handler.State;
    CurrentInput = input;
    var newstate = trans.OnAction(_handler);
    Util.Trace(2, "tsm {0}: {1} => {2}", input, _handler.State, newstate);
    return newstate;
  }
}

