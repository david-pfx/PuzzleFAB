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
    FindO,      // Find set of objects
    FindOM,     // Find set of moving objects
    TestON,     // Test set of objects at location
    TestOMN,    // Test moving set of objects at location
    ScanODN,    // Scan set of objects from location
    ScanOMDN,   // Scan moving set of objects from location
    CreateO,    // Create object at index
    DestroyO,   // Destroy object at index
    MoveOM,     // Set object movement at index
    RMoveOM,    // Rigid Set object movement at index
    CommandC,   // command code
    MessageT,   // message with text
    SoundT,     // sound seed
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

    internal void Emit(int value) {
      _gcode.Add(value);
    }

    internal void Emit(bool noFlag) {
      _gcode.Add(noFlag ? 1 : 0);
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

    internal void Emit(HashSet<Direction> set) {  // .NET 3.5
      _gcode.Add(set.Cast<int>());

    }

    internal void Emit(HashSet<int> objects) {
      _gcode.Add(objects);
    }

    // decode current compiled code
    internal void Decode(string message, TextWriter tw) {
      tw.WriteLine("Decode {0} len={1}", message, _gcode.IsEmpty);
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
          break;
        case Opcodes.StepD:
          sw.Write((Direction)_gcode.Code[_gpc++]);
          break;
        case Opcodes.TrailX:
          sw.Write(_gcode.Code[_gpc++]);
          break;
        case Opcodes.CreateO:
        case Opcodes.DestroyO:
          sw.Write("objs: {0}", DecodeSet());
          break;
        case Opcodes.FindO:
        case Opcodes.TestON:
          sw.Write("objs: {0} {1}", DecodeSet(), (MatchOperator)_gcode.Code[_gpc++]);
          break;
        case Opcodes.FindOM:
        case Opcodes.MoveOM:
        case Opcodes.RMoveOM:
          sw.Write("objs: {0} {1}", DecodeSet(), (Direction)_gcode.Code[_gpc++]);
          break;
        case Opcodes.ScanODN:
        case Opcodes.TestOMN:
          sw.Write("objs: {0} step:{1} {2}", DecodeSet(), (Direction)_gcode.Code[_gpc++], (MatchOperator)_gcode.Code[_gpc++]);
          break;
        case Opcodes.ScanOMDN:
          sw.Write("objs: {0} step:{1} {2} {3}", DecodeSet(), (Direction)_gcode.Code[_gpc++], 
            (Direction)_gcode.Code[_gpc++], (MatchOperator)_gcode.Code[_gpc++]);
          break;
        case Opcodes.CommandC:
          sw.Write((RuleCommand)_gcode.Code[_gpc++]);
          break;
        case Opcodes.SoundT:
        case Opcodes.MessageT:
          sw.Write("'{0}'", DecodeText());
          break;
        default:
          throw Error.Evaluation("bad opcode: {0}", opcode);
        }
        tw.WriteLine(sw.ToString());
        sw.GetStringBuilder().Length = 0;
      }
    }

    string DecodeText() {
      var len = _gcode.Code[_gpc++];
      var list = new List<char>();
      while (len-- > 0)
        list.Add((char)_gcode.Code[_gpc++]);
      return new string(list.ToArray());
    }

    string DecodeSet() {
      var len = _gcode.Code[_gpc++];
      var list = new List<int>();
      while (len-- > 0)
        list.Add(_gcode.Code[_gpc++]);
      return String.Format(" {{{0}}}", list.Join());
    }
  }
}
