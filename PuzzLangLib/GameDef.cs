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
    None,
    Up, Left, Down, Right,              // arrow movements
    Action, Reaction, Fire1, Fire2, Fire3, Hover, // pseudo-movements
    Tick,     // nothing
    Restart,  // go to beginning of level or last checkpoint
    Reset,    // go to beginning of level regardless of checkpoint
    Level,    // load specified level
    Undo,     // return to previous state
    Init,     // run initial rules and checkpoint
  }

  public enum OptionSetting {
    None,
    run_rules_on_level_start, repeat_action, require_player_movement, debug, verbose_logging,
    throttle_movement, undo, action, restart, scanline,
    title, author, homepage, 
    background_color, text_color, 
    key_repeat_interval, realtime_interval, again_interval,
    flickscreen, zoomscreen, 
    color_palette, youtube,
    // extensions
    pause_at_end_level,
    pause_on_again,
    statistics_logging,
    click,
    hover,
    arrows,
  }

  // used in win conditions and rule object matching
  internal enum MatchOperator {
    None, No, Some, All,
    Any,    // alias for Some
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
    Text,     // text string
  }

  // directions used in rules and sound triggers
  internal enum Direction {
    // absolute directions, use for rules, patterns and actions
    None, Up, Down, Left, Right,      // basic directions
    Horizontal, Vertical, Orthogonal, // combos of the above
    // note: Orthogonal must be last in above group
    // absolute directions, patterns and actions only
    Action, Fire1, Fire2, Fire3, Hover, // input events treated as direction
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
    Action, Reaction, Create, Destroy,
    Move, Cantmove,               // optional direction
    // there are related to game events
    Titlescreen, Startgame, Cancel, Endgame, Startlevel, Endlevel,
    Restart, Undo, Showmessage, Closemessage,
    Checkpoint, Win, Again, Tick,
    // these must be specified as a command in a rule
    Sfx0, Sfx1, Sfx2, Sfx3, Sfx4, Sfx5, Sfx6, Sfx7, Sfx8, Sfx9, Sfx10,
  }

  // commands allowed in rules and used for both parsing and runtime
  // commands are executed in this order
  internal enum RuleCommand {
    None,
    Again,        // run rules again
    Checkpoint,   // save state of level (but no other state)
    Message,      // message, requires text argument
    Sound_,       // play a sound (internal command after parsing)
    Cancel,       // stop processing, discard new state
    Undo,         // stop processing, discard current state
    Restart,      // stop processing, discard all states, return to checkpoint
    Win,          // stop processing, set finished
    // sounds as parsed, not used at runtime
    Sfx0, Sfx1, Sfx2, Sfx3, Sfx4, Sfx5, Sfx6, Sfx7, Sfx8, Sfx9, Sfx10,  
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
  public class PuzzleObject {
    public string Name;
    public int Layer;               // layer on which to display
    public Pair<float,float> Pivot; // fixed point relative to cell
    public double Height;           // size of image (1.0 = size of cell)
    public int Width;               // sprite width in pixels
    public IList<int> Sprite;       // list of colours for each pixel
    public string Text;             // string to display over sprite
    public int TextColour;          // colour of text
  }

  /// <summary>
  /// A sound triggered by some combination of object, direction and event
  /// </summary>
  internal class SoundAction {
    internal SoundTrigger Trigger;
    internal HashSet<int> ObjectIds;  // .NET 3.5
    internal HashSet<Direction> Directions;
    internal string Seed;
  }

  /// <summary>
  /// A test for when the level is completed
  /// </summary>
  internal class WinCondition {
    internal MatchOperator Action;
    internal HashSet<int> ObjectIds;
    internal HashSet<int> Others;

    public override string ToString() {
      return String.Format("WinCo<{0}:{1}:{2}>", Action, ObjectIds.Join(), Others?.Join());
    }
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
      return String.Format("RuleGroup<{0}:{1}{2}>[{3}]", Loop, Id, IsRandom ? " random" : "",
        Rules.Select(r => r.RuleId).Join());
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
      return String.Format("Rule<{0}:{1},{2},{3},{4}>", 
        RuleId, RuleDirection, PatternCode, ActionCode, CommandCode);
    }
  }

  /// <summary>
  /// The compiled game as data. All strings replaced by integers/enums
  /// </summary>
  public class GameDef {
    static internal HashSet<Direction> MoveDirections = new HashSet<Direction> {
      Direction.Up, Direction.Right, Direction.Down, Direction.Left,
      Direction.Action, Direction.Fire1, Direction.Fire2, Direction.Fire3, Direction.Hover,
    };

    public int ObjectCount { get { return PuzzleObjects.Count; } }
    public int LevelCount { get { return LevelIndex.Count; } }

    internal Dictionary<OptionSetting, bool> BoolSettings = new Dictionary<OptionSetting, bool>();
    internal Dictionary<OptionSetting, int> IntSettings = new Dictionary<OptionSetting, int>();
    internal Dictionary<OptionSetting, float> NumberSettings = new Dictionary<OptionSetting, float>();
    internal Dictionary<OptionSetting, Pair<int, int>> PairSettings = new Dictionary<OptionSetting, Pair<int, int>>();  // .NET 3.5
    internal Dictionary<OptionSetting, string> StringSettings = new Dictionary<OptionSetting, string>();
    internal int[] Palette = new int[16];

    internal List<PuzzleObject> PuzzleObjects = new List<PuzzleObject>();
    internal List<RuleGroup> EarlyRules = new List<RuleGroup>();
    internal List<RuleGroup> LateRules = new List<RuleGroup>();
    internal Dictionary<SoundTrigger, string> GameSounds = new Dictionary<SoundTrigger, string>();
    internal List<SoundAction> ObjectSounds = new List<SoundAction>();
    internal List<WinCondition> WinConditions = new List<WinCondition>();
    internal List<string> Messages = new List<string>();
    internal List<Level> Levels = new List<Level>();
    internal List<Pair<LevelKind, int>> LevelIndex = new List<Pair<LevelKind, int>>();
    internal int LayerCount;
    internal int RngSeed = 99;
    internal HashSet<int> Background = null;
    internal HashSet<int> Players = new HashSet<int>();
    internal HashSet<int> Clickables = new HashSet<int>();

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
      return (value == -99) ? other : GetRGB(value);
    }

    // get colour by index or value as RGB or -1
    public int GetRGB(int value) {
      return (value >= 0 && value < Palette.Length) ? Palette[value]
        : (value == -1) ? value  // transparent
        : value & 0xffffff;
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

    public PuzzleObject GetObject(int id) {
      return PuzzleObjects[id - 1];
    }

    internal void SetObjectLayer(int obj, int layer) {
        PuzzleObjects[obj - 1].Layer = layer;
    }

    internal int GetLayer(int obj) {
      return PuzzleObjects[obj - 1].Layer;
    }

    public string GetName(int obj) {
      return (obj == 0) ? "(--)" : PuzzleObjects[obj - 1].Name;
    }

    //--- sounds
    public IList<string> GetSounds() {
      return ObjectSounds.Select(o => o.Seed)
        .Concat(GameSounds.Values)
        .Distinct().ToList();
    }

    internal void AddGameSound(SoundTrigger trigger, string seed) {
      GameSounds[trigger] = seed;
    }

    internal void AddObjectSound(SoundTrigger trigger, HashSet<int> objects, IEnumerable<Direction> directions, string seed) {
      ObjectSounds.Add(new SoundAction {
        Trigger = trigger,
        ObjectIds = objects,
        Directions = (directions == null) ? null : new HashSet<Direction>(directions),
        Seed = seed,
      });
    }

    // get the correct rule group, creating if needed
    internal RuleGroup GetRuleGroup(bool late, bool plus, bool random, bool rigid, int loop) {
      var groups = (late) ? LateRules : EarlyRules;
      if (!plus || groups.Count == 0) {
        groups.Add(new RuleGroup {
          Id = groups.Count,
          IsRandom = random,
          IsRigid = rigid,
          Loop = loop,
        });
      }
      var group = groups.Last();
      group.IsRigid |= rigid;   // any rigid rule applies to the entire group
      return group;
    }

    internal void AddWinCondition(MatchOperator action, HashSet<int> objects, HashSet<int> others) {
      WinConditions.Add(new WinCondition {
        Action = action,
        ObjectIds = objects,
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
