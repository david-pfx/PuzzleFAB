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
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PuzzLangLib;

public class ScriptItemView : MonoBehaviour, ISelectHandler {
  public Text NameText;

  static string _clicked;
  ScriptSelectionView _parent;

  void Start() {
    _parent = FindObjectOfType<ScriptSelectionView>();
  }

  private void OnEnable() {
    _clicked = "";
  }

  // TODO: why is this not called? Events?
  public void OnSelect(BaseEventData eventData) {
    _parent.ItemButtonHandler(NameText.text, "select", NameText.text);
  }

  // Adding event handler kills off the scroll wheel for the list so only click comes here. 
  // So reluctantly we don't do hover, and emulate it instead.
  public void ButtonHandler(string input = "click") {
    Util.Trace(2, "Handled {0} for {1}", input, NameText.text);
    //_parent.ItemButtonHandler(NameText.text, input, NameText.text);
    if (NameText.text != _clicked) {
      _clicked = NameText.text;
      _parent.ItemButtonHandler(NameText.text, "enter", "Click again to play " + NameText.text);
    } else
      _parent.ItemButtonHandler(NameText.text, input, NameText.text);
  }

}
