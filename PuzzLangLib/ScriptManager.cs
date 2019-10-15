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
using MoonSharp.Interpreter;
using DOLE;

namespace PuzzLangLib {
  internal class ScriptState {
    internal Table varTable;
  }

  /// <summary>
  /// Implements parsing and compiling a call to a script
  /// </summary>
  internal class ScriptFunctionCall {
    ScriptManager _manager;
    string _name;
    IList<string> _arguments;
    ParseManager _parser;

    //Script scriptMain { get { return _manager.scriptMain; } }

    public override string ToString() {
      return (_arguments == null) ? $"{_name}" : $"{_name}({_arguments.Join()})";
    }

    static internal ScriptFunctionCall Create(ScriptManager manager, ParseManager parser, string ident, 
                                              IList<string> args) {
      return new ScriptFunctionCall {
        _name = ident, _arguments = args, _manager = manager, _parser = parser,
      };
    }

    // check validity of call at parse time
    internal void CheckParse() {
      if (_manager.scriptMain == null)
        _parser.CompileError($"no scripts defined");
//      if (scriptMain.Globals.RawGet(Name) == null)
//        _parser.CompileError($"unknown function: {Name}");
    }

    // generate code at compile time
    internal void Emit(Generator gen) {
      if (_arguments == null) {
        gen.Emit(Opcodes.LoadT);
        gen.Emit(_name);
      } else EmitFunc(gen);
    }

    private void EmitFunc(Generator gen) {
      for (int i = 0; i < _arguments.Count; i++) {
        var arg = _arguments[i];
        var obj = _parser.ParseSymbol(arg);
        if (obj != null) {
          gen.Emit(Opcodes.FArgO);
          gen.Emit(obj);
          continue;
        }
        var num = arg.SafeIntParse();
        if (num != null) {
          gen.Emit(Opcodes.FArgN);
          gen.Emit((int)num);
          continue;
        }
        gen.Emit(Opcodes.FArgT);
        gen.Emit(arg);
      }
      gen.Emit(Opcodes.CallT);
      gen.Emit(_name);
    }

  }

  /// <summary>
  /// Implement evaluation of script calls
  /// </summary>
  class ScriptEval {
    internal ScriptManager ScriptManager;

    Script scriptMain { get { return ScriptManager.scriptMain; } }
    DynValue _result;
    List<DynValue> _arguments = new List<DynValue>();

    // manipulate values at runtime
    internal bool GetResult() {
      return _result.CastToBool();
    }

    internal IList<int> GetResultList() {
      if (_result.Type == DataType.Number) {
        var dbl = _result.CastToNumber();
        return (dbl == null) ? new List<int>() : new List<int> { (int)dbl };
      }
      if (_result.Type == DataType.Table) {
        return _result.Table.Values.Select(v => (int)v.CastToNumber()).ToList();
      }
      return new List<int>();
    }

    internal int GetResult(int deflt = 0) {
      var dbl = _result.CastToNumber();
      return (dbl == null) ? deflt : (int)dbl;
    }

    internal string GetResult(string deflt = "") {
      return _result.CastToString();
    }

    internal void OpLoadT(string name) {
      _result = ScriptManager.scriptMain.DoString(name + "\n");
      //_result = ScriptManager.scriptMain.Globals.Get(name);
      //_result = ScriptManager.scriptMain.Globals.Get("state").Table.Get(name);
      //_result = ScriptManager.scriptVariables.Get(name);
    }

    internal void OpCallT(string name) {
      try {
        var func = scriptMain.Globals.Get(name);
        Logger.WriteLine(3, "Script call {0}({1})", name, _arguments.Join());
        _result = scriptMain.Call(func, _arguments.ToArray());
        Logger.WriteLine(3, "Script return {0}", _result);
        _arguments.Clear();
      } catch (ScriptRuntimeException ex) {
        throw Error.Fatal(ex.DecoratedMessage);
      }
    }

