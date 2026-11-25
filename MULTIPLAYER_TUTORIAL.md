# Full Tutorial: Adding 1v1 Multiplayer to Dishonored-Battle

This walks through everything end to end, in order. Follow it top to bottom the
first time — later you can jump to whichever section you need.

**What you're building:** one player hosts, shares their public IP, the other
player joins with it. Both see the existing character-select screen, pick a
fighter, and get dropped onto the battle plane. Winning a round adds a point.

---

## Part 0 — Before you start

- Unity 2021 LTS or newer (Netcode for GameObjects supports 2021.3+).
- Your project already has: a main menu with a Multiplayer button, a
  player-select screen with 9 characters, and a battle/fight scene.
- Have all three scenes (Menu, Select, Battle) added to
  **File → Build Settings → Scenes In Build** — Netcode's scene manager can only
  load scenes that are in this list.

---

## Part 1 — Install Netcode for GameObjects

1. **Window → Package Manager**.
2. Top-left dropdown → switch from "In Project" to **"Unity Registry"**.
3. Search **"Netcode for GameObjects"**.
4. Click **Install**. This pulls in **Unity Transport** as a dependency
   automatically — you don't need to install that separately.
5. Wait for the compile to finish (bottom-right spinner in Unity).

---

## Part 2 — Set up the NetworkManager (in your Main Menu scene)

1. In the Menu scene, create an empty GameObject and name it `NetworkManager`.
2. **Add Component → Netcode → NetworkManager**.
3. This auto-adds a **Unity Transport** component too — if it doesn't, add it
   manually via **Add Component → Netcode → Transports → Unity Transport**.
4. On the **NetworkManager** component:
   - **Player Prefab**: leave this **empty**. We're spawning fighters manually
     in the Battle scene (via `BattleSpawner`), not using Netcode's automatic
     "spawn on connect" behavior.
   - **Network Transport**: should already show "Unity Transport" — if not,
     drag the Unity Transport component in from the same object.
   - Tick **Scene Management** (usually on by default). This lets the host
     move both players between scenes together, which `CharacterSelectNetwork`
     relies on.
