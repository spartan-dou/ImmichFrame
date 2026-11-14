# Revue de code — ImmichFrame (fork `spartan-dou/ImmichFrame`)

- **Date** : 2026-11-15
- **Révision analysée** : `origin/main` à `e612b0c` (3 commits d'avance, 6 de retard sur `upstream/main`)
- **Périmètre** : `ImmichFrame.WebApi`, `ImmichFrame.Core`, `immichFrame.Web`, `Dockerfile`, `docker/`, `.github/workflows/`, configuration de build. Le client généré (`ImmichFrame.Core/Api/ImmichApi.cs` hors méthode ajoutée à la main, `immichFrameApi.ts`) et le site Docusaurus `docs/` n'ont pas été relus ligne à ligne.
- **Emplacement du rapport** : à la racine, car `docs/` est le site de documentation Docusaurus.

## Méthode et limites

- Lecture du code source ; aucun constat ne repose sur le seul README.
- Le SDK .NET n'est pas installé sur la machine d'analyse : le projet n'a été **ni compilé ni exécuté**, et `dotnet list package --vulnerable` n'a pas pu être lancé. Les constats de comportement sont déduits du code ; ceux qui restent à confirmer à l'exécution sont marqués **(hypothèse)**.
- `npm audit --package-lock-only` a été exécuté sur `immichFrame.Web`.
- État des workflows relevé avec `gh run list` et paramètres Actions du fork relevés par l'API GitHub.
- Échelle : critique / élevée / moyenne / faible. **Aucun constat critique** (pas de secret en dur, pas d'injection ni d'exécution de code à distance non authentifiée trouvés).

## Résumé — les points les plus importants

1. **Élevée — Runners auto-hébergés sur un fork public, déclenchés par `pull_request`** (`.github/workflows/test.yml:5-11`, `.github/workflows/pr-require-label.yml:3-9`). Une PR externe fait exécuter son code (`dotnet test`) sur le runner domestique ; la politique d'approbation du fork est `first_time_contributors` seulement. Voir F1.
2. **Élevée — .NET 8 n'est plus supporté depuis le 10 novembre 2026** (`Directory.Build.props:4`, `Dockerfile:2`, `Dockerfile:39`). Plus aucun correctif de sécurité pour le runtime et Kestrel. Voir S1.
3. **Moyenne — Secrets d'administration en clair et sans protection contre la force brute** : mot de passe admin, secret client et clés d'API Immich sont stockés en clair dans SQLite, renvoyés tels quels par `GET /api/Admin/Settings`, et le mot de passe transite à chaque requête, sans TLS ni limitation de débit côté application (`ImmichFrame.WebApi/Services/SettingsService.cs:232`, `ImmichFrame.WebApi/Controllers/AdminController.cs:103-109`, `ImmichFrame.WebApi/Services/AdminAuthService.cs:56-67`). Voir S2, S3.
4. **Moyenne — Un compte Immich supprimé ou modifié reste utilisé** : le traqueur d'assets est un singleton qui conserve indéfiniment les anciennes logiques de compte, avec leur ancienne clé d'API (`ImmichFrame.WebApi/Program.cs:83`, `ImmichFrame.Core/Logic/AccountSelection/BloomFilterAssetAccountTracker.cs:13`). Voir D1.
5. **Moyenne — Cache disque des images (`DownloadImages`) fragile** : répertoire non inscriptible dans l'image Docker officielle (hypothèse forte), parcours complet du dossier à chaque image, fichiers tronqués servis en cas d'échec, aucune limite de taille (`ImmichFrame.Core/Logic/PooledImmichFrameLogic.cs:16`, `:112-162`, `Dockerfile:54-58`). Voir P1.

Autres points notables : jusqu'à 25 appels HTTP séquentiels vers Immich pour un seul `GET /api/Asset` quand les souvenirs sont combinés à une autre source (P2), un `HttpClient` créé par webhook (P4), API ouverte sans authentification par défaut (S4), aucune validation des réglages généraux (D2), le front-end n'est ni testé ni vérifié en CI (D7).

---

## 1. Sécurité

### S1 — Runtime .NET 8 en fin de support — **élevée**
- **Où** : `Directory.Build.props:4` (`net8.0`), `Dockerfile:2` (`sdk:8.0`), `Dockerfile:39` (`aspnet:8.0-jammy`), `.github/workflows/test.yml:20`.
- **Constat** : .NET 8 (LTS) a atteint sa fin de support le 10 novembre 2026. L'application est publiée en mode dépendant du framework (`Dockerfile:24`, `--self-contained false`), donc Kestrel et le runtime viennent de l'image de base, qui ne recevra plus de correctifs. Les paquets `Microsoft.*` sont de plus figés en `8.0.20` (`Directory.Packages.props:11-15`).
- **Recommandation** : migrer vers .NET 10 (LTS) : `TargetFramework`, images `sdk`/`aspnet`, paquets `Microsoft.AspNetCore.*` et `Microsoft.EntityFrameworkCore.*`, `dotnet-version` de la CI. Lancer ensuite `dotnet list package --vulnerable --include-transitive` (non exécuté ici) et l'ajouter à la CI.

### S2 — Mot de passe admin, secret client et clés d'API stockés et renvoyés en clair — **moyenne**
- **Où** : `ImmichFrame.WebApi/Models/ServerSettings.cs:73-74` et `:82`, `ImmichFrame.WebApi/Services/SettingsService.cs:232` (sérialisation brute en base), `ImmichFrame.WebApi/Controllers/AdminController.cs:103-109`, `ImmichFrame.WebApi/Services/AdminAuthService.cs:56-67`.
- **Constat** : `AdminPassword` est conservé tel quel dans `immichframe.db` et comparé au jeton reçu ; `GET /api/Admin/Settings` renvoie le mot de passe admin, `AuthenticationSecret`, `WeatherApiKey` et toutes les `ApiKey` Immich. Le choix est assumé en commentaire (aller-retour GET/PUT), mais une lecture du fichier SQLite ou d'une réponse interceptée livre tous les secrets, y compris le mot de passe lui-même.
- **Recommandation** : stocker un hachage du mot de passe admin (PBKDF2 via `Rfc2898DeriveBytes.Pbkdf2`, ou Argon2) et ne jamais le renvoyer ; masquer les secrets dans le GET avec une valeur sentinelle « inchangé » acceptée par le PUT ; créer `immichframe.db` avec des permissions `600`.

### S3 — Authentification admin : aucune limitation de débit, mot de passe envoyé à chaque requête, pas de TLS — **moyenne**
- **Où** : `ImmichFrame.WebApi/Helpers/ImmichFrameAdminAuthenticationHandler.cs:41-51`, `ImmichFrame.WebApi/Program.cs:147` (redirection HTTPS commentée), `immichFrame.Web/src/lib/services/admin.ts:9-11`, `immichFrame.Web/src/lib/stores/admin.store.ts:21`.
- **Constat** : le mot de passe sert directement de jeton `Bearer` sur chaque appel et reste dans `sessionStorage`. Aucun mécanisme ne ralentit les essais successifs, et l'application écoute en HTTP simple. La comparaison est bien à temps constant (`AdminAuthService.cs:64`).
- **Recommandation** : ajouter `AddRateLimiter` (.NET 7+) sur `api/Admin/*` ; échanger le mot de passe contre un jeton de session à durée limitée ; documenter l'obligation d'un proxy TLS et activer `UseForwardedHeaders`.

### S4 — API cliente ouverte par défaut ; comparaison du secret non constante — **moyenne (comportement documenté)**
- **Où** : `ImmichFrame.WebApi/Models/ServerSettings.cs:73` (défaut `null`), `ImmichFrame.WebApi/Helpers/ImmichFrameAuthenticationHandler.cs:29` et `:50`.
- **Constat** : sans `AuthenticationSecret`, toutes les routes `api/Asset`, `api/Calendar`, `api/Weather` sont anonymes : photos, noms des personnes, lieux et rendez-vous sont lisibles par quiconque atteint le port. Atténuation réelle constatée : `GetAsset` ne sert que les identifiants déjà distribués par le cadre (filtre de Bloom, `BloomFilterAssetAccountTracker.cs:39-62`), aux faux positifs près. La comparaison `token == _authenticationSecret` (`:50`) n'est pas à temps constant, contrairement au schéma admin.
- **Recommandation** : afficher un avertissement dans l'UI admin et dans les journaux quand le secret est vide ; réutiliser `CryptographicOperations.FixedTimeEquals` ; proposer la génération d'un secret aléatoire à l'installation.

### S5 — Secret client passé dans l'URL et conservé dans `localStorage` — **faible**
- **Où** : `immichFrame.Web/src/lib/components/home-page/home-page.svelte:78-83`, `immichFrame.Web/src/lib/stores/persist.store.ts:26`.
- **Constat** : `?authsecret=` est lu puis persisté, mais le paramètre n'est jamais retiré de l'URL : il reste dans l'historique, les journaux du proxy et les favoris.
- **Recommandation** : après lecture, appeler `history.replaceState` pour retirer le paramètre ; préférer le fragment (`#authsecret=`), qui n'est pas envoyé au serveur.

### S6 — Prise de contrôle initiale anonyme, sans exigence sur le mot de passe — **faible**
- **Où** : `ImmichFrame.WebApi/Controllers/AdminController.cs:65-96` (contrôle limité à `IsNullOrWhiteSpace`, `:72`).
- **Constat** : sur une instance vierge, le premier visiteur qui atteint `/admin` choisit le mot de passe. La course entre deux requêtes est correctement gérée (`SettingsService.cs:181-203`), mais rien ne prouve que le demandeur est le propriétaire, et un mot de passe d'un caractère est accepté.
- **Recommandation** : exiger un jeton d'installation à usage unique écrit dans les journaux au démarrage ; imposer une longueur minimale.

### S7 — `ApiKeyFile` : lecture de fichier arbitraire par un administrateur — **faible**
- **Où** : `ImmichFrame.WebApi/Models/ServerSettings.cs:100-107`, `ImmichFrame.WebApi/Controllers/AdminController.cs:138-152`, `ImmichFrame.WebApi/Helpers/ImmichServerVersionChecker.cs:32-34`.
- **Constat** : `POST /api/Admin/Settings/TestAccount` lit le chemin fourni dans `ApiKeyFile` puis envoie son contenu en en-tête `X-API-KEY` vers l'`ImmichServerUrl` fournie dans la même requête. Un administrateur peut ainsi faire sortir tout fichier d'une ligne lisible par le processus. Le même point d'entrée permet de sonder le réseau interne (URL libre). Requiert le mot de passe admin, d'où la sévérité.
- **Recommandation** : restreindre `ApiKeyFile` à une liste de répertoires (`/run/secrets`, répertoire de configuration) après `Path.GetFullPath` ; valider le schéma `http`/`https` de `ImmichServerUrl`.

### S8 — Mot de passe avec espaces en bordure : verrouillage — **faible**
- **Où** : `ImmichFrame.WebApi/Helpers/ImmichFrameAdminAuthenticationHandler.cs:47` (`.Trim()`), `ImmichFrame.WebApi/Controllers/AdminController.cs:72-81`.
- **Constat** : l'installation enregistre le mot de passe tel quel, mais la vérification retire les espaces du jeton reçu. Un mot de passe commençant ou finissant par une espace ne pourra jamais être validé (seule la variable d'environnement permet de reprendre la main).
- **Recommandation** : appliquer la même normalisation à l'enregistrement, ou refuser ces mots de passe.