    internal void OpFArgO(IList<int> list) {
      // TODO: handle refobjects
      _arguments.Add(DynValue.NewTable(scriptMain, list.Select(v => DynValue.NewNumber(v)).ToArray()));
    }

    internal void OpFArgN(int value) {
      _arguments.Add(DynValue.NewNumber(value));
    }

    internal void OpFArgT(string value) {
      _arguments.Add(DynValue.NewString(value));
    }
  }

  /// <summary>
  /// Manages all scripts, hiding details of implementation
  /// </summary>
  internal class ScriptManager {
    internal GameDef gameDef;
    internal GameModel gameModel;
    internal RuleState ruleState;
    internal Script scriptMain;

    Table _vartable;

    static internal ScriptManager Create(GameDef gamedef) {
      return new ScriptManager() {
        gameDef = gamedef,
      };
    }

    internal void AddScript(ParseManager parser, IList<string> lines) {
      Script script = new Script();
      try {
        _vartable = new Table(script);
        script.Globals["state"] = _vartable;
        var prog = String.Join("\n", lines.ToArray());
        script.DoString(prog, null, "lua script");
      } catch (SyntaxErrorException ex) {
        parser.CompileError(ex.DecoratedMessage);
      }
      scriptMain = script;
    }

    internal ScriptFunctionCall CreateFunctionCall(ParseManager parser, string ident, IList<string> args) {
      return ScriptFunctionCall.Create(this, parser, ident, args);
    }

    internal ScriptEval CreateEval() {
      return new ScriptEval {
        ScriptManager = this,
      };
    }

    //--- runtime utility functions

    // Called before game starts
    internal void SetModel(GameModel model) {
      if (scriptMain == null) return;
      gameModel = model;
      //UserData.RegisterType(typeof(WrapLevel));
      //scriptMain.Globals["level"] = UserData.Create(new WrapLevel { scriptManager = this });
    }

    // Called when new game state created
    internal ScriptState GetScriptState(GameState gamestate) {
      if (scriptMain == null) return null;
      // create new table with content copied from parent
      var vartable = new Table(scriptMain);
      var parent = (gamestate.Parent == null) ? _vartable : gamestate.Parent.ScriptState.varTable;
      foreach (var kvp in parent.Pairs)
        vartable.Set(kvp.Key, kvp.Value);
      scriptMain.Globals["state"] = vartable;
      return new ScriptState { varTable = vartable };
    }

    // Called before rules are applied
    internal void SetRuleState(RuleState rulestate) {
      ruleState = rulestate;
    }

    internal bool CallFunction(string function, string[] arguments) {
      var func = scriptMain.Globals[function];
      try {
        var ret = scriptMain.Call(func, arguments);
      } catch (ScriptRuntimeException ex) {
        throw Error.Fatal(ex.DecoratedMessage);        
      }
      return false;
    }
  }

#if MAYBE
  internal class WrapLevel {
    internal ScriptManager scriptManager;
    GameModel _model { get { return scriptManager.gameModel; } }
    RuleState _rulestate { get { return scriptManager.ruleState; } }

    internal IList<int> this[int index] {
      get {
        var level = _model.CurrentLevel;
        //if (!(index >= 0 && index < level.Length)) return null;
        return level.GetObjects(index);
      }
    }
    internal void Create(int index, int objectid) {
      //var level = _model.CurrentLevel;
      //if (!(index >= 0 && index < level.Length && objectid >= 0 
      //  && objectid >= 1 && objectid <= _gamedef.ObjectCount)) return;
      _rulestate.CreateObject(objectid, index);
    }
    internal void Destroy(int index, int objectid) {
      //var level = _model.CurrentLevel;
      //if (!(index >= 0 && index < level.Length && objectid >= 0 
      //  && objectid >= 1 && objectid <= _gamedef.ObjectCount)) return;
      _rulestate.DestroyObject(objectid, index);
    }
  }
#endif
}