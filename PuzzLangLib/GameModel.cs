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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static System.Math;
using DOLE;

namespace PuzzLangLib {
  enum ModelState {
    None,         // new object, no game
    // order required for OK
    Compiled,     // initial state post-compilation, load level expected
    Level,        // current level is valid, direction input expected
    EndLevel,     // just finished level, any input expected
    Message,      // current message is valid, any input expected
    // order required for game over
    Finished,     // final level completed, load level expected
    Failed,       // a fatal error occurred -- see message
  }

  /// <summary>
  /// Precise identification of a moving object
  /// </summary>
  class Mover {
    internal int Object;
    internal Locator Locator;
    internal Direction Direction;
    internal RuleGroup RuleGroup;

    // create a mover, which may target an illegal location
    static internal Mover Create(int obj, Locator loc, Direction dir, RuleGroup group = null) {
      return new Mover { Object = obj, Locator = loc, Direction = dir, RuleGroup = group };
    }
    public override string ToString() {
      return $"Mover<{Object},{Locator},{Direction}>";
    }
  }

  /// <summary>
  /// The model representing the current state of the game
  /// 
  /// The model has:
  ///  - a single current level index
  ///  - a stack of game states. These provide undo capability. 
  /// 
  /// Each game state:
  ///  - represents one successful move, 
  ///  - has a new level, sounds, messages and commands
  ///  - has a single rule state, which it may discard and/or reconstruct to handle rigid and cancel
  ///  - transitions from initial state to final state
  ///  
  ///  A rule state:
  ///   - has the current state of the level
  ///   - has a list of commands, sounds and messages  
  ///   - transitions according to the evaluation of rules
  ///   
  /// </summary>
  public class GameModel {
    public GameDef GameDef { get { return _gamedef; } }
    public int CurrentLevelIndex { get { return _levelindex; } }
    // copy of level for restart
    public Level Checkpoint { get; private set; }
    // input treated as None until again mode ended
    public bool Againing { get { return (InLevel || EndLevel) && _states.Last().AgainCount > 0; } }
    // status shortcuts
    public bool Ok { get { return _modelstate > ModelState.None && _modelstate != ModelState.Failed; } }
    public bool InMessage { get { return _modelstate == ModelState.Message; } }
    public bool InLevel { get { return _modelstate == ModelState.Level; } }
    public bool EndLevel { get { return _modelstate == ModelState.EndLevel; } }
    public bool GameOver { get { return _modelstate >= ModelState.Finished; } }

    public string CurrentMessage { get { return InMessage ? _message : null; } }
    public Level CurrentLevel { get { return InLevel || EndLevel ? _states.Last().Level : null; } }
    public HashSet<string> Sounds = new HashSet<string>();

    // for testing only
    public Level FirstLevel { get { return _states.First().Level; } }
    public Level LastLevel { get { return _states.Last().Level; } }

    internal string _sourcename;
    internal TextWriter _out;
    internal GameDef _gamedef;
    internal Evaluator _evaluator;
    internal Random Rng;
    internal ModelState _modelstate = ModelState.None;

    List<GameState> _states = new List<GameState>();
    int _levelindex;
    string _message;

    // access level (for testing)
    public IList<int> GetObjects(int location) {
      return LastLevel.GetObjects(location);
    }

    public int? Step(int location, string direction) {
      return LastLevel.Step(location, direction.SafeEnumParse<Direction>() ?? Direction.None);
    }

    //-- ctors
    public static GameModel Create(TextWriter tw, GameDef game, string sourcename) {
      var gm = new GameModel {
        _sourcename = sourcename,
        _out = tw, _gamedef = game,
        _evaluator = Evaluator.Create(tw),
        Rng = new Random(game.RngSeed),
        _modelstate = ModelState.Compiled,
      };
      gm.CheckTrigger(SoundTrigger.Startgame);
      return gm;
    }

    // execute with string inputs
    public bool Execute(string levelno, string inputs) {
      if (!Ok) return false;
      if (!LoadLevel(levelno.SafeIntParse() ?? 0)) return false;
      if (inputs != null && inputs != "") AcceptInputs(inputs);
      Logger.WriteLine(2, "Done ok={0}", Ok);
      return Ok;
    }

