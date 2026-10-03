# LoopTank: Umstellung von 2D auf 3D

Dieses Dokument hält fest, wie aus dem 2D-Prototypen ein 3D-Spiel mit Third-Person-Ansicht
wird. Es ist die Arbeitsgrundlage für weitere Sitzungen: Wer hier weitermacht, liest zuerst
„Stand" und „Nächste Schritte".

Zuletzt aktualisiert: 2026-10-03

## Ziel

Das Spiel funktioniert in 3D genauso wie in 2D (Loop fahren, Ghosts der Gegner ausweichen,
Last Tank Standing, Menü → Lobby → Rennen), wird aber aus einer Kamera hinter dem eigenen
Panzer gespielt. Die 2D-Szene bleibt bis zum Ende der Umstellung spielbar und dient als Vergleich.

## Stand

| Baustein | Stand |
| --- | --- |
| Spiel-Scripts laufen mit 2D- und 3D-Physik | geschrieben, kompiliert, **noch nicht im Spiel getestet** |
| Editor-Script, das die 3D-Szene `Race3D` erzeugt | in Unity ausgeführt, `Race3D` existiert |
| Prefabs `Tank3D`, `GhostTank3D`, Materialien, 3D-Renderer | erzeugt |
| Umschalter „Ansicht: 3D / 2D" im Menü | erzeugt |
| Fahrphysik, Drift, Maus-Kamera, Turm und Kanone, Tempo-Effekte (`CarPhysics3D`, `VerfolgerKamera`, `PanzerTurm3D`, `TempoEffekt`) | geschrieben, kompiliert, **noch nicht im Spiel getestet** |
| Streckengenerator und geordnete Checkpoints (`StreckenGenerator`) | geschrieben, kompiliert, **noch nicht in Unity gelaufen** |
| Menü, Lobby, HUD, Ladebildschirm (`UiBau`, `UiUebergang`, `RennHud`, `LobbyManager`) | geschrieben, kompiliert, **noch nicht im Spiel getestet** |
| Kanister, Strecke als wenige Meshes mit wechselnder Breite, Ghost-Treffer | geschrieben, kompiliert, **noch nicht im Spiel getestet** |
| Streckenformen, Höhen, Brücken, Buckel, Abzweigungen (`StreckenPlan`), Panzer und Ghosts folgen der Fahrbahnhöhe | geschrieben, kompiliert, **noch nicht in Unity gelaufen**; Strecke in `Race3D` muss neu erzeugt werden |
| Optik | Platzhalter aus Würfeln |

