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
internal class ModelInfo {
  // current compiled script name
  internal string ScriptName { get; private set; }
  // current compiled model -- may be null
  internal GameModel Model { get { return _model; } }
  // size set by flickscreen or zoomscreen, zero if not enabled
  internal Vector2Int ScreenSize;   
  // message from model for display
  internal string Message { get; private set; }

  MainController _main { get { return MainController.Instance; } }

  GameModel _model = null;
  Sprite[] _sprites;
  Dictionary<string, AudioClip> _soundlookup = new Dictionary<string, AudioClip>();
  StringWriter _logwriter = new StringWriter();

  internal Sprite GetSprite(int id) {
    return (id >= 1 && id <= _sprites.Length) ? _sprites[id - 1] : null;
  }

  // get a clip from a seed or name
  internal AudioClip GetClip(string name) {
    return _soundlookup.SafeLookup(name) ?? _main.DefaultSound;
  }

  // compile a script and set up all resources
  // if false, the reason why has been written to output
  internal bool Compile(string scriptname, string script) {
    Util.Trace(1, "Compile script '{0}'", scriptname);
    _model = null;
    ScriptName = scriptname;
    try {
      DOLE.Logger.Level = 0;
      var compiler = Compiler.Compile(scriptname, new StringReader(script), _logwriter);
      if (compiler.Success)
        _model = compiler.Model;
      else Message = _logwriter.ToString();
    } catch (Exception ex) {
      Message = ex.Message;
    }
    ShowLog();
    if (_model == null) return false;

    // set options required for proper timing; also logging
    DOLE.Logger.Level = _main.Noisy;
    _model.GameDef.SetSetting(OptionSetting.pause_at_end_level, true);
    _model.GameDef.SetSetting(OptionSetting.pause_on_again, true);
    if (_main.VerboseLogging) _model.GameDef.SetSetting(OptionSetting.verbose_logging, true);

    // get settings for UI
    _main.BackgroundColour = ColorFromRgb(_model.GameDef.GetColour(OptionSetting.background_color, 0));
    _main.ForegroundColour = ColorFromRgb(_model.GameDef.GetColour(OptionSetting.text_color, 0xffffff));
    _main.AgainInterval = _model.GameDef.GetSetting(OptionSetting.again_interval, 0.1f);
    _main.RealtimeInterval = _model.GameDef.GetSetting(OptionSetting.realtime_interval, 0.25f);
    var flickscreen = _model.GameDef.GetSetting(OptionSetting.flickscreen, (Pair<int, int>)null);
    var zoomscreen = _model.GameDef.GetSetting(OptionSetting.zoomscreen, (Pair<int, int>)null);
    ScreenSize = (flickscreen != null) ? new Vector2Int(flickscreen.Item1, flickscreen.Item2)
               : (zoomscreen != null) ? new Vector2Int(zoomscreen.Item1, zoomscreen.Item2)
               : Vector2Int.zero; // use level size
    LoadGameAssets();
    return true;
  }

  // input to cause state change
  internal void AcceptInput(string input) {
    Model.AcceptInput(input);
    ShowLog();
  }

  void ShowLog() {
    var output = _logwriter.ToString();
    _logwriter.GetStringBuilder().Length = 0;
    if (output != "") Debug.Log(output);
  }

  // make a table of all the images in this game
  void LoadGameAssets() {
    _sprites = new Sprite[_model.GameDef.ObjectCount];
    var spcount = 0;
    for (int i = 1; i <= _model.GameDef.ObjectCount; i++) {
      var obj = _model.GameDef.GetObject(i);
      if (obj.Width > 0) {
        var texture = MakeTexture(obj.Width, obj.Sprite);
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        _sprites[i - 1] = sprite;
        ++spcount;
      }
    }
    _soundlookup = new Dictionary<string, AudioClip>();
    foreach (var seed in _model.GameDef.GetSounds()) {
      var nseed = seed.SafeIntParse() ?? 0;
      _soundlookup[seed] = (nseed > 0) ? Nsfxr.Generate(nseed) : _main.DefaultSound;
    }
    Util.Trace(1, "Load assets objects={0} sounds={1}", spcount, _soundlookup.Count);
  }

  // Create a texture from an array of colours
  Texture2D MakeTexture(int width, IList<int> sprite) {
    var length = sprite.Count;
    var pixels = new Color32[length];

    // build pixel output in single pixels
    for (int i = 0; i < length; i++) {
      var y = length / width - i / width - 1;
      var x = i % width;
      // colours may be a palette index
      var colindex = sprite[y * width + x];
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

  // create colour from index (via palette)
  internal Color ColorFromIndex(int colindex) {
    var rgb = _model.GameDef.GetRGB(colindex);
    return ColorFromRgb(rgb);
  }

  // create colour from rgb
  Color ColorFromRgb(int rgb) {
    if (rgb == -1) return Color.clear;
    return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
  }

}
