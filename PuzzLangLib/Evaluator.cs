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
    GameDef _gamedef;

    //-- creation
    internal static Evaluator Create(GameDef gamedef, TextWriter output) {
      return new Evaluator() {
        Output = output,
        _gamedef = gamedef,
      };
    }

    // Entry point from with instance
    internal bool Exec(RuleCode code, RuleState rulestate) {
      var scope = new EvaluationScope {
        RuleCode = code,
        State = rulestate,
        Script = _gamedef.Scripts.CreateEval(),
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
    internal ScriptEval Script { get; set; }
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

    CommandName GetCmd() {
      var ret = (CommandName)RuleCode.Code[_gpc++];
      if (_logging) _sw.Write("{0} ", ret);
      return ret;
    }

    HashSet<int> GetSet() {
      return new HashSet<int>(GetList());   // TODO: optimise?
      //var len = RuleCode.Code[_gpc++];
      //var set = new HashSet<int>();
      //while (len-- > 0)
      //  set.Add(RuleCode.Code[_gpc++]);
      //if (_logging) _sw.Write("{0} ", set.Join());
      //return set;
    }

    IList<int> GetList() {
      var len = RuleCode.Code[_gpc++];
      var list = (len == -99) ? Script.GetResultList()
        : (len < 0) ? State.GetRefObject(~len)
        : new List<int>();
      while (len-- > 0)
        list.Add(RuleCode.Code[_gpc++]);
      if (_logging) _sw.Write("{0} ", list.Join());
      return list;
    }

    private string GetString() {
      var len = RuleCode.Code[_gpc++];
      var rets = (len == -99) ? Script.GetResult("") : "";
      if (len > 0) {
        var array = new char[len];
        for (int i = 0; i < len; i++)
          array[i] = (char)RuleCode.Code[_gpc++];
        rets = new string(array);
      }
      if (_logging) _sw.Write("'{0}' ", rets);
      return rets;
    }

    IList<string> GetArgs() {
      var len = RuleCode.Code[_gpc++];
      var args = new List<string>();
      while (len-- > 0)
        args.Add(GetString());
      if (_logging) _sw.Write("{0} ", args.Join());
      return args;
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
        case Opcodes.FindOM:
          _break = State.OpFindOM(GetSet(), GetDir(), GetOper());
          break;
        case Opcodes.TestOMN:
          _break = State.OpTestOMN(GetSet(), GetDir(), GetOper());
          break;
        case Opcodes.TestXMN:
          _break = State.OpTestXMN(GetInt(), GetDir(), GetOper());
          break;
        case Opcodes.ScanOMDN:
          _break = State.OpScanOMDN(GetSet(), GetDir(), GetDir(), GetOper());
          break;
        case Opcodes.CheckOMN:
          _break = State.OpCheckOMN(GetSet(), GetDir(), GetOper());
          break;
        case Opcodes.CreateO:
          _break = State.OpCreateO(GetList(), GetDir());
          break;
        case Opcodes.DestroyO:
          _break = State.OpDestroyO(GetSet());
          break;
        case Opcodes.MoveOM:
          _break = State.OpMoveOM(GetSet(), GetDir());
          break;
        case Opcodes.StoreXO:
          _break = State.OpStoreXO(GetInt(), GetSet());
          break;
        case Opcodes.CommandC:
          _break = State.OpCommandCSO(GetCmd(), null, 0);
          break;
        case Opcodes.CommandCS:
          _break = State.OpCommandCSO(GetCmd(), GetString(), 0);
          break;
        case Opcodes.CommandCSO:
          _break = State.OpCommandCSO(GetCmd(), GetString(), GetSet().First());
          break;
        case Opcodes.LoadT:
          Script.OpLoadT(GetString());
          break;
        case Opcodes.CallT:
          Script.OpCallT(GetString());
          break;
        case Opcodes.FArgO:
          Script.OpFArgO(GetList());
          break;
        case Opcodes.FArgN:
          Script.OpFArgN(GetInt());
          break;
        case Opcodes.FArgT:
          Script.OpFArgT(GetString());
          break;
        case Opcodes.TestR:
          _break = !State.OpTestR(Script.GetResult());
          //_break = !Script.GetResult();
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
