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
  GameModel _model { get { return _main.Model; } }
  GameDef _def { get { return _main.GameDef; } }
  IList<string> _scripts;

  void Start() {
  }

  void OnEnable() {
    _main = FindObjectOfType<MainController>();
    _scripts = null;
    GetComponent<Image>().color = _main.Items.BackgroundColour;
    TitleText.color = _main.Items.ForegroundColour;
    StatusText.color = _main.Items.ForegroundColour;
  }

  // Update is called once per frame
  void Update() {
    switch (_main.GameState) {
    case GameState.Select:
      if (_scripts == null) {
        LoadScriptList();
        TitleText.text = "Select Script";
        StatusText.text = "X to Select, Escape to cancel";
        ScriptText.text = "";
        ListVerticalScrollbar.value = 1;
      }
      break;
    }
  }

  internal void ItemButtonHandler(string name, string input, string prompt) {
    if ("enter,select".Contains(input)) {
      Selection = name;   // this selection is picked up on input
      ScriptText.text = _main.Items.ReadScript(name);
      ScriptVerticalScrollbar.value = 1;
    }
  }

  void LoadScriptList() {
    _scripts = _main.Items.ScriptList;
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
