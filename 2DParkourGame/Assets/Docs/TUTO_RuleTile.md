# Tuto : peindre un niveau avec la Tile Palette et le Rule Tile

Ce tuto se suit pas à pas dans Unity. Les liens pointent vers les fichiers du projet ou vers la documentation officielle d'Unity.

---

## 0. Générer les assets

Menu **PieParPie > Generate Art** (code : [PieParPieArt.cs](../Editor/PieParPieArt.cs)).

Cette commande crée ou met à jour tout ce qui suit, sans toucher à `Test_Scene` ni à `Game` :

| Quoi | Où |
|---|---|
| Sprites des tuiles de sol (16 combinaisons × 3 variantes) | `Assets/Art/Generated/Sprites/Ground/` |
| Sprites des objets : joueur, écharpe, pièce, pique, portail | `Assets/Art/Generated/Sprites/` |
| Une **Tile** par sprite de sol | `Assets/Art/Generated/Tiles/Ground_*.asset` |
| Le **Rule Tile** | `Assets/Art/Generated/Tiles/Ground_Rule.asset` |
| La **Tile Palette** | `Assets/Art/Generated/Palettes/PieParPie_Ground.prefab` |
| Le prefab de la bande de piques | `Assets/Prefabs/SpikeStrip.prefab` |
| La scène de démonstration | `Assets/Scenes/Art_Preview.unity` |

> Comme les assets sont générés par code, toute modification faite à la main sur les PNG serait écrasée à la prochaine génération. Pour changer le dessin, on modifie le code.

---

## 1. Les trois notions