### S9 — Valeur d'exemple `CHANGE_ME` pour le mot de passe admin — **faible**
- **Où** : `docker/example.env:16`.
- **Constat** : la variable d'environnement prime toujours sur la base (`AdminAuthService.cs:33-37`). Un `.env` copié sans modification donne une instance administrable avec `CHANGE_ME`.
- **Recommandation** : commenter la ligne dans l'exemple, et refuser au démarrage les valeurs factices connues.

### S10 — URL interne du serveur Immich exposée aux clients — **faible**
- **Où** : `ImmichFrame.Core/Api/AssetResponseDto.cs:12`, `ImmichFrame.Core/Logic/MultiImmichFrameLogicDelegate.cs:77-81`.
- **Constat** : chaque asset renvoyé au navigateur porte `ImmichServerUrl`. Le front-end ne l'utilise nulle part (seule occurrence : `admin-fields.ts:154`, pour le formulaire) ; le QR code pointe en dur sur `my.immich.app` (`overlay-qr.svelte:9`).
- **Recommandation** : supprimer le champ de la réponse, ou l'utiliser réellement pour le QR code (voir idée I7).

### S11 — URL de calendriers, potentiellement secrètes, écrites dans les journaux — **faible**
- **Où** : `ImmichFrame.Core/Services/IcalCalendarService.cs:43`, `:68`, `:87`.
- **Constat** : les URL iCal privées contiennent en général un jeton dans le chemin ou la requête ; elles sont journalisées en clair, y compris au niveau `Error` (`:87`), par interpolation de chaîne.
- **Recommandation** : ne journaliser que l'hôte, avec des gabarits structurés.

