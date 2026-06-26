# Unity + ROS2 sur Ubuntu — Guide d'installation ASUQTR

## Branche de référence

**`Unity-LQR-dev`** — Simulation Unity pour le développement et test du contrôleur LQR sur Ubuntu.

| Élément | Version |
|---|---|
| Unity Editor | **6000.0.75f1** |
| URP | 17.0.4 |
| OS testé | **Ubuntu 20.04 LTS** (laptop ASUS ROG en dual boot Windows 11 / Ubuntu) |
| ROS2 | Humble |

### Dual boot Windows 11 + Ubuntu

Ce setup a été développé et testé sur un laptop en **dual boot Windows 11 / Ubuntu 20.04**.

> **Important avant d'installer :** Désactiver le **Secure Boot** et le **Fast Boot** dans le BIOS, et installer Windows **avant** Ubuntu — le bootloader Windows écrase tout, GRUB doit être installé en dernier.

Guides recommandés :
- [Dual Boot Windows 11 + Ubuntu — Guide complet (sysguides.com)](https://sysguides.com/dual-boot-windows-11-and-ubuntu)
- [Guide avec Secure Boot + chiffrement (mikekasberg.com)](https://www.mikekasberg.com/blog/2024/05/20/dual-boot-ubuntu-24-04-and-windows-with-encryption.html)

---

## ⚠️ Avertissement GPU sous Ubuntu

> **Si tu as un GPU NVIDIA, tu DOIS installer les drivers avant d'ouvrir Unity.**
>
> Sans drivers NVIDIA, Unity tourne sur le GPU intégré AMD/Intel avec Mesa Vulkan (version trop ancienne pour URP 6). Résultat : **tous les matériaux s'affichent en rose/magenta**.

---

## 1. Prérequis système

```bash
sudo apt update && sudo apt install -y git git-lfs curl wget
```

---

## 2. Drivers NVIDIA

```bash
sudo ubuntu-drivers autoinstall
sudo reboot
```

Vérification après redémarrage :
```bash
nvidia-smi
```
Tu devrais voir ton GPU (ex: RTX 3050) avec le driver actif.

> **Note :** Sur un laptop avec GPU hybride (NVIDIA + AMD intégré), les deux GPUs sont visibles dans `lspci`. Ubuntu utilise le GPU intégré par défaut — les drivers NVIDIA activent le RTX pour Unity.

---

## 3. Unity Hub

```bash
wget -qO - https://hub.unity3d.com/linux/keys/public | gpg --dearmor | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg > /dev/null

sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'

sudo apt update && sudo apt install -y unityhub libgconf-2-4
```

### Installer Unity 6000.0.75f1

1. Lancer Unity Hub : `unityhub`
2. Se connecter (compte Unity gratuit — licence **Personal**)
3. **Installs → Install Editor → Archive**
4. Chercher `6000.0.75f1` et installer avec **Linux Build Support (IL2CPP)**

---

## 4. Cloner le repo et configurer Git LFS

```bash
# Cloner le repo
git clone git@github.com:ASUQTR/asuqtr-simulation.git ~/asuqtr_projets/asuqtr-simulation

# Initialiser Git LFS (requis pour les scènes Unity)
git lfs install

# Aller sur la bonne branche
cd ~/asuqtr_projets/asuqtr-simulation
git checkout Unity-LQR-dev

# Télécharger les vrais fichiers LFS (scènes, assets binaires)
git lfs fetch --all
git lfs checkout
```

> **Pourquoi Git LFS ?** Les fichiers `.unity`, prefabs et assets binaires sont volumineux. Git LFS les stocke séparément et met un pointeur de 3 lignes dans le repo. Sans `git lfs checkout`, les scènes s'ouvrent vides.

---

## 5. Ouvrir le projet dans Unity

1. Lancer Unity Hub
2. **Projects → Add project from disk**
3. Sélectionner `asuqtr-simulation/ASUQTR-AUV`
4. Choisir la version **6000.0.75f1** (pas "Latest LTS")
5. Ouvrir la scène : `Assets → Scenes → Practice.unity`

---

## 6. Installer rosbridge (côté ROS2)

Unity communique avec ROS2 via WebSocket sur le port **9090**.

```bash
sudo apt install ros-humble-rosbridge-suite
```

### Lancer rosbridge

```bash
source /opt/ros/humble/setup.bash
ros2 launch rosbridge_server rosbridge_websocket_launch.xml
```

Unity se connecte automatiquement à `ws://localhost:9090` au démarrage de la simulation.

---

## 7. Architecture de communication

```
Unity (simulation physique)              ROS2 (ros2_workspace)
────────────────────────────             ────────────────────────────
NavImuPublisher      ──────────────────→ /vectornav/imu
NavPositionPublisher ──────────────────→ /nav_node/position
NavVelocityPublisher ──────────────────→ /nav_node/velocity

ThrusterReceiver     ←────────────────── /thruster_cmd
                                          ↑
                                         control_node (LQR)
```

Le `control_node` de `ros2_workspace` reçoit les données du simulateur et publie les commandes moteurs — **aucun code ROS dans Unity**, tout passe par WebSocket/rosbridge.

---

## 8. Lancer le stack ROS2 complet (à venir)

```bash
# Dans ros2_workspace
source workspace/install/setup.bash
ros2 launch sub_launch unity_sim.launch.yaml
```

> Le fichier `unity_sim.launch.yaml` est à créer dans `sub_launch` — il lance `control_node` + `rosbridge_websocket` sans les nodes hardware ni Gazebo.

---

## Problèmes connus et solutions

### Matériaux roses (pink materials)
**Cause :** Drivers NVIDIA manquants ou Vulkan trop ancien (Mesa).  
**Fix :** Installer les drivers NVIDIA (section 2) et redémarrer.  
**Fix temporaire :** Forcer OpenGL au lieu de Vulkan :
```bash
/opt/unityhub/unityhub -- --args -force-glcore
```

### Scènes vides à l'ouverture
**Cause :** Git LFS non initialisé — les fichiers `.unity` sont des pointeurs.  
**Fix :**
```bash
git lfs install && git lfs fetch --all && git lfs checkout
```

### Erreur "Missing types" URP
```
Missing types referenced from component UniversalRenderPipelineGlobalSettings
```
**Cause :** Mauvaise branche — assets sauvegardés avec une version Unity plus récente.  
**Fix :** Utiliser la branche `Unity-LQR-dev` avec Unity **6000.0.75f1**.

### Modules manquants au démarrage
```
com.unity.modules.adaptiveperformance@1.0.0 not found
```
**Cause :** Modules non inclus dans cette version de Unity.  
**Fix :** Les retirer de `ASUQTR-AUV/Packages/manifest.json`.

### Erreur "Trying to reload asset but can't find object on disk"
**Cause :** Avertissement normal au premier import ou lors d'un changement de version Unity.  
**Fix :** Ignorer — non bloquant.

### Mesh 'Body77' self-intersecting
**Cause :** Défaut de géométrie dans le modèle 3D du sous-marin.  
**Fix :** Ignorer — non bloquant, Unity corrige automatiquement.
