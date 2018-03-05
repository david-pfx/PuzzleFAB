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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;

namespace DOLE {
  /// <summary>
  /// Operates a glass teletype on a supplied file or console
  /// 
  /// First level added is the driver and controls what is logger. 
  /// The rest are passive watchers: may see less, not more.
  /// Now uses only mod 100, but you can Push and Pop!
  /// 
  /// Lines starting with ">" are timed
  /// Lines starting with "|" are not padded
  /// </summary>
  public static class Logger {
    static List<TextWriter> _tws = new List<TextWriter>();
    static List<int> _levels = new List<int>();
    static List<int> _sublevels = new List<int>() { 0 };
    public static TextWriter Out {
      get { CheckInit(); return _tws[0]; }
      set { CheckInit(); _tws[0] = value; }
    }
    public static int Level { 
      get { CheckInit(); return _sublevels.First(); } 
      set {
        CheckInit();
        _levels[0] = 99;
        _sublevels.Clear();
        do {
          _sublevels.Add(value % 100);
          value /= 100;
        } while (value != 0);
      } 
    }

    static bool _neednl;
    static DateTime _firsttime = DateTime.Now;
    static DateTime _lasttime = DateTime.Now;
    static DateTime _timenow = DateTime.Now;

    public static void Open(int level) {
      Open(level, Console.Out);
    }
    public static void Open(int level, string name) {
      Open(level, new StreamWriter(name));
    }
    public static void Open(int level, TextWriter tw) {
      _levels.Add(level);
      _tws.Add(tw);
    }
    // special for writing to debugger trace output
    public static void OpenTrace(int level) {
      _levels.Add(level);
      _tws.Add(new TraceWriter());
    }
    public static void Close() {
      foreach (var tw in _tws)
        tw.Close();
      _tws.Clear();
      _levels.Clear();
    }

    public static void Push(int level) {
      _sublevels.Insert(0, level);
    }

    public static int Pop() {
      if (_sublevels.Count == 1) return 0;
      var ret = _sublevels[0];
      _sublevels.RemoveAt(0);
      return ret;
    }

    public static void Flush() {
      for (int i = 0; i < _levels.Count; ++i) {
        _tws[i].Flush();
      }
    }

    public static void Write(int level, string msg) {
      Write(level, msg, false);
    }
    public static void WriteLine(int level, string msg) {
      Write(level, msg, true);
    }
    public static void WriteLine(int level) {
      Write(level, "", true);
    }
    public static void Write(int level, string format, params object[] args) {
      Write(level, String.Format(format, args), false);
    }
    public static void WriteLine(int level, string format, params object[] args) {
      Write(level, String.Format(format, args), true);
    }
    public static void WriteLine(string format, params object[] args) {
      Write(0, String.Format(format, args), true);
    }
    public static void Assert(bool test, string format, params object[] args) {
      if (!test) {
        var msg = String.Format(format, args);
        Flush();
        throw new AssertException(msg);
      }
    }
    public static void Assert(bool test, object thing = null) {
      if (!test) {
        var msg = $"<<{thing}>>";
        Flush();
        throw new AssertException(msg);
      }
    }
    public class AssertException : Exception {
      public AssertException(string message) : base(message) { }
    }

    //--- impl

    static void CheckInit() {
      if (_levels.Count == 0)
        Open(0);
    }

    // common writer
    public static void Write(int level, string msg, bool newline) {
      if (level > Level) return;    // base level controls what is logged
      _timenow = DateTime.Now;
      for (int i = 0; i < _levels.Count; ++i) {
        if (level <= _levels[i]) {
          if (newline) {
            if (_neednl) _tws[i].WriteLine();
            _tws[i].WriteLine(Pad(level, msg));
          } else
            _tws[i].Write((_neednl) ? msg + ";" : Pad(level, msg) + ";");
        }
      }
      _lasttime = _timenow;
      _neednl = !newline;
    }

    static string Pad(int level, string msg) {
      bool timeit = msg.StartsWith(">");
      bool padit = msg.Length > 0 && Char.IsUpper(msg[0]);

      string padding = (timeit) ? String.Format(@"{0:mm\:ss\.fff} {1,3:N0}ms : ",
          _timenow - _firsttime, (_timenow - _lasttime).TotalMilliseconds)
        : (padit) ? " ".Repeat(2 * level) : "";
      return padding + msg;
    }
  }

  /// <summary>
  /// Implement writer using debug output
  /// </summary>
  class TraceWriter : TextWriter {
    StringBuilder _sb = new StringBuilder();
    public override Encoding Encoding {
      get { return Encoding.Default; }
    }
    public override void Write(char c) {
      if (c == '\n') {
        Trace.WriteLine("|" + _sb.ToString());
        _sb.Length = 0;  // .NET 3.5
      } else if (c!='\r') _sb.Append(c);
    }
  }
}
