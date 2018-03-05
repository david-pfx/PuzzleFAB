Puzzlang
========

Puzzlang is a pattern language and Unity player for abstract single-player games and puzzles. 
See http://www.polyomino.com/puzzlang.

Abstract games are those with no theme, no characters, no story, just a play area, rules and a way for players to win or lose. 
It usually means games of perfect information such as most board and paper-and-pencil games, but it needn't stop there.
It can include games for more players, but in Puzzlang games there is only one.
It can include games with random elements and with complex ways of moving, but Puzzlang is mostly about pushing stuff around.
It can include games with simple theming, as Sokoban where a 'Player' pushes 'crates' onto 'targets'.
It's hard to be sure where to draw the boundaries.
Perhaps there are none, but for now Puzzlang only does what is described here.

This release of Puzzlang implements a pattern language compatible with PuzzleScript.
PuzzleScript is an incredibly ingenious creation of Stephen Lavelle. 
More at http://www.polyomino.com/puzzlescript and www.puzzlescript.net.
Full credit to Stephen, but targeting only the web has both strength and limitations.
Puzzlang targets Unity, which in turn can be used to create players on almost any platform: Windows desktop, Linux, WebGL, Android, iOS and so on.

The release includes a Puzzlang games engine, a Unity player and a selection of games.
The Puzzlang engine compiles and executes games scripts. 
It is nearly feature complete with PuzzleScript, and has a few extensions.
The Unity player is quite basic, compared to what Unity can achieve, but it can play games and it too has a few enhancements.
The games are selected from the PuzzleScript demos, and show the range of what now works.

# Licensing

This is open source software and it is free, in both senses.
You are free to download it, free to use it or modify it and free to create games with it, at no charge.
The one restriction is that if you pass Puzzlang or your games on to others you have to do so in exactly the same way: 
free to use, free to modify and at no charge.
Open source is recommended, but not required as long as the game is free and distribution is on non-commercial terms.

For more details see http://www.polyomino.com/licence or the copy included with the software.

These terms apply to the Puzzlang source code and and to any game you distribute that includes Puzzlang licensed material.
The demo games, images, sound clips and font assets included in the release or used by you in creating a game may have their own licensing conditions. 
As well as your game being free, you need to comply with any of those licensing terms.

# Getting started

## The end user way

Download the binary release, unzip it somewhere, run the program. 
It should just work.
The release contains some sample games, in the Games directory.
This contains a selection of user-created games known to work with Puzzlang.

## The developer way 

1. Install Visual Studio. https://www.visualstudio.com/downloads/.
Any recent version should do, but the project is for VS 2015.

1. Install Unity. https://unity3d.com/get-unity/download/archive.
Any recent version should do, but the project was built with Version 2017.3. 

1. Download the Puzzlang project and unzip it somwhere. 
Alternatively, clone the Git repository into a directory of your choice.

1. Start Visual Studio and open the solution. 
Choose Debug or Release and then build. The build creates two DLLs that it copies into the UnityPlayer subdirectory.
Run the test suite (if you wish) by selecting Run All Tests.

1. Start Unity and open the Puzzlang/UnityPlayer subdirectory. 
The first time you do this, Unity will create a Library directory and perform some initialisation. 
The project will automatically build. Check there are no errors.

1. Open the Main Scene and run the default game in the Editor.
Select a different game and run that.
Select or skip a level.

1. Build a standalone player (if you wish), and run that.
Enjoy.

1. Add your own games. Just drop the text file into the Games directory.

1. Build a standalone player with your game and give it to someone else to enjoy.

Compatibility
=============

Puzzlang and its player can play many PuzzleScript games with 100% compatbility.
There are several known and other possible areas of incompatbility.

1. Conflicting rules. 
There are rule combinations that result in two pieces trying to move to the same location.
The order of resolution of such conflicts is unspecified, and may be different between PuzzleScript and Puzzlang.

1. Complex rules.
There are cases where the precise meaning of the rule is unspecified or uncertain.
This particularly applies to singleton patterns (only one cell) and combination 
directions (such as horizontal or parallel) in the action (right hand side), but there may be others.
The behaviour may differ between PuzzleScript and Puzzlang.

1. Sound.
The sounds in the Unity player are primitive, and are not intended to match PuzzleScript.
Feel free to find your own.

1. Randomness.
The behaviour is random, and is unlikely to match PuzzleScript.

Anything else you find is probably a bug!

The Future
----------
This release has a few minor enhancements. Other possibilities include:
* a language macro capability
* auto-save and restore in player
* mouse input events
* richer patterns, for path-following and adjacent groups.

Other ideas are welcome.

More Detail
===========

Compiler
--------
The parser uses a PEG grammar.
There is a dependency on a parser generator called Pegasus which has been customised to work with .NET 3.5.
This is built as part of the project.
There is no AST phase, the GameDef is constructed directly.
Rules are expanded (similar to PuzzleScript) and compiled into Virtual Machine code.

The implementation of the language compiler is complete, with all known valid programs compiling correctly. 
The parser is a little picky, and some games that PuzzleScript accepts may compile with warnings or minor errors, and should be corrected.

Most features are included, including rigid, startloop/endloop, random and randomdir.
A few are not: real-time games, flickscreen and zoomscreen. 
Those will be implemented as soon as I can find suitable sample games.

At the same time the language contains some minor extensions.

* **pause_at_end_level** makes the engine wait for an extra input at the end of each level
* **pause_on_again** makes the engine wait for an extra input when processing an again loop.
* Commands for **undo** and **reset** (start over, disarding any checkpoint).

Runtime
-------
At runtime there is:

* a GameDef object with static information about the game, including the Levels
* a GameModel object with the current game state
* a stack of Gamestate objects, one for each move, allowing undo
* a Rulestate object during rule evaluation, which is discarded when evaluation is complete or aborted.
* a set of Trail objects to track each pattern match.

Performance is generally good but falls short in two areas.

1. The Unity player allocates a game object for each possible sprite. 
It takes a while to allocate (say) 20x20x4 or 1600 game objects.
A future release will take a different approach and improve this a lot.

1. Pattern matching on non-moving objects requires scanning the entire level.
For some games this can be a bit slow.
This too is on the list for a future release.

## API and Testing

The Puzzlang engine is invoked through a clearly defined API, which provides a strong separation between the game logic and player logic.
There is no common source code, just the public exports from a library DLL in its own namespace.
The engine is provided with a small mainline for manual testing and has a test suite of some hundreds of test cases, 
both of which use the same API.

The Puzzlang library is built with Visual Studio, but could easily be built with Mono instead.
However the test cases are specific to Visual Studio.

## Unity Player

The Unity player is quite small and simple, and accesses the game player only through the defined API.
Many aspects of the visual design could be improved with no impact on the Puzzlang engine, or the player could 
even be rewritten to target a different environment if desired (such as a native mobile platform).

The language scripts specify the levels, sprites, text and sound triggers used in the game.
Choosing different graphic elements, fonts, sounds and music would require changes to the player.

User games should be placed in the Games directory.
Every file with a TXT extension is taken to be a games script.

The Unity scripts are C# and can be modified with either Visual Studio or Mono, at your choice.
Unity supports a wide variety of platforms, but Puzzlang has not been tested on them yet. 
Indeed the project should be portable to just about any device, desktop or server platform with a modest amount of effort.
Feel free to try, and let me know on GitHub or at polyomino/puzzlang.

