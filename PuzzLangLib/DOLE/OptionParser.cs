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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOLE {
  /// <summary>
  /// Parse some options and filenames
  /// </summary>
  public class OptionParser {
    public int Noisy { get { return Logger.Level; } }
    public int PathsCount { get { return _paths.Count; } }
    public string GetPath(int n) {
      return (n < _paths.Count) ? _paths[n] : null;
    }

    List<string> _paths = new List<string>();
    Dictionary<string, Action<string>> _options;
    string _help;

    public static OptionParser Create(Dictionary<string, Action<string>> options, string help) {
      return new OptionParser { _options = options, _help = help };
    }

    public bool Parse(string[] args) {
      for (var i = 0; i < args.Length; ++i) {
        var arg = args[i];
        if (arg == "--") {
          _paths.Add("--");
        } else if (arg.StartsWith("/") || arg.StartsWith("-")) {
          if (!Option(arg.Substring(1), arg.Substring(2, arg.Length - 2)))
            return false;
        } else _paths.Add(arg);
      }
      return true;
    }

    // Capture the options
    bool Option(string arg, string rest) {
      if (arg == "?") {
        Logger.WriteLine(_help);
        return false;
      } else if (Regex.IsMatch(arg, "^[0-9]+$")) {
        Logger.Level = int.Parse(arg);
      } else {
        var parts = arg.Split(new char[] { '=' }, 2);
        var option = parts[0].ToLower();
        var matches = _options.Keys.Where(k => k.Left(option.Length) == option);
        if (matches.Count() == 1)
          _options[matches.First()](parts.Length == 1 ? null : parts[1]);
        else {
          Logger.WriteLine("*** Bad option: {0}", arg);
          return false;
        }
      }
      return true;
    }

    // return all the file names including directory and/or wildcard expansion
    public IList<string> GetPathnames(params string[] defaults) {
      var paths = _paths.Count > 0 ? _paths : defaults.ToList();
      var filelist = new List<string>();
      foreach (var path in paths) {
        if (path == "--") filelist.Add(path);
        else if(File.Exists(path)) filelist.Add(path);
        else if (Directory.Exists(path)) {
          foreach (var file in Directory.GetFiles(path))
            filelist.Add(file);
        } else {
          var dir = Path.GetDirectoryName(path);
          if (dir == "") dir = @".\";
          var files = Directory.GetFiles(dir, Path.GetFileName(path));
          if (files.Length == 0) throw Error.Fatal($"no such file(s): '{path}'");
          filelist.AddRange(files);
        }
      }
      return filelist;
    }
  }
}
