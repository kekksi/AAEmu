# AAEmu → Verteilte Architektur (Weg B) — Projektstand

**Stand:** 2026-07-30 · Branch `distributed` · Dev-CT **257 `aaemu-dist`** (pveh1, `10.11.10.57`)
**Ziel:** AAEmu-Monolith zu verteiltem Server umbauen — Zonen als eigene Prozesse, Gateway davor, verteilbar auf mehrere Hosts/CTs. Ohne Neuschreiben des Spiels.

---

## TL;DR — wo wir stehen

Die **serverseitige verteilte Architektur ist funktional komplett und ohne Client bewiesen.** Ein echter Character wird in einem **separaten Zone-Prozess** geladen, gespawnt, tickt, und die volle Char-Enter-Init-Sequenz (inkl. `SCUnitState 0x69`) wird durch den selbstgebauten Cluster-Tunnel zurück Richtung Client gesendet. Der Monolith läuft die ganze Zeit unbeschädigt daneben (ohne verbundene Zone = exakt altes Verhalten).

**Einzig verbleibend:** `B2.6d` = Byte-Korrektheit gegen echten 1.2-Client (braucht DNAT-Swap + Shadow, mit Gimmo am Schirm).

---

## Architektur (Ist)

```
  ┌──────────────┐   Cluster-RPC :1300     ┌────────────────────┐
  │   Gateway    │◄───────────────────────►│    Zone-Prozess    │
  │ (AAEmu.Game) │   Client-Paket-Tunnel   │   (AAEmu.Zone)     │
  │ Login/Auth/  │◄───────────────────────►│  lebende Welt,     │
  │ Char-Select  │   (GZ/ZGClientPacket)   │  tickt, AI/Physics │
  └──────────────┘                         └────────────────────┘
        │ :1237 (Login-Prozess AAEmu.Login)          │
        │ :1239 (Client-Game)                  ZoneTester (Test-Char id=9001)
        │ :1250 (Stream)                       lebt hier: objId, inRegion=True
        └── echter 1.2-Client (via DNAT)       Init-Sequenz tunnelt zurück
```

- **Gateway** = der bisherige `AAEmu.Game`-Monolith. Behält Client-Sockets (1239), Login/Auth, Char-List/Char-Select, Routing. TCP-Server für Cluster auf **1300**.
- **Zone** = neuer `AAEmu.Zone`-Prozess (TCP-Client). Baut eigenen DI-Container (Teilmenge der Manager), lädt die Welt aus dem game_pak, tickt, hält Chars.
- **Cluster-Protokoll** = `AAEmu.Commons/Network/Cluster/*`, abgeleitet aus Logins `Internal`-RPC-Stack. Wire `[u16 len][u16 typeId][body]`.
- **Naht** = `ISession`: ein `TunnelSession : ISession` (Socket=null, SendPacket→Cluster) lässt die Zone den **echten** `GameConnection`+`GameProtocolHandler`-Stack wiederverwenden. Kein Nachbau der Paket-Logik.

---

## Fortschritt (alle Commits auf `distributed`)

| Schritt | Was | Commit |
|---|---|---|
| B0 | Architektur-Audit (Naht = `WorldInstance`, Kopplung mittel) | — |
| B1 | Dev-Fork gebaut + läuft (Login+Game, voller Content) | `0fc47765` (Telemetry-Dispose-Fix) |
| B2.1 | Zone↔Gateway RPC register + heartbeat | `f5763711` |
| B2.2 | Zone eigener DI-Subset + WorldInstance erstellt | `2a51cd81` |
| B2.3 | Zone spawnt + tickt (lebender Sim-Shard) | `5c738db1` |
| B2.4a | Client-Paket-Tunnel-Rohre + synth. Round-Trip | `e6cf72e8` |
| B2.4b | ClientRoutingTable + Inbound-Weiche | `ec62d6ad` |
| B2.4c | `GameConnection.SendRaw` + Outbound-Write | `32d72ab4` |
| B2.4d | `TunnelSession:ISession` + echte GameConnection in Zone | `6531f8fc` |
| B2.5 | echter C2G-Dispatch in Zone (Ping→Pong bewiesen) | `571f2f0b`, `48f23f7e` |
| B2.6a | EnterWorld-getriggerte Ownership (bei Char-Select) | `6d510adc` |
| B2.6b | **Char-Load + Spawn in Zone** (objId-Autorität Zone) | `155eb8a5` |
| B2.6c | **Zone emittiert Char-Enter-Init-Sequenz durch Tunnel** | `da29b044` |

