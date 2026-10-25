# Changelog

Toutes les évolutions notables de FlashRevise sont consignées ici.
Le format suit [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) et le projet suit
[Semantic Versioning](https://semver.org/lang/fr/).

## [1.2.0] — 2026-10-25

### Ajouté

#### IA : lecture de photos et scanner

- `IAiService.ExtractImageTextAsync` : transcription d'une photo ou d'un scan par le modèle vision
  Groq (`Groq:VisionModel`, repli automatique sur `VisionFallbackModels`), avec compression de
  l'image à 1600 px et encodage base64. Le texte, les formules et le code source sont conservés.
- Écran **Générer avec l'IA** : boutons **Scanner une photo** (caméra) et **Galerie**, aperçu de la
  photo, permission Android `CAMERA`.
- Raccourci **Scanner** sur l'accueil : ouvre l'écran IA et déclenche directement la caméra.
- Champ libre **Matière / nom du paquet** : un nom inconnu crée automatiquement la matière ;
  le sélecteur de matières existantes reste disponible.

### Corrigé

- Génération IA bloquée à 90 % sur « Dernière mise en forme… » : le délai d'attente Groq était
  interprété comme une annulation utilisateur et aucune erreur n'était affichée. Le délai, les
  erreurs HTTP (401/429/404) et les réponses vides sont désormais remontés avec un bouton
  **Réessayer** ; la progression continue lentement au lieu de plafonner à 90 %.
- Budget de sortie Groq porté à 8192 tokens pour éviter les réponses JSON tronquées.
- Les photos ne déclenchent plus l'erreur « extraction indisponible » : elles passent par la
  transcription vision avant la génération des cartes.

## [1.1.0] — 2026-10-25

### Ajouté

#### Authentification (API FlashRevise)

- `IAuthService` / `AuthService` : inscription, connexion, rafraîchissement des jetons (rotation),
  déconnexion, mot de passe oublié, `GET`/`DELETE /v1/account` et test `GET /health/live`.
- Écrans `login`, `register` et `forgot-password` : validation locale du mot de passe
  (8 caractères minimum, une majuscule, un chiffre), messages d'erreur traduits depuis les codes
  de l'API (`invalid_credentials`, `account_locked`, `email_already_used`, `consent_required`).
- Entrée « Se connecter » sur l'accueil invité et carte **Compte** dans le profil : état connecté,
  e-mail, déconnexion, suppression définitive du compte avec double confirmation.
- Jetons d'accès et de rafraîchissement conservés **uniquement** dans `SecureStorage` ; seuls le nom
  et l'e-mail publics sont mis en cache dans les préférences.
- Adresse de l'API configurable dans le profil (défaut Windows `localhost:5047`, émulateur Android
  `10.0.2.2:5047`, téléphone = IP LAN du PC) avec bouton « Tester le serveur ».
- Android : `android:usesCleartextTraffic="true"` pour joindre l'API HTTP de développement.
- Le profil local reprend le nom affiché du compte à la connexion et repasse en invité à la déconnexion.

### Modifié

- README et ARCHITECTURE : lancement de l'API, secret JWT fourni par variable d'environnement,
  endpoints, configuration réseau téléphone / PC et limites du mode HTTP en développement.

## [1.0.0] — 2026-10-25

Première version livrée, Android (priorité) et Windows PC.

### Ajouté

#### Design & thème

- Récupération des maquettes Stitch dans `Design/Stitch/` (HTML, captures, logo).
- Design system « Neo-Study Kinetic » : `Colors.xaml`, `Themes.xaml`, `Styles.xaml`, `Icons.xaml`.
- Thèmes clair et sombre complets via `AppThemeBinding`, sur tous les écrans.
- Composant `RingProgress` (anneau de progression dessiné en `GraphicsView`).
- Master-detail adaptatif téléphone / tablette / PC.

#### Navigation

- `AppShell` avec `ServiceRouteFactory<TPage>` : chaque page est résolue par injection de dépendances.
- Barre d'onglets custom (5 emplacements) avec FAB central « Réviser ».
- `Routes.cs` centralise les 18 routes ; deep link depuis les notifications de rappel.

