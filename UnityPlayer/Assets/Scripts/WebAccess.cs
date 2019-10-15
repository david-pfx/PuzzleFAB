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
using UnityEngine.Networking;
using DOLE;
using SimpleJSON;

public enum LoadStatus {
  None, Busy, Done, Error,
}

public class WebAccess : MonoBehaviour {
  public string GistApi = @"https://api.github.com/gists/";
  public string WebGLSite = @"https://david-pfx.github.io/PuzzlangWeb/Puzzles/";

  internal LoadStatus Status { get; private set; }
  internal string Message { get; private set; }

  MainController _main { get { return MainController.Instance; } }
  ScriptLoader _scldr { get { return _main.ScriptLoader; } }

  // load a script via URL query parameters
  internal string ExtractQueryUrl(string url) {
    var args = ExtractUrlParameters(url);
    if (args.ContainsKey("u"))          // url
      return args["u"];
    if (args.ContainsKey("p"))          // common root
      return ExtractUrlPath(url) + args["p"];
    if (args.ContainsKey("w"))          // web site
      return WebGLSite + args["w"];
    if (args.ContainsKey("g"))          // gist
      return GistApi + args["g"];       // BUG:
    return null;
  }

  // start loading a script by direct URL
  internal bool StartLoadUrl(string name, string url) {
    if (Status == LoadStatus.Busy) return false;
    StartCoroutine(LoadUrl(name, url));
    Status = LoadStatus.Busy;
    return true;
  }

  // Load a script directly from a URL, extract the text and update the lookup
  IEnumerator LoadUrl(string name, string url) {
    var request = UnityWebRequest.Get(url);
    yield return request.SendWebRequest();
    if (request.isNetworkError || request.isHttpError) {
      Message = request.error;
      _scldr.SetScriptValue(name, "Error: " + request.error + "\n" + url);
      Status = LoadStatus.Error;
    } else {
      _scldr.SetScriptValue(name, request.downloadHandler.text);
      Status = LoadStatus.Done;
    }
    Util.Trace(2, "Load done {0} {1} {2}", name, Status, Message);
  }

  // start loading a gist  by name (may have gist: prefix)
  // eventually the result will be decoded and used to update the script lookup
  internal bool StartLoadGist(string name, string path) {
    if (Status == LoadStatus.Busy) return false;
    StartCoroutine(LoadJson(name, path));
    Status = LoadStatus.Busy;
    return true;
  }

  // Load a piece of JSON given a gist id, extract the text and update the lookup
  IEnumerator LoadJson(string name, string path) {
    var url = GistApi + path;
    var request = UnityWebRequest.Get(url);
    yield return request.SendWebRequest();
    if (request.isNetworkError || request.isHttpError) {
      Message = request.error;
      _scldr.SetScriptValue(name, "Error: " + request.error); // note: magic string!
      Status = LoadStatus.Error;
    } else {
      _scldr.SetScriptValue(name, JsonExtract(request.downloadHandler.text));
      Status = LoadStatus.Done;
    }
    Util.Trace(2, "Load done {0} {1} {2}", name, Status, Message);
  }

  private string JsonExtract(string json) {
    var pjson = JSON.Parse(json);
    return pjson["files"]["script.txt"]["content"];
  }

  // get query parameters from a URL as dictionary
  static internal Dictionary<string, string> ExtractUrlParameters(string url) {
    var args = url.After("?").Split('&');
    //var args = Uri.UnescapeDataString(url.After("?")).Split('&');  // is this needed?
    var keys = args.Select(a => a.Split('='))
      .Where(s => s.Length == 2)
      .ToDictionary(k => k[0], v => v[1]);
    return keys;
  }

  // get url path minus item name and query string but including trailing /
  static internal string ExtractUrlPath(string url) {
    var before = url.Before("?");
    var index = before.LastIndexOf("/");
    return index == -1 ? before + "/" : url.Left(index + 1);
  }

}