    // load and initialise some level
    public bool LoadLevel(int levelindex) {
      DebugWriteline("Load level={0}", levelindex);
      if (levelindex < 0) return false;
      _levelindex = levelindex;
      if (_levelindex >= _gamedef.LevelIndex.Count) {
        _modelstate = ModelState.Finished;
        CheckTrigger(SoundTrigger.Endgame);
      } else {
        var levelinfo = _gamedef.LevelIndex[_levelindex];
        if (levelinfo.Item1 == LevelKind.Message) {
          _message = _gamedef.Messages[levelinfo.Item2];
          _modelstate = ModelState.Message;
          CheckTrigger(SoundTrigger.Showmessage);
        } else {
          var level = _gamedef.Levels[levelinfo.Item2];
          _states = new List<GameState> { GameState.Create(this, level, 1) };
          _message = null;
          _modelstate = ModelState.Level;
          SetCheckpoint(CurrentLevel);
          if (_gamedef.GetSetting(OptionSetting.run_rules_on_level_start, false))
            AcceptInputs("init");
          // take checkpoint after run rules
          SetCheckpoint(CurrentLevel ?? level);
          CheckTrigger(SoundTrigger.Startlevel);
        }
      }
      if (Logger.Level > 2) ShowLevel("loadlevel");
      return true;
    }

    public void AcceptInputs(string inputs) {
      try {
        foreach (var inevent in ParseInputEvents(inputs)) {
          if (!GameOver) 
            Accept(inevent);
        }
      } catch (Exception ex) {
        _message = "Error: {0}".Fmt(ex.Message);
        if (ex is DOLEException)
          Logger.WriteLine("*** '{0}': runtime error: {1}", _sourcename, ex.Message);
        else Logger.WriteLine("*** '{0}': runtime exception: {1}", _sourcename, ex.ToString());
        _modelstate = ModelState.Failed;
      }
    }

    internal void SetCheckpoint(Level currentLevel) {
      Checkpoint = currentLevel.Clone("checkpoint");
    }

    internal void DebugWriteline(string format, params object[] args) {
      if (GameDef.GetSetting(OptionSetting.verbose_logging, false))
        _out.WriteLine(String.Format(format, args));
      else Logger.WriteLine(1, format, args);
    }

    public void ShowLevel(string message) {
      if (InMessage)
        Logger.WriteLine(0, "Level message: {0}", CurrentMessage);
      else if (InLevel)
        ShowLevel(message, CurrentLevel);
      else Logger.WriteLine(0, "Level status: {0}", _modelstate);
    }

    // show all objects in level using sorted integers
    void ShowLevel(string message, Level level) {
      Logger.WriteLine(0, "At {0} level: '{0}'", message, level.Name);
      Func<int, char> tochar = n => (char)((n == 0) ? '.' : (n <= 9) ? '0' + n : 'A' + n - 10);

      var objs = Enumerable.Range(0, level.Length)
        .Select(x => level
          .GetObjects(x)
          .OrderBy(o => o)
          .Select(o=>tochar(o))
          .Join(""))
        .ToList();
      var wid = objs.Max(o => o.Length) + 1;
      var text = objs.Select(o => o.PadRight(wid - 1)).Join("|");
      for (var y = 0; y < level.Height; y++) {
        Logger.WriteLine(0, text.Substring(y * wid * level.Width, wid * level.Width - 1));
      }

    }
    // show all objects in level using one char each .0-9A-Z
    void OldShowLevel(Level level) {
      Logger.WriteLine(0, "Level: '{0}'", level.Name);
      Func<int, char> tochar = n => (char)((n == 0) ? '.' : (n <= 10) ? '0' + n - 1 : 'A' + n - 11);
      for (var y = 0; y < level.Height; y++) {
        var sb = new StringBuilder();
        for (var x = 0; x < level.Width; x++) {
          var n = 0;
          for (var z = 0; z < level.Depth; z++)
            n |= 1 << level[x, y, z + 1];
          sb.Append(tochar(n / 2 - 1));
          sb.Append(' ');
        }
        Logger.WriteLine(0, sb.ToString());
      }
    }

    // reset/restore to an earlier level (no undo right now)
    // arg is current state (for move no and future features)
    internal void Restart(GameState state) {
      _states = new List<GameState> {
        GameState.Create(this, Checkpoint, state.MoveNumber)
      };
    }

    //--- impl

