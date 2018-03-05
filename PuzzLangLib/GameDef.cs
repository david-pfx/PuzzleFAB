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
using System.Linq;
using DOLE;

namespace PuzzLangLib {
  // all the possible input events
  public enum InputEvent {
    None, Up, Left, Down, Right,
    Action,
    Tick,
    Restart,  // go to beginning of level or last checkpoint
    Reset,    // go to beginning of level regardless of checkpoint
    Undo,     // return to previous state
    Init,     // run initial rules and checkpoint
  }

  public enum OptionSetting {
    None,
    run_rules_on_level_start, norepeat_action, require_player_movement, debug, verbose_logging,
    throttle_movement, noundo, noaction, norestart, scanline,
    title, author, homepage, 
    background_color, text_color, 
    key_repeat_interval, realtime_interval, again_interval,
    flickscreen, zoomscreen, 
    color_palette, youtube,
    // extensions
    pause_at_end_level,
    pause_on_again,
  }

  // used in win conditions and rule object matching
  internal enum MatchOperator {
    None, No, Any, Some, All
  }

  // used in defining mask and pile objects
  internal enum LogicOperator {
    None, And, Or, Not,
  }

  // used in defining a symbol (just objects for now)
  internal enum SymbolKind {
    None,
    Alias,   // real object with sprite
    Pile,     // set of objects occupying single space, for level definition
    Mask,     // set of objects sharing common property, for rule testing
    Mixed,    // set of objects of mixed parentage -- usually an error
  }

  // directions used in rules and sound triggers
  internal enum Direction {
    // absolute directions, use for rules, patterns and actions
    None, Up, Down, Left, Right,      // basic directions
    Horizontal, Vertical, Orthogonal, // combos of the above
    // note: Orthogonal must be last in above group
    // absolute directions, patterns and actions only
    Action,                           // input event treated as direction
    Forward,                          // pattern/action: same as rule direction
    Moving,                           // moving, excludes Action
    Stationary,                       // not moving, excludes Action
    Parallel,                         // ) pattern: generate 2 rules relative to rule dir
    Perpendicular,                    // ) action: must match pattern
    // relative directions, patterns and actions only, rotated when matched
    LeftArrow_, RightArrow_, UpArrow_, DownArrow_,  // < > ^ v
    Random,                           // pattern: choose rule; action: choose object
    Randomdir,                        // action: choose direction
  }

  // events to trigger sound
  internal enum SoundTrigger {
    None,
    // these require an object
    Action, Create, Destroy,
    Move, Cantmove,               // optional direction
    // there are related to game events
    Titlescreen, Startgame, Cancel, Endgame, Startlevel, Endlevel,
    Restart, Undo, Showmessage, Closemessage,
    Checkpoint, Win, Again, Tick,
    // these must be specified as a command in a rule
    Sfx0, Sfx1, Sfx2, Sfx3, Sfx4, Sfx5, Sfx6, Sfx7, Sfx8, Sfx9, Sfx10,
    MaxWithObject_ = Cantmove,
    MinSound_ = Sfx0,     // everything after here is a sound (even if it's a command)
  }

  // commands allowed in rules
  internal enum RuleCommand {
    None, Again, Cancel, Checkpoint, Restart, Undo, Win,    // single commands
    Message,                                          // message, requires text argument
    Sfx0, Sfx1, Sfx2, Sfx3, Sfx4, Sfx5, Sfx6, Sfx7, Sfx8, Sfx9, Sfx10,  // sound
    Sound_,       // used in parsing only
  }

  // special actions to prefix rules
  internal enum RulePrefix {
    None, Late, Rigid, Random, Plus_, Startloop_, Endloop_,
  }

  internal enum LevelKind {
    None, Level, Message,
  }

  /// <summary>
  /// An Object is a moveable piece with a visible representation
  /// </summary>
  internal class Object {
    internal string Name;
    internal int Layer;
    internal IList<int> Sprite;
    internal int Width;
  }

  /// <summary>
  /// A sound triggered by some combination of object, direction and event
  /// </summary>
  internal class SoundAction {
    internal SoundTrigger Trigger;
    internal HashSet<int> Object;  // .NET 3.5
    internal HashSet<Direction> Directions;
    internal string Seed;
  }

  /// <summary>
  /// A test for when the level is completed
  /// </summary>
  internal class WinCondition {
    internal MatchOperator Action;
    internal HashSet<int> Objects;
    internal HashSet<int> Others;
  }

  /// <summary>
  /// Group of rules subject to special processing
  /// </summary>
  internal class RuleGroup {
    internal int Id;
    internal bool IsRandom = false;
    internal bool IsRigid = false;
    internal int Loop;
    internal List<CompiledRule> Rules = new List<CompiledRule>();

    public override string ToString() {
      return String.Format("RuleGroup<{0}:{1}>{{{2}}}", Id,
        Util.JoinNonEmpty(" ", IsRandom ? "random" : null, IsRigid ? "rigid" : null, Loop), 
        Rules.Select(r=>r.RuleId).Join());
    }
  }

  /// <summary>
  /// Compiled rule ready for use
  /// </summary>
  internal class CompiledRule {
    internal int RuleId;
    internal Direction RuleDirection;
    internal RuleCode PatternCode;
    internal RuleCode ActionCode;
    internal RuleCode CommandCode;

