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
using UnityEngine.Networking;
using DOLE;
using System.Text.RegularExpressions;
using SimpleJSON;

public enum LoadStatus {
  None, Busy, Done, Error,
}

public class WebAccess : MonoBehaviour {
  public string GistApi = @"https://api.github.com/gists/";

  internal LoadStatus Status { get; private set; }
  internal string Message { get; private set; }

  internal MainController _main;
  ItemManager _itemmx { get { return _main.Items; } }

  // start loading a gist  by name (may have gist: prefix)
  // eventually the result will be decoded and uses to update the script lookup
  internal bool StartLoadGist(string name) {
    _main = FindObjectOfType<MainController>();
    if (Status == LoadStatus.Busy) return false;
    StartCoroutine(LoadJson(name));
    Status = LoadStatus.Busy;
    return true;
  }

  // Load a piece of JSON given a gist id, extract the text and update the lookup
  IEnumerator LoadJson(string name) {
    var id = (name.StartsWith("gist:")) ? name.Substring(5) : name;
    var url = GistApi + id;
    var request = UnityWebRequest.Get(url);
    yield return request.SendWebRequest();
    if (request.isNetworkError || request.isHttpError) {
      Message = request.error;
      _itemmx.ScriptLookup[name] = "Error: " + request.error;
      Status = LoadStatus.Error;
    } else {
      _itemmx.ScriptLookup[name] = JsonExtract(request.downloadHandler.text);
      Status = LoadStatus.Done;
    }
    Util.Trace(2, "Load done {0} {1} {2}", name, Status, Message);
  }

  private string JsonExtract(string json) {
    var pjson = JSON.Parse(json);
    return pjson["files"]["script.txt"]["content"];
  }
  //private string JsonExtract(string json) {
  //  var restr = @"#content#: *#([^#]*)#".Replace('#', '"');
  //  var regex = new Regex(restr);
  //  var matches = regex.Matches(json);
  //  Util.Trace(2, "Found {0} matches", matches.Count);
  //  if (matches.Count == 2)
  //    Util.Trace(2, "Found {0} groups", matches[1].Groups.Count);
  //  if (matches.Count == 2 && matches[1].Groups.Count == 2) {
  //    var text = matches[1].Groups[1].Value.Replace(@"\n", "\n");
  //    Util.Trace(2, "Text: {0}", text);
  //    return text;
  //  } else return "bad json";
  //}
}