    // Accept input for the current level
    // may result in level up or game over
    bool Accept(InputEvent input) {
      DebugWriteline("Accept input of {0}", input);
      Sounds.Clear();
      if (InMessage) {
        CheckTrigger(SoundTrigger.Closemessage);
        LoadLevel(_levelindex + 1);
        if (Logger.Level >= 3) _out.WriteLine("Message: '{0}'", _message);
      } else if (InLevel && IsValidInGame(input)) {
        HandleInput(input);
        while (Againing && !_gamedef.GetSetting(OptionSetting.pause_on_again, false)) {
          if (Logger.Level >= 3) ShowLevel("againing");
          HandleInput(InputEvent.Tick);
        }
        if (Logger.Level >= 2) ShowLevel("new state");
        var newstate = _states.Last();
        if (newstate.Finished) {
        CheckTrigger(SoundTrigger.Endlevel);
          if (_gamedef.GetSetting(OptionSetting.pause_at_end_level, false))
            _modelstate = ModelState.EndLevel;
          else LoadLevel(_levelindex + 1);
        }
      } else if (EndLevel) {
        LoadLevel(_levelindex + 1);
      } else return false;
      return true;
    }

    // test whether this input is permitted in this game
    bool IsValidInGame(InputEvent inevent) {
      switch (inevent) {
      case InputEvent.None:
        return false;
      case InputEvent.Action:
        return !GameDef.GetSetting(OptionSetting.noaction, false);
      case InputEvent.Restart:
        return !GameDef.GetSetting(OptionSetting.norestart, false);
      case InputEvent.Undo:
        return !GameDef.GetSetting(OptionSetting.noundo, false);
      }
      return true;
    }

    // compute and handle next state based on input event
    void HandleInput(InputEvent input) {
      Logger.WriteLine(2, "HandleInput input={0}", input);
      switch (input) {
      case InputEvent.Up:
      case InputEvent.Left:
      case InputEvent.Down:
      case InputEvent.Right:
      case InputEvent.Action:
      case InputEvent.Tick:
      case InputEvent.Init:
        var dir = (Againing) ? Direction.None
          : input.ToString().SafeEnumParse<Direction>() ?? Direction.None;

        // apply input and rules to derive next state
        var newstate = _states.Last().NextState(dir);

        // null means throw it away
        if (newstate == null) {
          _states.Last().AgainCount = 0;

          // undo means discard top of stack
        } else if (newstate.Undo) {
          _states.Remove(_states.Last());
          CheckTrigger(SoundTrigger.Undo);
          _states.Last().AgainCount = 0;
        } else {

          // handle againing
          if (newstate.AgainCount > 0) {
            newstate.AgainCount += _states.Last().AgainCount;
            if (newstate.AgainCount >= 100) throw Error.Fatal("too many agains");
            Logger.WriteLine(2, "Again count {0}", newstate.AgainCount);
            _states.Remove(_states.Last());
          }

          // add final state to stack
          _states.Add(newstate);
          Sounds.UnionWith(newstate.Sounds);
          CheckTrigger(input == InputEvent.Action ? SoundTrigger.Action
            : input == InputEvent.Tick ? SoundTrigger.Tick : SoundTrigger.Move);
        }
        break;
      case InputEvent.Reset:
        //CheckTrigger(SoundTrigger.Startlevel);
        LoadLevel(_levelindex);
        break;
      case InputEvent.Restart:
        Restart(_states.Last());
        CheckTrigger(SoundTrigger.Restart);
        break;
      case InputEvent.Undo:
        if (_states.Count > 1) {
          _states.Remove(_states.Last());
          CheckTrigger(SoundTrigger.Undo);
        }
        break;
      default:
        throw Error.Assert($"input {input}");
      }
    }

    void CheckTrigger(SoundTrigger trigger) {
      var sound = _gamedef.GameSounds.SafeLookup(trigger);
      if (sound != null && !Sounds.Contains(sound)) {
        Sounds.Add(sound);
        DebugWriteline("Sound {0} {1}", trigger, sound);
      }
    }

    // parse string list of input events
    IList<InputEvent> ParseInputEvents(string input) {
      return (input ?? "").SplitTrim(",")
        .Select(i => i.SafeEnumParse<InputEvent>() ?? InputEvent.None).ToList();
    }

    // obs???
    //void ShowAll() {
    //  var msgidx = 0;
    //  for (int levelno = 0; levelno < _gamedef.Levels.Count; ++levelno) {
    //    var level = _gamedef.Levels[levelno];
    //    while (msgidx < level.MessageIndex)
    //      _out.WriteLine(_gamedef.Messages[msgidx++]);
    //    ShowLevel(level);
    //    var state = GameState.Create(this, level, 1);
    //  }

    //  while (msgidx < _gamedef.Messages.Count)
    //    _out.WriteLine(_gamedef.Messages[msgidx++]);
    //}
  }
}
