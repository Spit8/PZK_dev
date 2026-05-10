---
name: Audit P2-P3 Mise en Marche
overview: "Audit technique exhaustif des Phases 2 et 3 du projet PZK : identification de toutes les configurations Unity Editor + code nécessaires pour passer du code à un prototype jouable avec Core Loop + Networking fonctionnels."
todos:
  - id: verify-scene-singletons
    content: Verifier que les 4 singletons scene (GameSession+sous-composants, EffectManager, Generator, ItemSpawner) ont chacun un NetworkIdentity et tous leurs scripts
    status: pending
  - id: verify-player-prefab
    content: Verifier que le Player Prefab est un asset avec tous les 12+ composants requis et champs Inspector assignes
    status: pending
  - id: verify-spawn-prefabs
    content: Verifier que tous les prefabs spawnes reseau sont dans PZKNetworkManager.spawnPrefabs
    status: pending
  - id: verify-data-assets
    content: Verifier ItemDatabase contient tous les ItemData, chaque ItemData a un worldPrefab, chaque PickupItem.itemId correspond
    status: pending
  - id: verify-effect-definitions
    content: Verifier que chaque NetworkEffectType et NetworkSoundType a une definition avec prefab/clip assigne dans le NetworkEffectManager
    status: pending
  - id: verify-layers-masks
    content: Verifier WeaponSystem.attackMask inclut le layer joueur, PlayerHighlightObject.interactableLayers inclut les items
    status: pending
  - id: verify-input-actions
    content: Verifier que PlayerInputHandler a moveAction et runAction assignees
    status: pending
  - id: verify-camera-pivot
    content: Verifier que PlayerCameraController.cameraPivot pointe vers un Transform enfant valide
    status: pending
  - id: verify-validator-speed
    content: Verifier que ServerMovementValidator.maxAllowedSpeed >= vitesse max reelle du joueur
    status: pending
  - id: smoke-test
    content: "Test Host + 2e client : spawn, mouvement, animation, ready, combat, pickup, mort, respawn"
    status: pending
isProject: false
---


# Audit Technique P2-P3 -- De Code Mort a Prototype Jouable

---

## 1. Audit de la Phase 2 (Fondations) -- Configuration Unity Editor

### 1.1 Player Prefab -- Le coeur du probleme

Le Player Prefab est l'objet le plus critique. Il DOIT contenir tous ces composants sur le **meme GameObject racine** (ou hierarchie coherente avec `GetComponent`) :

**Composants Mirror obligatoires :**
- `NetworkIdentity` (client authority = true, pour `NetworkTransformReliable` ClientToServer)
- `NetworkTransformReliable` (direction : **Client To Server**, car le mouvement est client-authoritative avec validation serveur)

**Scripts NetworkBehaviour du projet :**
- `PlayerMovement` -- requiert `CharacterController` sur le meme GO
- `PlayerInputHandler` -- requiert `InputAction moveAction` et `InputAction runAction` assignees dans l'Inspector
- `PlayerCameraController` -- requiert un `Transform cameraPivot` (enfant du joueur) assigne dans l'Inspector
- `PlayerHealth` -- SyncVars `currentHealth`, `maxHealth`, `isDead`, `lastAttackerNetId`
- `PlayerInventory` -- requiert `ItemDatabase itemDatabase` (asset SO) assigne dans l'Inspector
- `WeaponSystem` -- requiert `LayerMask attackMask` configure, `float eyeHeight` regle
- `ServerMovementValidator` -- requiert `maxAllowedSpeed` >= vitesse max reelle du joueur (sinon faux positifs anti-cheat)
- `PlayerLobbyReady` -- aucun champ Inspector critique
- `PlayerHighlightObject` -- requiert `LayerMask interactableLayers` configure
- `PlayerUIController` -- champ `crosshairVisual` optionnel (recherche runtime du Canvas sinon)
- `PlayerDamageFeedback` -- zero champ Inspector (UI construite par code dans `OnStartLocalPlayer`)