### S12 — Dépendances front-end vulnérables (outillage de développement) — **faible**
- **Où** : `immichFrame.Web/package-lock.json`, `immichFrame.Web/package.json:41` (`vite`).
- **Constat** : `npm audit` signale 15 vulnérabilités (1 élevée, 10 modérées, 4 faibles) : `vite` 7.0.0–7.3.6 (serveur de développement) et la chaîne `typescript-eslint`/`minimatch`. Le livrable étant statique (`adapter-static`), l'exposition se limite au poste de développement et à la CI.
- **Recommandation** : `npm audit fix` (correctif disponible sans `--force`), puis activer Dependabot ou Renovate (aucune configuration présente dans `.github/`).

### S13 — Durcissement CI et Docker — **faible**
- **Actions référencées par étiquette mobile**, jamais par SHA : tous les workflows ; `actions/checkout@v3` et `actions/setup-dotnet@v3` sont en outre obsolètes (`.github/workflows/test.yml:14`, `:16`). Recommandation : épingler par SHA et passer aux versions majeures courantes.
- **Pas de bloc `permissions`** dans `test.yml`, `pr-require-label.yml` ni sur le job `build` de `deploy.yml` : le jeton hérite des droits par défaut du dépôt. Recommandation : `permissions: contents: read` au niveau du workflow.
- **Injection de script** : `.github/workflows/build-docker.yml:54` interpole `github.event.inputs.version` directement dans le shell. Seuls les comptes ayant un droit d'écriture peuvent déclencher le workflow. Recommandation : passer la valeur par `env:`.
- **Images de base non épinglées par condensat** : `Dockerfile:2`, `:27`, `:39`. **Pas de `HEALTHCHECK`**. `docker/docker-compose.yml:5` utilise `:latest`, sans `read_only`, `cap_drop` ni `no-new-privileges`. Point positif : exécution sous un utilisateur non privilégié (`Dockerfile:58`).
- **En-têtes de sécurité absents** (CSP, `X-Content-Type-Options`, `Referrer-Policy`) : `ImmichFrame.WebApi/Program.cs:132-155`.

