using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DOLE;
using PuzzLangLib;
using NsfxrLib;

/// <summary>
/// Implements a wrapper around the game model to load resources and manage access
/// </summary>
internal class GameModelInfo {
  internal ItemManager _itemmx;
  internal GameModel Model { get { return _model; } }
  // size set by flickscreen or zoomscreen, zero if not enabled
  internal Vector2Int ScreenSize;   
  internal string Message { get { return _message; } }
  internal List<Sprite> _sprites = new List<Sprite>();

  GameModel _model = null;
  Dictionary<string, AudioClip> _soundlookup = new Dictionary<string, AudioClip>();
  string _message = null;
  StringWriter _logwriter = new StringWriter();

  public GameModelInfo(ItemManager itemManager) {
    _itemmx = itemManager;
  }

  internal Sprite GetSprite(int id) {
    return (id >= 1 && id <= _sprites.Count) ? _sprites[id - 1] : null;
  }

  // get a clip from a seed or name
  internal AudioClip GetClip(string name) {
    return _soundlookup.SafeLookup(name) ?? _itemmx.DefaultSound;
  }

  // compile a script and set up all resources
  // if false, the reason why has been written to output
  internal bool Compile(string scriptname, string script) {
    Util.Trace(1, "Compile script '{0}'", scriptname);
    _model = null;
    try {
      var compiler = Compiler.Compile(scriptname, new StringReader(script), _logwriter);
      if (compiler.Success)
        _model = compiler.Model;
      else _message = _logwriter.ToString();
    } catch (Exception ex) {
      _message = ex.Message;
    }
    ShowLog();
    if (_model == null) return false;

    // set options required for proper timing; also logging
    _model.GameDef.SetSetting(OptionSetting.pause_at_end_level, true);
    _model.GameDef.SetSetting(OptionSetting.pause_on_again, true);
    if (_itemmx.VerboseLogging) _model.GameDef.SetSetting(OptionSetting.verbose_logging, true);

    // get settings for UI
    _itemmx.BackgroundColour = ColorFromRgb(_model.GameDef.GetColour(OptionSetting.background_color, 0));
    _itemmx.ForegroundColour = ColorFromRgb(_model.GameDef.GetColour(OptionSetting.text_color, 0xffffff));
    _itemmx.AgainInterval = _model.GameDef.GetSetting(OptionSetting.again_interval, 0.1f);
    _itemmx.RealtimeInterval = _model.GameDef.GetSetting(OptionSetting.realtime_interval, 0.25f);
    var flickscreen = _model.GameDef.GetSetting(OptionSetting.flickscreen, (Pair<int, int>)null);
    var zoomscreen = _model.GameDef.GetSetting(OptionSetting.zoomscreen, (Pair<int, int>)null);
    ScreenSize = (flickscreen != null) ? new Vector2Int(flickscreen.Item1, flickscreen.Item2)
               : (zoomscreen != null) ? new Vector2Int(zoomscreen.Item1, zoomscreen.Item2)
               : Vector2Int.zero; // use level size
    LoadGameAssets();
    return true;
  }

  // input to cause state change
  internal void AcceptInputs(string input) {
    Model.AcceptInputs(input);
    ShowLog();
  }

  // new level to cause state change
  internal void LoadLevel(int levelindex) {
    Model.LoadLevel(levelindex);
    ShowLog();
  }

  void ShowLog() {
    var output = _logwriter.ToString();
    _logwriter.GetStringBuilder().Length = 0;
    if (output != "") Debug.Log(output);
  }

  // make a table of all the images in this game
  void LoadGameAssets() {
    for (int i = 1; i <= _model.GameDef.ObjectCount; i++) {
      var texture = MakeTexture(_model.GameDef.GetObjectSprite(i));
      var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
      _sprites.Add(sprite);
    }
    _soundlookup = new Dictionary<string, AudioClip>();
    foreach (var seed in _model.GameDef.GetSounds()) {
      var nseed = seed.SafeIntParse() ?? 0;
      _soundlookup[seed] = (nseed > 0) ? Nsfxr.Generate(nseed) : _itemmx.DefaultSound;
    }
    Util.Trace(1, "Load assets objects={0} sounds={1}", _sprites.Count, _soundlookup.Count);
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
      var colindex = pair.Item2[y * width + x];
      var rgb = _model.GameDef.GetRGB(colindex);
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
