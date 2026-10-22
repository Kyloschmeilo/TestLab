# TestLab

Among-Us-Mod (C#, BepInEx 6 IL2CPP x64, wie CrewStats/SpecialRoles) zum **Testen anderer Mods allein**: Bots in lokalen
Lobbys, jeden Spieler steuern, Bots stimmen ab. Gehört zum Projekt „Spiele-Netzwerk“ (Bot Kyloschmeilo/AmongUsManager,
Mods Kyloschmeilo/CrewStats und Kyloschmeilo/SpecialRoles). Kommunikation mit dem Nutzer (Kilian): **Deutsch**.

## Entscheidungen (21.10.2026)
- Wunsch: „eine Art Mod, mit der ich andere Mods wie SpecialRoles testen kann, ohne viele Spieler zu organisieren“.
- **Nur lokale Spiele als Host** (`NetworkModes.LocalGame`, „Lokal → Spiel erstellen“). Online tut die Mod nichts –
  Fake-Clients auf offiziellen Servern wären Betrug bzw. würden gekickt. Nur der Host braucht die Mod.
- Vorbild: die Mod „MCI“ (Multi Client Instancing): Fake-`ClientData` + `AmongUsClient.CreatePlayer`, Steuern durch
  Umstellen von `PlayerControl.LocalPlayer` und `ClientId`/`HostId`.
- **CrewStats darf Testrunden nicht senden** (sonst XP/Statistik für Bots): `Game/CompatGuard.cs` patcht CrewStats per
  Name (`GameRecorder.OnGameStart`, `SettingsReporter.OnGameStart`, `AutoMute.Send`, `RoundUploader.SaveAndSend`) und
  überspringt sie, solange Bots da sind. **Benennt CrewStats diese Methoden um, hier mitziehen** (das Fenster warnt).
- Keine Chat-Nachrichten ins Spiel (wie bei den anderen Mods).

## Aufbau (`TestLab/`)
- `Core/Planning.cs` – spielunabhängig, auch von `Tests/` kompiliert: `VotePlanner` (Bot-Stimmen), `Roster`
  (Client-IDs ab 100, „Bot N“, Auffüllen, nächster Spieler).
- `Game/Bots.cs` – Bots anlegen/entfernen, jeden Frame `IsReady`/`InScene` setzen (sonst wirft `CoStartGameHost` sie beim
  Start raus), nach der Runde neu erzeugen (das Spiel holt nur echte Clients zurück). `allClients` nur unter
  Il2Cpp-`Monitor` ändern (Netzwerk-Thread). `CreatePlayer` ist im lokalen Spiel synchron (kein Warten auf den Server).
  **Bots brauchen vollständige Spielerdaten** (Name, Farbe, Level, Hut/Skin/Visier/Pet/Namensschild – `SetUp` setzt sie per
  `RpcSet*`): sonst gilt `NetworkedPlayerInfo.IsIncomplete` und `PlayerControl.ClientInitialize` trennt nach ~30 s den
  Host („Timeout while waiting for other player data“, im Spiel „You disconnected from the host“) – so in 0.1.0 passiert.
- `Game/Control.cs` – Spieler wechseln (Licht, Kamera, `Role.Initialize` für Knöpfe, Kill-Cooldown bleibt), Kill-Knopf-
  Kopien anderer Mods ausblenden, beim Rundenende (`OnGameEnd`/`ExitGame`-Prefix) IDs zurück. Nicht im Meeting.
- `Game/RoleSwitch.cs` – „Rolle ändern“ (gesteuerter Spieler, mitten in der Runde): Spiel-Rolle per
  `RoleManager.Instance.SetRole` (wie bei der Rollenvergabe), **nicht** `RpcSetRole` – dessen `CoSetRole` zählt
  Rollen-Statistik im Profil und startet ggf. das Intro (`HudManager.CoShowIntro`). Danach `RecomputeTaskCounts` und
  `Control.RefreshButtons`. SpecialRoles-Rolle per `ModInfo.SetSpecialRole` → `RoleState.Apply(RoleSync)` (Reflection,
  nur lokal, nicht übers Netz); Mod-Rollen setzen die Spiel-Rolle auf Crewmate/Impostor (nur darauf vergibt SpecialRoles).
- `Game/BotVotes.cs` – im Meeting `MeetingHud.CmdCastVote` für alle außer dem gesteuerten Spieler; abgelehnter Skip
  (CrewStats-Hausregel) → zufälliger Spieler.
- `Game/Actions.cs` (Töten = `RpcMurderPlayer` auf sich selbst, Holen = `RpcSnapTo`, Meeting = `ReportDeadBody(null)`,
  Ende = `RpcEndGame`), `Game/Patches.cs` (Sofortstart, kein automatisches Ende, Rundenende),
  `Game/ModInfo.cs` (SpecialRoles-Rolle per Reflection: `SpecialRoles.Game.RoleState.Get/Has/Apply/Settings`,
  `Core.RoleCatalog.Get/All`, `Core.RoleSync`, `Core.Assignment` – **benennt SpecialRoles das um, hier mitziehen**).
- `Ui/TestLabWindow.cs` – IMGUI-Fenster (MonoBehaviour per `AddComponent`). **Im Spiel ist IMGUI teilweise
  weggekürzt**: vorhanden sind u.a. `GUILayout.Label/Button/Toggle/Space/BeginHorizontal/BeginVertical/BeginScrollView/
  Window/Width/Height`, `GUI.Label/Box/contentColor/matrix`; es fehlen z.B. `TextField`, Slider, `GUI.DragWindow`,
  `GUIStyle.richText`, `GUIStyle.onNormal` (vorhanden: `new GUIStyle(GUI.skin.label)`, `wordWrap`, `normal.background`,
  `new Texture2D(1, 1)` + `SetPixel`/`Apply`, `Event.current.type`). Vor neuen GUI-Aufrufen im Cpp2IL-ISIL-Dump prüfen. Zustand in
  `Update` sammeln, Klicks im nächsten `Update` ausführen (sonst Layout-Fehler zwischen Layout- und Repaint-Durchgang). Klicks gehen sonst auch an die
  Spiel-Knöpfe darunter → `ClickThroughPatch` (`PassiveButtonManager.Update` aussetzen, solange die Maus über dem Fenster ist).
- Andere Mods per `CompatGuard.FindAssembly(name).GetType(…)` suchen, **nicht** `AccessTools.TypeByName`/`Method("Typ:Name")`
  (durchsucht alle Assemblies und schreibt seitenweise TypeLoad-Warnungen ins Log).

## Bauen & Testen
- Cloud: `nuget.bepinex.dev` ist gesperrt → Mod auf GitHub Actions bauen (`build.yml`, bei jedem Push).
  `dotnet test Tests` geht lokal (nur nuget.org). `nuget.config` liegt deshalb in `TestLab/`.
  Kompilier-Check in der Cloud geht auch ohne Feed: Interop-DLLs aus `C:\Users\petra\AppData\Roaming\.bepinex\game-libs\…`
  und `Among Us\BepInEx\core\*.dll` vom Gerät holen und direkt referenzieren (+ Analyzer aus `bepinex.autoplugin`-nupkg).
- Windows: wie CrewStats; **nur ins Spiel kopieren, wenn Kilian es sagt** → `-p:AmongUs=C:/nicht-kopieren`.
- Release: `release.yml` (workflow_dispatch, Version aus der csproj, Notizen `release-notes/vX.Y.Z.md`, 0.x = Pre-Release,
  hängt DLL + Installer an). **Nur auf Kilians Wunsch** auslösen.
- Installer `installer/TestLab-Installer.bat` = SpecialRoles-Installer mit anderem Namen (Batch/PowerShell-Hybrid,
  **CRLF, ohne BOM**, holt auch Pre-Releases). Testen mit Fake-Ordner: `TESTLAB_GAME_DIR`, `TESTLAB_BEPINEX_ZIP`,
  `TESTLAB_MOD_DLL` (geht in der Cloud mit portablem pwsh, `Read-Host` per Funktion überschreiben).

## Stand / offen
- 0.1.0 (21.10.2026, Pre-Release auf GitHub): Bots, Steuern, Abstimmen, Fenster, CrewStats-Schutz.
- Erster Test im echten Spiel (21.10.2026, Claude per Computer-Steuerung auf Kilians PC, zusammen mit CrewStats 1.3.4 und
  SpecialRoles 0.1.0): alle drei Mods laden, CrewStats-Schutz 4/4, lokale Lobby, Fenster, „+ Bot“/F5, „Auffüllen“,
  „Entfernen“ funktionieren. **Fehler in 0.1.0:** nach ~30 s „You disconnected from the host“ (unvollständige Bot-Daten,
  s.o.) – Runde ließ sich deshalb nicht starten; außerdem Klicks durchs Fenster. Behoben in 0.1.1.
- Test mit **0.1.1** (22.10.2026, im echten Spiel bestätigt): keine Trennung mehr, Runde mit 8 Spielern startet,
  Steuern/F3, Holen, SpecialRoles-Sheriff (Knopf „Schießen“, traf den Shapeshifter), Viper-Acid, Shapeshifter-Knöpfe
  (Shift/Kill/Vent/Sabotage), Engineer-Vent, Meeting über das Fenster, Bots stimmen ab (Skip), „Crew gewinnt“, Bots
  kommen nach der Runde zurück, zweite Runde startet (SpecialRoles verteilt neu, z.B. Nachtsicht + Flash).
  **Im lokalen Spiel schließt das Meeting-Ergebnis nicht von selbst** – das Spiel wartet, bis der Host auf „Proceed“
  klickt (`MeetingHud.Update`: Auto-Weiter nur online). Kein TestLab-Fehler.
- **0.1.2** (22.10.2026 im Spiel bestätigt, nicht released): Fenster breiter (540, „Töten“ war abgeschnitten), dunkler
  Hintergrund, Rollen ohne Umbruch und kürzer („Sheriff“ statt „Crewmate · Sheriff“), Knopf ◀/▶ schiebt das Fenster
  nach links/rechts (Config `[Fenster] Links`; rechts verdeckt es in der Lobby die Lobby-Infos, links im Spiel die
  Aufgabenliste). F6 „Bots holen“ und „Impostor gewinnen“ funktionieren auch.
  Hintergrund: `GUILayout.BeginVertical(GUIContent, style)` um den ganzen Inhalt, `style.normal.background` = eigene
  1×1-Textur (`HideAndDontSave`). Ein `GUI.Box` vor dem Fenster blieb unsichtbar (vermutlich, weil `GUILayout.Window`
  im Layout-Durchgang nur die übergebene Höhe zurückgibt – der Kasten wäre dann 10 px hoch).
- **0.1.3** (22.10.2026 im Spiel bestätigt, nicht released): „Rolle ändern …“ im Fenster (Kilians Wunsch: „meine Rolle
  mitten im Test changen“). Geklappt: Phantom → Engineer (Vent, Hinweis), → Impostor (Kill/Sabotage/Vent, Name rot),
  → Sheriff und Putzkraft (SpecialRoles-Text, Namensschild, Knopf), Flash an. Bekannte Schönheitsfehler – Kilian:
  „scheiß auf die Kleinigkeiten“, daher nicht behoben: (1) Impostor-Kopf „Sabotage and kill everyone / Fake Tasks“
  bleibt nach dem Wechsel zur Crew stehen (`ImpostorRole.Deinitialize` sucht den `ImportantTextTask` unter dem Namen
  „ImpostorRole“, `PlayerTask.GetOrCreateTask` nennt ihn anders) → wäre: `ImportantTextTask`s aus `myTasks` entfernen,
  danach `Role.SpawnTaskHeader(player)`; (2) Namensfarben bleiben wie beim Rundenstart → `PlayerNameColor.Set(pc)` für
  alle (macht das Spiel in `CoSetRole`), auch beim Steuern; (3) SpecialRoles-Knöpfe (Sheriff, Putzkraft) heißen „KILL“
  statt „Schießen“/„Putzen“ – vermutlich setzt der `TextTranslatorTMP` des Kill-Knopf-Klons die Beschriftung zurück
  (Fix in SpecialRoles `RoleButton.Create`: Übersetzer abschalten/zerstören vor `OverrideText`), ungeprüft, ob das auch
  in normalen Runden passiert.
- Computer-Steuerung: Among Us über Steam starten (Bibliothek → Spielen), **nicht** per `open_application` auf die
  `Among Us.exe` – das startet eine zweite Instanz („Another instance is already running“). Logs:
  `BepInEx/LogOutput.log` und `%USERPROFILE%\AppData\LocalLow\Innersloth\Among Us\Player.log` (Unity-Log mit den
  Trennungsgründen). Die neue DLL lässt sich nur bei geschlossenem Spiel ersetzen.
- **SpecialRoles 0.2.0 komplett mit TestLab getestet** (22.10.2026, 3 Runden, Ergebnis im Projektordner
  `rollen-mod/testlab-test-0.2.0.md`). Erkenntnisse fürs Testen: erst alle Rollen setzen, dann testen – „Rolle ändern“
  setzt den ganzen Rundenzustand von SpecialRoles zurück (`Round.NewGame`: Cooldowns, Schilde, Fallen, „Rolle vor dem
  Tod“). Kill-/Fähigkeits-Cooldowns laufen nur beim gesteuerten Spieler. SpecialRoles-Knöpfe sind bei Spielern mit
  Haustier (Kylo) meist unsichtbar (SpecialRoles-Fehler, Pet- statt Use-Knopf) → Bots ohne Haustier nehmen oder neben
  etwas Benutzbarem stehen. Flash gemessen ca. 20 % schneller.
  **Offene TestLab-Fehler:** (1) „Rolle ändern“ wirft NullReferenceException in `ImpostorRole.Deinitialize`, wenn der
  Spieler eine reparierte Sabotage-Aufgabe hatte (zerstörtes Objekt in `myTasks`) → vor `RoleManager.SetRole`
  null-Einträge aus `myTasks` entfernen; (2) wechselt man von Kylo zu einem Bot, springt Kylo an eine ältere Position
  zurück; (3) „Holen“ geht nur, solange man Kylo steuert.
- Noch nicht getestet: Abstimmung „Zufall“, Aufgaben der Bots beim Steuern.
- Ideen: Bots laufen lassen (Wegpunkte), Aufgaben per Knopf erledigen, Rollen vor dem Start festlegen (nur lokal!).
