# FlashRevise

> **Apprends moins. Retiens plus.**
> Application de révision intelligente : flashcards générées par IA, répétition espacée SM-2, suivi de progression et rappels — sur Android et Windows.

---

## 1. Fonctionnalités

| Domaine | Détail |
| --- | --- |
| **Flashcards** | Création manuelle (recto / verso / astuce / difficulté / tags / image), édition, suppression, recherche |
| **Matières** | Couleur d'accent, description, archivage, progression et maîtrise par matière |
| **IA (Groq)** | Génération de cartes depuis un texte, un PDF ou une image ; explication d'une carte ; chat tuteur en streaming ; résumé de notes |
| **Compte** | Connexion, création de compte, jetons JWT rafraîchis automatiquement, déconnexion et suppression du compte via l'API FlashRevise — le mode invité reste disponible |
| **Répétition espacée** | Algorithme SM-2 (variante Anki) : qualité de rappel, intervalle, facteur de facilité, échéances |
| **Sessions de révision** | File de cartes, retournement Know / Didn't know, progression, chrono, résumé de fin de session (XP, précision, durée) |
| **Progression** | Streak, objectif quotidien, XP, maîtrise globale, graphiques LiveCharts (révisions et précision sur 14 jours) |
| **Gamification** | Succès (achievements), livres suivis avec progression |
| **Rappels** | Notifications locales récurrentes par matière, deep link vers la session de révision |
| **Hors ligne** | Détection de connectivité, file d'attente persistée des opérations, rejeu automatique au retour en ligne |
| **Thèmes** | Clair et sombre complets, téléphone et tablette/PC (layouts adaptatifs) |

---

## 2. Prérequis

- **.NET SDK 10.0.401** (ou supérieur) avec la workload MAUI : `dotnet workload install maui`
- **Windows** : Windows 10 version 1903+ (build 10.0.19041 minimum)
- **Android** : SDK Platform **API 36** + Build-Tools, JDK 17 ; `minSdk 26` (Android 8.0), `targetSdk 36`
  - Si l'API 36 manque : `dotnet build -f net10.0-android -t:InstallAndroidDependencies -p:AndroidSdkDirectory="<chemin SDK>" -p:AcceptAndroidSDKLicenses=True`
- Un émulateur Android configuré (ou un appareil en débogage USB) pour lancer l'app Android

---

## 3. Démarrage

```bash
# Windows (unpackaged)
dotnet build -f net10.0-windows10.0.19041.0
dotnet run   -f net10.0-windows10.0.19041.0

# Android
dotnet build -f net10.0-android
dotnet run   -f net10.0-android
```

### Clé API Groq — obligatoire pour l'IA

La clé **n'est jamais** dans le code, dans `appsettings.json` ni dans le dépôt.

1. Lancer l'app → onglet **Profil**.
2. Section *Clé API IA* → coller la clé `gsk_...` → **Enregistrer**.
3. La clé est stockée via `SecureStorage` (Keystore Android / DPAPI Windows) et n'est lue qu'au moment des appels Groq.

Les écrans IA affichent un encart d'avertissement tant qu'aucune clé n'est configurée.

> Si une clé a été exposée (copiée dans un chat, un écran, un commit), **révoque-la** depuis la console Groq et génère-en une nouvelle.

### API d'authentification FlashRevise

L'application intègre le client de l'API du dépôt
[`nonochristelle12-crypto/FlashRevise`](https://github.com/nonochristelle12-crypto/FlashRevise)
(`src/FlashRevise.Api`) :

| Méthode | Endpoint | Usage dans l'app |
| --- | --- | --- |
| `POST` | `/v1/auth/register` | Créer un compte (nom, e-mail, mot de passe, niveau, consentement) |
| `POST` | `/v1/auth/login` | Connexion |
| `POST` | `/v1/auth/refresh` | Rotation automatique des jetons (accès 15 min, refresh 30 jours) |
| `POST` | `/v1/auth/logout` | Révocation de la famille de refresh tokens |
| `POST` | `/v1/auth/forgot-password` | Demande de réinitialisation (réponse neutre) |
| `GET` / `DELETE` | `/v1/account` | Profil du compte connecté / suppression du compte |
| `GET` | `/health/live` | Bouton « Tester le serveur » du profil |

**Lancer l'API en local** (le secret JWT n'est jamais commité) :