1. **Tile** : une case peinte, qui associe un sprite et une collision. Ici, chaque tuile a une collision de la taille du bloc (`Collider Type = Grid`), même quand l'herbe ou les roches débordent du bloc.
2. **Tile Palette** : la « boîte de peinture ». Elle s'ouvre par **Window > 2D > Tile Palette**. On y choisit une tuile, puis on peint dans la scène.
3. **Rule Tile** : un pinceau « intelligent ». On peint toujours avec le même pinceau, et il choisit tout seul la bonne variante selon les cases voisines. Il vient du package *2D Tilemap Extras* ([doc Unity du Rule Tile](https://docs.unity3d.com/Packages/com.unity.2d.tilemap.extras@6.0/manual/RuleTile.html)).

Pour aller plus loin : [la doc Unity sur les Tilemaps](https://docs.unity3d.com/Manual/Tilemap.html).

---

## 2. Comment le Rule Tile choisit la tuile

Pour chaque case, le Rule Tile regarde **4 voisins** : haut, bas, gauche et droite. Chaque côté est soit **occupé** (même Rule Tile), soit **exposé** (vide, ou une autre tuile). Cela donne 2⁴ = **16 combinaisons**, et chacune a sa règle.

Le nom des sprites indique les **côtés exposés** :

| Lettre | Côté exposé | Effet visuel |
|---|---|---|
| `T` (top) | haut | bande d'herbe de 2 px, avec des brins qui dépassent au-dessus |
| `B` (bottom) | bas | bord irrégulier et petites roches qui pendent |
| `L` (left) | gauche | bord de terre sombre ; avec `T`, l'herbe déborde et descend sur la gauche |
| `R` (right) | droite | pareil à droite |
| `F` (full) | aucun | terre pleine (milieu) |

Exemples : `Ground_TL_0` est le dessus d'un bord gauche, `Ground_B_2` un dessous (variante 2), `Ground_TBLR_1` un bloc isolé.

Chaque règle a **3 variantes** tirées au hasard (`Output = Random`), pour que le sol ne paraisse pas répété.

Pour voir les règles : sélectionne `Ground_Rule.asset` dans le Project. L'Inspector affiche la liste des règles. Dans la grille 3 × 3 de chaque règle, la **flèche verte** signifie « voisin présent » et la **croix rouge** « pas de voisin ».

> **Piège** : le Rule Tile ne reconnaît comme voisins que les cases peintes avec **lui-même**. Une tuile manuelle posée à côté compte comme du vide, donc le bord du Rule Tile affichera de l'herbe ou un bord à cet endroit. C'est voulu : ça permet de forcer un bord.

---

## 3. Peindre pas à pas

1. Ouvre **Window > 2D > Tile Palette**.
2. Dans la liste déroulante en haut de la fenêtre, choisis la palette **PieParPie_Ground**.
3. En haut de la palette, sur une ligne seule, se trouve le **Rule Tile**. En dessous, les tuiles manuelles sont rangées par variante, dans trois groupes côte à côte :
   ```
   TL T TR  .  TLR      ← dessus
   L  F  R  .  LR       ← milieu
   BL B  BR .  BLR      ← dessous
   (ligne vide)
   TBL TB TBR . TBLR    ← plateforme d'un bloc d'épaisseur, bloc isolé
   ```
4. Dans la scène, sélectionne la Tilemap à peindre : par exemple `Grid > Ground` dans `Art_Preview`. Dans la Tile Palette, vérifie que **Active Tilemap** pointe bien sur elle.
5. Outils de la palette (raccourcis) :
   - **B** : pinceau. Clic gauche pour peindre.
   - **U** : rectangle. Remplit une zone d'un seul geste, pratique avec le Rule Tile.
   - **D** : gomme. On peut aussi peindre avec **Maj + clic**.
   - **I** : pipette. Reprend la tuile d'une case de la scène.
   - **G** : pot de peinture.
6. **Avec le Rule Tile** : peins librement des formes (sol, plateforme, pilier). Les bords, l'herbe et les roches se placent tout seuls, et se recalculent si tu ajoutes ou retires une case voisine.
7. **Avec une tuile manuelle** : pour un cas particulier, choisis la tuile exacte, par exemple un dessus sans herbe sur un côté. Elle ne changera jamais toute seule.

---

## 4. La collision

Pour que le joueur marche dessus, la Tilemap doit avoir un **Tilemap Collider 2D**. C'est déjà le cas sur `Ground` dans `Art_Preview`. Dans `Game`, le sol généré par [PieParPieSetup.cs](../Editor/PieParPieSetup.cs) ajoute aussi un **Composite Collider 2D**, qui fusionne les cases en un seul contour. C'est plus propre pour la physique.

> Si tu repeins le sol de `Game` avec un Composite Collider, la géométrie se régénère normalement toute seule. En cas de doute, sélectionne le Composite Collider 2D, puis **clic droit > Regenerate Geometry**.

---

## 5. La bande de piques (`SpikeStrip`)

Code : [SpikeStrip.cs](../Scripts/Entities/SpikeStrip.cs), poignée d'édition : [SpikeStripEditor.cs](../Editor/SpikeStripEditor.cs).

1. Glisse `Assets/Prefabs/SpikeStrip.prefab` dans la scène et pose son origine sur la surface, à l'endroit où commence la bande.
2. **Étirer** : sélectionne la bande, puis tire la **poignée carrée orange** au bout de la bande. Le nombre de piques (`count`, le « stock ») n'augmente que lorsqu'une pique entière tient. Le collider suit ce nombre, pas la position exacte de la poignée. On peut aussi taper `count` dans l'Inspector.
3. **Orienter** avec la rotation Z de l'objet :
   - `0` : au sol, piques vers le haut ;
   - `180` : au plafond, piques vers le bas ;
   - `90` : contre le côté gauche d'un mur, piques vers la gauche ;
   - `-90` : contre le côté droit d'un mur, piques vers la droite.
4. **Supports** : dans la scène, une base **verte** signifie que la pique repose sur quelque chose, une croix **rouge** qu'elle est dans le vide. En jeu, chaque pique fait un raycast vers son support : une pique sans support est **masquée**, et sa zone est **retirée du collider**. Le collider est découpé en un morceau par groupe de piques posées.
5. Deux piques par bloc, segment droit uniquement (pas de courbe).
6. **Dans et derrière la terre** : l'origine de la bande se pose sur la surface. Les piques sont enfoncées de 4 px (`sinkDepth`) et affichées derrière les tuiles (`sortingOrder = -1`), donc l'herbe et la terre recouvrent leur base. La zone mortelle ne couvre que la partie visible.

### Alignement automatique des pièges

Code : [TrapSnapper.cs](../Editor/TrapSnapper.cs). Il s'applique aux `SpikeStrip` et aux `Trap`.

Quand tu lâches un piège et que tu ne le touches plus pendant **3 secondes**, l'outil :
1. arrondit sa rotation au quart de tour le plus proche (bandes de piques seulement) ;
2. aligne le début de la bande, ou le bord gauche d'un `Trap`, sur la **grille des demi-blocs** ;
3. cherche une surface à moins d'un demi-bloc, avec le **même raycast** que celui qui masque les piques, et y colle le piège.

S'il ne trouve aucune surface, le piège est seulement aligné sur la grille, et un message l'indique dans la console. Chaque alignement s'annule avec **Ctrl+Z**. L'outil ne s'exécute jamais pendant un glisser ni en Play Mode.

---

## 6. La scène `Art_Preview`

Ouvre `Assets/Scenes/Art_Preview.unity`. Elle contient :
- du sol peint au Rule Tile : sol principal, plateforme fine, pilier, massif 3 × 3, bloc isolé ;
- le joueur avec son écharpe, dont la couleur est teintée en bleu pour l'exemple dans `Player_Preview > ScarfAnchor > Scarf` ;
- des pièces et le portail dans son arche ;
- quatre bandes de piques : au sol, au plafond, contre un mur, et une qui dépasse du bord d'une plateforme.

Lance le **Play** : la bande `Spikes_Edge_PartlyUnsupported` doit perdre ses piques qui sont dans le vide. Cette scène n'est pas dans le build, c'est seulement une vitrine.
