# MoonSharp

Version: 2.0.0.0 (stable release), net35 managed interpreter.

Official source: https://github.com/moonsharp-devs/moonsharp
Release: https://github.com/moonsharp-devs/moonsharp/releases/tag/v2.0.0.0
Distribution: moonsharp_release_2.0.0.0.zip, interpreter/net35/MoonSharp.Interpreter.dll

DLL SHA-256: `97F3207A579D19235FC68F742A9E5F2431C5035F309B7B3A4371B48886442A87`.

The upstream license is included in LICENSE.txt. The accompanying link.xml
preserves the interpreter for Unity managed stripping and IL2CPP builds.
Application types exposed to Lua may need their own preservation rules.

Use `MoonSharp.Interpreter` from C#. Lua scripts should be run with a restricted
module set and explicit game APIs when the card-effect system is implemented.
