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
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public static class Util {
  public static int MaxLoggingLevel = 2;
  static float realtime = Time.realtimeSinceStartup;

  public static void Trace(int level, string format, params object[] args) {
    if (level <= MaxLoggingLevel) {
      if (format.StartsWith(">")) {
        var t = Time.realtimeSinceStartup;
        var time = String.Format("<{0:f1}", 1000 * (t - realtime));
        Debug.Log(string.Format(time + format, args));
        realtime = t;
      } else
        Debug.Log(string.Format(format, args));
    }
  }

  public static void IsTrue(bool test, object msg = null) {
    if (test) return;
    Trace(0, "Assertion IsTrue failed: {0}", msg);
  }

  public static void NotNull(object obj, object msg = null) {
    if (obj != null) return;
    Trace(0, "Assertion NotNull failed: {0}", msg);
  }

  // augmented Path.Combine, ignore null components, convert '\' to '/'
  public static string Combine(string arg, params string[] args) {
    var ret = arg;
    for (int i = 0; i < args.Length; i++)
      if (args[i] != null) ret = Path.Combine(ret, args[i]);
    return ret.Replace('\\', '/');
  }

}