Die 3D-Szene wurde am 2026-10-03 in Unity erzeugt. Die neue Fahrphysik und Kamera sind noch ungetestet;
damit beginnt die nächste Sitzung (siehe „Nächste Schritte").

## So wird die 3D-Szene erzeugt

1. Unity öffnen, Play-Modus beenden, Kompilieren abwarten.
2. Menü **LoopTank > 3D-Rennszene erzeugen**.
3. Aus `MainMenu` starten. Der Knopf **Ansicht** schaltet zwischen 3D und 2D um. Standard ist
   3D, sobald `Race3D` in den Build Settings steht. Im Multiplayer zählt die Wahl des Hosts.

Das Script baut `Race3D` und die 3D-Prefabs bei jedem Lauf komplett neu. Handarbeit in
`Race3D.unity`, `Tank3D.prefab` oder `GhostTank3D.prefab` geht dabei verloren. Solange die
Szene generiert wird, gehören Änderungen deshalb ins Script, nicht in die Szene.
Die 2D-Szene `Race.unity` wird nur gelesen.

## Architektur

### Eine Spiellogik, zwei Physik-Welten

Es gibt keine zweite Kopie der Spiellogik. Rennleitung, Lobby, Netzwerk, Ghost-Übertragung,
Timer, Runden und UI sind für 2D und 3D dieselben Scripts. Nur dort, wo Unity zwischen 2D- und
3D-Physik unterscheidet, gibt es eine Weiche:

| Aufgabe | 2D | 3D | Gemeinsam |
| --- | --- | --- | --- |
| Fahrphysik | `CarPhysics` (Rigidbody2D) | `CarPhysics3D` (Rigidbody) | Basisklasse `FahrPhysik`; `PlayerCar` kennt nur sie |
| Kamera | `CameraFollow` | `VerfolgerKamera` (erbt von `CameraFollow`) | `GameControl.m_CameraFollow`, `SetTarget()` |
| Trigger und Kollision | `OnTriggerEnter2D` usw. | `OnTriggerEnter` usw. | beide Callbacks rufen dieselbe Methode (`PlayerCar`, `Checkpoint`, `FuelPickup`) |
| Rundenaufnahme | Position (x, y), Z-Drehung | Position (x, z), Y-Drehung | `LapRecorder` erkennt den Rigidbody-Typ |
| Ghost-Wiedergabe | `GhostTank.prefab` | `GhostTank3D.prefab` | `GhostReplay`; `GhostManager` erkennt 3D am Rigidbody des Prefabs |
| Färbung pro Spieler | SpriteRenderer | MeshRenderer | `SpielerFarben.Einfaerben()` über die Tags `TankBody` / `TankTop` |
| Panzer im Netz | `Tank.prefab`, `NetworkRigidbody2D` | `Tank3D.prefab`, `NetworkRigidbody` | Owner-Autorität, `NetworkTransform` |

Welche Welt läuft, entscheidet allein die Szene: Sie legt über `GameControl.m_PlayerCarPrefab`
und `GhostManager.ghostPrefab` fest, welche Prefabs gespawnt werden.

### Koordinaten

Die 2D-Fahr-Ebene XY wird zur 3D-Ebene XZ, Y zeigt nach oben:

- Punkt: `(x, y)` → `(x, 0, y)`
- Kästen und Wände (lokale X-Achse bleibt X): Z-Drehung `w` → Y-Drehung `-w`
- Panzer: In 2D ist „vorwärts" die lokale X-Achse, in 3D die lokale Z-Achse. Z-Drehung `w` →
  Y-Drehung `90 - w`
- Lenken: In 2D ist positive Drehung gegen den Uhrzeigersinn, um die Y-Achse ist es umgekehrt.
  `CarPhysics3D` dreht das Vorzeichen deshalb um.

Aufgezeichnete Runden und das Netzwerk-Paket bleiben `Vector3(Ebene-x, Ebene-y, Winkel)`. In 3D
steht darin `(x, z, Y-Drehung)`. 2D- und 3D-Aufnahmen sind nicht untereinander austauschbar.

### Was die 3D-Szene aus der 2D-Szene übernimmt

Das Editor-Script `Renn3DErzeugen` liest `Race.unity` und baut daraus:

- **Wände:** jedes Segment der `EdgeCollider2D` wird ein Würfel mit BoxCollider.
- **Checkpoints, Ziellinie, Schutzzone am Start:** gleiche Lage und Größe, als 3D-Trigger.
- **Startplätze:** gleiche Lage, Drehung umgerechnet.
- **Panzer:** Länge und Breite aus dem Collider des 2D-Panzers; Masse, Dämpfung, `m_SpeedForce`,
  Sounds und alle `PlayerCar`-Einstellungen werden kopiert.
- **UI, Timer, GhostManager, Fuel:** werden als Kopie aus der 2D-Szene übernommen.

Ändert sich die Strecke in 2D, genügt ein neuer Lauf des Scripts.

### Fahrphysik in 3D

`CarPhysics3D` ist seit dem 2026-10-03 keine Kopie der 2D-Physik mehr, sondern eine eigene
Arcade-Steuerung für die Third-Person-Ansicht. Ziel: schnell, aber sehr gut kontrollierbar.

- **Prinzip:** Tempo und Drehrate werden direkt geführt statt über Kräfte und Dämpfung. Der
  Panzer fährt ein Zieltempo mit festen Raten an (Beschleunigen, Bremsen, Ausrollen). Die
  Dämpfung des Rigidbody setzt das Script beim Start auf 0, die Werte im Prefab wirken nicht mehr.
- **Lenkung:** im Stand wendig (`m_DrehrateStand`), bei Tempo ruhiger (`m_DrehrateSchnell`).
  `m_LenkAnsprechen` bestimmt, wie direkt sie reagiert. Rückwärts lenkt der Panzer wie ein Auto
  (`m_RueckwaertsWieAuto`).
- **Haftung:** `m_Seitenhalt` baut seitliches Rutschen ab. Hoch = wie auf Schienen, niedrig = Drift.
- **Wände:** Der Collider bekommt ein reibungsfreies Material, der Panzer gleitet an Wänden entlang.
- **Boost:** `PlayerCar` erhöht wie bisher `m_SpeedForce`; `CarPhysics3D` erkennt das und
  multipliziert Höchsttempo und Beschleunigung mit `m_BoostFaktor`. `m_SpeedForce` selbst hat in
  3D keine weitere Wirkung.
- **Eingabe:** `PlayerCar` schreibt die rohen Achsen in `FahrPhysik.m_Gas` und `m_Lenkung`. Die
  alten Felder `m_InputAccelerate` / `m_InputSteer` nutzt nur noch die 2D-Physik.
- **Optik:** Die Wanne neigt sich in Kurven und nickt beim Gasgeben und Bremsen. Dafür hängt
  `CarPhysics3D` beim Start alle Kinder unter ein Objekt `Optik`. `PlayerCar` sucht den Turm
  (`TankTop`) deshalb in der ganzen Hierarchie.
- **Kamera:** `VerfolgerKamera` folgt der Drehung direkter, weitet bei Tempo den Blickwinkel und
  rückt etwas zurück.

Alle Werte stehen mit Tooltip im Inspector von `Tank3D.prefab` (`CarPhysics3D`) und an der
`Main Camera` in `Race3D`. Achtung: `Tank3D.prefab` und `Race3D` werden vom Menüpunkt neu
gebaut. Dauerhafte Abstimmung gehört deshalb in die Standardwerte im Script.

### Turm, Kanone, Kamera und Drift

Seit dem 2026-10-03, geschrieben und kompiliert, **noch nicht im Spiel getestet**.

- **Steuerung in 3D:** W/A/S/D fahren, Maus umsehen und zielen, Leertaste halten = Drift,
  linke Shift-Taste = Boost. In 2D bleibt die Leertaste der Boost und die Pfeiltasten drehen
  den Turm. Die Weiche ist `FahrPhysik.KannDriften`.
- **Kamera (`VerfolgerKamera`):** nach dem Muster von World of Tanks und War Thunder. Die
  Blickrichtung gehört allein der Maus; die Kamera folgt der Position des Panzers, aber nicht
  seiner Drehung. Sie kreist um einen Punkt über dem Panzer (`m_DrehpunktHoehe`), damit die
  Bildmitte über den eigenen Panzer hinwegschaut. Die mittlere Maustaste schwenkt den Blick
  wieder hinter die Wanne (überholt: seit dem zweiten Update vom 2026-10-03 ist der freie Blick
  an die rechte Maustaste gebunden, siehe unten). Der Cursor ist während Countdown und Rennen gefangen, Esc gibt ihn
  frei, ein Klick fängt ihn wieder. Im Ergebnis-Bildschirm ist er frei.
  Eine erste Fassung (Kamera dreht mit der Wanne, zentriert sich selbst, Drehpunkt am Boden
  vor dem Panzer) wurde verworfen: Das Fadenkreuz lag meist auf dem eigenen Panzer und der
  Blick war nicht vorhersehbar.
- **Zwei Markierungen:** Das Fadenkreuz in der Bildmitte zeigt, wohin man schaut. Ein Ring
  zeigt, wohin die Kanone gerade zeigt, und läuft dem Fadenkreuz hinterher. Wird er grün, ist
  die Kanone ausgerichtet.
- **Zielpunkt:** `VerfolgerKamera.ZielPunkt` ist der erste Treffer eines Strahls durch die
  Bildmitte (Wand oder Panzer, sonst die Fahr-Ebene). Der eigene Panzer wird übersprungen.
- **Turm (`PanzerTurm3D`):** dreht träge auf diesen Punkt, die Kanone hebt und senkt sich
  passend. `RohrZielPunkt` ist der Punkt, auf den die Kanone tatsächlich zeigt: die
  Grundlage für späteres Schießen.
- **Gewicht:** Wanne, Turm und Kanone haben je eine eigene Feder. Die Wanne nickt direkt
  (`CarPhysics3D`), der Turm kippt und verrutscht etwas später, die Kanone wippt am weichsten
  und längsten nach. Die Beschleunigung wird aus der Bewegung der Wanne gemessen, deshalb
  funktioniert es auch bei fremden Panzern.
- **Netzwerk:** Die Turmrichtung überträgt `PlayerCar.TurmWinkel` (Besitzer schreibt, höchstens
  15-mal pro Sekunde). Kanonenhöhe und Federn sind lokal und werden nicht übertragen.
- **Drift:** Solange die Leertaste gehalten wird, sinkt der Seitenhalt und die Drehrate steigt.
  Die Nase dreht schnell in die Kurve, die Fahrtrichtung folgt verzögert. Ein- und Ausblenden
  (`m_DriftEinblenden`, `m_DriftAusblenden`) verhindern Rucke, `m_DriftMaxWinkel` begrenzt den
  Winkel zwischen Nase und Fahrtrichtung.
- **Prefab:** `PanzerTurm3D` wird vom Generator ins Prefab gesetzt. Ältere `Tank3D`-Prefabs
  bekommen die Komponente beim Start von `CarPhysics3D` automatisch mit Standardwerten.
- **Ghosts** haben keinen beweglichen Turm, er zeigt immer nach vorn.

### Tempo-Gefühl, Ruhe und Durchdringen

Seit dem 2026-10-03, geschrieben und kompiliert (der Shader-Kern wurde mit dem DirectX-Compiler
geprüft), **noch nicht im Spiel getestet**.

- **Bildeffekt (`TempoEffekt` + `Resources/TempoEffekt.shader`):** radiale Unschärfe,
  Geschwindigkeitsstreifen links und rechts, Farbsaum und Vignette. Alles wächst zum Bildrand
  und mit dem Tempo, die Bildmitte bleibt scharf. Beginnt bei 45 % des Höchsttempos
  (`m_AbTempo`), Boost und Drift legen etwas drauf. Läuft über `OnRenderImage`, also nur mit der
  Built-in-Pipeline; das Post-Processing von URP steht hier nicht zur Verfügung. Der Shader
  liegt in `Resources`, damit er im Build enthalten ist.
- **Dynamische Kamera (`VerfolgerKamera`):** weiterer Blickwinkel und mehr Abstand bei Tempo,
  zusätzlich im Boost; die Position zieht beim Anfahren kurz nach (`m_PositionTraegheit`);
  leichte Schräglage im Drift; feines Rütteln bei hohem Tempo. Grundregel: Kein Effekt darf die
  Blickrichtung ändern, sonst wandert der Zielpunkt. Schräglage dreht nur um die Blickachse,
  Rütteln verschiebt nur die Position, und gezielt wird vor beiden.
- **Ruhe beim Zielen:**
  - Die Beschleunigung für die Federn wird im Physik-Takt gemessen (`PanzerTurm3D.FixedUpdate`)
    und danach geglättet. Vorher wurde sie aus gerenderten Positionen abgeleitet und rauschte.
  - Gezielt wird von einem festen Bezugspunkt aus (`ZielUrsprung`), nicht vom wippenden Gelenk.
  - Die Entfernung für die Kanonen-Markierung ist geglättet, der Ring springt an Wandkanten nicht.
  - Die Mausbewegung ist leicht geglättet (`m_MausGlaetten`).
  - Reihenfolge pro Bild: Kamera (100) vor Turm (200), über `DefaultExecutionOrder`.
- **Kein Durchdringen:** Ein gekippter Turm hebt sich um den Betrag an, um den seine Kante
  eintauchen würde. Die Kanone senkt sich nur so weit, wie über dem Wannendeck Platz ist
  (`MaxSenkung`, abhängig von der Turmstellung); zeigt sie über die Wannenkante hinaus, darf sie
  tiefer. Die Maße liest `PanzerTurm3D` beim Start aus `TankBody`, `Turm` und `Rohr`.

### Streckengenerator und Checkpoints

Seit dem 2026-10-03, geschrieben und kompiliert, **noch nicht in Unity gelaufen**. Die
Mathematik der Mittellinie wurde außerhalb von Unity auf 500 Seeds geprüft: Mit den
Standardwerten entstand jedes Mal eine befahrbare Strecke (Rundenlänge 1500 bis 2000 Einheiten).

- **`StreckenGenerator`** (Komponente am Objekt `StreckenGenerator` in `Race3D`) baut Fahrbahn,
  Wände aus Würfeln, Tore, Ziellinie, Startaufstellung und Schutzzone. Alles liegt unter dem
  Kind `Erzeugt` und wird bei jedem Lauf ersetzt.
- **Verfahren:** Kontrollpunkte im Kreis um die Mitte mit zufälligem Abstand, Catmull-Rom-Kurve
  hindurch, auf gleich lange Stücke umgerechnet. Die Mittellinie kann sich so nie kreuzen. Ist
  eine Kurve zu eng für die Breite, wird mit weniger Unregelmäßigkeit neu gewürfelt (bis zu
  14-mal, danach Kreis als Notfall).
- **Im Editor:** Menü **LoopTank > Strecke generieren (3D)** richtet den Generator ein und
  ersetzt die aus der 2D-Szene übernommene Strecke (Objekt `Strecke`). Danach genügen die Knöpfe
  im Inspector: „Strecke erzeugen" (Seed aus dem Feld) und „Neue zufällige Strecke".
- **Zur Laufzeit:** Mit `m_ZurLaufzeitErzeugen` baut `GameControl.StreckeVorbereiten` die
  Strecke beim Rennstart. Der Host legt den Seed fest (`GameControl.StreckenSeed`), alle Clients
  bauen daraus dieselbe Strecke. Mit `m_ZufaelligerSeed` gibt es jedes Rennen eine neue.
  Wichtig: `System.Random` statt `UnityEngine.Random`, damit die Folge auf jedem Rechner gleich ist.
- **Achtung:** „3D-Rennszene erzeugen" baut `Race3D` komplett neu und bringt wieder die Strecke
  aus der 2D-Szene. Danach „Strecke generieren (3D)" erneut ausführen.

Checkpoints (gilt für 2D und 3D):

- ID 0 ist die Ziellinie, alle anderen sind Zwischen-Checkpoints.
- Sie müssen **in aufsteigender Reihenfolge** durchfahren werden (`RaceTrackControl.ZwischenIds`,
  `PlayerCar.CheckpointPassiert`). Tore außer der Reihe werden ignoriert, erst danach zählt die
  Ziellinie. Die allererste Überquerung nach dem Start zählt immer.
- Vorher galt nur „alle irgendwann berührt", und in der aus 2D übernommenen Strecke gab es
  genau einen Zwischen-Checkpoint an einer Stelle, deren Lage zur Fahrbahn nie geprüft wurde.
- Der Generator verteilt die Tore gleichmäßig über die Runde, quer zur Fahrtrichtung und etwas
  breiter als die Fahrbahn. Die Startaufstellung steht in Fahrtrichtung vor der Ziellinie.
- `RennHud` zeigt oben links den eigenen Fortschritt („Checkpoints 3 / 7").

### Streckenformen, Höhe, Brücken, Abzweigungen (drittes Update vom 2026-10-03)

Geschrieben und gegen die Unity-DLLs kompiliert, **noch nicht in Unity gelaufen**. Der Verlauf
(`StreckenPlan`) wurde außerhalb von Unity auf je 500 Seeds pro Form geprüft, bei Radius 260 und
500: nie ein Notfall-Kreis, Startgerade immer eben, Höhenabfrage auf der Mittellinie immer richtig.
Meshes, Fahrphysik und Ghosts sind ungetestet.

Die Abschnitte „Streckengenerator" oben und „Strecke" unten beschreiben den Stand davor; wo sie
abweichen, gilt dieser Abschnitt.

- **Aufteilung:** `StreckenPlan` rechnet den Verlauf aus (reine Mathematik, ohne Szenenobjekte),
  `StreckenGenerator` baut daraus die Meshes. Eine Fahrspur ist eine `StreckenBahn`: die
  Hauptrunde oder eine Abzweigung. Die Bahnen werden in der Szene gespeichert.
- **Formen (`m_Form`):** `Rundkurs` (Runde mit ein bis zwei breiten Einbuchtungen, dort dreht die
  Strecke in die Gegenrichtung), `Acht` (kreuzt sich in der Mitte, gleich viele Links- und
  Rechtskurven), `Schleife` (große Runde mit einer Schleife nach innen). `Zufall` wählt je Seed.
  Mit `m_RichtungZufaellig` wird die Runde je nach Seed links- oder rechtsherum gefahren.
  Die Schleife braucht Platz: Bei Radius 260 und Breite 60 gelingt sie fast nie, dann wird
  automatisch ein Rundkurs gebaut. Bei Radius 500 gelingt sie immer.
- **Brücken:** Wo sich die Mittellinie kreuzt, liegt eine Fahrbahn um `m_BrueckenHoehe` über der
  anderen. Die Brücke ist unten offen (Platte mit `m_BrueckenDicke`), davor und dahinter steht die
  Fahrbahn auf einem Sockel. Kreuzen sich die Bahnen zu flach oder kommt sich die Strecke
  anderswo zu nah, wird neu gewürfelt.
- **Höhe:** Fest sind die Startgerade (Höhe 0) und die Kreuzungen. Dazwischen liegen zufällige
  Hügel und Senken bis `m_Hoehenunterschied`, etwa alle `m_HuegelAbstand`, nie steiler als
  `m_MaxSteigung`. Höher liegende Fahrbahn steht auf einem Sockel: Die Außenwand reicht bis zum Boden.
- **Unebenheiten:** `m_BuckelAbschnitte` kurze Stücke mit Bodenwellen (`m_BuckelHoehe`,
  `m_BuckelLaenge`), nur auf fast ebenen Stücken. Die Wellen sind so fein wie `m_Stuecklaenge`.
- **Abzweigungen (`m_Abzweigungen`):** Die Strecke teilt sich um eine Insel in zwei Wege und führt
  wieder zusammen. Die Abzweigung ist schmaler (`m_AbzweigBreite`), kann einen eigenen Hügel
  haben und bekommt einen Kanister. Die Wände beider Wege enden genau dort, wo die andere
  Fahrbahn beginnt. Checkpoints in diesem Bereich haben einen zweiten Trigger auf der Abzweigung.
- **Fahren auf der Höhe:** Die Fahrbahn hat keinen Collider. `StreckenGenerator.Aktiv.BodenHoehe`
  liefert Höhe und Neigung an einer Stelle; an einer Brücke entscheidet die Höhe des Fragenden,
  ob oben oder unten gemeint ist. `CarPhysics3D.HoeheFolgen` hält den Panzer auf der Fahrbahn.
  Fällt sie schneller ab, als er fallen kann (Buckel bei Tempo), fliegt er kurz (`m_Schwerkraft`).
  Die sichtbare Wanne legt sich auf den Hang, der Collider bleibt waagerecht.
  Auf Strecken ohne gespeicherten Verlauf (2D-Nachbau, alte erzeugte Strecke) bleibt alles wie bisher.
- **Netzwerk:** Die Höhe wird jetzt mit übertragen (`NetworkTransform.SyncPositionY`, gesetzt in
  `CarPhysics3D.Awake` und im Prefab-Generator).
- **Ghosts:** Aufgezeichnet bleibt nur (x, z, Drehung). `GhostReplay` fragt die Höhe bei der
  Strecke ab. Sprünge über Buckel zeigt ein Ghost deshalb nicht.
- **Tore und Kanister** sind auf Strecken mit Brücke nur so hoch wie die Wände, damit ein Tor
  unter der Brücke nicht auch oben auslöst.

Offen: Die Kamera weicht der Brückenplatte nicht aus. Ein Kopf-an-Kopf-Vergleich der Wege einer
Abzweigung (welcher ist schneller) wurde nicht gemessen.

### Oberfläche, Ladebildschirm, Kanister, Ghost-Treffer (Update vom 2026-10-03)

Geschrieben und kompiliert, **noch nicht im Spiel getestet**.

**Oberfläche**

- Menü, Lobby, HUD und Ladebildschirm werden vollständig im Code aufgebaut (`UiBau`, `UiKnopf`).
  Es gibt keine Inspector-Verdrahtung mehr. Die Menü-Szene braucht nur noch Kamera,
  EventSystem, NetworkManager und ein Objekt mit `LobbyManager`.
- `LobbyManager` entfernt beim Start das alte, in der Szene gespeicherte Canvas `Menue`, falls
  es noch da ist. Die Szene muss also nicht neu erzeugt werden.
- **Menü:** Startseite und Lobby wischen gegeneinander (`UiBau.Schieben`). Die Lobby zeigt den
  Join-Code mit Kopier-Knopf und vier Plätze mit Farbe, „DU" und „HOST".
- **`UiUebergang`:** Szenenübergang und Ladebildschirm in einem, lebt über Szenenwechsel hinweg.
  `Zu(text, aktion)` wischt das Bild zu und führt dann die Aktion aus, `Auf()` gibt es frei.
  Zugedeckt wird beim Start aus dem Menü, bei jeder Lade-Nachricht des Netzwerks
  (`NetzwerkSitzung.OnSzenenEreignis`, also auch bei Clients und beim Rematch) und beim Weg
  zurück ins Menü. Aufgedeckt wird, sobald der Countdown beginnt (`GameControl.Update`) bzw.
  das Menü steht. Nach 20 Sekunden gibt die Rennszene das Bild in jedem Fall frei.
- **`RennHud`:** Zeit (pulsiert in den letzten 10 Sekunden rot), Runde, Checkpoints als
  Markenreihe, Tempo mit Balken, Treibstoff mit Balken, Countdown, kurze Meldungen
  (`Meldung`), Banner, Spielerliste und der Ergebnis-Bildschirm (`ErgebnisZeigen`).
  Gilt für die 2D- und die 3D-Szene.
- Die alte Oberfläche (`ui-base`: Timer-Text, Rundentexte, `game-ui`, `win-ui`, `lose-ui`) wird
  nicht mehr gezeichnet: `RennHud` schaltet ihre Canvas und die `ui-camera` ab. Die Objekte
  bleiben aktiv, weil `Timer`, `GhostManager` und `FuelMechanic` an ihnen hängen.

**Strecke**

- Die Strecke besteht aus wenigen Meshes statt vieler Objekte: Boden, Fahrbahn, beide Wände
  als ein Mesh mit einem MeshCollider, je ein Mesh für Ziellinie, Checkpoint-Markierungen und
  Startplätze. Vorher waren es rund 260 Würfel mit je eigenem Collider.
- Die Breite wechselt in langen Abschnitten (`m_BreitenVariation`, `m_BreitenAbschnitte`).
  Um Start und Ziel ist sie nie schmaler als der Mittelwert. Mit den Standardwerten liegt sie
  zwischen etwa 42 und 78 Einheiten (außerhalb von Unity auf 500 Seeds geprüft).
- Nach Änderungen am Generator die Strecke neu erzeugen, sonst bleibt die alte in der Szene.

**Kanister**

- `TreibstoffKanister` füllt den Tank (`FuelMechanic.AddFuel`) und erscheint nach
  `m_RespawnSekunden` wieder. Mit vollem Tank bleibt der Kanister liegen.
- Der Generator verteilt sie über die Runde (`m_KanisterAnzahl`). Lage und Seite hängen nur
  vom Seed ab. Eingesammelt wird lokal, jeder Spieler hat seine eigenen.
- Alle Kanister teilen sich ein Mesh und ein Material mit GPU-Instancing.

**Ghost-Treffer**

- Geprüft wird jetzt beim Eintreten und in jedem Physikschritt der Überlappung
  (`PlayerCar.TriggerBeruehrt`). Vorher zählte nur das Eintreten: Wer in diesem Moment
  geschützt war (Startzone oder frisch erschienener Ghost), wurde nie wieder geprüft.
- Die Schutzzone ist ein Zeitstempel statt eines Schalters, der hängen bleiben konnte.
- Der Panzer schläft nie ein (`sleepThreshold = 0`), sonst bekäme ein stehender Panzer keine
  Trigger-Meldungen.
- Kein Fehler, sondern Regel: Im Multiplayer sind die eigenen Ghosts halb durchsichtig
  und töten nicht (`GameControl.m_EigeneGhostsToedlich`).

### Profil, Startaufstellung, Kamera (zweites Update vom 2026-10-03)

Geschrieben und kompiliert, **noch nicht im Spiel getestet**. Zwei gemeldete Fehler sind
**nicht geklärt** (siehe unten).

- **Name und Farbe (`SpielerProfil`):** Jeder wählt in der Lobby Namen und Farbe. Die Wahl
  liegt in den PlayerPrefs. Der Host führt die Liste und verteilt sie über benannte Nachrichten
  (in der Lobby gibt es noch keine Netzwerk-Objekte). Jede Farbe gibt es nur einmal. Beim Spawn
  schreibt der Host Namen und Farbe in `PlayerCar.SpielerName` / `FarbIndex`.
- **Startaufstellung:** Der Host weist jedem Panzer seinen Platz über `StartPosition`,
  `StartDrehung`, `StartGesetzt` zu. Der Besitzer stellt sich selbst dorthin und hält sich bis
  zum Start fest (`PlayerCar.FixedUpdate`). Alle anderen Clients halten fremde Panzer bis zum
  Start ebenfalls auf deren Platz (`PlayerCar.LateUpdate`). Im Moment des Starts meldet jeder
  Besitzer seine Position noch einmal als Sprung.
- **Gerade Startzone:** Der Generator sucht das geradeste Stück der Runde, zieht es ganz gerade
  (`m_StartGerade`) und legt die Ziellinie an sein Ende. Die Startplätze liegen auf einer
  exakten Geraden. Außerhalb von Unity auf 500 Seeds geprüft: Biegung vor dem Ziel höchstens
  0,2 Grad, Breite am Start nie unter dem Mittelwert.
- **Kamera:** Frei umsehen und zielen nur, solange die rechte Maustaste gehalten wird
  (`VerfolgerKamera.m_FreiTaste`). Sonst bleibt die Kamera hinter der Wanne und dreht in Kurven
  mit; nach dem Loslassen schwenkt sie zurück.

**Offene Fehler (vom Nutzer gemeldet, Ursache nicht gefunden):**

1. Im Mehrspieler landete der Panzer des zweiten Spielers „irgendwo in der Mitte der Map".
2. Clients konnten Namen und Farbe nicht ändern, nur der Host.

Geprüft und ausgeschlossen: veralteter Build (der Build enthielt den aktuellen Code),
unterschiedliche Strecken (Laufzeit-Erzeugung war aus). Die Absicherungen oben sollten
Fehler 1 abfangen, belegt ist das nicht. Für die Eingrenzung gibt es zwei Anzeigen:

- **Lobby, unten rechts:** Rolle, eigene ID, Spielerzahl, Zahl der Profile, gesendete und
  empfangene Profil-Nachrichten. Bleibt „empfangen" beim Client auf 0, kommen die Nachrichten
  des Hosts nicht an.
- **Rennen, F3:** für jeden Panzer Besitzer, Platz, aktuelle Position, Startposition und ob der
  Körper kinematisch ist. Auf Host und Client vergleichen.
- Konsole: Zeilen mit `[Start]` zeigen, welchen Platz der Host vergibt und wohin sich der
  Besitzer stellt.

### Rendering

Das Projekt rendert mit der **Built-in-Pipeline**: Das URP-Paket ist zwar installiert und unter
`Assets/Settings` liegen URP-Assets, aber weder in den Graphics Settings noch in den Quality
Settings ist ein Pipeline-Asset eingetragen. Die 3D-Materialien nutzen deshalb den
`Standard`-Shader, und die 3D-Kamera braucht keine besondere Einstellung.

Falls das Projekt später auf URP umgestellt wird: `Renn3DErzeugen` erkennt das, nimmt dann
`Universal Render Pipeline/Lit` und trägt für die 3D-Kamera einen zweiten Renderer
(`Assets/Settings/Renderer3D.asset`) neben dem 2D-Renderer ein. Die Materialien werden bei
jedem Lauf auf den passenden Shader gesetzt.

Die UI zeichnet in beiden Szenen die vorhandene `ui-camera`.

## Bewusst vereinfacht im Prototyp

- **Physik:** Der Boden hat keinen Collider. Auf erzeugten Strecken folgt der Panzer der
  abgefragten Fahrbahnhöhe und kann über Buckel kurz abheben; der Collider bleibt dabei
  waagerecht. Auf der aus 2D nachgebauten Strecke ist er wie bisher auf seiner Höhe festgehalten.
- **Fahrgefühl:** eigene Arcade-Steuerung (siehe „Fahrphysik in 3D"), Werte noch nicht am Spiel abgestimmt.
- **Kamera:** kein Ausweichen vor Wänden, Abstand und Höhe sind Schätzwerte aus der Panzerlänge.
- **Optik:** Würfel, einfarbige Materialien, kein Straßenverlauf auf dem Boden.
- **Fehlt in 3D:** Reifenspuren und Drift-Sound, Fuel-Pickups (in 2D ebenfalls deaktiviert),
  Rauch-Partikel.
- **Schießen** gibt es noch nicht; Turm und Fadenkreuz sind dafür vorbereitet.

## Nächste Schritte

1. **Test Einzelspieler 3D:** Szene erzeugen, fahren, Ziellinie, zweite volle Runde → Ghost,
   in den Ghost fahren, Ergebnis-Bildschirm, Neustart.
   Dabei prüfen: Stimmt die Lenkrichtung? Fährt der Panzer vorwärts in Blickrichtung?
   Zählen die Runden? Ist die UI sichtbar?
2. **Test Multiplayer 3D** mit zwei Spielern (Multiplayer Play Mode): Positionen, Ghosts der
   Gegner, Ausscheiden, Zuschauen, Rematch.
3. **Test 2D-Regression:** Ansicht auf 2D stellen und einmal durchspielen. Die gemeinsamen
   Scripts wurden für 3D umgebaut, die 2D-Szene muss sich wie vorher verhalten.
4. **Fahrgefühl und Kamera abstimmen:** `CarPhysics3D` (Lenkung, seitliche Dämpfung),
   Rigidbody-Dämpfung, `VerfolgerKamera` (Abstand, Höhe, Trägheit).
5. **Strecke gestalten:** Straßenverlauf sichtbar machen, Wände glätten, später echte Modelle.
   Ab hier lohnt es sich, die Szene nicht mehr zu generieren, sondern von Hand zu pflegen
   (dann den Menüpunkt entfernen oder die Szene vor dem Überschreiben schützen).
6. **3D-Modelle** für Panzer und Ghost. Wichtig: Wurzel-Tag `Player` bzw. `Ghost`, ein Kind
   namens `TankTop` als Drehpunkt des Turms, eingefärbte Teile mit Tag `TankBody` / `TankTop`,
   beim Ghost ein Kind `CollisionIgnorer` mit gleichnamigem Tag.
7. **Optional:** Schießen, Reifenspuren und Pickups in 3D, Kamera-Kollision, Drift-Effekte (`CarPhysics3D.DriftAnteil`).
8. **Abschluss:** Wenn 3D die 2D-Version ersetzt, 2D-Szene, 2D-Prefabs, `CarPhysics`,
   `CameraFollow`-Logik und die 2D-Callbacks ins Archiv verschieben und den Umschalter entfernen.

## Dateien

| Datei | Zweck |
| --- | --- |
| `Scripts/Editor/Renn3DErzeugen.cs` | erzeugt `Race3D`, 3D-Prefabs, Materialien, Renderer, Menü-Knopf |
| `Scripts/Gameplay/FahrPhysik.cs` | Basisklasse der Fahrphysik |
| `Scripts/Gameplay/CarPhysics3D.cs` | Fahrphysik auf der Ebene XZ |
| `Scripts/Gameplay/VerfolgerKamera.cs` | Third-Person-Kamera |
| `Scripts/Gameplay/StreckenPlan.cs` | Verlauf der Strecke: Form, Breite, Höhe, Brücken, Abzweigungen |
| `Scripts/Gameplay/StreckenGenerator.cs` | baut aus dem Plan Meshes, Tore, Start und Kanister; Höhenabfrage |
| `Scripts/Network/NetzwerkSitzung.cs` | `RennSzene` legt fest, welche Rennszene der Host lädt |
| `Scenes/Race3D.unity` | generierte 3D-Rennszene |
| `Prefabs/Gameplay3D/` | generierte Prefabs `Tank3D`, `GhostTank3D` |
| `Art/Materials3D/` | generierte Materialien |

Alle Pfade relativ zu `Assets/LoopTank/`.

## Hinweise für die Arbeit am Projekt

- **Szenen und Prefabs per Editor-Script ändern**, nicht per Hand in den YAML-Dateien. Die
  Menüpunkte unter **LoopTank** sind wiederholbar: „Multiplayer einrichten", „Rennszene auf
  neues Setup umstellen", „3D-Rennszene erzeugen".
- **Nie „fehlende Scripts" automatisch aus Szenen entfernen.** Beim Aufsetzen des Multiplayers
  hat genau das die Rennszene zerstört, weil Unity die Scripts nur noch nicht fertig importiert
  hatte. Editor-Scripts prüfen stattdessen vor jeder Änderung, ob die Szene vollständig ist.
- **Start immer aus `MainMenu`.** Die Rennszenen brauchen eine laufende Netzwerk-Sitzung, auch
  der Einzelspieler läuft als lokaler Host.
- **Git:** gearbeitet wird auf dem Branch `multiplayer`. `multiplayer-alt` ist die Sicherung des
  ersten Multiplayer-Versuchs, `main` der alte Singleplayer-Stand.
- Die Multiplayer-Architektur (Owner-Autorität, Ghost-Übertragung, Ausscheiden) steht in den
  Kommentaren von `GameControl`, `PlayerCar` und `GhostManager`.