**Composants Unity obligatoires :**
- `CharacterController` -- utilise par `PlayerMovement.Update()` (l.51) et `ServerMovementValidator.ForcePositionCorrection` (l.95-114)
- `Animator` -- sur enfant ou assigne via `[SerializeField] playerAnimator` dans `PlayerMovement` ; **parametres bool requis** : `isWalking`, `isRunning`, `isTurningLeft`, `isTurningRight`, plus trigger `AttackTrigger` (par defaut dans `WeaponData`)
- **Camera** enfant sous `cameraPivot` -- ou detection automatique via `Camera.main` / `GetComponentInChildren<Camera>(true)` dans `PlayerCameraController.Start()`
- **AudioListener** sur la camera enfant (desactive pour les joueurs non-locaux dans `PlayerCameraController.Start()` l.281-302)

**Hierarchie attendue du prefab :**
```
PlayerPrefab (root)
  +-- NetworkIdentity
  +-- NetworkTransformReliable
  +-- CharacterController
  +-- PlayerMovement
  +-- PlayerInputHandler
  +-- PlayerCameraController
  +-- PlayerHealth
  +-- PlayerInventory
  +-- WeaponSystem
  +-- ServerMovementValidator
  +-- PlayerLobbyReady
  +-- PlayerHighlightObject
  +-- PlayerUIController
  +-- PlayerDamageFeedback
  |
  +-- CameraPivot (Transform enfant)
  |     +-- PlayerCamera (Camera + AudioListener)
  |
  +-- ModelRoot (enfant avec Animator + mesh/skinned)
```

### 1.2 PZKNetworkManager -- Configuration Scene

[PZKNetworkManager.cs](Assets/Scripts/PZKNetworkManager.cs) herite de `Mirror.NetworkManager`. Dans l'Inspector du GameObject portant ce composant :