Points positifs vérifiés : le client HTTP porteur de la clé d'API ne suit pas les redirections (`ImmichApiHttpClientExtensions.cs:14`) ; `clientIdentifier` est assaini avant journalisation ; Swagger n'est exposé qu'en développement (`Program.cs:126-130`) ; aucun usage de `{@html}` ni de `innerHTML` dans le front-end ; la clé d'API Immich n'est jamais envoyée au navigateur hors UI admin.

---

## 2. Performance

### P1 — Cache disque des images (`DownloadImages`) — **moyenne**
- **Où** : `ImmichFrame.Core/Logic/PooledImmichFrameLogic.cs:16`, `:112-135`, `:151-162` ; `Dockerfile:54-58`.
- **Constats** :
  1. Le cache est placé dans `<répertoire de l'application>/ImageCache` (`:16`). Dans l'image Docker, seul `/app/Config` est rendu inscriptible pour l'UID 1000 (`Dockerfile:55`) ; `Directory.CreateDirectory` (`:116`) devrait donc échouer et toute demande d'image renvoyer une erreur 500 dès que l'option est activée **(hypothèse, non exécuté ; aucun volume `ImageCache` n'est mentionné dans `docs/` ni `docker/`)**.
  2. `Directory.GetFiles` parcourt tout le dossier à chaque image (`:119-120`) : coût linéaire en nombre de fichiers en cache.
  3. Le fichier est créé puis rempli (`:158-159`) sans fichier temporaire : si le téléchargement échoue, un fichier tronqué reste et sera servi pendant `RenewImagesDuration` jours ; deux requêtes simultanées sur le même identifiant se heurtent sur le même fichier.
  4. Aucune limite de taille ; un fichier n'est supprimé que lorsqu'il est redemandé après expiration (`:124-133`).
  5. La réponse Immich (`data`) n'est jamais libérée dans ce chemin.
- **Recommandation** : placer le cache sous le répertoire de configuration (ou un chemin configurable, déclaré en `VOLUME`) ; accéder directement à `<id>.webp` / `<id>.jpeg` avec `File.Exists` ; écrire dans un fichier temporaire puis renommer ; ajouter une purge périodique (âge et taille maximale).

### P2 — Jusqu'à 25 appels séquentiels vers Immich par lot d'assets — **moyenne**
- **Où** : `ImmichFrame.Core/Logic/Pool/MultiAssetPool.cs:14-21`, `ImmichFrame.Core/Logic/Pool/IAssetPool.cs:20-32`, `ImmichFrame.Core/Logic/PooledImmichFrameLogic.cs:57` et `:67`.
- **Constat** : dès qu'un compte combine les souvenirs avec une autre source, `MultiAssetPool` est utilisé. `GetAssets(25)` boucle alors 25 fois, et chaque tour appelle `pool.GetAssets(1)`, soit une requête `SearchRandom` de taille 1 par asset quand le tirage tombe sur la recherche. Les appels sont séquentiels.
- **Recommandation** : tirer d'abord la répartition (combien d'assets par pool), puis faire un seul appel par pool avec la taille voulue.

### P3 — Chargement des souvenirs en N+1 — **moyenne**
- **Où** : `ImmichFrame.Core/Logic/Pool/MemoryAssetsPool.cs:26-33`.
- **Constat** : un appel `GetAssetInfoAsync` séquentiel par asset dépourvu d'EXIF, pendant que la première requête du jour attend. Le résultat est mis en cache jusqu'à minuit, ce qui limite l'impact à une fois par jour et par compte.
- **Recommandation** : paralléliser avec un degré borné (`Parallel.ForEachAsync`), ou charger ces détails à la demande.

### P4 — Un `HttpClient` créé par notification webhook — **moyenne**
- **Où** : `ImmichFrame.Core/Helpers/WebhookHelper.cs:11`, `:22` ; appels sans attente dans `ImmichFrame.WebApi/Controllers/AssetController.cs:106` et `:160`.
- **Constat** : chaque image affichée instancie un `HttpClient` jamais libéré (épuisement possible des sockets sur la durée), sans délai d'expiration autre que les 100 s par défaut. L'appel n'étant pas attendu, toute exception est perdue ; le résultat est écrit avec `Console.WriteLine` (`:26`, `:30`).
- **Recommandation** : injecter `IHttpClientFactory`, fixer un délai court, journaliser avec `ILogger`, et encapsuler l'envoi dans un `try/catch`.