5. Make this `NetworkManager` object **persist across scenes**: either tick
   "Don't Destroy On Load" via a small script, or simply keep it in the Menu
   scene and never unload that scene (Netcode's own DontDestroyOnLoad handling
   covers the NetworkManager object itself automatically once networking
   starts — you don't need to do anything extra here).
6. Attach the **`GameNetworkManager.cs`** script (from the files I gave you) to
   this same object. Set **Default Port** to `7777` (or any port you like,
   as long as host and client use the same one).

---

## Part 3 — Build the Host / Join UI

1. On your Multiplayer button's existing click handler, show a small panel with:
   - A **Host** button
   - A **Join** button
   - A **TMP_InputField** (or regular InputField) for the IP address
2. Wire the buttons' `OnClick` events:
   - Host → `GameNetworkManager.Instance.HostGame()`
   - Join → `GameNetworkManager.Instance.JoinGame(ipInputField.text)`
3. That's it for connecting — at this point, running Host on one machine and
   Join (with `127.0.0.1`) on another instance should connect them. Test this
   before moving on (see Part 8).

---

## Part 4 — Shared match state (character picks + scores)

`MatchState` needs to exist as a **spawned NetworkObject** before the select
screen or battle scene need it, and it needs to survive the scene change.

1. In the Menu scene, create an empty GameObject `MatchState`.
2. **Add Component → NetworkObject**.
3. Attach the `MatchState` script (from `GameNetworkManager.cs` — both classes
   are in that one file).
4. Create a tiny bootstrap script (or add to `GameNetworkManager`) that spawns
   it once hosting begins:

```csharp
[SerializeField] private NetworkObject matchStateObject;

private void Start()
{
    NetworkManager.Singleton.OnServerStarted += () =>
    {
        if (!matchStateObject.IsSpawned)
            matchStateObject.Spawn();
    };
}
```

   Drag the `MatchState` GameObject into `matchStateObject` in the Inspector.
   `OnServerStarted` fires for the host (since the host IS a server), so this
   spawns it exactly once, at the right time, automatically.

---

## Part 5 — Wire up the player-select screen

1. Add a **NetworkObject** component to the root of your select-screen UI
   (or a dedicated empty GameObject that lives alongside it).
2. Attach `CharacterSelectNetwork.cs`. Set **Battle Scene Name** to the exact
   name of your battle scene (case-sensitive, must match Build Settings).
3. Spawn this object the same way as `MatchState` — either in the same
   `OnServerStarted` callback, or when the select screen loads, whichever fits
   your scene flow better.
4. In your **existing** select-screen script, find where a character click is
   handled — something like:
   ```csharp
   void OnCharacterClicked(int index)
   {
       selectedCharacter = index;
       // ...your existing highlight/preview logic...
   }
   ```
   Add one line:
   ```csharp
   characterSelectNetwork.SubmitChoice(index);
   ```
5. Find your existing "Ready" / "Start" / "Confirm" button handler and add:
   ```csharp
   characterSelectNetwork.SetReady();
   ```
6. Nothing else about your select screen needs to change — visuals, previews,
   animations all stay as they are. Once **both** players have called
   `SetReady()`, the host automatically loads the Battle scene for both.

---

## Part 6 — Prepare your 9 character prefabs

For each of the 9 playable-character prefabs:

1. **Add Component → NetworkObject**.
2. **Add Component → Netcode → Components → NetworkTransform** (syncs
   position/rotation across the network — without this the other player won't
   see movement).
3. If characters have Animator-driven animations both players should see, also
   add **NetworkAnimator**.
4. Add the `PlayerHealth.cs` script.
5. **Register every prefab with the NetworkManager**: select your
   `NetworkManager` GameObject → NetworkManager component → **Network Prefabs**
   list → click **+** and drag in all 9 character prefabs. This step is easy to
   miss and causes a silent spawn failure if skipped.

---

## Part 7 — Set up the Battle scene

1. Create an empty GameObject `BattleSpawner`, add **NetworkObject**, attach
   `BattleSpawner.cs`.
   - Drag your 9 prefabs into **Character Prefabs**, in the *same order* as
     they appear on the select screen (index 0 = first character, etc).
   - Create two empty GameObjects on the fight plane, e.g. `SpawnPointHost`
     and `SpawnPointClient`, positioned where each fighter should appear.
     Assign them to **Host Spawn Point** / **Client Spawn Point**.
2. Create another empty GameObject `RoundManager`, add **NetworkObject**,
   attach `RoundManager.cs`. Assign the same two spawn points, and set
   **Rounds To Win** (e.g. `3`) and **Respawn Delay** (e.g. `2` seconds).
3. Because Scene Management is on, both of these objects spawn automatically
   for both players the moment the host loads the Battle scene — you don't
   need to manually call `.Spawn()` on them (unlike `MatchState`, which exists
   *before* any scene load happens).

---

## Part 8 — Hook up combat/hit detection

Find wherever your existing fight code currently does something like:

```csharp
opponentHealth -= damage;
```

Replace it with a call to the networked version, run **from the side that
detects the hit** (attacker's client is fine — `RequireOwnership = false`
lets anyone call it, and the server enforces the actual health change):

```csharp
opponentPlayerHealth.TakeDamageServerRpc(damage);
```

You'll need a reference to the opponent's `PlayerHealth` component — however
your project already identifies "who did I hit" (collision, raycast, hitbox
trigger) is fine; just call `GetComponent<PlayerHealth>()` on whatever you hit.

---

## Part 9 — Local testing (do this before testing over the internet)

**Editor + Build method (simplest, no extra tools):**
1. **File → Build Settings → Build** a standalone copy of the game.
2. Run the built `.exe` — click **Host**.
3. Press Play in the Unity **Editor** — click **Join**, enter `127.0.0.1`.
4. You should see both characters spawn in the Battle scene once both players
   ready up on the select screen.

**Optional — testing two Editor instances at once:**
Unity doesn't allow two Editor windows on the same project by default. The
free **ParrelSync** package (via Package Manager → Add package from git URL:
`https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync`) creates a
linked clone project so you can run two Editor instances side by side without
building. Handy while iterating, optional overall.

---

## Part 10 — Testing over the internet (the real "play from anywhere" setup)

1. **Host finds their public IP**: visit whatsmyip.com.
2. **Host port-forwards** on their router:
   - Log into the router admin page (usually `192.168.0.1` or `192.168.1.1`).
   - Find **Port Forwarding** (sometimes under "NAT", "Virtual Server", or
     "Applications & Gaming").
   - Create a rule: **External Port 7777 → Internal Port 7777**, protocol
     **UDP**, pointing at the host PC's **local** IP (find it via `ipconfig`
     on Windows, look for IPv4 Address).
   - It's worth setting the host PC's local IP to be **static/reserved** in
     the router (a "DHCP reservation") so the forwarding rule doesn't break
     next time the router hands out a new address.
3. **Windows Firewall**: when you first run the host build, Windows may prompt
   to allow the app through the firewall — allow it on both Private and
   Public networks. If it doesn't prompt, add a manual inbound rule for UDP
   7777.
4. Host shares their public IP with the other player (not their local
   `192.168.x.x` address — that only works on the same network).
5. Other player enters that public IP in the Join field and connects.

---

## Troubleshooting

- **"Failed to connect" / timeout, nothing happens**: almost always port
  forwarding or firewall. Double-check the rule is UDP (not TCP), pointed at
  the right local IP, and that Windows Firewall isn't silently blocking it.
- **Connects on LAN but not over the internet**: some ISPs use **CGNAT**
  (common on mobile/some budget ISPs), which makes port forwarding impossible
  no matter how correctly it's configured. If this is the case, direct
  connection can't work and you'd need a relay service (Unity Relay is the
  natural next step — it's a bigger change, ask me if you hit this).
- **"NetworkObject not registered" / spawn silently fails in Battle scene**:
  you forgot to add a character prefab to the NetworkManager's **Network
  Prefabs** list (Part 6, step 5).
- **Other player's character doesn't move on your screen**: missing
  **NetworkTransform** on that character prefab (Part 6, step 2).
- **Both players see different results / desync**: means something is being
  changed directly on the client instead of through a `ServerRpc` — audit for
  any leftover `health -= damage` style code that bypasses `PlayerHealth`.

---

## Where to go from here

Once this baseline works, natural next steps: a countdown before each round,
a proper win-screen UI wired to `AnnounceMatchWinnerClientRpc`, disconnect
handling (what happens if a player quits mid-match), and eventually Unity
Relay if CGNAT turns out to be a problem for your players.