    public override string ToString() {
      return String.Format("Rule<{0},{1},{2},{3},{4}>", 
        RuleId, RuleDirection, PatternCode.IsEmpty, ActionCode.IsEmpty, PatternCode.IsEmpty);
    }
  }

  /// <summary>
  /// The compiled game as data. All strings replaced by integers/enums
  /// </summary>
  public class GameDef {
    internal Dictionary<OptionSetting, bool> BoolSettings = new Dictionary<OptionSetting, bool>();
    internal Dictionary<OptionSetting, int> IntSettings = new Dictionary<OptionSetting, int>();
    internal Dictionary<OptionSetting, float> NumberSettings = new Dictionary<OptionSetting, float>();
    internal Dictionary<OptionSetting, Pair<int, int>> PairSettings = new Dictionary<OptionSetting, Pair<int, int>>();  // .NET 3.5
    internal Dictionary<OptionSetting, string> StringSettings = new Dictionary<OptionSetting, string>();
    internal int[] Palette = new int[16];

    internal List<Object> Objects = new List<Object>();

    internal List<RuleGroup> EarlyRules = new List<RuleGroup>();
    internal List<RuleGroup> LateRules = new List<RuleGroup>();
    internal Dictionary<SoundTrigger, string> GameSounds = new Dictionary<SoundTrigger, string>();
    internal List<SoundAction> ObjectSounds = new List<SoundAction>();
    internal List<WinCondition> WinConditions = new List<WinCondition>();
    internal List<string> Messages = new List<string>();
    internal List<Level> Levels = new List<Level>();
    internal List<Pair<LevelKind, int>> LevelIndex = new List<Pair<LevelKind, int>>(); 

    public int ObjectCount { get { return Objects.Count; } }
    public int LevelCount { get { return LevelIndex.Count; } }
    internal int LayerCount;
    internal HashSet<int> Players = null;  // .NET 3.5
    internal HashSet<int> Background = null;
    internal int RngSeed = 99;

    public string GetSetting(OptionSetting name, string other) {
      return StringSettings.SafeLookup(name) ?? other;
    }
    public float GetSetting(OptionSetting name, float other) {
      return NumberSettings.SafeLookup(name, other);
    }
    public bool GetSetting(OptionSetting name, bool other) {
      return BoolSettings.SafeLookup(name, other);
    }
    public Pair<int,int> GetSetting(OptionSetting name, Pair<int,int> other) {
      return PairSettings.SafeLookup(name, other);
    }

    // get colour by name, or other if not defined
    public int GetColour(OptionSetting name, int other) {
      var value = IntSettings.SafeLookup(name, -99);
      return (value == -99) ? other : GetColour(value);
    }

    // get colour by index or value
    public int GetColour(int value) {
      return (value >= 0) ? value
        : (value == -1) ? value  // transparent
        : Palette[-value - 2];
    }


    public void SetSetting(OptionSetting name, string value) {
      if (value == null) StringSettings.Remove(name);
      else StringSettings[name] = value;
    }
    public void SetSetting(OptionSetting name, float value) {
      NumberSettings[name] = value;
    }
    public void SetSetting(OptionSetting name, int value) {
      StringSettings[name] = "#{0:h6}".Fmt(value);  // TEST
    }
    public void SetSetting(OptionSetting name, bool value) {
      BoolSettings[name] = value;
    }
    public void SetSetting(OptionSetting name, Pair<int, int> value) {
      PairSettings[name] = value;
    }

    public Pair<int,IList<int>> GetObjectSprite(int id) {
      var obj = Objects[id - 1];
      return Pair.Create(obj.Width, obj.Sprite);
    }

    internal void SetObjectLayer(int obj, int layer) {
        Objects[obj - 1].Layer = layer;
    }

    internal int GetLayer(int obj) {
      return Objects[obj - 1].Layer;
    }

    public string GetName(int obj) {
      return Objects[obj - 1].Name;
    }

    internal void AddGameSound(SoundTrigger trigger, string seed) {
      GameSounds[trigger] = seed;
    }

    internal void AddObjectSound(SoundTrigger trigger, HashSet<int> objects, IEnumerable<Direction> directions, string seed) {
      ObjectSounds.Add(new SoundAction {
        Trigger = trigger,
        Object = objects,
        Directions = (directions == null) ? null : new HashSet<Direction>(directions),
        Seed = seed,
      });
    }

    internal void AddRule(CompiledRule rule, bool late, bool plus, bool random, bool rigid, int loop) {
      var rules = (late) ? LateRules : EarlyRules;
      if (!plus)
        rules.Add(new RuleGroup {
          Id = rules.Count,
          IsRandom = random,
          IsRigid = rigid,
          Loop = loop, });
      rules.Last().Rules.Add(rule);
    }

    internal void AddWinCondition(MatchOperator action, HashSet<int> objects, HashSet<int> others) {
      WinConditions.Add(new WinCondition {
        Action = action,
        Objects = objects,
        Others = others
      });
    }

    internal void AddLevel(Level level) {
      LevelIndex.Add(Pair.Create(LevelKind.Level, Levels.Count));
      Levels.Add(level);
    }

    internal void AddMessage(string message) {
      LevelIndex.Add(Pair.Create(LevelKind.Message, Messages.Count));
      Messages.Add(message);
    }

  }
}