### P5 — Fichiers statiques servis sans compression — **faible**
- **Où** : `immichFrame.Web/svelte.config.js:20` (`precompress: true`), `ImmichFrame.WebApi/Program.cs:132`.
- **Constat** : le build produit des variantes `.gz` et `.br`, mais `UseStaticFiles` ne les sert pas ; aucun `UseResponseCompression` n'est configuré non plus pour le JSON (25 assets avec EXIF et personnes par lot). Sans proxy qui compresse, tout part non compressé, et les variantes pré-compressées alourdissent l'image pour rien.
- **Recommandation** : activer `AddResponseCompression`, ou servir les variantes pré-compressées (`MapStaticAssets` après migration vers .NET 9+).

### P6 — Front-end : requêtes en série et import de toutes les locales — **faible**
- **Où** : `immichFrame.Web/src/lib/components/home-page/home-page.svelte:356-408` ; `immichFrame.Web/src/lib/components/elements/clock.svelte:5`, `asset-info.svelte:4`.
- **Constat** : pour chaque asset, l'image, les albums, les informations et les visages sont demandés l'un après l'autre (jusqu'à 4 allers-retours, chacun relayé vers Immich), alors qu'ils sont indépendants. `import * as locale from 'date-fns/locale'` avec accès par clé dynamique embarque toutes les locales dans le bundle, ce qui pèse sur les cadres peu puissants.
- **Recommandation** : `Promise.all` sur les requêtes indépendantes ; charger la locale par `import()` dynamique.

### P7 — Divers — **faible**
- `ImmichFrame.Core/Logic/Pool/IAssetPool.cs:16` : le délai d'une minute n'est jamais appliqué, car les appelants passent toujours un `CancellationToken` non nul (`AggregatingAssetPool.cs:12`). Aucun contrôleur ne transmet `HttpContext.RequestAborted` : une requête abandonnée continue de solliciter Immich.
- `ImmichFrame.WebApi/Controllers/AssetController.cs:163-172` : `RandomImageAndInfo` charge l'image entière en mémoire puis en base64 ; le flux `asset.FileStream` n'est jamais libéré ; `ThumbhashImage` étant une propriété calculée (`AssetResponseDto.cs:10`), le thumbhash est décodé trois fois (`:169-171`) et l'affectation de `Position` porte sur un flux aussitôt abandonné.
- `ImmichFrame.Core/Services/OpenWeatherMapService.cs:13-25` : l'appel à OpenWeatherMap part même sans clé configurée (le composant horloge le demande toujours, `clock.svelte:38`) ; l'échec est avalé (`:44`) et `null` est mis en cache 5 minutes.
- `ImmichFrame.Core/Services/IcalCalendarService.cs:66-89` : calendriers téléchargés en série ; une seule erreur réseau fait échouer tout l'appel.
- `Dockerfile:4` : `COPY . /source` avant `dotnet restore` invalide le cache de couche à chaque modification de source.
- `.github/workflows/test.yml:4-7` : `push` et `pull_request` sur toutes les branches : les tests tournent deux fois pour chaque PR interne.

---

## 3. Design et architecture

### D1 — Traqueur d'assets singleton jamais purgé — **moyenne**
- **Où** : `ImmichFrame.WebApi/Program.cs:83`, `ImmichFrame.Core/Logic/AccountSelection/BloomFilterAssetAccountTracker.cs:13` et `:39-58`, `ImmichFrame.Core/Logic/ReloadingImmichFrameLogic.cs:36-60`.
- **Constat** : à chaque changement de comptes, `ReloadingImmichFrameLogic` reconstruit le graphe et libère l'ancien après 60 s. Mais le traqueur, singleton, garde les anciennes instances comme clés de dictionnaire et ne les retire jamais. Conséquences : fuite mémoire à chaque sauvegarde ; surtout, `ForAsset` continue de router les assets déjà vus vers l'ancienne logique, donc vers l'ancienne URL et l'**ancienne clé d'API**. Un compte retiré dans l'UI admin reste servi jusqu'au redémarrage.
- **Recommandation** : créer le traqueur avec le graphe (une instance par `MultiImmichFrameLogicDelegate`) plutôt qu'en singleton, ou le vider dans `OnSettingsChanged`. Ajouter un test « compte supprimé, asset inaccessible ».

### D2 — Aucune validation des réglages généraux — **moyenne**
- **Où** : `ImmichFrame.WebApi/Models/ServerSettings.cs:76` (`Validate()` vide).
- **Constat** : l'UI admin peut enregistrer des valeurs qui cassent l'exécution plus loin :
  - `WeatherLatLong` mal formé : `float.Parse` et l'accès à l'indice 1 lèvent une exception hors du `try` (`OpenWeatherMapService.cs:19-20`), d'où une erreur 500 sur `api/Weather` ; l'analyse dépend en outre de la culture du serveur.
  - `Language` invalide : `new CultureInfo(...)` (`AssetController.cs:174`) **(hypothèse : dépend du mode de globalisation du conteneur)**.
  - `Interval` ou `RenewImagesDuration` nuls ou négatifs, `Webhook` et `Webcalendars` sans contrôle de format.
