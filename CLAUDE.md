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
- `Game/Control.cs` – Spieler wechseln (Licht, Kamera, `Role.Initialize` für Knöpfe, Kill-Cooldown bleibt), Kill-Knopf-
  Kopien anderer Mods ausblenden, beim Rundenende (`OnGameEnd`/`ExitGame`-Prefix) IDs zurück. Nicht im Meeting.
- `Game/BotVotes.cs` – im Meeting `MeetingHud.CmdCastVote` für alle außer dem gesteuerten Spieler; abgelehnter Skip
  (CrewStats-Hausregel) → zufälliger Spieler.
- `Game/Actions.cs` (Töten = `RpcMurderPlayer` auf sich selbst, Holen = `RpcSnapTo`, Meeting = `ReportDeadBody(null)`,
  Ende = `RpcEndGame`), `Game/Patches.cs` (Sofortstart, kein automatisches Ende, Rundenende),
  `Game/ModInfo.cs` (SpecialRoles-Rolle per Reflection: `SpecialRoles.Game.RoleState.Get/Has`, `Core.RoleCatalog.Get`).
- `Ui/TestLabWindow.cs` – IMGUI-Fenster (MonoBehaviour per `AddComponent`). **Im Spiel ist IMGUI teilweise
  weggekürzt**: vorhanden sind u.a. `GUILayout.Label/Button/Toggle/Space/BeginHorizontal/BeginVertical/BeginScrollView/
  Window/Width/Height`, `GUI.Label/Box/contentColor/matrix`; es fehlen z.B. `TextField`, Slider, `GUI.DragWindow`,
  `GUIStyle.richText`. Vor neuen GUI-Aufrufen im Cpp2IL-ISIL-Dump prüfen. Zustand in `Update` sammeln, Klicks im
  nächsten `Update` ausführen (sonst Layout-Fehler zwischen Layout- und Repaint-Durchgang).

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
- 0.1.0 (21.10.2026, Pre-Release auf GitHub, auf Kilians Wunsch auch direkt in sein `BepInEx/plugins` gelegt): Bots,
  Steuern, Abstimmen, Fenster, CrewStats-Schutz. Gegen die Spiel-DLLs kompiliert, Logik-Tests und Installer-Test grün.
  **Noch nie im Spiel gelaufen** – nächster Schritt: Kilian testet lokal (Lobby → Bots → Start →
  Steuern → Meeting → Rundenende → Bots wieder in der Lobby).
- Unsicher bis zum ersten Test: ob die Bots nach der Runde zurückkommen, ob `Role.Initialize` beim Wechsel alle Knöpfe
  richtig setzt (Engineer-Vent, Shapeshifter …), Aufgaben der Bots beim Steuern.
- Ideen: Bots laufen lassen (Wegpunkte), Aufgaben per Knopf erledigen, Rollen vor dem Start festlegen (nur lokal!).
