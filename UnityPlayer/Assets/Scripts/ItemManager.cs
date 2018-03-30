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
using System.IO;
using UnityEngine;
using DOLE;
using PuzzLangLib;
using NsfxrLib;
using System.Text.RegularExpressions;

public class ItemManager : MonoBehaviour {
  // settings
  public string GamesDirectory = "Games";
  public string GistFile = "Gists.txt";
  public TextAsset StartupScript;
  public Color BackgroundColour = Color.yellow;
  public Color ForegroundColour = Color.blue;
  public bool VerboseLogging = false;
  public float AgainInterval = 0.25f;
  public float RealtimeInterval = 0.25f;
  public AudioClip DefaultSound;

  internal List<string> ScriptList { get { return _scripts; } }
  internal List<string> GistList { get { return _gists; } }
  internal Dictionary<string, string> ScriptLookup { get { return _scriptlookup; } }

  readonly string[] _patterns = new string[] { "*.pzl", "*.txt" };
  string _appdirectory;
  string _userdirectory;
  List<string> _scripts = new List<string>();
  List<string> _gists = new List<string>();
  Dictionary<string, string> _scriptlookup = new Dictionary<string, string>();

  // Find all games and add to script list
  internal void FindAllScripts() {
    _appdirectory = Path.GetDirectoryName(Application.dataPath);
    _userdirectory = Util.Combine(_appdirectory, GamesDirectory);
    _scriptlookup["<startup>"] = StartupScript.text;
    AddScripts(_userdirectory);
    AddGists(Util.Combine(_appdirectory, GistFile));
  }

  // load a script by name for viewing
  internal string ReadScript(string name, bool reload) {
    if (reload) {
      Util.Trace(2, "Read script {0}", name);
      if (name.StartsWith("gist:"))
        ReadGistScript(name);
      else ReadFileScript(name);
    }
    return _scriptlookup.SafeLookup(name);
  }

  // really load a script file
  internal void ReadFileScript(string name) {
    var path = Path.Combine(_userdirectory, name);
    if (File.Exists(path)) {
      using (var sr = new StreamReader(path)) {
        _scriptlookup[name] = sr.ReadToEnd();
      }
    }
  }

  // really load a gist file -- eventually
  internal void ReadGistScript(string name) {
    _scriptlookup[name] = "Waiting for gist...";
    WebAccess webaccess = GetComponent<WebAccess>();
    webaccess.StartLoadGist(name);
  }

  // Load games found in a directory
  void AddScripts(string directory) {
    try {
      foreach (var pattern in _patterns) {
        var scripts = Directory.GetFiles(directory, pattern);
        foreach (var script in scripts)
          _scripts.Add(Path.GetFileName(script));
      }
    } catch (Exception ex) {
      Util.Trace(1, "Exception finding scripts: {0}", ex.ToString());
    }
    Util.Trace(2, "loaded {0} script(s)", _scripts.Count);
  }

  // load gists from a list in a file
  void AddGists(string path) {
    var regex = new Regex("[0-9a-z]{32}");
    try {
      if (!File.Exists(path)) return;
      using (var sr = new StreamReader(path)) {
        var line = sr.ReadLine();
        while (line != null) {
          var match = regex.Match(line);
          if (!match.Success)
            Util.Trace(1, "invalid gist: '{0}'", line);
          else _gists.Add(match.Value);
          line = sr.ReadLine();
        }
      }
    } catch (Exception ex) {
      Util.Trace(1, "Exception finding gists: {0}", ex.ToString());
    }
    Util.Trace(2, "loaded {0} gist(s)", _gists.Count);
  }

}