- **Recommandation** : implémenter `GeneralSettings.Validate()` (bornes, `CultureInfo.InvariantCulture`, `Uri.TryCreate`) ; l'UI affiche déjà les erreurs 400 renvoyées par `UpdateSettings`.

### D3 — Même liste de réglages dupliquée à cinq endroits — **moyenne**
- **Où** : `ImmichFrame.WebApi/Models/ServerSettings.cs:39-74`, `ImmichFrame.WebApi/Models/LiveSettings.cs:22-57`, `ImmichFrame.WebApi/Models/ClientSettingsDto.cs:7-34`, `ImmichFrame.Core/Interfaces/IClientSettings.cs` et `IServerSettings.cs`, `immichFrame.Web/src/lib/components/admin/admin-fields.ts`.
- **Constat** : ajouter un réglage impose de modifier la classe, deux interfaces, la façade, le DTO et le formulaire. Un oubli dans `ClientSettingsDto` ne casse aucune compilation.
- **Recommandation** : faire porter les valeurs par un enregistrement immuable unique exposé via `IOptionsMonitor` ou un simple accesseur `Current`, et dériver le DTO client par un attribut ou par projection testée.

### D4 — Authentification réimplémentée en middleware — **faible**
- **Où** : `ImmichFrame.WebApi/Helpers/CustomAuthenticationMiddleware.cs:14-39`, `ImmichFrame.WebApi/Program.cs:115`, `:148-151`.
- **Constat** : le middleware authentifie toutes les requêtes et écrit lui-même le 401, en doublon de `UseAuthentication`/`UseAuthorization`. Le schéma client « réussit » en anonyme pour toute route sans `[Authorize]` (`ImmichFrameAuthenticationHandler.cs:29`). La politique `AllowAnonymous` déclarée en `Program.cs:115` n'est pas utilisée. Une nouvelle route oubliée sans `[Authorize]` est donc publique par défaut.
- **Recommandation** : définir une `FallbackPolicy` exigeant l'authentification, laisser le framework produire les 401 (`HandleChallengeAsync`), et marquer explicitement les routes publiques.

### D5 — Gestion d'erreurs hétérogène — **faible**
- Exceptions avalées : `OpenWeatherMapService.cs:44`. Écritures console au lieu d'`ILogger` : `WebhookHelper.cs:26`, `:30`. Journalisation par interpolation : `IcalCalendarService.cs:43`, `:68`, `:87`, `QueuingAssetPool.cs:38`.
- `ImmichFrame.Core/Logic/AccountSelection/TotalAccountImagesSelectionStrategy.cs:21-23` : si tous les comptes rapportent zéro asset, `ChooseOne` renvoie `null` (`CollectionExtensionMethods.cs:38-50`) et `chosen.GetNextAsset()` lève une `NullReferenceException` au lieu d'un « aucun asset ».
- Aucun gestionnaire d'exceptions global : `AssetNotFoundException` ressort en 500 et non en 404.
- **Recommandation** : `UseExceptionHandler` avec correspondance exception/statut, et un `ILogger` partout.

### D6 — Concurrence — **faible**
- `Random` statique partagé entre threads sans synchronisation : `ImmichFrame.Core/Helpers/CollectionExtensionMethods.cs:5`, `ImmichFrame.Core/Logic/Pool/CachingApiAssetsPool.cs:8`. Recommandation : `Random.Shared`.
- `_raw` est lu sans verrou ni `volatile` par `GetRawSettings` (`SettingsService.cs:43`, `:258`).
- Les caches météo et calendrier ne sont pas invalidés lors d'un changement de réglages (`OpenWeatherMapService.cs:7`, `IcalCalendarService.cs:14`) : une modification dans l'UI admin met jusqu'à 5 et 15 minutes à apparaître.

### D7 — Tests et CI — **moyenne**
- **Front-end** : aucun test (pas de lanceur de tests dans `immichFrame.Web/package.json:5-15`), et la CI n'exécute ni `npm run lint`, ni `npm run check`, ni le build (`.github/workflows/test.yml:21-24`). `home-page.svelte` (604 lignes) concentre machine d'états des transitions, préchargement et rendu.
- **API** : `AssetControllerTests.cs` ne contient que 2 tests ; le test vidéo est commenté (`:187-189`), si bien que le relais avec `Range` (`AssetController.cs:111-127`) n'est pas couvert. Aucun test ne vérifie le rejet 401 du schéma client (les seuls tests 401 portent sur l'admin, `AdminControllerTests.cs:85-115`).
- **Recommandation** : ajouter un job front (`npm ci`, `lint`, `check`, `build`), extraire la logique de transition de `home-page.svelte` dans un module testable, réactiver le test vidéo.

