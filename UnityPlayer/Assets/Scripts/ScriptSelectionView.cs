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
using System;

public class ScriptSelectionView : MonoBehaviour {
  public Text TitleText;
  public Text StatusText;
  public Text ScriptText;
  public GameObject ContentPanel;
  public GameObject ScriptItemPrefab;
  public Scrollbar ListVerticalScrollbar;
  public Scrollbar ScriptVerticalScrollbar;

  internal string Selection;
  internal int SelectionPageNo = 0;

  MainController _main { get { return MainController.Instance; } }
  ScriptLoader _scldr { get { return _main.ScriptLoader; } }
  IList<string> _scripts;
  string _notreadytext = "???";
  int _activepageno = -1;

  void OnEnable() {
    GetComponent<Image>().color = _main.BackgroundColour;
    TitleText.color = _main.ForegroundColour;
    StatusText.color = _main.ForegroundColour;
  }

  void Update() {
    if (_activepageno != SelectionPageNo) {
      var sources = _scldr.GetScriptSources();
      var selno = (SelectionPageNo + sources.Count) % sources.Count;
      var source = sources[selno];
      LoadScriptList(_scldr.GetScripts(source));
      StatusText.text = "X to Select, Left/Right, Enter, Escape to cancel";
      TitleText.text = "Select " + source;
      _notreadytext = "Waiting...";
      Selection = null;
      ListVerticalScrollbar.value = 1;
      _activepageno = SelectionPageNo = selno;
    }
    var text = (Selection == null) ? "Please make a selection" : _scldr.ReadScript(Selection);
    ScriptText.text = (text == null) ? _notreadytext : text.Left(2000);
  }

  internal void ItemButtonHandler(string name, string input, string prompt) {
    if ("enter,select".Contains(input)) {
      Selection = name;
      ScriptText.text = _scldr.ReadScript(Selection, true);  // first time, force reload
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
