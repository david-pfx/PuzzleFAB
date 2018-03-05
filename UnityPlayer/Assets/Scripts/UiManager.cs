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

  internal int RestartLevelIndex;

  MainController _main;
  GameModel _model { get { return _main.Model; } }
  GameDef _def { get { return _main.GameDef; } }
  EnablePanel _enable = EnablePanel.None;

  // Use this for initialization
  void Start() {
    _main = FindObjectOfType<MainController>();
    TitleText.text = "PuzzLang 1.0";
    MessageText.text = "start up";
    StatusText.text = "";
  }

  // Convert state changes into visible UI
  void Update() {
    switch (_main.GameState) {
    case GameState.Error:
      SetVisible(EnablePanel.Main);
      MessageText.text = _main.Message;
      StatusText.text = "Error!";
      break;
    case GameState.Intro:
      RestartLevelIndex = 0;
      SetVisible(EnablePanel.Main);
      TitleText.text = _def.GetSetting(OptionSetting.title, "unknown") +
        "\nby " +
        _def.GetSetting(OptionSetting.author, "anonymous");
      var options = new List<string>();
      if (!_def.GetSetting(OptionSetting.noaction, false)) options.Add("X to action");
      if (!_def.GetSetting(OptionSetting.norestart, false)) options.Add("R to restart");
      if (!_def.GetSetting(OptionSetting.noundo, false)) options.Add("Z to undo");
      MessageText.text = 
        "arrow keys to move\n" +
        options.Join(", ") + "\n\n" +
        "S to Select, Q to Quit\n" +
        "Escape to Pause";
      StatusText.text = "X or space to continue";
      break;
    case GameState.Message:
      SetVisible(EnablePanel.Main);
      MessageText.text = _model.CurrentMessage;
      StatusText.text = "X or space to continue";
      break;
    case GameState.Pause:
      SetVisible(EnablePanel.Main);
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
      MessageText.text = 
        "Game Over!\n" +
        "\n" +
        "S to Select, Q to Quit";
      StatusText.text = "X or space to replay";
      break;
    default:
      SetVisible(EnablePanel.None);
      break;
    }
  }

  void SetVisible(EnablePanel enable) {
    if (enable != _enable) {
      MainPanel.SetActive(enable == EnablePanel.Main);

      MainPanel.GetComponent<Image>().color = _main.Items.BackgroundColour;
      TitleText.color = _main.Items.ForegroundColour;
      MessageText.color = _main.Items.ForegroundColour;
      StatusText.color = _main.Items.ForegroundColour;

      SelectionPanel.SetActive(enable == EnablePanel.Selection);

      _enable = enable;
    }
  }

}