### D8 — Code mort et incohérences — **faible**
- `ImmichFrame.Core/Logic/Pool/QueuingAssetPool.cs:51` : classe non utilisée (le commentaire le dit), mais testée.
- `docker/docker-compose.dev.yml:5` cible une étape `dev` absente du `Dockerfile` racine ; `immichFrame.Web/Dockerfile:18` et `:29` lancent `node build/index.js`, que `adapter-static` ne produit pas (`svelte.config.js:2`).
- `immichFrame.Web/static/pwa-service-worker.js:98-100` : repli sur un cache qui n'est jamais alimenté.
- Espaces de noms : `ImmichFrame.Core/Helpers/JsonConverters.cs:4` déclare `ImmichFrame.WebApi.Helpers` ; `WebhookHelper`, `IcalCalendarService`, `OpenWeatherMapService`, `ImmichFrameAuthenticationHandler` et `CustomAuthenticationMiddleware` sont dans l'espace global.
- `ILogger<AssetController>` injecté dans trois autres contrôleurs : `ConfigController.cs:11`, `CalendarController.cs:12`, `WeatherController.cs:12`.
- `ImmichFrame.Core/Logic/Pool/MemoryAssetsPool.cs:35` : la description réelle de la photo est écrasée par un texte anglais en dur (« years ago »), sans tenir compte de `Language`.
- `Directory.Packages.props:16` : `Microsoft.Extensions.Caching.Memory` 9.0.5 au milieu de paquets 8.x ; `:10` : `Ical.Net` 4.3.1, une version majeure de retard.

Points positifs : séparation Core/WebApi nette, pools d'assets composables et bien testés, bascule à chaud des réglages soignée (verrou, jeton de concurrence, mode dégradé quand un compte est invalide), gestion centralisée des versions NuGet.

---

## 4. Commits propres au fork

`origin/main` compte **3 commits** absents de `upstream/main` (dont un commit de fusion) et a **6 commits de retard** (spécification OpenAPI Immich 3.3.5, version minimale 3.3.0, réglages de souvenirs par type). Le différentiel propre au fork ne touche que la CI : 4 fichiers, +9 −5 lignes.

| Fichier | Modification |
| --- | --- |
| `.github/workflows/test.yml:11`, `:17-18` | runner auto-hébergé, `DOTNET_INSTALL_DIR` dans le répertoire temporaire du runner |
| `.github/workflows/pr-require-label.yml:9` | runner auto-hébergé |
| `.github/workflows/release.yml:16` | runner auto-hébergé |
| `.github/workflows/deploy.yml:16-17`, `:55` | runner auto-hébergé, build conditionné au dépôt d'origine |

### F1 — Runners auto-hébergés sur un dépôt public avec déclencheur `pull_request` — **élevée**
- **Où** : `.github/workflows/test.yml:5-11`, `.github/workflows/pr-require-label.yml:3-9`.
- **Constat** : le fork est public (vérifié) et sa politique d'approbation des PR externes est `first_time_contributors` (vérifié par l'API). `test.yml` exécute `dotnet test` sur le code de la PR : une PR ouverte depuis un autre fork fait donc tourner du code arbitraire sur le runner domestique, avec accès à son réseau. La première PR d'un compte demande une approbation manuelle, facile à accorder par inadvertance ; les suivantes non. GitHub déconseille les runners auto-hébergés sur les dépôts publics pour cette raison.
- **Recommandation** : passer la politique à « exiger l'approbation pour tous les contributeurs externes » ; garder `ubuntu-latest` pour les événements `pull_request` venant de forks, par exemple `runs-on: ${{ github.event.pull_request.head.repo.fork && 'ubuntu-latest' || 'dou-s-home-arc-immichframe' }}` ; vérifier que les runners ARC sont éphémères et isolés du réseau local par une `NetworkPolicy`.

### F2 — Divergence permanente sur 4 fichiers de workflow — **faible**
- **Constat** : le nom du runner est écrit en dur, ce qui expose à un conflit à chaque synchronisation avec upstream et empêche de proposer ces fichiers en amont.
- **Recommandation** : `runs-on: ${{ vars.RUNNER_LABEL || 'ubuntu-latest' }}` avec une variable de dépôt. Le différentiel devient neutre pour upstream et pourrait même y être proposé.

### F3 — `deploy.yml` — **faible**
- **Où** : `.github/workflows/deploy.yml:16`, `:55`.
- **Constat** : la condition porte sur le seul job `build` ; `deploy` est bien sauté par dépendance (`needs: build`), le comportement est donc correct. La condition compare le nom complet du dépôt d'origine, et le job `deploy` a été basculé sur le runner auto-hébergé alors qu'il ne s'exécute jamais sur le fork.
- **Recommandation** : sans gravité ; avec F2, le changement de runner disparaît et seule la condition reste.

