# Fork d'ImmichFrame

Série de patchs sur [immichFrame/ImmichFrame](https://github.com/immichFrame/ImmichFrame),
portée par la branche `patches`, rebasée sur chaque version amont. Elle remplace
l'ancienne surcouche en iframe du cadre photo (`frame-overlay` dans `cluster-config`).

Le cadre ne connaît pas Home Assistant : c'est l'intégration `immichframe` du
[fork de Home Assistant](https://github.com/spartan-dou/home-assistant-core) qui
l'appelle, comme un appareil ESPHome.

```mermaid
flowchart LR
    HA["Intégration immichframe<br/>(Home Assistant)"] -->|"PUT /api/Overlay/Sensors<br/>à chaque changement + chaque minute"| IF["ImmichFrame (fork)"]
    HA -->|"PUT /api/Memories"| IF
    HA -->|"POST /api/Notification"| IF
    PAGE["Page du cadre"] -->|"GET /api/Overlay, toutes les 2 s"| IF
```

| Patch | Fichiers |
|---|---|
| Souvenirs : `GET`/`PUT /api/Memories` (`enabled`, `only`), **seule source de vérité** — `ShowMemories` est ignoré, l'état est gardé dans `$IMMICHFRAME_STATE_PATH/memories.json` (à défaut, le dossier de config). Masqués tant que l'API ne les a pas allumés, comme en amont. `only` : les souvenirs du jour seuls, et les photos habituelles un jour sans souvenir | `IMemoriesSwitch`, `MemoriesSwitch`, `ToggleableAssetPool`, `MemoriesOnlyAssetPool`, `MemoriesController` ; le constructeur de `PooledImmichFrameLogic` |
| Lots sans doublon : `MultiAssetPool` tire avec remise, la même photo sortait deux fois côte à côte en mode portrait | `DistinctAssetPool` |
| Valeurs sous l'heure : `PUT /api/Overlay/Sensors`, en mémoire. Sans nouvelle poussée depuis 5 min, les valeurs s'affichent « -- » plutôt que figées | `SensorStore`, `OverlayController` |
| Notifications : `POST`/`DELETE /api/Notification`, en mémoire. Trois au plus, la plus récente en haut ; un message déjà affiché remonte au lieu de prendre une deuxième place | `NotificationController`, `NotificationStore` |
| Surcouche : heure et date à la place de l'horloge amont (mêmes formats, tailles et réglage `Style`), valeurs, notifications, tap qui rouvre Home Assistant | `home-assistant-overlay.svelte`, deux lignes dans `home-page.svelte` |
| Image | `.github/workflows/fork-image.yml` |

Le code amont n'est touché qu'en trois endroits (`PooledImmichFrameLogic.cs`,
`Program.cs`, `home-page.svelte`) : ce sont les seuls conflits possibles au rebase.

Les API sont protégées comme le reste d'ImmichFrame : par `AuthenticationSecret`
s'il est défini, ouvertes sinon.

## Monter de version

```bash
git fetch upstream --tags
git rebase --onto v<nouvelle> v<actuelle> patches
git push --force-with-lease origin patches
```

La CI teste, puis publie `ghcr.io/spartan-dou/immichframe:patches-<sha>`, à reporter
avec son digest dans `cluster-config`.
