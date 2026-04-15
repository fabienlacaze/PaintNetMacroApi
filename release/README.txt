==================================================================
  Paint.NET Macro API
  https://github.com/fabienlacaze/PaintNetMacroApi
==================================================================

WHAT IS THIS?
-------------
A plugin for Paint.NET 5.x that lets you:
  - record sequences of UI actions and replay them pixel-perfect
  - drive Paint.NET from external scripts via a local HTTP API
    (http://127.0.0.1:8787)


HOW TO INSTALL
--------------
Right-click "Install.bat" and choose "Run as administrator".
Accept the UAC prompt. That's it.

Then start Paint.NET and open:
  Effects > Tools > Macro API


HOW TO UNINSTALL
----------------
Right-click "Uninstall.bat" and choose "Run as administrator".


REQUIREMENTS
------------
  - Paint.NET 5.x      (https://getpaint.net)
  - Windows 10 or 11 x64

The .NET 9 runtime is already bundled with Paint.NET 5.x.


HOW TO USE
----------
  1. Open Paint.NET, open or create a document
  2. Click  Effects > Tools > Macro API
  3. Click  ●  Record   - give your macro a name
  4. Do whatever you want (paint, apply effects, layers...)
  5. Click  ■  Stop      - macro is auto-saved
  6. Select it in the list and click  ▶  Play
     (or just double-click the row)

For the HTTP API and scripting examples, see:
  https://github.com/fabienlacaze/PaintNetMacroApi


WHERE ARE MY MACROS?
--------------------
  %AppData%\PaintNetMacroApi\macros\

(Open the Macro API window and click the folder icon to jump there.)


LICENSE
-------
MIT - see LICENSE file or the GitHub repository.


FEEDBACK / BUGS
---------------
Please open an issue on GitHub:
  https://github.com/fabienlacaze/PaintNetMacroApi/issues