```powershell
# PowerShell, dans le dépôt de l'API
$env:FLASHREVISE_DEV_JWT_KEY = [Convert]::ToBase64String(
  (1..48 | ForEach-Object { Get-Random -Maximum 256 }))
dotnet run --project src/FlashRevise.Api --urls http://0.0.0.0:5047
```

```bash
# bash / macOS / Linux
export FLASHREVISE_DEV_JWT_KEY="$(openssl rand -base64 48)"
dotnet run --project src/FlashRevise.Api --urls http://0.0.0.0:5047
```

- `http://0.0.0.0:5047` rend l'API accessible depuis le téléphone sur le même Wi-Fi.
- Autoriser le port **5047** dans le pare-feu Windows si le téléphone ne joint pas le PC.
- La clé doit faire **au moins 32 octets** ; toute valeur réelle reste hors du dépôt.

**Configurer l'adresse dans l'app** — onglet **Profil → Compte → Adresse de l'API** :

| Cible | Adresse par défaut |
| --- | --- |
| Windows | `http://localhost:5047/` |
| Émulateur Android | `http://10.0.2.2:5047/` |
| Téléphone physique | IP LAN du PC, ex. `http://192.168.1.10:5047/` (bouton **Tester le serveur**) |

- Les jetons sont conservés **uniquement** dans `SecureStorage` ; seuls le nom et l'e-mail publics
  sont mis en cache dans les préférences pour afficher le compte hors ligne.
- Android autorise le trafic HTTP en clair (`android:usesCleartextTraffic="true"`) pour joindre l'API
  de développement en HTTP. En production, servir l'API en HTTPS et retirer cette autorisation.
- Le mode invité reste le comportement par défaut : la connexion est facultative et se fait depuis
  l'accueil (« Se connecter ») ou le profil.

---

## 4. Configuration

`Resources/Raw/appsettings.json` (embarqué en `MauiAsset`, sans secret) :

```json
{
  "Groq": {
    "BaseUrl": "https://api.groq.com/openai/v1/",
    "Model": "openai/gpt-oss-120b",
    "VisionModel": "meta-llama/llama-4-scout-17b-16e-instruct",
    "VisionFallbackModels": [
      "meta-llama/llama-4-maverick-17b-128e-instruct"
    ],
    "TimeoutSeconds": 90,
    "MaxRetries": 3,
    "MaxOutputTokens": 8192
  },
  "Sync": { "Endpoint": "" },
  "Auth": { "BaseUrl": "" }
}
```

- `Groq:Endpoint` vide dans `Sync` ⇒ l'écran *Synchronisation* reste en mode local et l'indique.
- `Auth:BaseUrl` vide ⇒ l'adresse par défaut dépend de la plateforme (`localhost` sur Windows,
  `10.0.2.2` sur émulateur Android). Sur téléphone, renseigner l'IP LAN du PC dans **Profil → Compte**.
- Changer de modèle ne demande aucune modification de code.
- `Groq:VisionModel` est utilisé pour transcrire une photo ou un scan (texte, formules, code) ;
  les modèles de `VisionFallbackModels` sont essayés automatiquement si le premier n'est plus servi.
  La génération de cartes reste assurée par `Groq:Model`.
- Dans l'écran **Générer avec l'IA**, la matière peut être saisie librement : un nom inconnu crée
  automatiquement la matière. Le scanner (caméra) et la galerie sont accessibles directement,
  et le raccourci **Scanner** de l'accueil ouvre la caméra.

### Stockage local

Base SQLite dans `FileSystem.AppDataDirectory/flashrevise.db3` (créée et migrée au premier lancement, seed de démonstration via `DataSeeder`).

---

## 5. Packages et justifications

| Package | Version | Rôle |
| --- | --- | --- |
| `Microsoft.Maui.Controls` | 10.0.110 | Référence explicite requise par .NET 8+ (warning MA002) |
| `CommunityToolkit.Mvvm` | 8.4.2 | `[ObservableProperty]` en propriétés partielles, `[RelayCommand]` — MVVM sans boilerplate |
| `CommunityToolkit.Maui` | 15.0.1 | Behaviors et helpers UI |
| `LiveChartsCore.SkiaSharpView.Maui` | 2.0.5 | Graphiques de progression (SkiaSharp) |
| `Plugin.LocalNotification` | 14.1.2 | Notifications locales planifiées et récurrentes |
| `PdfPig` | 0.1.16 | Extraction de texte des PDF côté client |
| `sqlite-net-pcl` | 1.11.285 | ORM léger, synchrone, mappage attributs, compatible AOT/trimming |
| `AsyncAwaitBestPractices` | 10.0.0 | Sécurité `async void` pour les handlers d'événements |
| `Microsoft.Extensions.Http` | 10.0.0 | `HttpClient` typé, gestion du cycle de vie |
| `Microsoft.Extensions.Configuration.Json` | 10.0.0 | Lecture d'`appsettings.json` |
| `Microsoft.Extensions.Logging.Debug` | 10.0.0 | Logs de débogage |