**Beweis B2.6c (self-verified, ohne Client):** Init-Opcodes tunneln aus der Zone zum Gateway — `SCCharacterState 0x40 (535B)`, `GamePoints 0x188`, `Inventory 0x4b/0x4c`, `ActionSlots 0x12d`, `Factions 0x08/0x09` (bis 5210B), `SCUnitState 0x69` (297/491/464/**529**/214/486B, self + Region-Units).

---

## Wichtige Design-Entscheidungen / Befunde

- **Kein Game-Gameplay-Code verbogen.** Blast-Radius fast komplett in `AAEmu.Zone` + Cluster-Layer. Einzige Game-Logik-Änderungen: 1 Zeile `ClearOwner` in `GameProtocolHandler.OnDisconnect`, 1 Zeile Early-Skip in `CSSelectCharacterPacket`.
- **Ownership-Trigger = Ende `CSSelectCharacterPacket`** (nicht X2EnterWorld — das setzt nur Lobby). Erst bei Char-Select ist die Ziel-Zone deterministisch. SetOwner nur wenn Zone registriert → **ohne Zone = null Monolith-Änderung**.
- **ObjId-Kollision gelöst:** Zone ist alleinige ObjId-Autorität für ihre Instanz; Gateway vergibt für zonen-eigene Chars keine.
- **Doppel-Load/Save gelöst:** Gateway überspringt lokalen Char-Load für zonen-eigene Chars; Zone ruft nie `SaveManager.Initialize` (kein periodic save).
- **Kopplung mittel:** World-Pfad + Char-Load ziehen nur read-only/Instanz-Manager (alle im Zone-DI-Subset). Cross-Zone-Gameplay (Chat/Auction/Team/Mail) NICHT auf dem Pfad — trennen sich später sauber.

---

## Dev-Umgebung (CT257)

- **CT 257 `aaemu-dist`**, pveh1, Debian 13.1, IP `10.11.10.57`, 4C/6G/40G auf datapool-storage, unprivileged+nesting.
- **.NET 10.0.301** (dotnet-install.sh), **MariaDB 11.8.6**. DBs `aaemu_game`+`aaemu_login`, user `aaemu`/`AAEmu_srv_2026`.
- Source `/root/AAEmu` (Kopie von Live-CT250, Fork `AgimDur/aaemu-auroria`).
- **Client-Daten:** game_pak (24.8G) via read-only bind-mount von CT250 → `/opt/clientsrc`, + Symlink `/root/AAEmu/.server_files/AAEmu.Game/ClientData/game_pak` (rebuild-fest).
- **systemd:** `aaemu-login` (1237/1234), `aaemu-game` (1239/1250/1300), `aaemu-zone`. Alle `active`.
- **Build:** `sudo pct exec 257 -- bash -c 'export PATH=/usr/share/dotnet:$PATH; cd /root/AAEmu && dotnet build AAEmu.Zone/AAEmu.Zone.csproj -c Release'`
- **Test-Char:** `aaemu_game.characters` id=9001 `ZoneTester` (race1/gender1 lvl50, Nuian 15578/15382/126). Account/User id=9001 `zonetest`.
- **Self-Test:** `ClusterNetwork.TunnelSelfTest=true` in `AAEmu.Game/Config.json` → beim Zone-Register feuert synth. Handoff (conn=997) → Char-Load + Init-Sequenz. Zone-Restart (~90s Bootstrap) re-triggert.

---

## Nächster Schritt: B2.6d (echter Client)

**Was fehlt:** Byte-Korrektheit der zonen-generierten Pakete gegen den echten 1.2-Client. Zu verifizieren:
- `SCUnitState 0x69` — bekanntes **523B Self-Layout** + Tail-Ketten-Reihenfolge (beobachtet 529B = plausibler Voll-Self)
- `SCCharacterState 0x40` Feldpackung, Faction-Blobs `0x08/0x09`
- `Inventory 0x4b/0x4c`, `ActionSlots 0x12d` Container/Slot-Encoding
- level-1-Framing-CRC bei echtem Handshake

**Rig:** DNAT-Swap (Client→Gateway→Tunnel→Zone) + Shadow-1.2-Client, Gimmo am Schirm.
**Hebel:** das parkierte **Rust-aurora-server-Projekt** hat all dies Byte-Wissen (WorldList u8, wf-Timestamp, SCUnitState 523B, Tail-Kette) — direkt anwendbar. Die beiden Projekte greifen ineinander.

---

## Verweise
- Memory: `project_aurora_server.md` (voller Verlauf B0→B2.6c + Rust-Projekt)
- Rust-Projekt (parkiert, Byte-Referenz): CT256 `aurora-server`, GitHub `kekksi/aurora-server`
- Live 1.2: CT250 (unangetastet), `playauroria.com` / 46.4.58.85
