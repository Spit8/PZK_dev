# Architecture du projet PZK

## Présentation

PZK est un jeu multijoueur FPS sur les zombies développé avec Unity. Le projet utilise l'architecture modulaire pour permettre une évolution continue et la maintenance facile.

## Structure du projet

### Répertoires principaux

```
Assets/
├── Scripts/
│   ├── Item/                 # Système d'objets
│   ├── House/                # Génération de maisons
│   ├── playerMovement.cs     # Mouvement joueur
│   ├── PlayerCameraController.cs  # Caméra joueur
│   ├── PlayerInventory.cs    # Inventaire joueur
│   ├── PZKNetworkManager.cs  # Gestion réseau
│   └── itemSpawner.cs      # Spawning objets
├── Scenes/                 # Scènes du jeu
└── Item/                     # Assets objets
```

## Composants principaux

### 1. Système de mouvement hybride ISO/FPS

**Fichier : `playerMovement.cs`**
- Gestion du déplacement en mode ISO ou FPS
- Support pour les modes de jeu ISO (comme Zomboid) et FPS
- Rotation et déplacement optimisés pour chaque mode

**Fichier : `PlayerCameraController.cs`**
- Caméra hybride avec panning ISO
- Vue FPS avec position stable
- Transition fluide entre modes

### 2. Système d'inventaire

**Fichier : `PlayerInventory.cs`**
- Ramassage et lâché d'objets
- Gestion des collisions
- Interface utilisateur pour les interactions

**Fichier : `PickupItem.cs`**
- Composant pour objets ramassables
- Synchronisation réseau avec Mirror
- Positionnement dans la main du joueur

### 3. Système d'objets

**Fichier : `ItemDatabase.cs`**
- Base de données centralisée des objets
- Gestion par ID et propriétés (nom, poids, prefab)
- Récupération d'objets par ID

**Fichier : `ItemData.cs`**
- Structure de données pour les objets
- Propriétés : ID, nom, poids, prefab 3D

**Fichier : `itemSpawner.cs`**
- Génération aléatoire d'objets dans le monde
- Spawning avec synchronisation réseau

### 4. Système de réseau

**Fichier : `PZKNetworkManager.cs`**
- Gestion du réseau avec Mirror
- Synchronisation des mouvements et objets
- Connexion et gestion des joueurs

### 5. Système audio

**Fichier : `fallingImpact.cs`**
- Effets sonores pour les impacts
- Volume basé sur la violence du choc

## Architecture technique

### Frameworks utilisés
- **Unity 3D** : Moteur de jeu principal
- **Mirror** : Framework réseau pour le multijoueur
- **C#** : Langage de programmation
- **Input System** : Gestion des entrées utilisateur

### Principes de conception
- **Modularité** : Chaque composant a une responsabilité claire
- **Réutilisabilité** : Composants conçus pour être réutilisés
- **Synchronisation réseau** : Tous les objets et actions sont synchronisés
- **Performance** : Optimisations spécifiques à chaque mode de jeu

## Système de base de données

### Structure des objets
- **ID unique** : Pour identification dans la base de données
- **Nom** : Affichage utilisateur
- **Poids** : Propriété physique
- **Prefab 3D** : Modèle à afficher au sol

### Gestion des objets
- Base de données centralisée dans `itemDatabase.asset`
- Récupération par ID
- Système de synchronisation réseau

## Architecture réseau

### Communication
- Synchronisation des mouvements
- Spawning d'objets
- Gestion des interactions entre joueurs
- Transmission des états de l'inventaire

### Sécurité
- Utilisation de Mirror pour éviter les erreurs RPC
- Vérifications de sécurité dans les composants réseau

## Système de caméra

### Fonctionnalités
- **Mode ISO** : Vue en perspective isométrique avec panning
- **Mode FPS** : Vue première personne avec position stable
- **Transition fluide** : Changement entre modes
- **Zoom** : Contrôle du zoom dans les deux modes

## Gestion des entrées

### Input System
- Gestion des mouvements (mouvement, course)
- Interaction (ramassage)
- Navigation de la caméra (vue FPS)

## Tests et validation

### Composants testés
- Système d'inventaire
- Synchronisation réseau
- Spawning d'objets
- Interface utilisateur

## Extensions possibles

1. **Système de progression** : Amélioration des objets avec niveaux
2. **IA zombies** : Comportements avancés
3. **Système de quêtes** : Objectifs et missions
4. **Améliorations réseau** : Optimisations de performance
5. **Interface utilisateur** : Améliorations de l'UX

## Dépendances

- Unity 3D (version compatible)
- Mirror (framework réseau)
- Input System (gestion des entrées)
- Universal Render Pipeline (rendu graphique)