### Pourquoi `sqlite-net-pcl` plutôt qu'EF Core ?

| Critère | sqlite-net-pcl | EF Core + SQLite |
| --- | --- | --- |
| Poids / démarrage | Très léger, pas de runtime de modélisation | Plus lourd, compilation de modèles au démarrage |
| Android / AOT | Mappage direct par attributs, trimming simple | Intercepteurs, expressions et réflexion plus délicats en AOT |
| Requêtes | SQL explicite ou LINQ basique, prévisible | LINQ riche, migrations intégrées |
| Complexité du domaine | Le domaine est simple (CRUD + agrégats) | Sur-dimensionné pour 11 tables sans relations complexes |
| Contrôle | Transactions et SQL maîtrisés | Migrations automatiques |

➡️ Pour un domaine local, offline-first et sans jointures complexes, `sqlite-net-pcl` réduit la surface de démarrage et évite les surprises AOT.

> **Note dépendances SQLite** : `sqlite-net-pcl` 1.11.285 embarque déjà la pile moderne `SQLitePCLRaw.core 3.0.3` + `SourceGear.sqlite3 3.53.3` (SQLite patché). L'ajout de `SQLitePCLRaw.bundle_green 2.1.11` provoquait un doublon de `libe_sqlite3.so` dans l'APK et tirait une version vulnérable (avis GHSA-2m69-gcr7-jv3q). Il a donc été retiré : les deux cibles compilent sans avertissement de sécurité.

---

## 6. Structure du projet

```
FlashRevise/
├── App.xaml(.cs)              # ressources globales, création de la fenêtre
├── AppShell.xaml(.cs)         # Shell + enregistrement des routes DI
├── MauiProgram.cs             # configuration + injection de dépendances
├── Routes.cs                  # noms de routes centralisés
├── Controls/RingProgress.cs   # anneau de progression (GraphicsView)
├── Converters/                # convertisseurs XAML
├── Data/
│   ├── FlashReviseDatabase.cs # connexion, création des tables
│   └── Repositories/          # 11 repositories
├── Design/Stitch/             # exports de design (html, screenshots, svg)
├── Models/                    # 11 entités SQLite + enums
├── Resources/
│   ├── AppIcon/               # icône adaptative (#101221)
│   ├── Fonts/                 # SpaceGrotesk, Inter, MaterialSymbols
│   ├── Brand/                 # 3 variantes du logo (assets bruts)
│   ├── Raw/appsettings.json
│   ├── Splash/splash.svg
│   └── Styles/                # Colors, Styles, Themes, Icons
├── Services/                  # métier, SM-2, notifications, offline, stockage
│   ├── Ai/                    # Groq, contrats, coffre de clé
│   └── Auth/                  # client de l'API d'authentification (JWT, SecureStorage)
├── ViewModels/                # ViewModels (CommunityToolkit.Mvvm)
└── Views/                     # pages + vues d'onglets
    └── Auth/                  # connexion, inscription, mot de passe oublié
```

---

## 7. Écrans

**Onglets principaux** (`Views/MainTabsPage.xaml`) : Accueil · Matières · Statistiques · Profil, avec **FAB central « Réviser »**.

| Route | Écran |
| --- | --- |
| `splash` | Splash animé |
| `onboarding` | 3 slides de bienvenue |
| `subject?id=` | Détail matière (cartes, progression, actions) |
| `flashcard?id=` | Création / édition de carte |
| `revision-config` | Choix matière, nombre de cartes, aléatoire |
| `revision-session` | Session de révision (retournement, chrono) |
| `summary` | Résumé de session |
| `ai-upload` | Génération IA : texte / fichier / matière / nombre |
| `ai-loading` | Génération en cours (anneau, étapes, annulation) |
| `ai-review` | Revue éditable des cartes générées |
| `ai-chat` | Chat tuteur en streaming |
| `schedule?id=` | Programmer un rappel pour une matière |
| `reminders` | Liste des rappels |
| `subject-stats?id=` | Statistiques détaillées d'une matière |
| `goals` | Objectif quotidien + bibliothèque de livres |
| `achievements` | Succès débloqués / à venir |
| `offline` | État hors ligne + file de synchronisation |
| `login` | Connexion au compte FlashRevise |
| `register` | Création de compte (nom, e-mail, mot de passe, niveau, consentement) |
| `forgot-password` | Demande de réinitialisation du mot de passe |

