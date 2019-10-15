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
  public class TestCommon {

    // compile, run the tests
    protected void DoTests(string name, string variations, string template, string[,] tests,
                 Action<Compiler, string, string, string> dotest, [CallerMemberName] string method = "") {
      foreach (var variation in variations.Split(';')) {
        var setup = template.Replace("{0}", variation);
        var compiled = DoCompile(name, name + ":" + method, setup);
        for (int i = 0; i < tests.GetLength(0); i++) {
          var moreinfo = $"[{variation}][{tests[i, 0]}] [{tests[i, 1]}]";
          dotest(compiled, tests[i, 0], tests[i, 1], moreinfo);
        }
      }
    }

    // compile the script
    protected Compiler DoCompile(string name, string title, string setup) {
      var testcase = StaticTestData.GetTestCase(name, title, setup);
      Logger.Level = 0;
      var game = new StringReader(testcase.Script);
      var compiler = Compiler.Compile(testcase.Title, game, Console.Out);
      Logger.Level = 2;
      Assert.IsTrue(compiler.Success, compiler.Message);
      compiler.Model.AcceptInputs("level 0");
      return compiler;
    }

    // accept input, check decoded level now matches expected
    protected void DoTestInputDecodeLevel(Compiler compiled, string inputs, string expected, string moreinfo) {
      var model = compiled.Model;
      model.AcceptInputs("level 0," + inputs);
      var endlevel = compiled.DecodeLevel(model.CurrentLevel);
      var result = DecodeResult(model);
      Assert.AreEqual(expected, endlevel + result, true, moreinfo);
    }

    // accept input, check object value
    protected void DoTestInputObjectText(Compiler compiled, string inputs, string expected, string moreinfo) {
      var model = compiled.Model;
      model.AcceptInputs("level 0," + inputs);
      var result = DecodeResult(model);
      var varname = expected.Before("=");
      var objid = compiled.Model.GetObjectId(varname);
      var objval = compiled.Model.GetObject(objid);
      Assert.AreEqual(expected, result ?? $"{varname}={objval.Text}", true, moreinfo);
    }

    protected void DoTestModel(Compiler compiled, string inputs, string tests, string moreinfo) {
      var model = compiled.Model;
      model.AcceptInputs("level 0," + inputs);
      var ts = tests.Split(';');
      var result = DecodeResult(model) ?? "ok";
      foreach (var test in ts) {
        var tss = test.Split('=');
        if (tss.Length == 1) Assert.AreEqual(test, result, moreinfo);
        if (tss[0] == "MSG") Assert.AreEqual(tss[1], model.CurrentMessage ?? "null", moreinfo);
        if (tss[0] == "STA") Assert.AreEqual(tss[1], model.Status ?? "null", moreinfo);
      }
      //Assert.AreEqual(ts[0], result, moreinfo);
      //Assert.AreEqual(ts[1], model.CurrentMessage ?? "null", moreinfo);
    }

    protected void DoTestLocationValue(Compiler compiled, string inputs, string tests, string moreinfo) {
      var model = compiled.Model;
      model.AcceptInputs("level 0," + inputs);
      var ts = tests.Split(';');
      var location = ts[0].SafeIntParse();
      var direction = ts[1].Trim();
      var objs = ts[2].Trim().Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
      for (int i = 0; i < objs.Length; i++) {
        var result = DecodeResult(model) ??
          ((location == null) ? "end"
          : model.GetObjects(location.Value).OrderBy(v => v).Join(""));
        Assert.AreEqual(objs[i], result, moreinfo);
        location = model.Step(location ?? 0, direction);
      }
    }

    string DecodeResult(GameModel model) {
      var result = (!model.Ok) ? "error"
        : (model.EndLevel) ? "done"
        : (model.GameOver) ? "over"
        : (model.InMessage) ? "msg"
        : (!model.InLevel) ? "unknown" : null;
      return result;
    }
  }
}