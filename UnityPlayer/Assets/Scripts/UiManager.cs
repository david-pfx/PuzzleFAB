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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PuzzLangLib;
using DOLE;

enum EnablePanel {
  None, Main, Selection,
}

public class UiManager : MonoBehaviour {
  public GameObject MainPanel;
  public GameObject SelectionPanel;
  public Text TitleText;
  public Text MessageText;
  public Text StatusText;

  MainController _main { get { return MainController.Instance; } }
  internal int RestartLevelIndex;

  GameModel _model { get { return _main.Model; } }
  GameDef _def { get { return _main.GameDef; } }

  EnablePanel _enable = EnablePanel.None;

  private void Start() {
    StatusText.text = _main.Version;
  }

  // Convert state changes into visible UI
  void Update() {
    if (_main == null) return;
    // do nothing unless something changes
    switch (_main.GameState) {
    case GameState.None:
      break;
    case GameState.Error:
      SetText(_main.Title, _main.Message, "Q to Quit, R to Retry, S to Select");
      break;
    case GameState.Wait:
      SetText(_main.Title, _main.Message, "Q to Quit, S to Select");
      break;
    case GameState.Intro:
      RestartLevelIndex = 0;
      var options = new List<string>();
      if (_def.GetSetting(OptionSetting.action, false)) options.Add("X to action");
      if (_def.GetSetting(OptionSetting.restart, true)) options.Add("R to restart");
      if (_def.GetSetting(OptionSetting.undo, true)) options.Add("Z to undo");
      var intext = "\nby " + _def.GetSetting(OptionSetting.author, "anonymous") + "\n\n" +
        (_def.GetSetting(OptionSetting.arrows, false) ? "Arrow keys to move\n" : "") +
        (_def.GetSetting(OptionSetting.click, false) ? "Click to move\n" : "") +
        options.Join(", ") + "\n" +
        "S to Select, Q to Quit\n" +
        "Escape to Pause";
      SetText(_main.Title, intext, "X or space to continue");
      break;
    case GameState.Level:
      SetText(_main.Title, "", _main.Status);
      RestartLevelIndex = _model.CurrentLevelIndex;
      break;
    case GameState.Message:
      SetText(_main.Title, _main.Message, "X or space to continue");
      break;
    case GameState.EndLevel:
      SetText(_main.Title, "", _main.Message);
      RestartLevelIndex = _model.CurrentLevelIndex;
      break;
    case GameState.Pause:
      var pstext =
        "Paused at stage {0}\n" +
        "\n" +
        "S to Select, Q to Quit\n" +
        "Up, Down, R to Restart at stage {1}";
      SetText(_main.Title, pstext.Fmt(_model.CurrentLevelIndex, RestartLevelIndex), "X or space to continue");
      break;
    case GameState.Select:
      SetVisible(EnablePanel.Selection);
      break;
    case GameState.GameOver:
      SetText(_main.Title, "Game Over!\n" + "\n" + "S to Select, Q to Quit", "X or space to replay");
      break;
    case GameState.Info:
      SetText("Info", _main.Message, "Escape to cancel");    // message text set elsewhere 
      break;
    default:
      SetVisible(EnablePanel.None);
      break;
    }
  }

  void SetText(string title, string message, string status) {
    SetVisible(EnablePanel.Main);
    TitleText.text = title;
    MessageText.text = message;
    StatusText.text = status;
    MainPanel.GetComponent<Image>().color = _main.BackgroundColour;
    TitleText.color = _main.ForegroundColour;
    MessageText.color = _main.ForegroundColour;
    StatusText.color = _main.ForegroundColour;
  }

  void SetVisible(EnablePanel enable) {
    if (enable != _enable) {
      Util.Trace(2, "UI state={0} enable={1}", _main.GameState, enable);
      MainPanel.SetActive(enable == EnablePanel.Main);
      SelectionPanel.SetActive(enable == EnablePanel.Selection);
      _enable = enable;
    }
  }
}
