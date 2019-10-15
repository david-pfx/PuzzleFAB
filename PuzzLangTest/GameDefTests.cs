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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DOLE;
using PuzzLangLib;
using System.Runtime.CompilerServices;

namespace PuzzLangTest {
  [TestClass]
  public class GameDefTests : TestCommon {

    [TestMethod]
    public void SettingColour() {
      var setup =
        "(pre):background_color green;" +
        "@(win):;";
      var gamedef = DoCompile("PRBG", "PRBG bare", setup).Model.GameDef;
      Assert.AreEqual(0xbe2633, gamedef.GetRGB(8));
      Assert.AreEqual(0x44891a, gamedef.GetRGB(16));
      Assert.AreEqual(0x44891a, gamedef.GetColour(OptionSetting.background_color, 0));
    }

    [TestMethod]
    public void SpriteSizeText() {
      var setup =
       "(pre):;" +
      "@(obj):Background;black;;PLAYER P;white;;R;RED;;B;BLUE;;G;green;;Y;yellow;;K;Pink;;" +
              "obj2x2;Black White;.0;1.;;" +
              "obj8a;Black White Grey DarkGrey LightGrey Gray DarkGray LightGray;012;345;670;;" +
              "obj8b;Red DarkRed LightRed Brown DarkBrown LightBrown Orange Yellow;012;345;670;;" +
              "obj8c;Green DarkGreen LightGreen Blue LightBlue DarkBlue Purple Pink;012;345;670;;" +
              "objs left 0.5;Red;0.;.0;;" +
              "objt 0.5;Red green;message hello world!;;" +
      "@(col):Background;Y;Player,R,B,G;K;obj2x2,obj8a,obj8b,obj8c,objs,objt;" +
              "";

      var gamedef = DoCompile("PRBG", "PRBG bare", setup).Model.GameDef;
      CheckObject(gamedef.GetObject(1),  "Background", 1, 1.0f, 1, "0", 0, null);
      CheckObject(gamedef.GetObject(2),  "PLAYER",     3, 1.0f, 1, "1", 1, null);
      CheckObject(gamedef.GetObject(8),  "obj2x2",     5, 1.0f, 2, "-1,0,1,-1", 1, null);
      CheckObject(gamedef.GetObject(9),  "obj8a",      5, 1.0f, 3, "0,1,2,3,4,5,6,7,0", 7, null);
      CheckObject(gamedef.GetObject(12), "objs",       5, 0.5f, 2, "8,-1,-1,8", 8, null);
      CheckObject(gamedef.GetObject(13), "objt",       5, 0.5f, 1, "8", 16, "hello world!");
    }

    void CheckObject(PuzzleObject obj, string name, int layer, float scale, int width, string sprite, int tcolour, string text) {
      Assert.AreEqual(name, obj.Name);
      Assert.AreEqual(layer, obj.Layer);
      Assert.AreEqual(scale, obj.Scale);
      Assert.AreEqual(width, obj.Width);
      Assert.AreEqual(sprite, obj.Sprite.Join());
      Assert.AreEqual(tcolour, obj.TextColour);
      Assert.AreEqual(text, obj.Text);
    }
  }
}