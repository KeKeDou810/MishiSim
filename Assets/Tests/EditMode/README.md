# Battle and external mode tests

Current tests also cover authoritative action restrictions, external Content
resolution, JSON mode validation, deck copy/faction limits, opening hands,
phase draws and first-turn skips. `NetworkTestMatchTests` uses an isolated legacy
four-card fixture; the menu uses `FromDecks` and the selected JSON mode.

For the two-player test entry and configurable fields, see
`Content/Rules/README.md`. Select test mode and load its example deck on both
players, then Host / Join. Pure C# tests do not exercise FishNet transport or
Unity rendering; verify both in Multiplayer Play Mode.

## Original combat slice notes

Open Unity Test Runner (Window > General > Test Runner), choose EditMode,
and run Mishi.Battle.Tests.

The 19 tests cover instance isolation, unit-to-unit declarations, adjacency,
exhaustion, response-pass order, ordinary power comparison, contract attackers,
optional occupation and protected nodes. Equal power favors the attacker,
as explicitly confirmed by the user. A weaker attacking unit remains on board.

Scope: synthetic board fixtures and combat with no decisions, foresight,
modifiers or other Lua effects. ResolveWithoutCardEffects is a test-slice API,
not the production resolution entry point. It does not implement those windows.

Destroyed contracts produce draw and (for level zero) player-flip requests.
These contract-defeat requests are not yet executed by the networking test.
Further attacks are blocked while a contract draw is pending.

Still to implement: decision/effect resolution, foresight, player attacks,
clocks, deck recycling and contract-defeat draw/player-flip effects.

Source: user-supplied rulebook sections 4, 8, 9, 10, 12 and Q&A.
Deck-building limits have NOT been changed based on the older-looking rulebook
wording; standard mode retains four copies and one contract. Test mode explicitly
relaxes the copy limit through external JSON.
