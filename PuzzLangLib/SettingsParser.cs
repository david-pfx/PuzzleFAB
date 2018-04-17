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
using System.Text;
using DOLE;

namespace PuzzLangLib {
  class SettingsParser {
    internal GameDef Game { get; set; }
    internal ColourParser Colour { get; set; }

    Dictionary<OptionSetting, Func<SettingsParser, OptionSetting, string, bool>> _settinglookup = 
      new Dictionary<OptionSetting, Func<SettingsParser, OptionSetting, string, bool>>() {
      { OptionSetting.None,                     (x,s,v) => false                },
      { OptionSetting.run_rules_on_level_start, (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.repeat_action,            (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.require_player_movement,  (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.debug,                    (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.verbose_logging,          (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.throttle_movement,        (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.undo,                     (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.action,                   (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.restart,                  (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.scanline,                 (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.pause_at_end_level,       (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.pause_on_again,           (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.statistics_logging,       (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.click,                    (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.hover,                    (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.arrows,                   (x,s,v) => x.ParseBool(s, v)    },
      { OptionSetting.title,                    (x,s,v) => x.ParseAny(s, v)     },
      { OptionSetting.author,                   (x,s,v) => x.ParseAny(s, v)     },
      { OptionSetting.homepage,                 (x,s,v) => x.ParseAny(s, v)     },
      { OptionSetting.background_color,         (x,s,v) => x.ParseColour(s, v)  },
      { OptionSetting.text_color,               (x,s,v) => x.ParseColour(s, v)  },
      { OptionSetting.key_repeat_interval,      (x,s,v) => x.ParseNumber(s, v)  },
      { OptionSetting.realtime_interval,        (x,s,v) => x.ParseNumber(s, v)  },
      { OptionSetting.again_interval,           (x,s,v) => x.ParseNumber(s, v)  },
      { OptionSetting.flickscreen,              (x,s,v) => x.ParseWxH(s, v)     },
      { OptionSetting.zoomscreen,               (x,s,v) => x.ParseWxH(s, v)     },
      { OptionSetting.color_palette,            (x,s,v) => x.ParsePalette(s, v) },
      { OptionSetting.youtube,                  (x,s,v) => x.ParseAny(s, v)     },
    };

    // parse setting and keep value
    internal bool Parse(string name, string value) {
      var setting = name.SafeEnumParse<OptionSetting>() ?? OptionSetting.None;
      return _settinglookup[setting](this, setting, value);
    }

    bool ParseAny(OptionSetting setting, string value) {
      Game.StringSettings[setting] = value;
      return true;
    }
    bool ParseBool(OptionSetting setting, string value) {
      Game.BoolSettings[setting] = value.SafeBoolParse() ?? true;
      return true;
    }
    bool ParseNumber(OptionSetting setting, string value) {
      var v = value.SafeDoubleParse() ?? 0;
      Game.NumberSettings[setting] = (float)v;
      return v >= 0;
    }
    bool ParseWxH(OptionSetting setting, string value) {
      var s = value.ToUpper().Split('X');
      if (s.Length != 2) return false;
      Game.PairSettings[setting] = Pair.Create(s[0].SafeIntParse() ?? 0, s[1].SafeIntParse() ?? 0);
      return true;
    }
    bool ParsePalette(OptionSetting setting, string value) {
      if (Colour.SetPalette(value)) {
        Game.Palette = Colour.GetPalette();
        return true;
      }
      return false;
    }
    bool ParseColour(OptionSetting setting, string value) {
      if (!Colour.IsColour(value)) return false;
      Game.IntSettings[setting] = Colour.ParseColour(value);
      return true;
    }

  }
}
