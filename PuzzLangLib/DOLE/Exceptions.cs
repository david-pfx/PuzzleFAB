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
/// Error handling and exceptions
/// 
using System;

namespace DOLE {
  public enum ErrorKind {
    Warn,     // never fatal
    Error,    // fatal if not handled
    Fatal,    // call handler but always fatal
    Assert,    // assertion error
    Panic     // immediately fatal
  };

  [Serializable]
  public class DOLEException : Exception {
    public ErrorKind Kind = ErrorKind.Error;
    public DOLEException(string msg) : base(msg) { }
    public DOLEException(ErrorKind kind, string msg, params object[] args) : base(String.Format(msg, args)) {
      Kind = kind;
    }
  }

  /// <summary>
  /// Shortcut class to throw commonly used exceptions
  /// Outer wrapper provides more info. Messages here should not say 'error'
  /// </summary>
  public class Error {
    public static Exception NullArg(string arg) {
      return new ArgumentNullException(arg);
    }
    public static Exception OutOfRange(string arg) {
      return new ArgumentOutOfRangeException(arg);
    }
    public static Exception Invalid(string msg) {
      return new InvalidOperationException(msg);
    }
    public static Exception MustOverride(string arg) {
      return new NotImplementedException("must override " + arg);
    }
    public static Exception Argument(string msg) {
      return new ArgumentException(msg);
    }
    public static Exception Fatal(string msg) {
      return new DOLEException(ErrorKind.Fatal, "fatal: " + msg);
    }
    public static Exception Fatal(string msg, params object[] args) {
      return new DOLEException(ErrorKind.Fatal, "fatal: " + msg, args);
    }
    //public static Exception Fatal(string origin, string msg, params object[] args) {
    //  var fmsg = string.Format(msg, args);
    //  return new DOLEException(ErrorKind.Fatal, "Fatal error ({0}): {1}", origin, msg);
    //}
    public static Exception NotImpl(string msg, params object[] args) {
      return new DOLEException(ErrorKind.Fatal, "not implemented: " + msg, args);
    }
    public static Exception Assert(string msg, params object[] args) {
      return new DOLEException(ErrorKind.Assert, "assertion failed: " + msg, args);
    }
    public static Exception Evaluation(object msg, params object[] args) {
      return new DOLEException(ErrorKind.Assert, "evaluation failed: " + msg, args);
    }
    public static Exception Syntax(object msg, params object[] args) {
      return new DOLEException(ErrorKind.Assert, "syntax: " + msg, args);
    }
  }
}
