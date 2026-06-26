# Installation Unity sur Ubuntu (Linux) — Notes ASUQTR

## Version Unity requise
**6000.0.75f1** — disponible dans Unity Hub → Installs → Install Editor → Archive.

> Ne pas utiliser la branche `users/elliot` ou `Ubuntu_Unity_tryout` : elles requièrent Unity 6000.4.11f1 et causent des erreurs de types URP manquants.

## Installation Unity Hub

```bash
wget -qO - https://hub.unity3d.com/linux/keys/public | gpg --dearmor | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg > /dev/null
sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'
sudo apt update && sudo apt install -y unityhub libgconf-2-4
```

## Drivers NVIDIA (requis)

Sans les drivers NVIDIA, Unity tourne sur le GPU intégré AMD avec Mesa Vulkan 1.2 (trop vieux pour URP 6) → tous les matériaux s'affichent en rose.

```bash
sudo ubuntu-drivers autoinstall
# Redémarrer après l'installation
```

Vérification après redémarrage :
```bash
nvidia-smi
```

## Git LFS (requis)

Les scènes Unity (`.unity`), prefabs et assets binaires sont stockés en Git LFS. Sans ça, les scènes s'ouvrent vides.

```bash
sudo apt install git-lfs
git lfs install
git lfs fetch --all
git lfs checkout
```

## Problème : matériaux roses (pink materials)

**Cause :** Unity utilise Vulkan par défaut sur Linux. Si les drivers NVIDIA ne sont pas installés ou si Vulkan n'est pas supporté correctement, tous les shaders URP échouent.

**Fix temporaire** (avant installation drivers) :
```bash
# Lancer Unity Hub en forçant OpenGL
/opt/unityhub/unityhub -- --args -force-glcore
```

**Fix permanent :** installer les drivers NVIDIA (voir section ci-dessus).

## Problème : scènes vides à l'ouverture

**Cause :** Git LFS non installé — les fichiers `.unity` sont des pointeurs de 3 lignes au lieu des vraies scènes.

**Fix :** voir section Git LFS ci-dessus.

## Problème : erreurs "Missing types" URP

```
Missing types referenced from component UniversalRenderPipelineGlobalSettings
```

**Cause :** la branche utilisée a été sauvegardée avec une version de Unity/URP plus récente que celle installée.

**Fix :** utiliser la branche `upgrade-unity-6000-0-75f1` avec Unity 6000.0.75f1.

## Problème : modules manquants au démarrage

```
com.unity.modules.adaptiveperformance@1.0.0 not found
com.unity.modules.vectorgraphics@1.0.0 not found
```

**Cause :** ces modules ne sont pas inclus dans toutes les versions de Unity.

**Fix :** les retirer du `Packages/manifest.json`.

## Ouvrir le projet

1. Lancer Unity Hub
2. **Projects → Add project from disk**
3. Sélectionner `asuqtr-simulation/ASUQTR-AUV`
4. Choisir la version **6000.0.75f1**
5. Ouvrir la scène : `Assets → Scenes → Practice.unity`

## Architecture ROS2 ↔ Unity

Unity communique avec ROS2 via **rosbridge WebSocket** (port 9090) — pas de code ROS dans Unity.

| Unity publie | Topic ROS2 |
|---|---|
| IMU simulée | `/vectornav/imu` |
| Position | `/nav_node/position` |
| Vitesse | `/nav_node/velocity` |

| Unity reçoit | Topic ROS2 |
|---|---|
| Commandes moteurs | `/thruster_cmd` |

Pour lancer côté ROS2 (à venir) : `ros2 launch sub_launch unity_sim.launch.yaml`