### F4 — `release.yml` sur le fork — **faible**
- **Où** : `.github/workflows/release.yml:3-5`, `:16`.
- **Constat** : pousser les étiquettes `v*` d'upstream sur le fork (par exemple avec `git push --tags`) crée une release sur le fork, avec `contents: write` sur le runner auto-hébergé. De même, `build-docker.yml:9-11` publierait une image dans le registre du fork.
- **Recommandation** : conditionner ces deux workflows au dépôt, ou ne pas pousser les étiquettes d'upstream.

### F5 — Branches annexes — **information**
- `origin/patches` porte 23 commits de plus que `main` (retouches d'affichage, correctif des souvenirs, workflow « Image du fork »). Elle n'entre pas dans la comparaison demandée (branches par défaut) et n'a pas été relue ici. Les branches `ccr-139f80a9-q9c79j` et `claude/tender-hopper-x62qpm` semblent résiduelles et peuvent être supprimées.
- Le correctif `npm audit fix` présent sur `patches` (d'après l'intitulé du run) n'est pas sur `main` : voir S12.

### État des workflows du fork (25 dernières exécutions)

- **Dernières exécutions par branche : toutes en succès** — `main` : « Run Tests » et « CodeQL / Analyze » (2026-11-06) ; `patches` : « Run Tests » et « Image du fork » ; `ccr-139f80a9-q9c79j` : « Run Tests ».
- **Deux échecs historiques, tous deux résolus** :
  - « Deploy to GitHub Pages » sur `main` le 2026-11-06 : déclenché par la fusion d'upstream, corrigé par le commit suivant `e612b0c` (condition sur le dépôt).
  - « Enforce PR labels » le 2026-10-09 : PR sans étiquette, repassé en succès une fois l'étiquette posée.
- « Enforce PR labels » échoue sur toute PR sans étiquette ; la PR qui porte ce rapport reçoit donc l'étiquette `documentation`.

---

## 5. Idées de fonctionnalités

- **I1 — Profils par cadre.** `clientIdentifier` est déjà envoyé sur chaque requête mais ne sert qu'aux journaux (`AssetController.cs:38-41`). L'utiliser pour des réglages par appareil (albums, intervalle, disposition) permettrait à un seul serveur d'alimenter le cadre du salon et celui des grands-parents avec des sélections différentes.
- **I2 — Sélecteurs d'albums, de personnes et d'étiquettes dans l'UI admin.** Aujourd'hui il faut saisir des GUID (`ServerSettings.cs:92-94`). Le serveur possède déjà la clé d'API : un point d'entrée admin listant albums et personnes alimenterait des listes à cocher avec vignettes.
- **I3 — Plages horaires et mode nuit.** Écran noir ou luminosité réduite selon un horaire, avec réveil au toucher. Les cadres restent allumés en permanence et rien ne le permet aujourd'hui côté client.
- **I4 — Rechargement à chaud des clients.** La configuration n'est lue qu'au chargement de la page (`+page.ts:15-16`). Un flux SSE notifiant `SettingsChanged` éviterait de recharger chaque cadre à la main après une modification dans l'UI admin.
- **I5 — Télécommande.** Points d'entrée « suivant / précédent / pause » par client, relayés par le même canal SSE : intégration directe avec Home Assistant, en complément du webhook sortant existant.
- **I6 — Vrai mode hors ligne.** Le service worker a un repli sur cache qui n'est jamais alimenté (`pwa-service-worker.js:98-100`). Mettre en cache les N dernières images permettrait au cadre de continuer à tourner pendant une coupure du serveur.
- **I7 — QR code vers la bonne instance.** `ImmichServerUrl` est déjà calculé côté serveur mais inutilisé ; le QR code pointe en dur sur `my.immich.app` (`overlay-qr.svelte:9`). Proposer une « URL publique Immich » par compte.
- **I8 — Actions depuis le cadre.** Boutons « favori » et « ne plus afficher » dans la surcouche d'informations, le second alimentant une liste d'exclusion locale (pas de droit d'écriture requis sur Immich).
- **I9 — Éviter les répétitions.** Le tirage est purement aléatoire à chaque lot. Un historique persistant des assets déjà montrés (le filtre de Bloom existant en est une base) garantirait un parcours complet avant de recommencer.
- **I10 — Santé et diagnostic.** Point d'entrée `/healthz` et `HEALTHCHECK` Docker ; page d'état dans l'UI admin (joignabilité de chaque compte, nombre d'assets, taille du cache, dernières erreurs), aujourd'hui visibles uniquement dans les journaux.
- **I11 — Texte des souvenirs localisé.** Remplacer le « years ago » en dur (`MemoryAssetsPool.cs:35`) par un champ dédié traduit côté client, sans écraser la description de la photo.

---

## Ordre de traitement suggéré

1. F1 : réglage d'approbation des PR externes (immédiat, sans code).
2. S1 : migration .NET 10, puis audit NuGet.
3. D1 et P1 : correctifs ciblés, faible risque.
4. S2, S3 : hachage du mot de passe admin, limitation de débit.
5. D2, D7 : validation des réglages, job CI front-end.
