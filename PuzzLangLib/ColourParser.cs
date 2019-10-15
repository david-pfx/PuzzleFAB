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
    static readonly Dictionary<string, int> _palettelookup = new Dictionary<string, int> {
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
    static readonly int[][] _colourtable = new int[][] {
      new int[] { // 1
        0x000000, 0xFFFFFF, 0x555555, 0x555500, 0xAAAAAA, 0x555555, 0x555500, 0xAAAAAA,
        0xFF0000, 0xAA0000, 0xFF5555, 0xAA5500, 0x550000, 0xFFAA00, 0xFF5500, 0xFFFF55,
        0x55AA00, 0x005500, 0xAAFF00, 0x5555AA, 0xAAFFFF, 0x000055, 0x550055, 0xFFAAFF, },
      new int[] { // 2
        0x000000, 0xFFFFFF, 0x7F7F7C, 0x3E3E44, 0xBAA7A7, 0x7F7F7C, 0x3E3E44, 0xBAA7A7,
        0xA7120C, 0x880606, 0xBA381F, 0x57381F, 0x3E2519, 0x8E634B, 0xBA4B32, 0xC0BA6F,
        0x517525, 0x385D12, 0x6F8E44, 0x5D6FA7, 0x8EA7A7, 0x4B575D, 0x3E3E44, 0xBA381F, },
      new int[] { // 3
        0x000000, 0xFFFFFF, 0xBBBBBB, 0x333333, 0xFFEEDD, 0xBBBBBB, 0x333333, 0xFFEEDD,
        0xDD1111, 0x990000, 0xFF4422, 0x663311, 0x331100, 0xAA6644, 0xFF6644, 0xFFDD66,
        0x448811, 0x335500, 0x88BB77, 0x8899DD, 0xBBDDEE, 0x666688, 0x665555, 0x997788, },
      new int[] { // 4
        0x000000, 0xFFFFFF, 0x9d9d9d, 0x697175, 0xcccccc, 0x9d9d9d, 0x697175, 0xcccccc,
        0xbe2633, 0x732930, 0xe06f8b, 0xa46422, 0x493c2b, 0xeeb62f, 0xeb8931, 0xf7e26b,
        0x44891a, 0x2f484e, 0xa3ce27, 0x1d57f7, 0xB2DCEF, 0x1B2632, 0x342a97, 0xde65e2, },
      new int[] {
        0x000000, 0xffffff, 0x7c7c7c, 0x080808, 0xbcbcbc, 0x7c7c7c, 0x080808, 0xbcbcbc,
        0xf83800, 0x881400, 0xf87858, 0xAC7C00, 0x503000, 0xFCE0A8, 0xFCA044, 0xF8B800,
        0x00B800, 0x005800, 0xB8F8B8, 0x0058F8, 0x3CBCFC, 0x0000BC, 0x6644FC, 0xF878F8, },
      new int[] {
        0x000000, 0xFFFFFF, 0x909090, 0x404040, 0xb0b0b0, 0x909090, 0x404040, 0xb0b0b0,
        0xA03C50, 0x700014, 0xDC849C, 0x805020, 0x703400, 0xCB9870, 0xCCAC70, 0xECD09C,
        0x58B06C, 0x006414, 0x70C484, 0x1C3C88, 0x6888C8, 0x000088, 0x3C0080, 0xB484DC, },
      new int[] {
        0x000000, 0xFFFFFF, 0x3e3e3e, 0x313131, 0x9cbcbc, 0x3e3e3e, 0x313131, 0x9cbcbc,
        0xf56ca2, 0xa63577, 0xffa9cf, 0xb58c53, 0x787562, 0xB58C53, 0xEB792D, 0xFFe15F,
        0x00FF4F, 0x2b732c, 0x97c04f, 0x0f88d3, 0x00fffe, 0x293a7b, 0xff6554, 0xeb792d, },
      new int[] {
        0x000000, 0xffffff, 0x555555, 0x555555, 0xaaaaaa, 0x555555, 0x555555, 0xaaaaaa,
        0xff5555, 0xaa0000, 0xff55ff, 0xaa5500, 0xaa5500, 0xffff55, 0xff5555, 0xffff55,
        0x00aa00, 0x00aaaa, 0x55ff55, 0x5555ff, 0x55ffff, 0x0000aa, 0xaa00aa, 0xff55ff, },
      new int[] {
        0x3d2d2e, 0xddf1fc, 0x9fb2d4, 0x7b8272, 0xa4bfda, 0x9fb2d4, 0x7b8272, 0xa4bfda,
        0x9d5443, 0x8c5b4a, 0x94614c, 0x89a78d, 0x829e88, 0xaaae97, 0xd1ba86, 0xd6cda2,
        0x75ac8d, 0x8fa67f, 0x8eb682, 0x88a3ce, 0xa5adb0, 0x5c6b8c, 0xd39fac, 0xc8ac9e, },
      new int[] {
        0x010912, 0xfdeeec, 0x051d40, 0x091842, 0x062151, 0x051d40, 0x091842, 0x062151,
        0xad4576, 0x934765, 0xab6290, 0x61646b, 0x3d2d2d, 0x8393a0, 0x0a2227, 0x0a2541,
        0x75ac8d, 0x0a2434, 0x061f2e, 0x0b2c79, 0x809ccb, 0x08153b, 0x666a87, 0x754b4d, },
      new int[] {
        0x6f686f, 0xd1b1e2, 0xb9aac1, 0x8e8b84, 0xc7b5cd, 0xb9aac1, 0x8e8b84, 0xc7b5cd,
        0xa11f4f, 0x934765, 0xc998ad, 0x89867d, 0x797f75, 0xab9997, 0xce8c5c, 0xf0d959,
        0x75bc54, 0x599d79, 0x90cf5c, 0x8fd0ec, 0xbcdce7, 0x0b2c70, 0x9b377f, 0xcd88e5, },
      new int[] {
        0x000000, 0xffffff, 0x7f7f7f, 0x636363, 0xafafaf, 0x7f7f7f, 0x636363, 0xafafaf,
        0xff0000, 0x7f0000, 0xff7f7f, 0xff7f00, 0x7f7f00, 0xffff00, 0xff007f, 0xffff7f,
        0x01ff00, 0x007f00, 0x7fff7f, 0x0000ff, 0x7f7fff, 0x00007f, 0x7f007f, 0xff7fff, },
      new int[] {
        0x000000, 0xffffff, 0x6C6C6C, 0x444444, 0x959595, 0x6C6C6C, 0x444444, 0x959595,
        0x68372B, 0x3f1e17, 0x9A6759, 0x433900, 0x221c02, 0x6d5c0d, 0x6F4F25, 0xB8C76F,
        0x588D43, 0x345129, 0x9AD284, 0x6C5EB5, 0x70A4B2, 0x352879, 0x6F3D86, 0xb044ac, },
      new int[] {
        0x202527, 0xeff8fd, 0x7b7680, 0x3c3b44, 0xbed0d7, 0x7b7680, 0x3c3b44, 0xbed0d7,
        0xbd194b, 0x6b1334, 0xef2358, 0xb52e1c, 0x681c12, 0xe87b45, 0xff8c10, 0xfbd524,
        0x36bc3c, 0x317610, 0x8ce062, 0x3f62c6, 0x57bbe0, 0x2c2fa0, 0x7037d9, 0xec2b8f, },
    };

    string _palette = "arnecolors";
    Regex _alphahex = new Regex("^#[0-9a-fA-Z]{8}$");
    Regex _longhex = new Regex("^#[0-9a-fA-Z]{6}$");
    Regex _shorthex = new Regex("^#[0-9a-fA-Z]{3}$");

    internal bool IsPalette(string name) {
      return _palettelookup.ContainsKey(name);
    }

    internal int[] GetPalette() {
      return _colourtable[_palettelookup[_palette] - 1];
    }

    // Check for valid palette name, then check whether defined
    internal bool SetPalette(string name) {
      if (!IsPalette(name)) return false;
      if (_colourtable[_palettelookup[name] - 1] != null) _palette = name;
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
            (((rgb & 0xf00) * 17) << 8)
          | (((rgb & 0xf0) * 17) << 4)
          | ((rgb & 0xf) * 17);
        return rgb | 0x08000000;
      }
      var colour = value.SafeEnumParse<Colours>() ?? Colours.Black;
      return colour == Colours.Transparent ? -1 : (int)colour;
    }

  }
}
