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
/// Virtual machine
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DOLE;

namespace PuzzLangLib {

  /// <summary>
  /// Implements the runtime for expression evaluation.
  /// Stack based vm code.
  /// </summary>
  internal class Evaluator {
    internal TextWriter Output { get; private set; }

    //-- creation
    internal static Evaluator Create(TextWriter output) {
      return new Evaluator() {
        Output = output,
      };
    }

    // Entry point from with instance
    internal bool Exec(CompiledRule rule, RuleCode code, RuleState rulestate) {
      Logger.WriteLine(5, "Exec rule {0}: {1}", rule.RuleId, rulestate);

      var scope = new EvaluationScope {
        RuleCode = code, State = rulestate,
      };
      return scope.Run();
    }
  }

  /// <summary>
  /// An evaluation scope, including execution stack
  /// </summary>
  internal class EvaluationScope {
    internal const int LogEval = 5;

    internal RuleCode RuleCode { get; set; }
    internal RuleState State { get; set; }
    StringWriter _sw;

    // return value
    bool _break = false;
    bool _logging = false; // logging flag
    int _gpc;

    int GetInt() {
      var ret = RuleCode.Code[_gpc++];
      if (_logging) _sw.Write("{0} ", ret);
      return ret;
    }

    bool GetBool() {
      var ret = (RuleCode.Code[_gpc++] != 0);
      if (_logging) _sw.Write("{0} ", ret);
      return ret;
    }

    MatchOperator GetOper() {
      var ret = (MatchOperator)RuleCode.Code[_gpc++];
      if (_logging) _sw.Write("{0} ", ret);
      return ret;
    }

    Direction GetDir() {
      var ret = (Direction)RuleCode.Code[_gpc++];
      if (_logging) _sw.Write("{0} ", ret);
      return ret;
    }

    RuleCommand GetCmd() {
      var ret = (RuleCommand)RuleCode.Code[_gpc++];
      if (_logging) _sw.Write("{0} ", ret);
      return ret;
    }

    HashSet<int> GetSet() {   // .NET 3.5
      var len = RuleCode.Code[_gpc++];
      var set = new HashSet<int>();
      while (len-- > 0)
        set.Add(RuleCode.Code[_gpc++]);
      if (_logging) _sw.Write("{0} ", set.Join());
      return set;
    }

    private string GetText() {
      var len = RuleCode.Code[_gpc++];
      var array = new char[len];
      for (int i = 0; i < len; i++)
        array[i] = (char)RuleCode.Code[_gpc++];
      if (_logging) _sw.Write("'{0}' ", new string(array));
      return new string(array);
    }

    // Evaluation engine for gencode
    internal bool Run() {
      _logging = Logger.Level >= LogEval;
      //if (_logging) Logger.WriteLine(LogEval, "Run {0}", State);

      _sw = new StringWriter();
      for (_gpc = 0, _break = false; _gpc < RuleCode.Code.Count && !_break;) {
        var opcode = (Opcodes)RuleCode.Code[_gpc++];
        if (_logging) _sw.Write("--- {0,3}: {1} ", _gpc, opcode);
        switch (opcode) {
        case Opcodes.Start:
          _break = State.OpStart();
          break;
        case Opcodes.StepD:
          _break = State.OpStepD(GetDir());
          break;
        case Opcodes.TrailX:
          _break = State.OpTrailX(GetInt());
          break;
        case Opcodes.FindO:
          _break = State.OpFindO(GetSet(), GetOper());
          break;
        case Opcodes.FindOM:
          _break = State.OpFindOM(GetSet(), GetDir(), GetOper());
          break;
        case Opcodes.TestON:
          _break = State.OpTestON(GetSet(), GetOper());
          break;
        case Opcodes.TestOMN:
          _break = State.OpTestOMN(GetSet(), GetDir(), GetOper());
          break;
        case Opcodes.ScanODN:
          _break = State.OpScanODN(GetSet(), GetDir(), GetOper());
          break;
        case Opcodes.ScanOMDN:
          _break = State.OpScanOMDN(GetSet(), GetDir(), GetDir(), GetOper());
          break;
        case Opcodes.CreateO:
          _break = State.OpCreateO(GetSet());
          break;
        case Opcodes.DestroyO:
          _break = State.OpDestroyO(GetSet());
          break;
        case Opcodes.MoveOM:
          _break = State.OpMoveOM(GetSet(), GetDir());
          break;
        case Opcodes.CommandC:
          _break = State.OpCommandC(GetCmd());
          break;
        case Opcodes.MessageT:
          _break = State.OpMessageT(GetText());
          break;
        case Opcodes.SoundT:
          _break = State.OpSoundT(GetText());
          break;
        case Opcodes.NOP:
          break;
        case Opcodes.EOS:
          break;

        //case Opcodes.PUSH:
        //  PushStack(gencode[pc++]);
        //  break;
        //case Opcodes.PUSHS:
        //  var pulen = gencode[pc++];
        //  var pulist = new HashSet<int>();
        //  while (pulen-- > 0) pulist.Add(gencode[pc++]);
        //  PushStack(pulist);
        //  break;
        //case Opcodes.PUSHSD:
        //  var pdlen = gencode[pc++];
        //  var pdlist = new HashSet<Direction>();
        //  while (pdlen-- > 0) pdlist.Add((Direction)gencode[pc++]);
        //  PushStack(pdlist);
        //  break;
        //case Opcodes.GOTO:
        //  if (_logging) sw.Write("{0}", gencode[pc]);
        //  var gotopc = (int)gencode[pc++];
        //  pc = gotopc;
        //  break;
        //case Opcodes.GOFALSE:
        //  if (_logging) sw.Write("{0} ({1})", gencode[pc], PeekStack());
        //  var gofpc = (int)gencode[pc++];
        //  var goftest = (bool)PopStack();
        //  if (!goftest) pc = gofpc;
        //  break;
        //case Opcodes.GOTRUE:
        //  if (_logging) sw.Write("{0} ({1})", gencode[pc], PeekStack());
        //  var gotpc = (int)gencode[pc++];
        //  var gottest = (bool)PopStack();
        //  if (gottest) pc = gotpc;
        //  break;
        //case Opcodes.LDNULL:
        //  PushStack(null);
        //  break;
        case Opcodes.RMoveOM:
        default:
          throw Error.Assert("bad opcode: {0}", opcode);
        }
        if (_logging) Logger.WriteLine(LogEval, _sw.ToString());
        if (_logging) _sw.GetStringBuilder().Length = 0;
      }
      if (_logging) Logger.WriteLine(LogEval, "[Run ret={0}]", _break);
      return _break;
    }

  }
}
