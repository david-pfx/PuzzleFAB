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

public class ItemManager : MonoBehaviour {
  // settings
  public bool VerboseLogging = false;
  public string GamesDirectory = "Games";
  public Color BackgroundColour = Color.yellow;
  public Color ForegroundColour = Color.blue;
  public float AgainInterval = 0.25f;
  public float RealtimeInterval = 0.25f;
  public Pair<int, int> FlickScreen = null;
  public Pair<int, int> ZoomScreen = null;
  public List<AudioClip> SoundEffects = new List<AudioClip>();

  internal string Message { get { return _writer.ToString(); } }
  internal List<string> ScriptList { get { return _scripts; } }

  string _userdirectory;
  readonly string[] _patterns = new string[] { "*.pzl", "*.txt" };
  List<string> _scripts = new List<string>();
  StringWriter _writer = new StringWriter();
  GameModel _model = null;
  List<Sprite> _sprites = null;
  StringWriter _logwriter = new StringWriter();

  internal void ShowLog() {
    var output = _logwriter.ToString();
    _logwriter.GetStringBuilder().Length = 0;
    if (output != "") Debug.Log(output);
  }

  internal Sprite GetSprite(int id) {
    return (id >= 1 && id <= _sprites.Count) ? _sprites[id - 1] : null;
  }

  // Find all games and add to script list
  internal void FindAllScripts() {
    _userdirectory = Util.Combine(Path.GetDirectoryName(Application.dataPath), GamesDirectory);
    // find all games included as text resources
    AddScripts(_userdirectory);
    Util.Trace(2, "[Find games {0}]", _scripts.Join());
  }

  // load a script by name
  internal string ReadScript(string scriptname) {
    Util.Trace(2, "Read script {0}", scriptname);
    var path = Path.Combine(_userdirectory, scriptname);
    if (!File.Exists(path)) return null;
    using (var sr = new StreamReader(path)) {
      return sr.ReadToEnd();
    }
  }

  // compile a script and return engine
  // if null, the reason why has been written to output
  internal GameModel Compile(string scriptname) {
    Util.Trace(2, "Compile script '{0}'", scriptname);
    try {
      var script = ReadScript(scriptname);
      var compiler = Compiler.Compile(scriptname, new StringReader(script), _logwriter);
      if (compiler.Success) _model = compiler.Model;
      else return null;
    } catch (Exception ex) {
      _writer.WriteLine(ex.Message);
      return null;
    }
    // set options required for proper timing; also logging
    _model.GameDef.SetSetting(OptionSetting.pause_at_end_level, true);
    _model.GameDef.SetSetting(OptionSetting.pause_on_again, true);
    if (VerboseLogging) _model.GameDef.SetSetting(OptionSetting.verbose_logging, true);

    // get settings for UI
    BackgroundColour = ColorFromRgb(_model.GameDef.GetColour(OptionSetting.background_color, 0));
    ForegroundColour = ColorFromRgb(_model.GameDef.GetColour(OptionSetting.text_color, 0xffffff));
    AgainInterval = _model.GameDef.GetSetting(OptionSetting.again_interval, 0.1f);
    RealtimeInterval = _model.GameDef.GetSetting(OptionSetting.realtime_interval, 0.25f);
    FlickScreen = _model.GameDef.GetSetting(OptionSetting.flickscreen, (Pair<int, int>)null);
    ZoomScreen = _model.GameDef.GetSetting(OptionSetting.zoomscreen, (Pair<int,int>)null);

    LoadAssets();
    return _model;
  }

  // get a clip from a seed or name
  internal AudioClip GetClip(string name) {
    var clip = SoundEffects.FirstOrDefault(s => s.name.ToLower().Contains(name.ToLower()));
    if (clip == null) {
      var seed = name.SafeIntParse() ?? 0;
      if (seed != 0) {
        var clips = SoundEffects.Where(s => s.name[0] == '0' + (seed % 10)).ToList();
        if (clips.Count > 0)
          clip = clips[seed / 100 % clips.Count];
      }
    }
    return clip;
  }

  // make a table of all the images in this game
  void LoadAssets() {
    Util.Trace(2, "Load assets count={0}", _model.GameDef.ObjectCount);
    _sprites = new List<Sprite>();
    for (int i = 1; i <= _model.GameDef.ObjectCount; i++) {
      var texture = MakeTexture(_model.GameDef.GetObjectSprite(i));
      var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
      _sprites.Add(sprite);
    }
  }

  // Load games found in a directory
  void AddScripts(string directory) {
    try {
      foreach (var pattern in _patterns) {
        var scripts = Directory.GetFiles(directory, pattern);
        foreach (var script in scripts)
          _scripts.Add(Path.GetFileName(script));
      }
    } catch (Exception) { }
  }

  // Create a texture from an array of colours
  Texture2D MakeTexture(Pair<int, IList<int>> pair) {
    var width = pair.Item1;
    var length = pair.Item2.Count;
    var pixels = new Color32[length];

    // build pixel output in single pixels
    for (int i = 0; i < length; i++) {
      var y = length / width - i / width - 1;
      var x = i % width;
      // colours may be a palette index
      var rgb = _model.GameDef.GetColour(pair.Item2[y * width + x]);
      pixels[i] = ColorFromRgb(rgb);
    }

    // now expand it
    var blockf = 16;
    var bwidth = blockf * width;
    var bpixels = new Color32[length * blockf * blockf];
    for (int i = 0; i < bpixels.Length; i++) {
      var y = (i / bwidth) / blockf;
      var x = (i % bwidth) / blockf;
      bpixels[i] = pixels[y * width + x];
    }

    var tex = new Texture2D(bwidth, bpixels.Length / bwidth);
    tex.SetPixels32(bpixels);
    tex.Apply();
    return tex;
  }

  // create colour from rgb
  Color ColorFromRgb(int rgb) {
    if (rgb == -1) return Color.clear;
    return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
  }


}
