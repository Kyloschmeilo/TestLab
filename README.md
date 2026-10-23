# TestLab – Among-Us-Mods allein testen

Eine BepInEx-Mod für **Among Us auf Steam (64-Bit)**, mit der du andere Mods (z.B. **SpecialRoles**) testen kannst,
ohne Mitspieler zu organisieren: Sie füllt eine **lokale Lobby mit Bots**, du kannst **jeden Spieler steuern** und die
Bots **stimmen in Meetings ab**. Praktisch ein kleiner Testserver auf deinem PC.

> **Stand 1.0.0:** im echten Spiel mit SpecialRoles (alle 30 Rollen) und CrewStats geprüft.

## Installieren

1. Unter [**Releases**](../../releases) die Datei **`TestLab-Installer.bat`** herunterladen.
2. Doppelklicken. Windows warnt evtl. vor einer unbekannten Datei → **Weitere Informationen → Trotzdem ausführen**.
3. Enter drücken – der Installer findet Among Us, installiert BepInEx (64-Bit, falls noch nicht da) und die Mod.

Neue Version: Installer nochmal starten. Deinstallieren: Installer starten und **2** wählen (andere Mods bleiben).
Nur **du** brauchst TestLab. In Online-Spielen tut die Mod nichts.

## So testest du

1. Among Us starten → **Lokal** → **Spiel erstellen**.
2. **F1** öffnet das TestLab-Fenster (rechts). **„Auffüllen“** holt Bots in die Lobby (oder **„+ Bot“** / **F5**).
3. Tipp: in den Lobby-Einstellungen die Diskussionszeit auf 0 stellen. Dann wie immer **Start**.
4. In der Runde: im Fenster bei einem Spieler **„Steuern“** (oder **F2** = nächster Spieler). Du bist jetzt dieser
   Spieler – mit seiner Rolle, seinen Knöpfen (Kill, Vent, Mod-Fähigkeiten), seinen Aufgaben und seiner Sicht.
   **F3** bringt dich zu deinem eigenen Spieler zurück.
5. Andere Rolle testen: **„Rolle ändern …“** im Fenster – jede Spiel-Rolle (Crew und Impostor) und jede fertige
   SpecialRoles-Rolle, dazu Flash an/aus. Gilt für den Spieler, den du gerade steuerst, sofort mitten in der Runde.
6. Meeting: alle, die du gerade nicht steuerst, stimmen automatisch ab.

Nach der Runde sind die Bots wieder in der Lobby.

## Fenster und Tasten

| Taste | |
|-------|--|
| F1 | Fenster ein/aus |
| F2 | nächsten Spieler steuern |
| F3 | zurück zu deinem Spieler |
| F5 | Bot hinzufügen (Lobby) |
| F6 | alle Bots zu dir holen (Runde) |

**Lobby:** „+ Bot“, „Auffüllen bis N“, „Alle entfernen“, Bots je Zeile entfernen, „Bots nach der Runde behalten“,
„Sofort starten“ (kein Countdown).

**Runde:** alle Spieler mit Rolle (auch der SpecialRoles-Rolle) und je **Steuern / Holen / Töten**; dazu **Meeting**,
**Crew gewinnt / Impostor gewinnen**, „Kein automatisches Spielende“, **Rolle ändern** und wie Bots abstimmen
(**Skip / Zufall / Keine**). ◀/▶ schiebt das Fenster auf die andere Seite.

Rolle ändern: wird der letzte Impostor zur Crew (oder gibt es so viele Impostor wie Crew), beendet das Spiel die Runde –
vorher „Kein automatisches Spielende“ anhaken.

## Mit anderen Mods

- **SpecialRoles:** einfach mit installieren. Steuerst du einen Bot, siehst du seine Mod-Rolle und benutzt seine
  Knöpfe (Sheriff `Q`, Putzkraft `F` …). Das Fenster zeigt die Mod-Rolle aller Spieler.
- **CrewStats:** zeichnet Runden mit Bots **nicht** auf und schickt nichts an Discord (keine XP, keine Statistik,
  kein Auto-Mute). Abschalten: `[CrewStats] TestrundenNichtSenden = false`.

## Was TestLab nicht kann

- Bots laufen nicht von selbst und machen keine Aufgaben – das macht nur, wen du gerade steuerst.
- Alles läuft auf deinem PC. **Netzwerk-Fehler** (zwei echte Spiele, die sich nicht einig sind) und das Verhalten
  offizieller Server findest du damit nicht – dafür braucht es weiter einen Testabend mit echten Spielern.

## Einstellungen

`BepInEx/config/de.spielenetzwerk.testlab.cfg` (nur bei geschlossenem Spiel ändern; das meiste geht auch im Fenster):
`[Bots] AuffuellenBis`, `Behalten`, `Abstimmung` · `[Lobby] SofortStarten` · `[Spiel] KeinAutomatischesEnde` ·
`[CrewStats] TestrundenNichtSenden` · `[Fenster] Anzeigen`, `Links`.

## Bauen

```
dotnet build TestLab -c Release                                  # kopiert nach BepInEx/plugins
dotnet build TestLab -c Release -p:AmongUs=C:/nicht-kopieren     # nur bauen
dotnet test Tests                                                # Tests der Logik
```

Jeder Push baut die DLL auch auf GitHub (Actions → Build → Artifacts).
