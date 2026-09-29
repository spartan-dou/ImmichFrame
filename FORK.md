# Fork d'ImmichFrame

Série de patchs sur [immichFrame/ImmichFrame](https://github.com/immichFrame/ImmichFrame),
portée par la branche `patches`, rebasée sur chaque version amont. Elle remplace
l'ancienne surcouche en iframe du cadre photo (`frame-overlay` dans `cluster-config`).

| Patch | Fichiers |
|---|---|
| Souvenirs : `GET`/`PUT /api/Memories`, **seule source de vérité** — `ShowMemories` est ignoré, l'état est gardé dans `$IMMICHFRAME_STATE_PATH/memories.json` (à défaut, le dossier de config). Masqués tant que l'API ne les a pas allumés, comme en amont | `IMemoriesSwitch`, `MemoriesSwitch`, `ToggleableAssetPool`, `MemoriesController` ; une ligne dans `PooledImmichFrameLogic` |
| Home Assistant : abonnement WebSocket aux entités de `HomeAssistant.yml`, états poussés | `ImmichFrame.WebApi/HomeAssistant/` |
| Notification : `POST`/`DELETE /api/Notification`, pour le `notify` REST de Home Assistant | `NotificationController`, `NotificationStore` |
| Surcouche : heure, capteurs, notification, tap qui rouvre Home Assistant | `home-assistant-overlay.svelte`, deux lignes dans `home-page.svelte` |
| Image | `.github/workflows/fork-image.yml` |

Le code amont n'est touché qu'en trois endroits (`PooledImmichFrameLogic.cs`,
`Program.cs`, `home-page.svelte`) : ce sont les seuls conflits possibles au rebase.

## `HomeAssistant.yml`

À côté de `Settings.yml`, hors de la base SQLite : l'interface d'admin ne le voit pas
et ne peut pas l'effacer. Sans ce fichier, la surcouche n'affiche que l'heure et les
notifications.

```yaml
Url: http://home-assistant.home-assistant.svc.cluster.local:8123
TokenFile: /secrets/ha-token
Sensors:
  - Entity: climate.salon_poele_thermostat   # current_temperature par défaut
    Icon: "🛋️"
  - Entity: sensor.jardin_thermometre         # l'état, et son unit_of_measurement
    Icon: "🌳"
  # Attribute: …   autre attribut que l'état
  # Unit: …        autre unité que celle de l'entité
```

⚠️ Sans capteur, le client ne se connecte pas : Home Assistant traite une liste
`entity_ids` vide comme « toutes les entités ».

## Monter de version

```bash
git fetch upstream --tags
git rebase --onto v<nouvelle> v<actuelle> patches
git push --force-with-lease origin patches
```

La CI teste, puis publie `ghcr.io/spartan-dou/immichframe:patches-<sha>`, à reporter
avec son digest dans `cluster-config`.
