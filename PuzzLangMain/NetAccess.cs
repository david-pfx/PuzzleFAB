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
using System.Net;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SimpleJSON;
using DOLE;

namespace PuzzLangMain {
  static class NetAccess {
    // get query parameters from a URL as dictionary
    static internal Dictionary<string,string> ExtractUrlParameters(string url) {
      var args = url.After("?").Split('&');
      var keys = args.Select(a => a.Split('='))
        .Where(s => s.Length == 2)
        .ToDictionary(k => Uri.UnescapeDataString(k[0]), v => Uri.UnescapeDataString(v[1]));
      return keys;
    }

#if USE_HTTP_UTILITY
    // tested, working, do not use
    static internal string ExtractUrlParameter(string url, string key) {
      try {
        var uri = new Uri(url);
        return HttpUtility.ParseQueryString(uri.Query)[key];
      } catch (FormatException) {
        return null;
      }
    }
#endif

    // get a script from a URL
    static internal string LoadUrlScript(string url) {
      var request = WebRequest.Create(url) as HttpWebRequest;
      request.Method = "GET";
      request.ContentType = "text/plain";
      request.UserAgent = "PS";
      request.ServicePoint.Expect100Continue = false;
      request.KeepAlive = false;
      var response = request.GetResponse();
      using (var st = new StreamReader(response.GetResponseStream()))
        return st.ReadToEnd();
    }

    // extract a gist ID from a URL (or something)
    static internal string ExtractGist(string gist) {
      var regex = new Regex("[0-9a-z]{32}");
      var match = regex.Match(gist);
      return match.Success ? match.Value : null;
    }

    // Load a PS game as a piece of JSON given a gist id
    static internal string LoadGist(string id) {
      var json = LoadJson(id);
      if (json == null) return null;
      var pjson = JSON.Parse(json);
      Logger.WriteLine(0, "test {0}", pjson[""]);
      return pjson["files"]["script.txt"]["content"];
    }

    // Load a piece of JSON given a gist id
    static internal string LoadJson(string id) {
      var url = @"https://api.github.com/gists/" + id;
      var request = WebRequest.Create(url) as HttpWebRequest;
      request.Method = "GET";
      request.ContentType = "application/x-www-form-urlencoded";
      request.UserAgent = "PS";
      request.ServicePoint.Expect100Continue = false;
      request.KeepAlive = false;
      var response = request.GetResponse();
      using (var st = new StreamReader(response.GetResponseStream()))
        return st.ReadToEnd();
    }
  }
}