#### Données

- 11 entités SQLite : `Subject`, `Flashcard`, `ReviewSchedule`, `ReviewLog`, `ReviewSession`,
  `Reminder`, `UserProfile`, `Book`, `Achievement`, `AiGenerationJob`, `SyncQueueItem`.
- 11 repositories et base `flashrevise.db3` créée au premier lancement.
- `DataSeeder` : matière de démonstration, cartes et profil initial.

#### Fonctionnalités métier

- CRUD matières (couleur, description, archivage) avec suppression en cascade transactionnelle.
- CRUD flashcards (question, réponse, astuce, difficulté, tags, image) et recherche.
- Répétition espacée SM-2 : qualité de rappel, intervalle, facteur de facilité, lapses.
- Sessions de révision : file de cartes, retournement, chrono, progression, fin de session.
- Résumé de session : précision, durée, XP, streak, encouragement contextuel.
- Statistiques : maîtrise globale, révisions et précision sur 14 jours (LiveCharts).
- Statistiques par matière et historique des 30 dernières sessions.
- Objectif quotidien, XP, streak et record ; bibliothèque de livres avec progression.
- Succès (achievements) évalués automatiquement en fin de session et à la mise à jour des livres.

#### Intelligence artificielle (Groq)

- `GroqAiService` : `GenerateFlashcardsAsync`, `ExplainCardAsync`, `ChatAsync`, `SummarizeAsync`,
  `StreamChatAsync`, `TestConnectionAsync`.
- Streaming SSE token par token dans le chat tuteur (annulation, retry, suggestions).
- Flux de génération en 3 écrans : import (texte / PDF / image) → chargement (étapes, annulation) → revue éditable.
- Suivi des générations dans `AiGenerationJob` (Running / Completed / Failed / Cancelled).
- Extraction de texte PDF via PdfPig et import d'images via `FilePicker`.
- Gestion des erreurs typées : clé absente, quota dépassé, indisponibilité du service ; retries et timeouts.

#### Offline & synchronisation

- Détection de connectivité et événements (`OnlineChanged`, `PendingSyncCountChanged`).
- File de synchronisation persistée avec rejeu FIFO, compteur de tentatives et dernier message d'erreur.
- Écran dédié : état de connexion, opérations en attente, synchronisation manuelle.
- Mode local complet lorsque `Sync:Endpoint` est vide.

#### Notifications

- `NotificationService` basé sur `Plugin.LocalNotification` : rappels quotidiens ou hebdomadaires par matière.
- Réarmement automatique des rappels au démarrage (`AppBootstrapper`).
- Demande de permission runtime et avertissement en cas de refus.
- Permissions Android déclarées : `POST_NOTIFICATIONS`, `RECEIVE_BOOT_COMPLETED`, `SCHEDULE_EXACT_ALARM`.

#### Sécurité

- Clé API Groq stockée **uniquement** dans `SecureStorage` (Keystore Android / DPAPI Windows).
- Aucune clé dans le code, `appsettings.json`, les ressources ou les logs.
- Écran Profil dédié à la saisie/validation de la clé, avec test de connexion.
- `.gitignore` durci : `bin/`, `obj/`, `*.user`, `appsettings.local.json`, `.env`, `*.key`, `*.keystore`, `*.jks`, `*.pfx`.

#### Compte & profil

- Démarrage en **compte invité** : aucun prénom inventé, l'utilisateur renseigne le sien quand il le souhaite.
- Photo de profil : sélection via le sélecteur système, copie locale et affichage sur l'accueil et le profil, suppression possible.
- Saisie de la clé Groq directement dans les écrans IA (génération et assistant), avec validation en un clic.

#### Identité visuelle

