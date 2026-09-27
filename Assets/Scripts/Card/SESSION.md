# Cross-scene card data

CardDatabaseService.Instance creates a root GameSession object on first use and
keeps it alive with DontDestroyOnLoad. Do not put CardBrowser or UI under it.
Directly entering another scene is supported: call EnsureLoaded before use.

```csharp
var service = CardDatabaseService.Instance;
service.EnsureLoaded();
CardDefinition card = service.GetCard("PD01-004");
preview.Show(card, service.ContentRoot);
```

The service owns the database and SelectedDeck. Use TryAdd/TryRemove to edit
the deck so subscribers refresh and battle locks are enforced. Do not cache the
SelectedDeck object: successful reload replaces it after validation.

CardBrowser registers for DatabaseReloaded, DeckChanged and ReloadFailed when
enabled and unregisters when disabled. Only enabled browsers with Auto Reload
Database selected register for automatic file polling. Other scenes do not
automatically reload. The existing Inspector reload button delegates to the
service. Invalid Lua or an incompatible deck preserves the previous models.

Before starting a match, call BeginBattle and retain its BattleCardSnapshot.
It copies deck counts, player IDs and immutable card definitions, and locks
manual/automatic reload and service deck editing. The battle controller must
call EndBattle when that match ends or is abandoned. This API does not start
combat or validate readiness of an entire 50-card deck; small test decks work.

The snapshot currently covers metadata only, because executable Lua effects
are not loaded by the current CardDatabase. A future effect loader must pin
the script/runtime version for the match as well.

Persistence here is across scenes within one Play session, not save-to-disk.
Scene transitions and UI refresh still require verification in Unity Play Mode.
