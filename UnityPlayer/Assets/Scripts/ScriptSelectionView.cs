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

public class ScriptSelectionView : MonoBehaviour {
  public Text TitleText;
  public Text StatusText;
  public Text ScriptText;
  public GameObject ContentPanel;
  public GameObject ScriptItemPrefab;
  public Scrollbar ListVerticalScrollbar;
  public Scrollbar ScriptVerticalScrollbar;

  internal string Selection;

  MainController _main;
  IList<string> _scripts;
  GameState _activestate;

  void Start() {
  }

  void OnEnable() {
    _main = FindObjectOfType<MainController>();
    _activestate = GameState.None;
    GetComponent<Image>().color = _main.Items.BackgroundColour;
    TitleText.color = _main.Items.ForegroundColour;
    StatusText.color = _main.Items.ForegroundColour;
  }

  // Update is called once per frame
  void Update() {
    if (_activestate != _main.GameState) {
      Selection = null;
      switch (_main.GameState) {
      case GameState.Select:
        LoadScriptList(_main.Items.ScriptList);
        TitleText.text = "Select Script";
        StatusText.text = "X to Select, Right for Gists, Escape to cancel";
        ScriptText.text = "";
        ListVerticalScrollbar.value = 1;
        break;
      case GameState.Gist:
        LoadScriptList(_main.Items.GistList);
        TitleText.text = "Select Gist";
        StatusText.text = "X to Select, Left for Scripts, Escape to cancel";
        ScriptText.text = "";
        ListVerticalScrollbar.value = 1;
        break;
      }
      _activestate = _main.GameState;
    }
    ScriptText.text = (Selection == null) ? "Please make a selection"
      : (_main.Items.ReadScript(Selection, false)).Left(2000);
  }

  internal void ItemButtonHandler(string name, string input, string prompt) {
    if ("enter,select".Contains(input)) {
      Selection = (_activestate == GameState.Gist) ? "gist:" + name : name;
      ScriptText.text = _main.Items.ReadScript(Selection, true);  // first time, force reload
      ScriptVerticalScrollbar.value = 1;
    }
  }

  void LoadScriptList(IList<string> scripts) {
    _scripts = scripts;
    foreach (Transform child in ContentPanel.transform)
      Destroy(child.gameObject);

    if (_scripts.Count > 0) {
      var counter = 0;
      foreach (var name in _scripts) {
        var newitem = Instantiate(ScriptItemPrefab);
        if (counter++ == 0) newitem.GetComponentInChildren<Button>().Select();
        var item = newitem.GetComponent<ScriptItemView>();
        newitem.transform.SetParent(ContentPanel.transform, false);
        item.NameText.text = name;
        newitem.transform.localScale = Vector3.one;
      }

    }
  }

}
