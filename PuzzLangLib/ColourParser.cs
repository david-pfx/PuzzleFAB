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
using System.Text.RegularExpressions;
using DOLE;

namespace PuzzLangLib {
  class ColourParser {
    // exactly 24 colours in precisely this order
    internal enum Colours {
      Black, White, Grey, DarkGrey, LightGrey, Gray, DarkGray, LightGray,
      Red, DarkRed, LightRed, Brown, DarkBrown, LightBrown, Orange, Yellow,
      Green, DarkGreen, LightGreen, Blue, LightBlue, DarkBlue, Purple, Pink,
      Maximum,
      Transparent = 98,
    };  

    // integer lookup is also permitted
    Dictionary<string, int> _palettelookup = new Dictionary<string, int> {
      { "mastersystem",     1 },
      { "gameboycolour",    2 },
      { "amiga",            3 },
      { "arnecolors",       4 }, // default
      { "famicom",          5 },
      { "atari",            6 },
      { "pastel",           7 },
      { "ega",              8 },
      { "amstrad",          9 },
      { "proteus_mellow",  10 },
      { "proteus_rich",    11 },
      { "proteus_night",   12 },
      { "c64",             13 },
      { "whitingjp",       14 },
    };
    int[][] _colourtable = new int[15][];

    internal ColourParser() { 
      _colourtable[_palettelookup["mastersystem"]] = new int[] {
        0x000000, 0xFFFFFF, 0x555555, 0x555500, 0xAAAAAA, 0x555555, 0x555500, 0xAAAAAA,
        0xFF0000, 0xAA0000, 0xFF5555, 0xAA5500, 0x550000, 0xFFAA00, 0xFF5500, 0xFFFF55,
        0x55AA00, 0x005500, 0xAAFF00, 0x5555AA, 0xAAFFFF, 0x000055, 0x550055, 0xFFAAFF,
      };
      _colourtable[_palettelookup["arnecolors"]] = new int[] {
        0x000000, 0xFFFFFF, 0x9d9d9d, 0x697175, 0xcccccc, 0x9d9d9d, 0x697175, 0xcccccc,
        0xbe2633, 0x732930, 0xe06f8b, 0xa46422, 0x493c2b, 0xeeb62f, 0xeb8931, 0xf7e26b,
        0x44891a, 0x2f484e, 0xa3ce27, 0x1d57f7, 0xB2DCEF, 0x1B2632, 0x342a97, 0xde65e2,
      };
    }

    string _palette = "arnecolors";
    Regex _alphahex = new Regex("^#[0-9a-fA-Z]{8}$");
    Regex _longhex = new Regex("^#[0-9a-fA-Z]{6}$");
    Regex _shorthex = new Regex("^#[0-9a-fA-Z]{3}$");

    internal bool IsPalette(string name) {
      return _palettelookup.ContainsKey(name);
    }

    internal int[] GetPalette() {
      return _colourtable[_palettelookup[_palette]];
    }

    // Check for valid paletter name, then check whether defined
    internal bool SetPalette(string name) {
      if (!IsPalette(name)) return false;
      if (_colourtable[_palettelookup[name]] != null) _palette = name;
      return true;
    }

    internal bool IsColour(string value) {
      if (_alphahex.IsMatch(value) || _longhex.IsMatch(value) || _shorthex.IsMatch(value)) return true;
      return value.SafeEnumParse<Colours>() != null;
    }

    // parse colour into string, defer palette lookup until later
    internal int ParseColour(string value) {
      if (value.StartsWith("#")) {
        var rgb = value.Substring(1).SafeHexParse() ?? 0;
        if (_shorthex.IsMatch(value)) return
            (((rgb & 0xf00) * 17) << 16)
          | (((rgb & 0xf0) * 17) << 8)
          | ((rgb & 0xf) * 17);
        return rgb;
      }
      var colour = value.SafeEnumParse<Colours>() ?? Colours.Black;
      return colour == Colours.Transparent ? -1 : -2 - (int)colour;
    }

  }
}
