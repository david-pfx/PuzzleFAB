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

///
/// Code generator
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DOLE;

namespace PuzzLangLib {
  internal enum Opcodes {
    NOP,
    EOS,        // end of statement, pop value as return
    PUSH,       // Push value
    PUSHS,      // Push list of values
    PUSHSD,     //
    Start,      // signals beginning of match process
    StepD,      // signals move to next pattern cell
    TrailX,     // set trail index for action
    FindOM,     // Find set of objects
    TestOMN,    // Test set of objects at location
    TestXMN,    // Test ditto but using path var
    ScanOMDN,   // Scan moving set of objects from location
    CheckOMN,   // Check objects at path index
    CreateO,    // Create object at index
    DestroyO,   // Destroy object at index
    MoveOM,     // Set object movement at index
    RMoveOM,    // Rigid Set object movement at index (not used)
    StoreXO,    // Store object ref into variable
    CommandC,   // command code
    CommandCS,  // command code, string arg
    CommandCSO, // command code, string arg, object list
    // opcodes for communicating with scripts
    LoadT,      // Load value from named script variable
    CallT,      // Call named script function with arguments
    FArgO,      // Push object list to arg list
    FArgN,      // Push number to arg list
    FArgT,      // Push string to arg list
    TestR,      // Break if result false
  };

  class RuleCode {
    internal static RuleCode Empty = new RuleCode();
    internal IList<int> Code { get; set; } = new List<int>();
    internal bool IsEmpty { get { return Code.Count == 0; } }

    internal void Add(int x) { Code.Add(x); }
    internal void Add(IEnumerable<int> ints) {
      Code.Add(ints.Count());
      foreach (var x in ints) Code.Add(x);
    }

    public override string ToString() {
      return "#{0}".Fmt(Code.Count);
    }
  }

  /// <summary>
  /// Code generator
  /// </summary>
  internal class Generator {
    internal RuleCode Code { get { return _gcode; } }

    RuleCode _gcode = new RuleCode();
    int _gpc = 0;

    internal static Generator Create() {
      Logger.WriteLine(3, "Generator create");
      return new Generator { };
    }

    internal void Emit(Opcodes opcode) {
      _gcode.Add((int)opcode);
    }

    internal void Emit(bool value) {
      _gcode.Add(value ? 1 : 0);
    }

    internal void Emit(int value) {
      _gcode.Add(value);
    }

    internal void Emit(Direction direction) {
      _gcode.Add((int)direction);
    }

    internal void Emit(MatchOperator oper) {
      _gcode.Add((int)oper);
    }

    internal void Emit(string text) {
      var asint = text.Select(c => (int)c);
      _gcode.Add(asint);
    }

    internal void Emit(IEnumerable<Direction> set) {
      _gcode.Add(set.Cast<int>());

    }

    internal void Emit(IEnumerable<int> intlist) {
      _gcode.Add(intlist);
    }

    internal void Emit(ObjectSymbol symbol) {
      if (symbol.Kind == SymbolKind.RefObject)
        Emit(~symbol.ObjectIds.First());
      else if (symbol.Kind == SymbolKind.Script)
        Emit(-99);
      else Emit(symbol.ObjectIds);
    }

    // decode current compiled code
    internal void Decode(string message, TextWriter tw) {
      tw.WriteLine("Decode {0} len={1}", message, _gcode.Code.Count);
      StringWriter sw = new StringWriter();
      for (_gpc = 0; _gpc < _gcode.Code.Count; ) {
        var opcode = (Opcodes)_gcode.Code[_gpc++];
        sw.Write("| {0,4} {1,-10}: ", _gpc - 1, opcode);
        switch (opcode) {
        case Opcodes.NOP:
        case Opcodes.EOS:
        case Opcodes.PUSH:
        case Opcodes.PUSHS:
        case Opcodes.PUSHSD:
        case Opcodes.Start:
        case Opcodes.TestR:
          break;
        case Opcodes.StepD:
          sw.Write((Direction)DecodeInt());
          break;
        case Opcodes.TrailX:
          sw.Write(DecodeInt());
          break;
        case Opcodes.DestroyO:
          sw.Write("objs:{0}", DecodeList());
          break;
        case Opcodes.StoreXO:
          sw.Write("var:v{0} objs:{1}", DecodeInt(), DecodeList());
          break;
        case Opcodes.MoveOM:
        case Opcodes.RMoveOM:
        case Opcodes.CreateO:
          sw.Write("objs:{0} move:{1}", DecodeList(), (Direction)DecodeInt());
          break;
        case Opcodes.FindOM:
        case Opcodes.TestOMN:
        case Opcodes.CheckOMN:
          sw.Write("objs:{0} move:{1} match:{2}", DecodeList(), (Direction)DecodeInt(), 
            (MatchOperator)DecodeInt());
          break;
        case Opcodes.TestXMN:
          sw.Write("off:{0} move:{1} match:{2}", DecodeInt(), (Direction)DecodeInt(),
            (MatchOperator)DecodeInt());
          break;
        case Opcodes.ScanOMDN:
          sw.Write("objs:{0} step:{1} move:{2} match:{3}", DecodeList(), (Direction)DecodeInt(), 
            (Direction)DecodeInt(), (MatchOperator)DecodeInt());
          break;
        case Opcodes.CommandC:
          sw.Write("call:{0}", (CommandName)DecodeInt());
          break;
        case Opcodes.CommandCS:
          sw.Write("call:{0} arg:{1}", (CommandName)DecodeInt(), DecodeText());
          break;
        case Opcodes.CommandCSO:
          sw.Write("call:{0} arg:{1} objs:{2}", (CommandName)DecodeInt(), DecodeText(), DecodeList());
          break;
        case Opcodes.LoadT:
          sw.Write("call:{0}", DecodeText());
          break;
        case Opcodes.CallT:
          sw.Write("call:{0}", DecodeText());
          break;
        case Opcodes.FArgO:
          sw.Write("arg:{0}", DecodeList());
          break;
        case Opcodes.FArgN:
          sw.Write("arg:{0}", DecodeInt());
          break;
        case Opcodes.FArgT:
          sw.Write("arg:{0}", DecodeText());
          break;
        default:
          throw Error.Evaluation("bad opcode: {0}", opcode);
        }
        tw.WriteLine(sw.ToString());
        sw.GetStringBuilder().Length = 0;
      }
    }

    int DecodeInt() {
      return _gcode.Code[_gpc++];
    }

    string DecodeText() {
      var len = _gcode.Code[_gpc++];
      var list = new List<char>();
      while (len-- > 0)
        list.Add((char)_gcode.Code[_gpc++]);
      return new string(list.ToArray());
    }

    string DecodeList() {
      var len = _gcode.Code[_gpc++];
      if (len == -99) return String.Format("@frtn");
      if (len < 0) return String.Format("@v{0}", ~len);
      var list = new List<int>();
      while (len-- > 0)
        list.Add(_gcode.Code[_gpc++]);
      return String.Format("{{{0}}}", list.Join());
    }

    string DecodeTextList() {
      var len = _gcode.Code[_gpc++];
      var list = new List<string>();
      while (len-- > 0)
        list.Add(DecodeText());
      return String.Format("{{{0}}}", list.Join());
    }
  }
}