- **Player Prefab** : assigner le prefab joueur decrit ci-dessus
- **Spawn Prefabs** (liste `spawnPrefabs` heritee) : **TOUS** les prefabs spawnes via `NetworkServer.Spawn()` doivent y figurer :
  - Chaque prefab de `ItemSpawner.lootPrefabs[]` (les prefabs d'items monde)
  - Les prefabs de `PlayerInventory.CmdDropItem` (= `ItemData.worldPrefab` de chaque item droppable)
  - Important : si un prefab est spawn reseau mais absent de cette liste, Mirror leve une erreur runtime `"Spawn scene object not found"` ou `"SpawnObject for <prefab> has no NetworkIdentity"`
- **Transport** : un composant Transport Mirror (KCP, Telepathy, etc.) doit etre sur le meme GO ou reference
- **Network Address** : `localhost` pour tests locaux
- **Max Connections** : >= nombre de joueurs voulu
- **Auto Create Player** : true (le code suppose `OnServerAddPlayer` est appele automatiquement)
- **Spawn Points** :
  - `Transform[] spawnPoints` (champ serialise l.17-19) : assigner des Transforms dans la scene, OU
  - Placer des GameObjects avec le composant `NetworkStartPosition` dans la scene (utilises par `startPositions` herite de Mirror, l.103-108)
  - Si aucun des deux n'est configure : fallback `Vector3(0, 2, 0)` (l.120)
- **`respawnDelay`** (float, l.15) : verifier la valeur (delai en secondes avant respawn)

### 1.3 GameSessionManager + Sous-composants -- Configuration Scene

**Un seul GameObject de scene** doit porter ces 4 `NetworkBehaviour` + un `NetworkIdentity` :

- `GameSessionManager` (singleton, l.33)
  - `warmupDuration` (float, serialise) -- duree de la phase warmup
  - `gameDuration` (float, serialise) -- duree de la phase active
  - `postGameDuration` (float, serialise) -- duree de la phase post-game
  - `minPlayersToStart` (int, serialise) -- minimum de joueurs pour demarrer
- `SessionTimerController` -- aucun champ Inspector (SyncVars `timerEndTime`, `timerRunning`)
- `SessionScoreboard` -- champs serialises : `killScoreValue`, `deathScorePenalty`
- `SessionReadySystem` -- aucun champ Inspector, `Awake` fait `GetComponent<SessionScoreboard>()`

**CRITIQUE** : `GameSessionManager.Awake()` fait `GetComponent` sur les trois autres (l.101-104). Si l'un manque sur le meme GameObject, `NullReferenceException` immediate.

### 1.4 NetworkEffectManager -- Configuration Scene

Un GameObject de scene avec `NetworkIdentity` + `NetworkEffectManager` (singleton, l.60).

**Dans l'Inspector :**
- **`effectDefinitions[]`** (tableau `EffectDefinition`) : UNE entree par `NetworkEffectType` enum :
  - `BloodSplatter` (0) : prefab avec `ParticleSystem` a la racine
  - `MuzzleFlash` (1) : prefab VFX
  - `HitSpark` (2) : prefab VFX
  - `RespawnFlash` (3) : prefab VFX
  - `DeathEffect` (4) : prefab VFX
  - Chaque entree : `Prefab` (GO), `PoolSize` (int), `MaxPoolSize` (int), `Lifetime` (float)
- **`soundDefinitions[]`** (tableau `SoundDefinition`) : UNE entree par `NetworkSoundType` enum :
  - `HitFlesh` (0) : `AudioClip` + `Volume` + `MaxDistance`
  - `HitMetal` (1)
  - `WeaponSwing` (2)
  - `Death` (5)
  - `Respawn` (6)
  - `Pickup` (3), `Drop` (4)
- **`audioPoolSize`** (int) : nombre d'`AudioSource` 3D crees en interne

**Si un slot `effectDefinitions` a un `Prefab` null**, le manager l'ignore silencieusement (l.166-168), mais l'effet ne jouera jamais.

### 1.5 NetworkGenerator + Generator -- Configuration Scene

Un GameObject de scene avec :
- `NetworkIdentity`
- `NetworkGenerator` (singleton, `[RequireComponent(typeof(Generator))]`)
- `Generator`

**Inspector `Generator` :**
- `floorPrefab`, `stairPrefab`, prefabs mur/porte/fenetre -- tous les prefabs de construction procedurale doivent etre assignes
- `networkControlled` : mis a `true` par `NetworkGenerator.Awake()` (l.87) -- ne PAS toucher manuellement

**Inspector `NetworkGenerator` :**
- `regenerateEachRound` (bool) : si true, regenere la map a chaque passage en Warmup

### 1.6 ItemSpawner -- Configuration Scene

Un GameObject de scene avec `NetworkIdentity` + `ItemSpawner` (singleton).

**Inspector :**
- **`lootPrefabs[]`** (GameObject array) : les prefabs d'items monde a spawner. **ATTENTION** : si ce tableau est vide, `SpawnInitialLoot` crash avec `IndexOutOfRangeException` (dette identifiee dans le plan). Chaque prefab doit avoir `NetworkIdentity` + `PickupItem` + `Rigidbody`.

### 1.7 PZKInterestManagement -- Configuration Scene

Sur le **meme GameObject** que le `PZKNetworkManager` (ou sur le NetworkManager GO directement, selon la convention Mirror) :
- `PZKInterestManagement` (herite `Mirror.InterestManagement`)
- `defaultVisibilityRange` (float, defaut 100m, l.31-32) : portee par defaut pour les objets non marques
- `rebuildInterval` : **champ herite** de `Mirror.InterestManagement`, configurable dans l'Inspector Mirror

### 1.8 UI Canvas de Scene

Un Canvas scene-level doit exister avec :
- `GameSessionUI` (MonoBehaviour) avec les champs assignes :
  - `stateText` (Text) -- affiche l'etat de jeu
  - `timerText` (Text) -- affiche le timer
  - `scoreboardPanel` (GameObject) -- panneau scoreboard
  - `scoreboardContent` (Transform) -- parent des entrees scoreboard
  - `scoreEntryPrefab` (GameObject, optionnel)
  - `startGameButton` (GameObject) -- bouton start (visible uniquement pour le host)
  - `announcementText` (Text) -- annonces (game started, game ended)
- `KillFeedUI` (MonoBehaviour) sur le meme Canvas ou enfant :
  - Aucun champ Inspector critique (UI construite par code dans `Awake`)
  - S'abonne a `SessionScoreboard.OnKillEvent`

### 1.9 PickupItem Prefabs -- Configuration Prefab

Chaque prefab d'item monde doit avoir :
- `NetworkIdentity` (`[RequireComponent]` l.9)
- `Rigidbody` (`[RequireComponent]` l.10)
- `PickupItem` : champs `displayName` (string), `itemId` (int, doit correspondre a un `ItemData.itemId` dans la database)
- `NetworkSyncDistance` (optionnel) : si present, l'objet sera filtre par distance dans l'Interest Management. Si absent, l'objet est **toujours visible** (l.163-166 de `PZKInterestManagement`)
- Un `Collider` (pour le raycast de `PlayerHighlightObject` et `OnTriggerEnter`)

### 1.10 ItemData ScriptableObjects

Chaque `ItemData` asset doit avoir :
- `itemId` (int unique) correspondant au `PickupItem.itemId` du prefab monde
- `worldPrefab` : le prefab monde (celui avec `PickupItem`) -- utilise par `PlayerInventory.CmdDropItem`
- `handPrefab` : le prefab affiche en main (instancie par `PlayerInventory.RefreshHandVisual`)
- `icon` (Sprite) : pour l'InventoryUI
- `weaponData` (WeaponData SO, optionnel) : si non null, `IsWeapon` retourne true et `WeaponSystem.ResolveWeaponStats` lit ses stats

### 1.11 ItemDatabase ScriptableObject

- Asset `ItemDatabase` avec la liste `allItems` remplie de tous les `ItemData` du jeu
- Cet asset doit etre **assigne** dans le champ `itemDatabase` de `PlayerInventory` sur le Player Prefab
- `OnEnable` reconstruit le lookup par `itemId` (l.11-24)

---

## 2. Audit de la Phase 3 (Mecaniques et Sync) -- Verification Mirror

### 2.1 SyncVars a verifier

| Script | SyncVar | Hook | Verification |
|--------|---------|------|--------------|
| `PlayerHealth` | `currentHealth` (int) | `OnHealthChanged` | Le hook met a jour `PlayerHealthUI` si present ; verifier que l'UI est bien assignee |
| `PlayerHealth` | `maxHealth` (int, defaut 100) | aucun | Verifier la valeur initiale dans l'Inspector du prefab |
| `PlayerHealth` | `isDead` (bool) | `OnIsDeadChanged` | Ce hook desactive controles + appelle `PlayerDamageFeedback.HideDeathOverlay()` a la resurrection |
| `PlayerHealth` | `lastAttackerNetId` (uint) | aucun | Utilise pour `ServerRecordKill` ; pas d'impact visuel |
| `PlayerMovement` | `walking` (bool) | `OnWalkingChanged` | Applique `isWalking` sur l'Animator des autres clients |
| `PlayerMovement` | `running` (bool) | `OnRunningChanged` | Applique `isRunning` sur l'Animator des autres clients |
| `PlayerInventory` | `activeSlotIndex` (int) | `OnActiveSlotChanged` | Rafraichit le visuel en main via `RefreshHandVisual()` |
| `SessionTimerController` | `timerEndTime` (double) | `OnTimerEndTimeChanged` | Appelle `GameSessionManager.NotifyTimerSynced()` |
| `SessionTimerController` | `timerRunning` (bool) | `OnTimerRunningChanged` | Idem |
| `GameSessionManager` | `currentGameState` (GameState) | `OnGameStateChanged` | Invoque `OnGameStateChangedEvent` |
| `NetworkGenerator` | `currentSeed` (int) | aucun | Utilise par les late-joiners dans `OnStartClient` |
| `NetworkGenerator` | `isGenerated` (bool) | aucun | Gate pour la generation late-join |
| `PickupItem` | `heldByNetId` (uint) | `OnHeldByChanged` | CODE MORT : jamais assigne a non-zero dans le flux actuel |

### 2.2 SyncList a verifier

| Script | SyncList | Callback |
|--------|----------|----------|
| `SessionScoreboard` | `SyncList<PlayerScoreData>` | `OnInventoryChanged` via `.Callback +=` dans `OnStartClient`, appelle `GameSessionManager.NotifyScoreboardChanged()` |
| `PlayerInventory` | `SyncList<ItemSlot>` | `OnInventoryChanged` via `.Callback +=`, rafraichit `InventoryUI` si ouverte |

### 2.3 Commands (Client vers Serveur)

| Script | Command | Verification |
|--------|---------|--------------|
| `PlayerMovement` | `CmdSetMovementState(bool, bool)` | Envoie les etats d'animation au serveur |
| `WeaponSystem` | `CmdAttack(Vector3 aimDirection)` | **CRITIQUE** : le raycast serveur utilise les positions courantes, pas de lag compensation |
| `PlayerInventory` | `CmdPickupItem(NetworkIdentity)` | Verifie distance + detruit l'objet via `NetworkServer.Destroy` |
| `PlayerInventory` | `CmdDropItem(int index)` | Instancie `worldPrefab` + `NetworkServer.Spawn` -- le prefab DOIT etre dans `spawnPrefabs` |
| `PlayerInventory` | `CmdSetActiveSlot(int)` | Change le slot actif |
| `PlayerLobbyReady` | `CmdToggleReady()` | Appelle `GameSessionManager.Instance.ServerSetPlayerReady` |
| `PlayerLobbyReady` | `CmdSetReady(bool)` | Idem avec valeur explicite |

### 2.4 ClientRpc (Serveur vers tous les clients)

| Script | RPC | Verification |
|--------|-----|--------------|
| `PlayerHealth` | `RpcOnDamage(Vector3 hitPoint)` | Joue `BloodSplatter` + `HitFlesh` via `NetworkEffectManager.Instance` -- VERIFIER que les definitions sont assignees |
| `PlayerHealth` | `RpcOnDeath(uint killerNetId)` | Joue `DeathEffect` + `Death` |
| `PlayerHealth` | `RpcOnRespawn()` | Joue `RespawnFlash` + `Respawn` |
| `WeaponSystem` | `RpcPlayAttackAnimation()` | Joue l'animation attack + `WeaponSwing` sound |
| `GameSessionManager` | `RpcNotifyGameStarted()` | Feedback debut de partie |
| `GameSessionManager` | `RpcNotifyGameEnded()` | Feedback fin de partie |
| `SessionScoreboard` | `RpcBroadcastKill(string, string)` | Invoque `OnKillEvent` static => `KillFeedUI` |
| `NetworkGenerator` | `RpcRegenerate(int seed)` | Regenere la map sur les clients |

### 2.5 TargetRpc (Serveur vers un client specifique)

| Script | TargetRpc | Verification |
|--------|-----------|--------------|
| `PlayerHealth` | `TargetOnDamage(conn, amount, hitDirection)` | Appelle `PlayerDamageFeedback.OnDamageReceived` -- verifier que le composant existe sur le prefab |
| `PlayerHealth` | `TargetOnDeathInfo(conn, respawnDelay)` | Appelle `PlayerDamageFeedback.ShowDeathOverlay` |
| `WeaponSystem` | `TargetHitConfirmed(conn)` | Appelle `PlayerDamageFeedback.ShowHitMarker` |
| `ServerMovementValidator` | `TargetCorrectPosition(conn, Vector3)` | Teleporte le client a la position corrigee |

---

## 3. Checklist de Mise en Marche

Actions concretes dans l'Inspector Unity, dans l'ordre :

### Etape A : Verifier la Scene

- [ ] **A1** : Un GameObject avec `PZKNetworkManager` existe dans la scene
- [ ] **A2** : Un composant `Transport` Mirror (KCP recommande) est sur le meme GO ou reference par le NetworkManager
- [ ] **A3** : `PZKInterestManagement` est sur le meme GO que le NetworkManager
- [ ] **A4** : Un GameObject avec `GameSessionManager` + `SessionTimerController` + `SessionScoreboard` + `SessionReadySystem` + `NetworkIdentity` existe
- [ ] **A5** : Un GameObject avec `NetworkEffectManager` + `NetworkIdentity` existe
- [ ] **A6** : Un GameObject avec `NetworkGenerator` + `Generator` + `NetworkIdentity` existe
- [ ] **A7** : Un GameObject avec `ItemSpawner` + `NetworkIdentity` existe
- [ ] **A8** : Au moins 2 `NetworkStartPosition` ou `spawnPoints` Transforms existent dans la scene
- [ ] **A9** : Un Canvas avec `GameSessionUI` existe, avec tous les champs UI assignes (`stateText`, `timerText`, `scoreboardPanel`, `scoreboardContent`, `startGameButton`, `announcementText`)
- [ ] **A10** : `KillFeedUI` est sur le Canvas (ou enfant)

### Etape B : Configurer le Player Prefab

- [ ] **B1** : Le prefab est un **asset** (pas un objet de scene) avec `NetworkIdentity` (Client Authority = true si utilise avec `NetworkTransformReliable` ClientToServer)
- [ ] **B2** : `NetworkTransformReliable` ajoute, direction **Client To Server**
- [ ] **B3** : `CharacterController` present
- [ ] **B4** : `PlayerMovement` present ; `playerAnimator` assigne si pas auto-detect
- [ ] **B5** : `PlayerInputHandler` present ; `moveAction` et `runAction` (InputAction) **assignes**
- [ ] **B6** : `PlayerCameraController` present ; `cameraPivot` (Transform enfant) **assigne**
- [ ] **B7** : `PlayerHealth` present ; `maxHealth` regle (defaut 100)
- [ ] **B8** : `PlayerInventory` present ; `itemDatabase` (asset ScriptableObject `ItemDatabase`) **assigne**
- [ ] **B9** : `WeaponSystem` present ; `attackMask` (LayerMask) configure pour inclure le layer des joueurs ; `eyeHeight` regle
- [ ] **B10** : `ServerMovementValidator` present ; `maxAllowedSpeed` >= `runSpeed` de `PlayerMovement` (sinon rubber-banding permanent)
- [ ] **B11** : `PlayerLobbyReady`, `PlayerHighlightObject`, `PlayerUIController`, `PlayerDamageFeedback` presents
- [ ] **B12** : Enfant `CameraPivot` avec une `Camera` + `AudioListener`
- [ ] **B13** : Enfant mesh/model avec `Animator` ; Animation Controller avec les parametres : `isWalking` (bool), `isRunning` (bool), `isTurningLeft` (bool), `isTurningRight` (bool), `AttackTrigger` (trigger)

### Etape C : Enregistrer les Prefabs Reseau

- [ ] **C1** : Le Player Prefab est assigne dans `PZKNetworkManager.playerPrefab`
- [ ] **C2** : Chaque prefab d'item monde (avec `PickupItem` + `NetworkIdentity` + `Rigidbody`) est ajoute a `PZKNetworkManager.spawnPrefabs`
- [ ] **C3** : Les memes prefabs sont aussi dans `ItemSpawner.lootPrefabs[]`
- [ ] **C4** : Les `worldPrefab` de chaque `ItemData` sont dans `spawnPrefabs` (necessaire pour `CmdDropItem` -> `NetworkServer.Spawn`)

### Etape D : Configurer les Assets Donnees

- [ ] **D1** : Au moins un asset `ItemData` SO existe avec un `itemId` unique
- [ ] **D2** : Si l'item est une arme : un asset `WeaponData` SO assigne dans `ItemData.weaponData` (champs: `damage`, `range`, `cooldown`, `attackType`, `animationTrigger`)
- [ ] **D3** : L'asset `ItemDatabase` contient tous les `ItemData` dans sa liste `allItems`
- [ ] **D4** : Le `itemId` de chaque `PickupItem` (sur le prefab monde) correspond a un `ItemData.itemId` dans la database

### Etape E : Configurer le NetworkEffectManager

- [ ] **E1** : `effectDefinitions` a 5 entrees (une par `NetworkEffectType`) avec prefab VFX assigne, pool size, lifetime
- [ ] **E2** : `soundDefinitions` a 7 entrees (une par `NetworkSoundType`) avec AudioClip assigne, volume, max distance
- [ ] **E3** : `audioPoolSize` >= 8 (pour couvrir les sons simultanes)

### Etape F : Configurer les Layers et Physics

- [ ] **F1** : Un Layer dedie aux joueurs (ex: "Player"), assigne au prefab joueur
- [ ] **F2** : `WeaponSystem.attackMask` inclut ce layer
- [ ] **F3** : `PlayerHighlightObject.interactableLayers` inclut les layers des items interactibles
- [ ] **F4** : Verifier la matrice de collision Physics (les joueurs doivent pouvoir toucher les items via trigger/raycast)

### Etape G : Test de base

- [ ] **G1** : Build and Run en mode Host (un client + serveur)
- [ ] **G2** : Verifier dans la Console : pas de "Spawn scene object not found", pas de "SpawnObject for X has no NetworkIdentity"
- [ ] **G3** : Verifier : le joueur apparait a un spawn point, peut se deplacer, l'animation sync
- [ ] **G4** : Ouvrir un 2e client (ParrelSync ou build standalone) ; verifier que les deux joueurs se voient
- [ ] **G5** : Tester le toggle ready + demarrage de partie
- [ ] **G6** : Tester le ramassage d'item
- [ ] **G7** : Tester l'attaque + feedback de degats (vignette, shake, hit marker)
- [ ] **G8** : Tester la mort + respawn

---

## 4. Points de Blocage Potentiels (P2 vers P3)

### BLOCAGE 1 : `spawnPrefabs` incomplet (Severite : CRITIQUE)

Si un prefab est instancie via `NetworkServer.Spawn()` mais absent de la liste `PZKNetworkManager.spawnPrefabs`, Mirror leve une erreur et l'objet n'apparait pas sur les clients. Cela concerne :
- Tous les prefabs de `ItemSpawner.lootPrefabs`
- Les `ItemData.worldPrefab` utilises par `PlayerInventory.CmdDropItem` (l.253-258)

**Symptome** : items visibles uniquement cote serveur/host, invisibles pour les clients.

### BLOCAGE 2 : `GetComponent` croise entre GameSessionManager et sous-composants (Severite : CRITIQUE)

`GameSessionManager.Awake()` (l.101-104) fait :
```csharp
timerController = GetComponent<SessionTimerController>();
scoreboard = GetComponent<SessionScoreboard>();
readySystem = GetComponent<SessionReadySystem>();
```
`SessionReadySystem.Awake()` fait aussi `GetComponent<SessionScoreboard>()`.

Si un seul de ces 4 scripts manque sur le meme GameObject, **toute la state machine de session est cassee** (NullRef des la premiere transition). Aucun guard null n'existe dans `ServerTransitionTo`.

### BLOCAGE 3 : `ServerMovementValidator.maxAllowedSpeed` mal calibre (Severite : HAUTE)

Si `maxAllowedSpeed` < vitesse reelle du joueur (incluant sprint), le validateur genere des violations en continu, le joueur est rubber-bande en boucle. Le `violationDecayInterval` attenuerait, mais l'experience serait injouable.

**Verification** : `maxAllowedSpeed` (dans l'Inspector du prefab) doit etre >= `runSpeed` (ou la vitesse max avec sprint) de `PlayerMovement` + une marge de 10-20%.

### BLOCAGE 4 : `WeaponSystem.attackMask` n'inclut pas le layer joueur (Severite : CRITIQUE)

`CmdAttack` fait `Physics.Raycast(origin, direction, out hit, range, finalMask)` (l.232-233). Si le `LayerMask attackMask` ne contient pas le layer du prefab joueur, **aucun hit ne sera jamais detecte**. Le combat sera silencieusement casse sans erreur console.

### BLOCAGE 5 : `ItemDatabase` non assigne dans `PlayerInventory` (Severite : CRITIQUE)

`PlayerInventory` utilise `itemDatabase.GetItemById(slot.itemId)` dans `CmdDropItem` et `RefreshHandVisual`. Si le champ `itemDatabase` est null dans l'Inspector du prefab joueur, tout pickup/drop/equip crash avec NullRef.

### BLOCAGE 6 : `NetworkEffectManager` definitions manquantes (Severite : MOYENNE)

Les `ClientRpc` dans `PlayerHealth` (`RpcOnDamage`, `RpcOnDeath`, `RpcOnRespawn`) appellent `NetworkEffectManager.Instance.PlayEffectLocally(...)`. Si `Instance` est null (GO absent de la scene), les appels sont silencieusement ignores (guard null dans chaque RPC). Si `Instance` existe mais qu'un slot `effectDefinitions` a un `Prefab` null, l'effet specifique est silencieusement skip. Pas de crash, mais **zero feedback visuel/sonore**.

### BLOCAGE 7 : `PlayerInputHandler` sans `InputAction` assignees (Severite : CRITIQUE)

`PlayerInputHandler` lit `moveAction.ReadValue<Vector2>()` et `runAction.ReadValue<float>()` dans `ReadContinuousState()` (l.114-133). Si ces `InputAction` ne sont pas assignees dans l'Inspector, NullReferenceException dans le `Update` de chaque frame pour le joueur local.

### BLOCAGE 8 : `PlayerCameraController.cameraPivot` null (Severite : CRITIQUE)

`LateUpdate` (l.112) : `if (!isLocalPlayer || cameraPivot == null) return`. Si `cameraPivot` n'est pas assigne, la camera ne suit jamais le joueur -- aucune erreur, mais aucune camera non plus. Le joueur voit la scene depuis le point d'origine.

### BLOCAGE 9 : Player Prefab pas un asset (instance de scene) (Severite : CRITIQUE)

Mirror exige que le `playerPrefab` soit un **prefab asset** (dans le dossier Assets), pas un objet deja present dans la scene. Si c'est un objet de scene, Mirror ne peut pas l'instancier pour les nouveaux joueurs.

### BLOCAGE 10 : `NetworkIdentity` manquant sur les singletons de scene (Severite : CRITIQUE)

`GameSessionManager`, `NetworkEffectManager`, `NetworkGenerator`, `ItemSpawner` sont des `NetworkBehaviour`. Sans `NetworkIdentity` sur leur GameObject, Mirror les ignore completement : aucun SyncVar ne sync, aucun RPC ne fonctionne, pas d'erreur explicite -- juste un silence total.

---

## Resume des fichiers cles

- [PZKNetworkManager.cs](Assets/Scripts/PZKNetworkManager.cs) -- lifecycle, respawn, spawn points
- [GameSessionManager.cs](Assets/Scripts/GameSessionManager.cs) -- state machine, singleton, events
- [SessionTimerController.cs](Assets/Scripts/SessionTimerController.cs) -- timer SyncVars
- [SessionScoreboard.cs](Assets/Scripts/SessionScoreboard.cs) -- SyncList scores, RpcBroadcastKill
- [SessionReadySystem.cs](Assets/Scripts/SessionReadySystem.cs) -- ready logic, auto-start
- [PlayerHealth.cs](Assets/Scripts/PlayerHealth.cs) -- SyncVars sante, TakeDamage, mort/respawn RPCs
- [WeaponSystem.cs](Assets/Scripts/WeaponSystem.cs) -- CmdAttack, TargetHitConfirmed
- [PlayerMovement.cs](Assets/Scripts/PlayerMovement.cs) -- CharacterController, SyncVar anim
- [PlayerInputHandler.cs](Assets/Scripts/PlayerInputHandler.cs) -- InputActions, events
- [PlayerCameraController.cs](Assets/Scripts/PlayerCameraController.cs) -- cameraPivot, ISO/FPS
- [PlayerInventory.cs](Assets/Scripts/PlayerInventory.cs) -- SyncList slots, CmdPickup/Drop
- [PlayerDamageFeedback.cs](Assets/Scripts/PlayerDamageFeedback.cs) -- feedback UI local
- [NetworkEffectManager.cs](Assets/Scripts/NetworkEffectManager.cs) -- VFX/SFX RPCs + pools
- [ItemSpawner.cs](Assets/Scripts/ItemSpawner.cs) -- lootPrefabs, spawn initial
- [ItemData.cs](Assets/Scripts/Item/ItemData.cs) -- SO avec weaponData ref
- [WeaponData.cs](Assets/Scripts/Item/WeaponData.cs) -- SO stats arme
- [ItemDB.cs](Assets/Scripts/Item/ItemDB.cs) -- database lookup par itemId
- [PZKInterestManagement.cs](Assets/Scripts/PZKInterestManagement.cs) -- filtrage visibilite
