using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using DOLE;
using System.Text.RegularExpressions;

internal enum ScriptKind { None, File, Gist, Asset }

internal class ScriptInfo {
  internal string Source;     // display showing where it comes from
  internal string Name;       // display showing name
  internal ScriptKind Kind;   // how to load it
  internal string Path;       // where to load it
  internal string Value;      // contents of script
}

/// <summary>
/// Handling loading of scripts from various sources
/// </summary>
internal class ScriptLoader : MonoBehaviour {

  internal MainController _main { get { return MainController.Instance; } }

  readonly string[] _patterns = new string[] { "*.pzl", "*.txt" };
  Dictionary<string, ScriptInfo> _scriptlookup = new Dictionary<string, ScriptInfo>();

  internal void Setup() {
    FindAllScripts();
  }

  // Find all games and add to script list
  internal void FindAllScripts() {
    _scriptlookup.Clear();
    foreach (var dir in _main.BuiltinPuzzlesDirectory.Split(','))
      AddAssetScripts(dir);
    foreach (var dir in _main.UserDirectory.Split(','))
      AddFileScripts(dir);
    AddGists(_main.Gists.text);
    var gistpath = Util.Combine(_main.AppDirectory, _main.GistFile);
    if (File.Exists(gistpath)) 
      AddGists(new StreamReader(gistpath).ReadToEnd());
  }

  internal void SetScriptValue(string name, string value) {
    _scriptlookup[name].Value = value;
  }

  internal IList<string> GetScriptSources() {
    return _scriptlookup.Values
      .Select(v => v.Source)
      .Distinct()
      .ToList();
  }

  internal IList<string> GetScripts(string source) {
    return _scriptlookup.Keys
      .Where(k => _scriptlookup[k].Source == source)
      .ToList();
  }

  // load a script by name for viewing
  internal string ReadScript(string name, bool reload) {
    var script = _scriptlookup.SafeLookup(name);
    if (script == null) return null;
    if (!reload && script.Value != "") return script.Value;
    Util.Trace(2, "Read script {0}", name);
    if (script.Kind == ScriptKind.Gist)
      ReadGistScript(script);
    else ReadFileScript(script);
    return _scriptlookup.SafeLookup(name).Value;
  }

  // really load a script file
  void ReadFileScript(ScriptInfo script) {
    if (File.Exists(script.Path)) {
      using (var sr = new StreamReader(script.Path)) {
        script.Value = sr.ReadToEnd();
      }
    }
  }

  // really load a gist file -- eventually
  void ReadGistScript(ScriptInfo script) {
    WebAccess webaccess = GetComponent<WebAccess>();
    webaccess.StartLoadGist(script.Name);
  }

  // Load games found in a directory
  void AddFileScripts(string directory) {
    var n = 0;
    try {
      foreach (var pattern in _patterns) {
        var paths = Directory.GetFiles(directory, pattern);
        foreach (var path in paths) {
          AddScript(Path.GetFileName(directory), Path.GetFileNameWithoutExtension(path), ScriptKind.File, path);
          n++;
        }
      }
    } catch (Exception ex) {
      Util.Trace(1, "Exception finding scripts: {0}", ex.ToString());
    }
    Util.Trace(2, "loaded {0} file script(s)", n);
  }

  // load gists from a list in a file
  void AddGists(string gistlist) {
    var regex = new Regex("[0-9a-z]{32}");
    var n = 0;
    var sr = new StringReader(gistlist);
    for (var line = sr.ReadLine(); line != null; line = sr.ReadLine()) {
      var match = regex.Match(line);
      if (!match.Success)
        Util.Trace(1, "invalid gist: '{0}'", line);
      else {
        AddScript("Gist Puzzles", match.Value, ScriptKind.Gist, match.Value);
        ++n;
      }
    }
    Util.Trace(2, "loaded {0} gist(s)", n);
  }

  void AddAssetScripts(string directory) {
    var texts = Resources.LoadAll<TextAsset>(directory);
    for (int i = 0; i < texts.Length; i++) {
      AddScript(directory, texts[i].name, ScriptKind.Asset, texts[i].name, texts[i].text);
      Resources.UnloadAsset(texts[i]);
    }
    Util.Trace(2, "loaded {0} asset script(s)", texts.Length);
  }

  void AddScript(string source, string name, ScriptKind kind, string path, string value = null) {
    _scriptlookup[name] = new ScriptInfo {
      Source = source, Name = name, Kind = kind, Path = path, Value = value
    };
  }

}