Les 7 écrans exportés depuis Stitch ont été repris à l'identique ; les 14 écrans restants ont été étendus en XAML en conservant strictement les tokens « Neo-Study Kinetic ».

---

## 8. Design system

- **Tokens** : `Resources/Styles/Colors.xaml` (palette claire/sombre), `Themes.xaml` (pinceaux de surface, hairlines, textes), `Styles.xaml` (typo, cartes, boutons, badgers), `Icons.xaml` (glyphes Material Symbols).
- **Palette** : Primary `#6C4CF1`, Secondary `#22D3EE`, Tertiary `#34D399`, Warning `#FBBF24`, Danger `#F87171`, Canvas sombre `#101221`, Canvas clair `#F8F9FC`.
- **Typographie** : Space Grotesk (titres), Inter (corps), Material Symbols (icônes).
- **Grille 8 pt**, rayons 8 / 12 / 16 / 24 / plein.
- **Règle d'or MAUI** : un `SolidColorBrush` ne peut pas alimenter une propriété `Color` (`TextColor`, `BackgroundColor`, `BorderColor`, `Stroke` d'un `BoxView.Color`…). Toutes ces propriétés utilisent `{AppThemeBinding Light={StaticResource LightX}, Dark={StaticResource DarkX}}`.
- Chaque écran est vérifié en **clair et sombre**.

### Identité visuelle

| Fichier | Usage |
| --- | --- |
| `Resources/AppIcon/appicon.svg` + `appiconfg.svg` | Icône applicative (mipmaps Android générés au build, assets Windows) |
| `Resources/Splash/splash.svg` | Écran de démarrage natif |
| `Resources/Brand/brand_logo_primary.svg` | Logo principal (marque + mot-symbole) |
| `Resources/Brand/brand_logo_mono.svg` | Variante monochrome (fonds clairs, impression) |
| `Resources/Brand/brand_logo_compact.svg` | Variante compacte carrée (avatar, favicon, store) |

---

## 9. Vérifications de build

| Cible | Résultat |
| --- | --- |
| `net10.0-windows10.0.19041.0` | ✅ 0 erreur, 0 avertissement |
| `net10.0-android` | ✅ 0 erreur, 0 avertissement |
| Manifest Android généré | `minSdkVersion=26`, `targetSdkVersion=36` |

Toutes les propriétés `[ObservableProperty]` utilisent des **propriétés partielles** (compatibles AOT/WinRT, zéro warning `MVVMTK0045`).

Vérification à l'exécution : l'app Windows a été lancée et parcourue (Splash → Onboarding → Accueil,
AI Upload) avec rendu réel en thème sombre. La cible Android a été installée et testée sur un
**TECNO KM5** (Android 15 / API 35) : accueil invité, profil, photo de profil et configuration de la
clé IA. Les écrans de connexion et d'inscription compilent et sont déployés avec le client d'API.

---

## 10. Documentation associée

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — couches, flux de révision, flux IA, schéma de données (ERD).
- [`CHANGELOG.md`](CHANGELOG.md) — historique des versions.

---

## 11. Dépannage

| Symptôme | Cause / solution |
| --- | --- |
| « Aucune clé API » sur les écrans IA | Configurer la clé dans Profil → *Clé API IA* |
| Erreur 401 Groq | Clé invalide ou révoquée → ressaisir dans Profil |
| La génération reste longue ou échoue | Le délai est de 90 s par appel ; l'écran affiche l'erreur réelle (délai, quota 429, clé 401, modèle vision indisponible) et propose **Réessayer** |
| Photo illisible / aucun texte détecté | Reprendre la photo à plat, bien éclairée ; la transcription vision gère aussi le code et les formules |
| Scanner absent sur PC | La capture caméra est mobile uniquement ; sur Windows, utiliser **Galerie** |
| `XA5207 android.jar introuvable` | Installer l'API 36 (commande §2) |
| Graphiques vides | Aucune révision enregistrée dans les 14 derniers jours |
| Rappels silencieux | Autoriser les notifications (Android 13+) ; l'app redemande la permission à l'activation |
| Synchronisation indisponible | `Sync:Endpoint` vide ⇒ fonctionnalité locale uniquement (volontaire) |