- Icône adaptative (`appicon.svg` + `appiconfg.svg`) sur canvas `#101221`, mipmaps Android générés au build.
- Splash natif animé (`splash.svg` + animation d'apparition du logo).
- 3 variantes de logo : `brand_logo_primary.svg`, `brand_logo_mono.svg`, `brand_logo_compact.svg`.

#### Documentation

- `README.md` : installation, configuration, structure, dépannage.
- `ARCHITECTURE.md` : couches, navigation, ERD, flux SM-2 et IA, mode hors ligne.
- `CHANGELOG.md` : ce fichier.

### Modifié

- Cibles limitées à **Android** et **Windows** (`net10.0-android`, `net10.0-windows10.0.19041.0`).
- `SupportedOSPlatformVersion` Android porté à **26.0** (`minSdk 26`), cible **API 36**.
- Toutes les propriétés `[ObservableProperty]` migrées en **propriétés partielles** (AOT/WinRT).
- `DisplayAlert` remplacé par `DisplayAlertAsync` ; `FadeTo`/`ScaleTo` par leurs variantes *Async*.

### Corrigé

- **Thème** : 81 affectations de `SolidColorBrush` vers des propriétés `Color`
  (`TextColor`, `BackgroundColor`, `BorderColor`, `BoxView.Color`, couleurs Shell, `RingProgress`)
  remplacées par des couleurs inline `AppThemeBinding` — évitait une exception de rendu sur chaque page.
- **Build** : suppression d'un paramètre de constructeur inutilisé (`ReviewSessionService`).
- **Build** : correction d'un déréférencement possible du `IDispatcherTimer` (`RevisionSessionViewModel`).
- **Build** : suppression de `StreamReader.EndOfStream` dans la lecture SSE (CA2024).
- **Sécurité des dépendances** : retrait de `SQLitePCLRaw.bundle_green 2.1.11` (avis GHSA-2m69-gcr7-jv3q,
  doublon `libe_sqlite3.so` dans l'APK). La pile `SQLitePCLRaw.core 3.0.3` + `SourceGear.sqlite3 3.53.3`,
  déjà fournie par `sqlite-net-pcl` 1.11.285, est utilisée.
- **Packaging** : ajout de la référence explicite `Microsoft.Maui.Controls` 10.0.110 (warning MA002).
- Nettoyage des artefacts du template (`MainPage`, image de démonstration).
- **Démarrage (runtime)** : `FlashReviseDatabase` dépend d'un chemin calculé au runtime et ne pouvait pas
  être résolu par le conteneur DI. Enregistrement via fabrique (`FileSystem.AppDataDirectory`), ce qui
  supprime le crash `0xC000027B / 0x80131509` au lancement Windows.
- **Assets** : les logos SVG sont expédiés en `MauiAsset` (`Resources/Brand/`) car le pipeline Resizetizer
  ne sait pas rendre le `<text>` et les graisses variables de ces fichiers (MAUIR0001).
- **Onboarding** : `CarouselView` remplacé par une vue mono-slide pilotée par `CurrentSlide`
  (le dimensionnement des items était peu fiable et un `ItemSizingStrategy` inexistant provoquait une
  `XamlParseException`). La décoration est déplacée dans un `AbsoluteLayout` pour ne plus gonfler la
  ligne `Auto`. Flèche de retour Shell masquée sur cet écran.
- **Thème** : brush → `Color` également corrigé sur les pages de détail, de révision et IA.
- **Profil** : le seed ne crée plus « Thomas » ; un profil existant encore au nom de démonstration
  repasse en invité une seule fois (sans toucher aux profils personnalisés).
- **Android** : crash `Duplicated Route: "splash"` quand le système recrée l'activité ; l'enregistrement
  des routes Shell est désormais idempotent.

### Vérifications

- Smoke test visuel Windows (rendu réel, thème sombre, fenêtre 1440×740) : Splash → Onboarding →
  Accueil, AI Upload.
- Build et lancement Windows validés de bout en bout ; **Android compilé mais non exécuté**
  (aucun émulateur disponible dans l'environnement de build).

### Résultats de build

| Cible | Erreurs | Avertissements |
| --- | --- | --- |
| `net10.0-windows10.0.19041.0` | 0 | 0 |
| `net10.0-android` | 0 | 0 |
