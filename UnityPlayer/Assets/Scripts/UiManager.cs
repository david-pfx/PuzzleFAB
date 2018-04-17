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

  // Use this for initialization
  void Start() {
    TitleText.text = "PuzzLang 1.0";
    MessageText.text = "start up";
    StatusText.text = "";
  }

  // Convert state changes into visible UI
  void Update() {
      switch (_main.GameState) {
      case GameState.Error:
        SetVisible(EnablePanel.Main);
        TitleText.text = _main.ModelInfo.ScriptName;
        MessageText.text = _main.Message;
        StatusText.text = "Error! Q to Quit, R to Retry, S to Select";
        break;
      case GameState.Intro:
        RestartLevelIndex = 0;
        SetVisible(EnablePanel.Main);
        TitleText.text = _def.GetSetting(OptionSetting.title, "unknown") +
          "\nby " +
          _def.GetSetting(OptionSetting.author, "anonymous");
        var options = new List<string>();
        if (_def.GetSetting(OptionSetting.action, false)) options.Add("X to action");
        if (_def.GetSetting(OptionSetting.restart, true)) options.Add("R to restart");
        if (_def.GetSetting(OptionSetting.undo, true)) options.Add("Z to undo");
        MessageText.text =
          (_def.GetSetting(OptionSetting.arrows, false) ? "Arrow keys to move\n" : "") +
          (_def.GetSetting(OptionSetting.click, false) ? "Click to move\n" : "") +
          options.Join(", ") + "\n" +
          "S to Select, Q to Quit\n" +
          "Escape to Pause";
        StatusText.text = "X or space to continue";
        break;
      case GameState.Message:
        SetVisible(EnablePanel.Main);
        TitleText.text = _def.GetSetting(OptionSetting.title, "unknown");
      MessageText.text = _model.CurrentMessage;
        StatusText.text = "X or space to continue";
        break;
      case GameState.Pause:
        SetVisible(EnablePanel.Main);
        TitleText.text = _def.GetSetting(OptionSetting.title, "unknown");
      var text =
          "Paused on level {0}\n" +
          "\n" +
          "S to Select, Q to Quit\n" +
          "R to Restart at level {1}";
        MessageText.text = text.Fmt(_model.CurrentLevelIndex, RestartLevelIndex);
        StatusText.text = "X or space to continue";
        break;
      case GameState.Select:
        SetVisible(EnablePanel.Selection);
        break;
      case GameState.GameOver:
        SetVisible(EnablePanel.Main);
        TitleText.text = _def.GetSetting(OptionSetting.title, "unknown");
      MessageText.text =
          "Game Over!\n" +
          "\n" +
          "S to Select, Q to Quit";
        StatusText.text = "X or space to replay";
        break;
      case GameState.Status:
        TitleText.text = "Status";
        // MessageText.text set elsewhere 
        StatusText.text = "Escape to cancel";
        break;
      default:
        SetVisible(EnablePanel.None);
        break;
      }
  }

  void SetVisible(EnablePanel enable) {
    if (enable != _enable) {
      Util.Trace(2, "UI state={0} enable={1}", _main.GameState, enable);
      MainPanel.SetActive(enable == EnablePanel.Main);

      MainPanel.GetComponent<Image>().color = _main.BackgroundColour;
      TitleText.color = _main.ForegroundColour;
      MessageText.color = _main.ForegroundColour;
      StatusText.color = _main.ForegroundColour;

      SelectionPanel.SetActive(enable == EnablePanel.Selection);

      _enable = enable;
    }
  }
}
