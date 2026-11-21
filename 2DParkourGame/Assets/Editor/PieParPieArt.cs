using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Outil editeur : genere par code le pixel art du jeu (32 px par bloc), les tiles, la Tile Palette,
/// le prefab SpikeStrip et la scene Art_Preview.</summary>
public static class PieParPieArt
{
    /// <summary>Dossier racine des assets generes.</summary>
    const string Root = "Assets/Art/Generated";
    /// <summary>Dossier des sprites.</summary>
    const string SpriteDir = Root + "/Sprites";
    /// <summary>Dossier des sprites du sol.</summary>
    const string GroundDir = Root + "/Sprites/Ground";
    /// <summary>Dossier des tiles.</summary>
    const string TileDir = Root + "/Tiles";
    /// <summary>Dossier de la palette.</summary>
    const string PaletteDir = Root + "/Palettes";
    /// <summary>Chemin de l'ancien Rule Tile du sol (supprime s'il existe encore).</summary>
    const string OldRuleTilePath = TileDir + "/Ground_Rule.asset";
    /// <summary>Chemin du prefab de la bande de piques.</summary>
    const string SpikePrefabPath = "Assets/Prefabs/SpikeStrip.prefab";
    /// <summary>Dossier des images d'animation du joueur (corps et echarpe).</summary>
    const string PoseDir = SpriteDir + "/Poses";
    /// <summary>Chemin du prefab GameManager (images d'animation et renderers assignes).</summary>
    const string GameManagerPrefabPath = "Assets/Prefabs/GameManager.prefab";
    /// <summary>Chemin du prefab Finish (arche et portail).</summary>
    const string FinishPrefabPath = "Assets/Prefabs/Finish.prefab";
    /// <summary>Chemin de la scene de previsualisation.</summary>
    const string PreviewScenePath = "Assets/Scenes/Art_Preview.unity";
    /// <summary>Pixels par unite (1 bloc = 32 px).</summary>
    const int PPU = 32;
    /// <summary>Marge autour d'un bloc (debordements autorises).</summary>
    const int M = 4;
    /// <summary>Taille d'une toile de bloc (bloc + marges).</summary>
    const int C = PPU + M * 2;
    /// <summary>Nombre de variantes aleatoires par tuile de sol.</summary>
    const int Variants = 3;

    /// <summary>Cote expose (sans voisin) : haut.</summary>
    const int T = 1;
    /// <summary>Cote expose : bas.</summary>
    const int B = 2;
    /// <summary>Cote expose : gauche.</summary>
    const int L = 4;
    /// <summary>Cote expose : droite.</summary>
    const int R = 8;

    /// <summary>Menu : genere tout le pixel art et ses assets, puis la scene de previsualisation.</summary>
    [MenuItem("PieParPie/Generate Art")]
    public static void GenerateAll()
    {
        if (EditorApplication.isPlaying)
        { Debug.LogError("[PieParPieArt] Quitter le Play Mode avant de generer."); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string d in new[] { SpriteDir, GroundDir, PoseDir, TileDir, PaletteDir }) Directory.CreateDirectory(d);

            WriteGroundPngs(); WriteObjectPngs(); WritePosePngs();
        }
        finally
        { AssetDatabase.StopAssetEditing(); }

        AssetDatabase.Refresh();

        Dictionary<int, Sprite[]> ground = ImportGround();
        Sprites s = ImportObjects();

        Dictionary<int, Tile[]> tiles = CreateTiles(ground);

        if (AssetDatabase.DeleteAsset(OldRuleTilePath)) Debug.Log("[PieParPieArt] Ancien Rule Tile supprime.");

        CreatePalette(tiles);
        GameObject spike = CreateSpikePrefab(s.Spike);
        UpdateFinishPrefab(s.Arch, s.Portal);
        ApplyPoses(ImportPoses());
        BuildPreviewScene(tiles, s, spike);

        AssetDatabase.SaveAssets();
        Debug.Log("[PieParPieArt] Pixel art, tiles, palette, poses, prefabs SpikeStrip et Finish, scene Art_Preview generes.");
    }

    /// <summary>Point d'entree en batchmode (-executeMethod PieParPieArt.RunBatch).</summary>
    public static void RunBatch() => GenerateAll();

    // ------------------------------------------------------------------ Couleurs

    /// <summary>Convertit "#rrggbb" en couleur.</summary>
    static Color32 Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }

    /// <summary>Pixel transparent.</summary>
    static readonly Color32 Clear = new(0, 0, 0, 0);
    /// <summary>Terre : base, sombre, bord, clair, cailloux.</summary>
    static readonly Color32 Dirt = Hex("#8a5a35"), DirtDark = Hex("#6b4226"), DirtEdge = Hex("#4e2f1a"), DirtLight = Hex("#a46c40"),
        Pebble = Hex("#9a8f85"), PebbleDark = Hex("#6f665e");
    /// <summary>Herbe : clair, moyen, sombre, ombre.</summary>
    static readonly Color32 GrassLight = Hex("#9be35a"), Grass = Hex("#5fbf3a"), GrassDark = Hex("#3d8f2e"), GrassShadow = Hex("#2c6b25");
    /// <summary>Roches pendantes : clair, base, sombre, contour.</summary>
    static readonly Color32 RockLight = Hex("#c3c6cc"), Rock = Hex("#9c9ea6"), RockDark = Hex("#6a6c74"), RockLine = Hex("#45464d");

    /// <summary>Palette du personnage et de l'echarpe, par caractere des grilles.</summary>
    static readonly Dictionary<char, Color32> CharColors = new()
    {
        ['K'] = Hex("#1f1a24"), ['H'] = Hex("#f2d05a"), ['h'] = Hex("#c9a23a"), ['L'] = Hex("#fff0a0"),
        ['S'] = Hex("#f2c29b"), ['s'] = Hex("#d69a76"), ['E'] = Hex("#2a2238"),
        ['T'] = Hex("#d23a3a"), ['t'] = Hex("#9e2a2f"), ['U'] = Hex("#ef6a5a"),
        ['P'] = Hex("#7d7f86"), ['p'] = Hex("#5a5c63"), ['Q'] = Hex("#6a6c73"),
        ['B'] = Hex("#4a3226"), ['b'] = Hex("#33221a"), ['W'] = Hex("#ffffff"), ['w'] = Hex("#bdbdbd"),
    };

    // ------------------------------------------------------------------ Toile

    /// <summary>Image en memoire (origine en bas a gauche), ecritures hors limites ignorees.</summary>
    sealed class Canvas
    {
        /// <summary>Largeur en pixels.</summary>
        public readonly int W;
        /// <summary>Hauteur en pixels.</summary>
        public readonly int H;
        /// <summary>Pixels (ligne par ligne depuis le bas).</summary>
        public readonly Color32[] Px;
        /// <summary>Cree une toile transparente.</summary>
        public Canvas(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }
        /// <summary>Vrai si (x, y) est dans la toile.</summary>
        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        /// <summary>Ecrit un pixel.</summary>
        public void Set(int x, int y, Color32 c) { if (In(x, y)) Px[y * W + x] = c; }
        /// <summary>Lit un pixel (transparent hors limites).</summary>
        public Color32 Get(int x, int y) => In(x, y) ? Px[y * W + x] : Clear;
        /// <summary>Vrai si le pixel est opaque.</summary>
        public bool Solid(int x, int y) => Get(x, y).a > 0;
    }

    /// <summary>Bruit deterministe dans [0, 1[ pour (x, y, graine).</summary>
    static float Rand(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;

            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    /// <summary>Dessine une grille de caracteres (premiere ligne = haut) avec sa ligne du bas en (x0, y0).</summary>
    static void DrawGrid(Canvas c, string[] rows, int x0, int y0)
    {
        for (int r = 0; r < rows.Length; r++)
            for (int i = 0; i < rows[r].Length; i++)
                if (CharColors.TryGetValue(rows[r][i], out Color32 col)) c.Set(x0 + i, y0 + rows.Length - 1 - r, col);
                else if (rows[r][i] != '.') Debug.LogWarning($"[PieParPieArt] Caractere inconnu '{rows[r][i]}' ligne {r}.");
    }

    // ------------------------------------------------------------------ Sol

    /// <summary>Nom lisible d'une combinaison de cotes exposes (F = plein, T haut, B bas, L gauche, R droite).</summary>
    static string MaskName(int m) => m == 0 ? "F" : $"{((m & T) != 0 ? "T" : "")}{((m & B) != 0 ? "B" : "")}{((m & L) != 0 ? "L" : "")}{((m & R) != 0 ? "R" : "")}";

    /// <summary>Chemin du PNG d'une tuile de sol.</summary>
    static string GroundPath(int m, int v) => $"{GroundDir}/Ground_{MaskName(m)}_{v}.png";

    /// <summary>Ecrit les 16 combinaisons de cotes exposes x variantes.</summary>
    static void WriteGroundPngs() { for (int m = 0; m < 16; m++) for (int v = 0; v < Variants; v++) WritePng(GroundPath(m, v), GroundTile(m, v)); }

    /// <summary>Dessine une tuile de sol : terre, bords exposes, herbe en haut, dessous irregulier avec roches pendantes.</summary>
    static Canvas GroundTile(int m, int v)
    {
        var c = new Canvas(C, C);
        int seed = 101 + m * 31 + v * 977, x0 = M, x1 = M + PPU - 1, y0 = M, y1 = M + PPU - 1;

        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) c.Set(x, y, DirtPixel(x, y, seed));

        if ((m & L) != 0) SideEdge(c, x0, 1, seed, (m & T) != 0);
        if ((m & R) != 0) SideEdge(c, x1, -1, seed + 5, (m & T) != 0);
        if ((m & B) != 0) Bottom(c, m, seed);
        if ((m & T) != 0) DrawGrass(c, m, seed);

        return c;
    }

    /// <summary>Couleur d'un pixel de terre : grain fin, taches plus larges, cailloux.</summary>
    static Color32 DirtPixel(int x, int y, int seed)
    {
        float n = Rand(x, y, seed), blob = Rand(x / 3, y / 3, seed + 7);

        if (Rand(x / 2, y / 2, seed + 13) < 0.035f) return n < 0.5f ? Pebble : PebbleDark;
        if (n < 0.10f) return DirtDark;
        if (n > 0.92f || blob > 0.86f) return DirtLight;
        return blob < 0.12f ? DirtDark : Dirt;
    }

    /// <summary>Bord lateral expose : colonne sombre irreguliere (dir = 1 bord gauche, -1 bord droit).</summary>
    static void SideEdge(Canvas c, int x, int dir, int seed, bool grassTop)
    {
        int top = M + PPU - 1 - (grassTop ? 2 : 0);

        for (int y = M; y <= top; y++)
        {
            bool bite = Rand(x, y, seed) < 0.22f && y < top - 1;

            if (bite)
            { c.Set(x, y, Clear); c.Set(x + dir, y, DirtEdge); }
            else
            { c.Set(x, y, DirtEdge); if (Rand(x + dir, y, seed) < 0.5f) c.Set(x + dir, y, DirtDark); }
        }
    }

    /// <summary>Dessous expose : bord irregulier puis petites roches qui pendent sous le bloc.</summary>
    static void Bottom(Canvas c, int m, int seed)
    {
        int xa = M, xb = M + PPU - 1;
        var depth = new int[C];

        for (int x = xa; x <= xb; x++)
        {
            int d = (int)(Rand(x / 2, 0, seed) * 3f);

            if (((m & L) != 0 && x <= xa + 1) || ((m & R) != 0 && x >= xb - 1)) d = Mathf.Max(d, 2);

            depth[x] = d;

            for (int k = 0; k < d; k++) c.Set(x, M + k, Clear);

            c.Set(x, M + d, DirtEdge);

            if (Rand(x, 1, seed) < 0.5f) c.Set(x, M + d + 1, DirtDark);
        }

        int rocks = 2 + (int)(Rand(3, 3, seed) * 2f);

        for (int i = 0; i < rocks; i++)
        {
            int cx = xa + 3 + (int)(Rand(i, 9, seed) * (PPU - 6)), w = 3 + (int)(Rand(i, 11, seed) * 2f), h = 2 + (int)(Rand(i, 13, seed) * 3f);
            int top = M + depth[Mathf.Clamp(cx, xa, xb)];

            DrawRock(c, cx - w / 2, top, w, h);
        }
    }

    /// <summary>Petite roche dont le haut s'accroche en (x, top) et qui pend vers le bas.</summary>
    static void DrawRock(Canvas c, int x, int top, int w, int h)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                bool corner = (j == h - 1) && (i == 0 || i == w - 1) && w > 2;

                if (corner) continue;

                bool edge = i == 0 || i == w - 1 || j == h - 1;
                Color32 col = edge ? RockLine : (i == 1 && j == 0) ? RockLight : (i >= w - 2 || j >= h - 2) ? RockDark : Rock;

                c.Set(x + i, top - 1 - j, col);
            }
    }

    /// <summary>Herbe : bande de 2 px en haut (6,25 %), brins qui depassent au-dessus, et qui descendent sur les cotes exposes.</summary>
    static void DrawGrass(Canvas c, int m, int seed)
    {
        int y1 = M + PPU - 1, xa = M - ((m & L) != 0 ? 1 : 0), xb = M + PPU - 1 + ((m & R) != 0 ? 1 : 0);

        for (int x = xa; x <= xb; x++)
        {
            c.Set(x, y1, GrassLight); c.Set(x, y1 - 1, Grass);

            float r = Rand(x, 50, seed);

            if (r < 0.40f) c.Set(x, y1 - 2, GrassDark);
            if (r < 0.12f) c.Set(x, y1 - 3, GrassShadow);

            float t = Rand(x, 60, seed);

            if (t < 0.45f) c.Set(x, y1 + 1, t < 0.2f ? GrassLight : Grass);
            if (t < 0.12f) c.Set(x, y1 + 2, GrassLight);
        }

        if ((m & L) != 0) GrassSide(c, M - 1, 1, seed);
        if ((m & R) != 0) GrassSide(c, M + PPU, -1, seed + 3);
    }

    /// <summary>Herbe qui deborde sur un cote expose et descend de quelques pixels (dir vers l'interieur du bloc).</summary>
    static void GrassSide(Canvas c, int x, int dir, int seed)
    {
        int y1 = M + PPU - 1, len = 3 + (int)(Rand(x, 70, seed) * 4f);

        for (int k = 0; k < len; k++)
        { c.Set(x, y1 - k, k < 2 ? Grass : GrassDark); if (k < len - 1) c.Set(x + dir, y1 - k, k < 2 ? GrassLight : Grass); }

        c.Set(x, y1 - len, GrassShadow);
        c.Set(x - dir, y1, Grass); if (Rand(x, 80, seed) < 0.6f) c.Set(x - dir, y1 + 1, GrassLight);
    }

    // ------------------------------------------------------------------ Objets

    /// <summary>Grille du personnage (vu de profil vers la droite), 16 px de large, pieds sur la derniere ligne.</summary>
    static readonly string[] PlayerRows =
    {
        "....KKKKKKK.....",
        "..KKHHHLLHHKK...",
        ".KHHHHHHLLHHHK..",
        ".KHHHHHHHHHHHHK.",
        "KhHHHHHHHHHHHHHK",
        "KhhHHHHHHhHHHHK.",
        "KhhHHhSSHHSHHK..",
        "KhhhSSSSSSSSSK..",
        ".KhhSSSSSSESSK..",
        ".KhhSSSSSSESSSK.",
        ".KhSSSSSSSSSSK..",
        "..KKsSSSSSsSK...",
        "....KKsSSSKK....",
        "....KSSSSK......",
        "...KTTTTTTTK....",
        "..KUTTTTTTTTK...",
        ".KUTKTTTTTTKTK..",
        ".KTtKTTTTTTKtK..",
        ".KSSKTTTTTTKSSK.",
        ".KSSKttttttKSSK.",
        "..KKKPPPPPPKKK..",
        "....KPPPPPPPK...",
        "....KPQPPPQPK...",
        "....KPQPKPQPK...",
        "....KPPPKPPPK...",
        "....KpPK.KpPK...",
        "....KpPK.KpPK...",
        "...KBBBK.KBBBK..",
        "...KbbbK.KbbbbK.",
    };

    /// <summary>Grille de l'echarpe (blanche, teintee en code), meme repere que le personnage.</summary>
    static readonly string[] ScarfRows =
    {
        "...KKKKKKKK.....",
        "..KWWWWWWWWK....",
        ".KwWWWWWWWwK....",
        "KWwKKKKKKKK.....",
        "KWWK............",
        ".KwWK...........",
        "..KK............",
    };

    /// <summary>Ligne de la grille du personnage ou commence l'echarpe (cou).</summary>
    const int ScarfRow = 12;

    /// <summary>Ecrit les PNG des objets : joueur, echarpe, piece, pique, portail.</summary>
    static void WriteObjectPngs()
    {
        int px = (C - 16) / 2;

        var player = new Canvas(C, C); DrawGrid(player, PlayerRows, px, M); WritePng($"{SpriteDir}/Player.png", player);
        var scarf = new Canvas(C, C); DrawGrid(scarf, ScarfRows, px, M + PlayerRows.Length - ScarfRow - ScarfRows.Length); WritePng($"{SpriteDir}/Scarf.png", scarf);
        WritePng($"{SpriteDir}/Coin.png", Coin());
        WritePng($"{SpriteDir}/Spike.png", Spike());
        WritePng($"{SpriteDir}/Arch.png", Gate(true, false));
        WritePng($"{SpriteDir}/Portal.png", Gate(false, true));
    }

    /// <summary>Piece doree classique : ovale vertical, contour, fente centrale, reflet.</summary>
    static Canvas Coin()
    {
        var c = new Canvas(PPU, PPU);
        Color32 line = Hex("#6e4508"), rim = Hex("#d99a1c"), face = Hex("#f8c838"), shine = Hex("#fff2a0"), slot = Hex("#c8901a"), slotLight = Hex("#ffe27a");
        const float cx = 15.5f, cy = 15.5f, rx = 7f, ry = 11f;
        bool Inside(int x, int y) { float dx = (x - cx) / rx, dy = (y - cy) / ry; return dx * dx + dy * dy <= 1f; }

        for (int y = 0; y < PPU; y++)
            for (int x = 0; x < PPU; x++)
            {
                if (!Inside(x, y)) continue;

                bool edge = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1);
                float dx = (x - cx) / rx, dy = (y - cy) / ry, d = dx * dx + dy * dy;
                Color32 col = edge ? line : d > 0.62f ? rim : face;

                if (!edge && d <= 0.62f && Mathf.Abs(x - cx) < 1f && Mathf.Abs(dy) < 0.55f) col = x < cx ? slot : slotLight;
                if (!edge && dx < -0.25f && dx > -0.6f && dy > 0.1f && dy < 0.6f) col = shine;

                c.Set(x, y, col);
            }

        return c;
    }

    /// <summary>Pique : triangle grise de 16 x 16 px, ombre cylindrique et reflet pres de la pointe (aspect arrondi).</summary>
    static Canvas Spike()
    {
        const int S = PPU / 2;
        var c = new Canvas(S, S);
        Color32 line = Hex("#34363d"), light = Hex("#c9ced6"), mid = Hex("#9aa0a9"), dark = Hex("#6c717a"), shine = Hex("#f4f7fb"), foot = Hex("#555a62");
        float Half(int y) => S * 0.5f * (1f - (y + 0.5f) / S);
        bool Inside(int x, int y) => y >= 0 && y < S && Mathf.Abs(x + 0.5f - S * 0.5f) <= Half(y) + 0.25f;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                if (!Inside(x, y)) continue;

                bool edge = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y + 1);
                float u = (x + 0.5f - S * 0.5f) / Mathf.Max(Half(y), 0.5f);
                Color32 col = edge ? line : y == 0 ? foot : u < -0.35f ? light : u < 0.25f ? mid : dark;

                if (!edge && y >= 6 && y <= 10 && u > -0.55f && u < -0.05f && (x + y) % 3 != 0) col = shine;

                c.Set(x, y, col);
            }

        return c;
    }

    /// <summary>Arche en bois aux bords droits (1 bloc de large, 4 de haut) et/ou portail rose-violet a l'interieur, sur deux images separees.</summary>
    static Canvas Gate(bool drawWood, bool drawSwirl)
    {
        int w = PPU, h = PPU * 4;
        var c = new Canvas(w, h);
        Color32 wood = Hex("#8b5a2b"), woodLight = Hex("#b07840"), woodDark = Hex("#5f3a1a"), woodLine = Hex("#3a2210"), nail = Hex("#c9c9c9");
        Color32[] swirl = { Hex("#3a1670"), Hex("#5a26b0"), Hex("#7a3cff"), Hex("#b04ce0"), Hex("#ff6ad5"), Hex("#ffb8ee") };
        const int post = 4, beam = 8, sill = 3;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool isPost = x < post || x >= w - post, isBeam = y >= h - beam, isSill = y < sill;

                if (isPost || isBeam || isSill)
                {
                    if (!drawWood) continue;

                    bool border = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                    bool outline = border || (isBeam && y == h - beam && !isPost) || (isPost && !isBeam && (x == post - 1 || x == w - post));
                    float g = isBeam || isSill ? Rand(x / 5, y, 41) : Rand(x, y / 5, 43);

                    c.Set(x, y, outline ? woodLine : g < 0.2f ? woodDark : g > 0.8f ? woodLight : wood);
                    continue;
                }

                if (!drawSwirl) continue;

                float nx = (x + 0.5f - w * 0.5f) / (w * 0.5f - post), ny = (y + 0.5f - (sill + (h - beam - sill) * 0.5f)) / ((h - beam - sill) * 0.5f);
                float r = Mathf.Sqrt(nx * nx + ny * ny * 0.35f), a = Mathf.Atan2(ny, nx) / (Mathf.PI * 2f);
                float t = Mathf.Repeat(a * 2f + r * 2.2f, 1f);
                int k = Mathf.Clamp(Mathf.FloorToInt((1f - Mathf.Clamp01(r)) * 3f + t * 2.5f), 0, swirl.Length - 1);

                if (r < 0.18f) k = swirl.Length - 1;
                if (x == post || x == w - post - 1 || y == sill || y == h - beam - 1) k = 0;

                c.Set(x, y, Rand(x, y, 47) < 0.012f ? (Color32)Color.white : swirl[k]);
            }

        if (drawWood) foreach (int nxp in new[] { 1, w - 2 })
        { c.Set(nxp, h - 3, nail); c.Set(nxp, h - beam + 2, nail); }

        return c;
    }

    // ------------------------------------------------------------------ Poses du joueur

    /// <summary>Morceau du personnage : des lignes placees a partir de la ligne Top de la grille complete (0 = haut, '.' = transparent).</summary>
    readonly struct Part
    {
        /// <summary>Ligne de la grille complete ou commence le morceau.</summary>
        public readonly int Top;
        /// <summary>Lignes du morceau, la premiere en haut.</summary>
        public readonly string[] Rows;

        /// <summary>Cree un morceau.</summary>
        public Part(int top, params string[] rows) { Top = top; Rows = rows; }
    }

    /// <summary>Une image d'animation : morceaux decales (dx vers la droite, dy vers le haut, en pixels) et decalage de la tete (echarpe).</summary>
    readonly struct Pose
    {
        /// <summary>Morceaux dans l'ordre de dessin (le dernier passe devant).</summary>
        public readonly (Part part, int dx, int dy)[] Layers;
        /// <summary>Decalage horizontal de la tete, suivi par l'echarpe.</summary>
        public readonly int HeadX;
        /// <summary>Decalage vertical de la tete, suivi par l'echarpe.</summary>
        public readonly int HeadY;

        /// <summary>Cree une image a partir des morceaux : bras arriere, jambes, torse, tete, bras avant.
        /// (ux, uy) decale tout le haut du corps, (hx, hy) la tete en plus.</summary>
        public Pose(Part head, Part torso, Part legs, (Part p, int dx, int dy) front, (Part p, int dx, int dy) back, int ux = 0, int uy = 0, int hx = 0, int hy = 0)
        {
            HeadX = ux + hx; HeadY = uy + hy;
            Layers = new[] { (back.p, back.dx + ux, back.dy + uy), (legs, 0, 0), (torso, ux, uy), (head, HeadX, HeadY), (front.p, front.dx + ux, front.dy + uy) };
        }
    }

    /// <summary>Copie des lignes de la grille du personnage.</summary>
    static string[] Rows(int from, int count) { var r = new string[count]; System.Array.Copy(PlayerRows, from, r, 0, count); return r; }

    /// <summary>Tete dont les deux lignes des yeux (8 et 9) sont remplacees.</summary>
    static Part Head(string eyesTop, string eyesBottom) { string[] r = Rows(0, 14); r[8] = eyesTop; r[9] = eyesBottom; return new(0, r); }

    /// <summary>Tete : normale, plissee par l'effort, yeux fermes.</summary>
    static readonly Part HeadOpen = new(0, Rows(0, 14)), HeadSquint = Head(".KhhSSSSSKKSSK..", ".KhhSSSSSSSSSSK."),
        HeadClosed = Head(".KhhSSSSSSSSSK..", ".KhhSSSSSKKSSSK.");

    /// <summary>Torse sans les bras : normal, et t-shirt gonfle par l'air.</summary>
    static readonly Part Torso = new(14, "...KTTTTTTTK....", "...KTTTTTTTTK...", "....KTTTTTTK....", "....KTTTTTTK....", "....KTTTTTTK....", "....KttttttK...."),
        TorsoPuffed = new(14, "...KTTTTTTTK....", "...KTTTTTTTTK...", "....KTTTTTTK....", "...KTTTTTTTTK...", "...KTTTTTTTTK...", "...KttttttttK...");

    /// <summary>Bras le long du corps : avant (gauche de la grille) et arriere (droite).</summary>
    static readonly Part ArmFront = new(15, "..KU", ".KUTK", ".KTtK", ".KSSK", ".KSSK", "..KKK"),
        ArmBack = new(16, "...........KTK..", "...........KtK..", "...........KSSK.", "...........KSSK.", "...........KKK..");

    /// <summary>Bras ecartes a l'horizontale (avant vers la gauche, a decaler de -3 ; arriere vers la droite).</summary>
    static readonly Part ArmFrontOut = new(15, "..KKKKK", "KSSTUTK", "KSStTtK", ".KKKKK."),
        ArmBackOut = new(15, "...........KKKKK.", "..........KTUTSSK", "..........KtTtSSK", "...........KKKKK.");

    /// <summary>Bras tendu vers l'avant et le bas (mains sur la pique).</summary>
    static readonly Part ArmReach = new(15, "..KKKK..........", ".KUTTTKK........", ".KTtttTSKK......", "..KKKKSSSSKK....", "......KKKSSSK...", ".........KKK....");

    /// <summary>Jambes : normales, sur la pointe des pieds, ecartees pieds pointes, accroupies (2 px plus courtes).</summary>
    static readonly Part Legs = new(20, Prepend(".....PPPPPP.....", Rows(21, 8))),
        LegsToe = new(20, Prepend(".....PPPPPP.....", Rows(21, 6), "...KBBBK.KBBBK..", "....KbbK..KbbK..")),
        LegsApart = new(20, ".....PPPPPP.....", "....KPPPPPPPK...", "....KPQPPPQPK...", "...KPQPKKPQPK...", "...KPPPK.KPPPK..",
            "..KpPK....KpPK..", "..KpPK....KpPK..", "..KBBK....KBBK..", "...KbK....KbK..."),
        LegsCrouch = new(22, ".....PPPPPP.....", "...KPPPPPPPPK...", "...KPQPPPPQPK...", "..KPPQPKKPQPPK..", "..KpPPK..KPPpK..",
            "..KBBBK..KBBBK..", ".KbbbbK..KbbbbK.");

    /// <summary>Concatene une ligne, des lignes et d'autres lignes.</summary>
    static string[] Prepend(string first, string[] middle, params string[] last)
    {
        var r = new List<string> { first };
        r.AddRange(middle); r.AddRange(last);

        return r.ToArray();
    }

    /// <summary>Saut sur place, 3 transitions de 2 images : impulsion (accroupi, pointe des pieds), montee (bras un peu leves,
    /// t-shirt gonfle, jambes ecartees, pieds pointes vers le sol), sommet (redescente des bras, gardee jusqu'a l'atterrissage).</summary>
    static readonly Pose[] JumpPoses =
    {
        new(HeadOpen, Torso, LegsCrouch, (ArmFront, 0, 0), (ArmBack, 0, 0), 0, -2),
        new(HeadOpen, Torso, LegsToe, (ArmFront, 0, 1), (ArmBack, 0, 1)),
        new(HeadOpen, TorsoPuffed, LegsApart, (ArmFront, 0, 1), (ArmBack, 0, 1)),
        new(HeadOpen, TorsoPuffed, LegsApart, (ArmFront, 0, 2), (ArmBack, 0, 2)),
        new(HeadOpen, TorsoPuffed, LegsApart, (ArmFront, 0, 2), (ArmBack, 0, 2), 0, 1),
        new(HeadOpen, TorsoPuffed, LegsApart, (ArmFront, 0, 1), (ArmBack, 0, 1)),
    };

    /// <summary>Empale sur une pique de mur, 3 transitions de 2 images : il remarque (bras ecartes, bras sur la pique),
    /// il tente de s'extirper (effort 1, effort 2), la tete s'effondre sur la pique (yeux ouverts, yeux fermes).</summary>
    static readonly Pose[] ImpalePoses =
    {
        new(HeadOpen, Torso, Legs, (ArmFrontOut, -3, 1), (ArmBackOut, 0, 1)),
        new(HeadOpen, Torso, Legs, (ArmReach, 0, 0), (ArmReach, 1, 1)),
        new(HeadSquint, Torso, Legs, (ArmReach, 0, 0), (ArmReach, 1, 1), -1, 0),
        new(HeadSquint, Torso, LegsToe, (ArmReach, 0, 0), (ArmReach, 1, 1), -2, -1),
        new(HeadOpen, Torso, LegsToe, (ArmFront, 0, -1), (ArmBack, 0, -1), 0, 0, 1, -2),
        new(HeadClosed, Torso, LegsToe, (ArmFront, 0, -1), (ArmBack, 0, -1), 0, 0, 1, -2),
    };

    /// <summary>Animations image par image du joueur, par nom de fichier.</summary>
    static readonly (string name, Pose[] poses)[] PoseClips = { ("Jump", JumpPoses), ("Impale", ImpalePoses) };

    /// <summary>Chemin du PNG d'une image d'animation (what = Player ou Scarf).</summary>
    static string PosePath(string what, string clip, int i) => $"{PoseDir}/{what}_{clip}_{i}.png";

    /// <summary>Ecrit le corps et l'echarpe de chaque image d'animation.</summary>
    static void WritePosePngs()
    {
        int px = (C - 16) / 2;

        foreach ((string name, Pose[] poses) in PoseClips)
            for (int i = 0; i < poses.Length; i++)
            {
                var body = new Canvas(C, C); DrawPose(body, poses[i], px); WritePng(PosePath("Player", name, i), body);

                var scarf = new Canvas(C, C);
                DrawGrid(scarf, ScarfRows, px + poses[i].HeadX, M + PlayerRows.Length - ScarfRow - ScarfRows.Length + poses[i].HeadY);
                WritePng(PosePath("Scarf", name, i), scarf);
            }
    }

    /// <summary>Dessine les morceaux d'une image, pieds de la grille complete en y = M.</summary>
    static void DrawPose(Canvas c, Pose p, int px)
    {
        foreach ((Part part, int dx, int dy) in p.Layers)
            for (int r = 0; r < part.Rows.Length; r++)
            {
                int y = M + PlayerRows.Length - 1 - (part.Top + r) + dy;

                for (int i = 0; i < part.Rows[r].Length; i++)
                    if (CharColors.TryGetValue(part.Rows[r][i], out Color32 col)) c.Set(px + i + dx, y, col);
                    else if (part.Rows[r][i] != '.') Debug.LogWarning($"[PieParPieArt] Caractere inconnu '{part.Rows[r][i]}' dans une pose.");
            }
    }

    // ------------------------------------------------------------------ Import

    /// <summary>Ecrit une toile en PNG.</summary>
    static void WritePng(string path, Canvas c)
    {
        var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false);
        tex.SetPixels32(c.Px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    /// <summary>Regle l'import d'un PNG en sprite pixel art (32 PPU, point, sans compression) et retourne le sprite.</summary>
    static Sprite ImportSprite(string path, Vector2 pivot)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter ti)
        { Debug.LogError($"[PieParPieArt] Import impossible : {path}"); return null; }

        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PPU; ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp;

        var st = new TextureImporterSettings(); ti.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom; st.spritePivot = pivot; st.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(st); ti.SaveAndReimport();

        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (s == null) Debug.LogError($"[PieParPieArt] Sprite introuvable apres import : {path}");

        return s;
    }

    /// <summary>Importe toutes les tuiles de sol (pivot au centre du bloc).</summary>
    static Dictionary<int, Sprite[]> ImportGround()
    {
        var d = new Dictionary<int, Sprite[]>();

        for (int m = 0; m < 16; m++)
        {
            d[m] = new Sprite[Variants];

            for (int v = 0; v < Variants; v++) d[m][v] = ImportSprite(GroundPath(m, v), new Vector2(0.5f, 0.5f));
        }

        return d;
    }

    /// <summary>Sprites des objets.</summary>
    struct Sprites
    {
        /// <summary>Corps du joueur.</summary>
        public Sprite Player;
        /// <summary>Echarpe (blanche).</summary>
        public Sprite Scarf;
        /// <summary>Piece.</summary>
        public Sprite Coin;
        /// <summary>Une pique.</summary>
        public Sprite Spike;
        /// <summary>Arche en bois vide.</summary>
        public Sprite Arch;
        /// <summary>Portail seul (interieur de l'arche).</summary>
        public Sprite Portal;
    }

    /// <summary>Importe les sprites des objets avec leurs pivots (pieds, centre, base).</summary>
    static Sprites ImportObjects()
    {
        var feet = new Vector2(0.5f, (float)M / C);

        return new Sprites
        {
            Player = ImportSprite($"{SpriteDir}/Player.png", feet), Scarf = ImportSprite($"{SpriteDir}/Scarf.png", feet),
            Coin = ImportSprite($"{SpriteDir}/Coin.png", new Vector2(0.5f, 0.5f)), Spike = ImportSprite($"{SpriteDir}/Spike.png", new Vector2(0.5f, 0f)),
            Arch = ImportSprite($"{SpriteDir}/Arch.png", new Vector2(0.5f, 0f)), Portal = ImportSprite($"{SpriteDir}/Portal.png", new Vector2(0.5f, 0f)),
        };
    }

    /// <summary>Importe les images d'animation (pivot aux pieds), par nom de clip : corps et echarpe.</summary>
    static Dictionary<string, (Sprite[] body, Sprite[] scarf)> ImportPoses()
    {
        var feet = new Vector2(0.5f, (float)M / C);
        var d = new Dictionary<string, (Sprite[], Sprite[])>();

        foreach ((string name, Pose[] poses) in PoseClips)
        {
            var body = new Sprite[poses.Length]; var scarf = new Sprite[poses.Length];

            for (int i = 0; i < poses.Length; i++)
            { body[i] = ImportSprite(PosePath("Player", name, i), feet); scarf[i] = ImportSprite(PosePath("Scarf", name, i), feet); }

            d[name] = (body, scarf);
        }

        return d;
    }

    // ------------------------------------------------------------------ Tiles

    /// <summary>Cree ou met a jour un asset (le GUID est conserve si l'asset existe deja) ; son nom interne est celui du fichier.</summary>
    static T SaveAsset<T>(T fresh, string path) where T : Object
    {
        fresh.name = Path.GetFileNameWithoutExtension(path);

        T existing = AssetDatabase.LoadAssetAtPath<T>(path);

        if (existing == null)
        { AssetDatabase.CreateAsset(fresh, path); return fresh; }

        EditorUtility.CopySerialized(fresh, existing); Object.DestroyImmediate(fresh); EditorUtility.SetDirty(existing);

        return existing;
    }

    /// <summary>Cree une Tile (collision de la taille du bloc) par sprite de sol.</summary>
    static Dictionary<int, Tile[]> CreateTiles(Dictionary<int, Sprite[]> ground)
    {
        var d = new Dictionary<int, Tile[]>();

        for (int m = 0; m < 16; m++)
        {
            d[m] = new Tile[Variants];

            for (int v = 0; v < Variants; v++)
            {
                var t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = ground[m][v]; t.colliderType = Tile.ColliderType.Grid;

                d[m][v] = SaveAsset(t, $"{TileDir}/Ground_{MaskName(m)}_{v}.asset");
            }
        }

        return d;
    }

    /// <summary>Position d'une combinaison dans la palette (un bloc 3x3, une colonne, une ligne, un bloc isole).</summary>
    static Vector2Int PalettePos(int m) => m switch
    {
        T | L => new(0, 2), T => new(1, 2), T | R => new(2, 2),
        L => new(0, 1), 0 => new(1, 1), R => new(2, 1),
        B | L => new(0, 0), B => new(1, 0), B | R => new(2, 0),
        T | L | R => new(4, 2), L | R => new(4, 1), B | L | R => new(4, 0),
        T | B | L => new(0, -2), T | B => new(1, -2), T | B | R => new(2, -2), _ => new(4, -2),
    };

    /// <summary>Cree la Tile Palette : toutes les tuiles rangees par variante (un groupe par variante, cote a cote).</summary>
    static void CreatePalette(Dictionary<int, Tile[]> tiles)
    {
        string path = $"{PaletteDir}/PieParPie_Ground.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);

        GameObject asset = GridPaletteUtility.CreateNewPalette(PaletteDir, "PieParPie_Ground", GridLayout.CellLayout.Rectangle,
            GridPalette.CellSizing.Manual, Vector3.one, GridLayout.CellSwizzle.XYZ);

        if (asset == null)
        { Debug.LogError("[PieParPieArt] Creation de la palette impossible."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            Tilemap map = root.GetComponentInChildren<Tilemap>();

            if (map == null)
            { Debug.LogError("[PieParPieArt] Palette sans Tilemap."); return; }

            for (int v = 0; v < Variants; v++)
                for (int m = 0; m < 16; m++)
                { Vector2Int p = PalettePos(m); map.SetTile(new Vector3Int(p.x + v * 6, p.y, 0), tiles[m][v]); }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ------------------------------------------------------------------ Prefab et scene

    /// <summary>Cree le prefab SpikeStrip (racine seule : les visuels sont crees dans chaque instance).</summary>
    static GameObject CreateSpikePrefab(Sprite spike)
    {
        var go = new GameObject("SpikeStrip");

        try
        {
            go.AddComponent<BoxCollider2D>().isTrigger = true;

            var strip = go.AddComponent<SpikeStrip>();
            var so = new SerializedObject(strip);
            so.FindProperty("spikeSprite").objectReferenceValue = spike;
            so.FindProperty("count").intValue = 2;
            so.FindProperty("sortingOrder").intValue = -1;
            so.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(go, SpikePrefabPath);
        }
        finally
        { Object.DestroyImmediate(go); }
    }

    /// <summary>Met a jour le prefab Finish : arche vide en enfant, portail en enfant (affiche seulement une fois les pieces recoltees),
    /// pivot aux pieds, zone de contact a l'interieur de l'arche. Les composants existants sont gardes (references des scenes conservees).</summary>
    static void UpdateFinishPrefab(Sprite arch, Sprite portal)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FinishPrefabPath) == null)
        { Debug.LogWarning($"[PieParPieArt] {FinishPrefabPath} absent : lancer PieParPie > Setup puis regenerer."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(FinishPrefabPath);

        try
        {
            if (!root.TryGetComponent(out Finish finish))
            { Debug.LogError("[PieParPieArt] Le prefab Finish n'a pas de composant Finish."); return; }

            if (root.TryGetComponent(out SpriteRenderer old)) Object.DestroyImmediate(old);

            root.transform.localScale = Vector3.one;
            if (!root.TryGetComponent(out BoxCollider2D box)) box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true; box.size = new Vector2(0.75f, 3.5f); box.offset = new Vector2(0f, 1.85f);

            Child(root.transform, "Arch", arch, 1);
            GameObject p = Child(root.transform, "Portal", portal, 0);

            var so = new SerializedObject(finish);
            so.FindProperty("portal").objectReferenceValue = p;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, FinishPrefabPath);
        }
        finally
        { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Trouve ou cree un enfant avec un SpriteRenderer.</summary>
    static GameObject Child(Transform parent, string name, Sprite sprite, int order)
    {
        Transform t = parent.Find(name);
        SpriteRenderer r = t == null ? MakeSprite(parent, name, sprite, order, Vector3.zero)
            : t.TryGetComponent(out SpriteRenderer found) ? found : t.gameObject.AddComponent<SpriteRenderer>();

        r.sprite = sprite; r.sortingOrder = order; r.transform.localPosition = Vector3.zero;

        return r.gameObject;
    }

    /// <summary>Assigne au prefab GameManager les images d'animation du joueur et, s'ils manquent, les renderers du corps et de l'echarpe.</summary>
    static void ApplyPoses(Dictionary<string, (Sprite[] body, Sprite[] scarf)> poses)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefabPath) == null)
        { Debug.LogWarning($"[PieParPieArt] {GameManagerPrefabPath} absent : images d'animation non assignees."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(GameManagerPrefabPath);

        try
        {
            if (!root.TryGetComponent(out GameManager gm))
            { Debug.LogError("[PieParPieArt] Le prefab GameManager n'a pas de composant GameManager."); return; }

            var so = new SerializedObject(gm);
            const string anim = "<Player>k__BackingField.<Animator>k__BackingField.";
            SetClip(so, anim + "jumpInPlace", poses["Jump"]); SetClip(so, anim + "impale", poses["Impale"]);
            so.ApplyModifiedPropertiesWithoutUndo();

            LinkRenderers(root.GetComponentInChildren<SceneLinker>(true));

            PrefabUtility.SaveAsPrefabAsset(root, GameManagerPrefabPath);
        }
        finally
        { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Remplit les tableaux d'images d'un SpriteClip serialise.</summary>
    static void SetClip(SerializedObject so, string path, (Sprite[] body, Sprite[] scarf) clip)
    {
        foreach ((string field, Sprite[] sprites) in new[] { ("Body", clip.body), ("Scarf", clip.scarf) })
        {
            SerializedProperty a = so.FindProperty($"{path}.{field}");

            if (a == null)
            { Debug.LogError($"[PieParPieArt] Champ introuvable : {path}.{field}"); continue; }

            a.arraySize = sprites.Length;

            for (int i = 0; i < sprites.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    /// <summary>Assigne au SceneLinker les renderers Body et Scarf du visuel du joueur s'ils ne le sont pas encore.</summary>
    static void LinkRenderers(SceneLinker link)
    {
        if (link == null || link.PlayerVisual == null)
        { Debug.LogWarning("[PieParPieArt] SceneLinker ou PlayerVisual introuvable dans le prefab GameManager."); return; }

        var so = new SerializedObject(link);

        foreach ((string field, string child) in new[] { ("PlayerSprite", "Body"), ("ScarfSprite", "Scarf") })
        {
            SerializedProperty p = so.FindProperty($"<{field}>k__BackingField");

            if (p == null || p.objectReferenceValue != null) continue;

            SpriteRenderer r = null;

            foreach (SpriteRenderer s in link.PlayerVisual.GetComponentsInChildren<SpriteRenderer>(true)) if (s.name == child) { r = s; break; }

            if (r == null) Debug.LogWarning($"[PieParPieArt] Renderer '{child}' introuvable sous le visuel du joueur.");
            else p.objectReferenceValue = r;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Construit la scene Art_Preview : decor en tuiles manuelles, joueur avec echarpe, pieces, bandes de piques, portail.</summary>
    static void BuildPreviewScene(Dictionary<int, Tile[]> tiles, Sprites s, GameObject spikePrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera"; cam.orthographic = true; cam.orthographicSize = 7f; cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#8fd3ff");
        new GameObject("Global Light 2D").AddComponent<UnityEngine.Rendering.Universal.Light2D>().lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;

        var grid = new GameObject("Grid", typeof(Grid));
        var mapGo = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        mapGo.transform.SetParent(grid.transform, false);
        var cells = new HashSet<Vector2Int>();
        void Fill(int xa, int ya, int xb, int yb) { for (int y = ya; y <= yb; y++) for (int x = xa; x <= xb; x++) cells.Add(new(x, y)); }

        Fill(-12, -4, 12, -2);   // sol principal (surface en y = -1)
        Fill(-9, 1, -5, 1);      // plateforme flottante d'un bloc d'epaisseur
        Fill(3, -1, 3, 2);       // pilier d'un bloc de large
        Fill(7, -1, 9, 1);       // massif 3 x 3
        Fill(-1, 3, -1, 3);      // bloc isole

        PaintCells(mapGo.GetComponent<Tilemap>(), cells, tiles);

        var player = new GameObject("Player_Preview");
        player.transform.position = new Vector3(-11f, -1f);
        MakeSprite(player.transform, "Body", s.Player, 5, Vector3.zero);
        var anchor = new GameObject("ScarfAnchor").transform; anchor.SetParent(player.transform, false);
        MakeSprite(anchor, "Scarf", s.Scarf, 6, Vector3.zero).color = Hex("#3fa9f5");

        foreach (Vector3 p in new[] { new Vector3(-8.5f, 0.5f), new Vector3(-7.5f, 2.5f), new Vector3(-6.5f, 2.5f), new Vector3(-0.5f, 4.5f), new Vector3(8.5f, 2.7f) })
            MakeSprite(null, "Coin", s.Coin, 2, p);

        Strip(spikePrefab, "Spikes_Floor", new Vector3(-3f, -1f), 0f, 6);
        Strip(spikePrefab, "Spikes_Edge_PartlyUnsupported", new Vector3(-5f, 2f), 0f, 4);
        Strip(spikePrefab, "Spikes_Ceiling", new Vector3(-5f, 1f), 180f, 8);
        Strip(spikePrefab, "Spikes_Wall", new Vector3(3f, -1f), 90f, 6);
        Strip(spikePrefab, "Spikes_Corner_Floor", new Vector3(1f, -1f), 0f, 4);
        Strip(spikePrefab, "Spikes_Duplicate_Overlap", new Vector3(-1f, -1f), 0f, 3);

        MakeSprite(MakeSprite(null, "Arch", s.Arch, 1, new Vector3(11.5f, -1f)).transform, "Portal", s.Portal, 0, Vector3.zero);

        EditorSceneManager.SaveScene(scene, PreviewScenePath);
    }

    /// <summary>Peint chaque case avec la tuile manuelle de ses cotes exposes (case voisine vide) et une variante tiree au hasard.</summary>
    static void PaintCells(Tilemap map, HashSet<Vector2Int> cells, Dictionary<int, Tile[]> tiles)
    {
        var sides = new[] { (Vector2Int.up, T), (Vector2Int.down, B), (Vector2Int.left, L), (Vector2Int.right, R) };

        foreach (Vector2Int c in cells)
        {
            int m = 0;

            foreach ((Vector2Int d, int bit) in sides)
            { if (!cells.Contains(c + d)) m |= bit; }

            int v = Mathf.Min(Variants - 1, (int)(Rand(c.x, c.y, 7) * Variants));
            map.SetTile(new Vector3Int(c.x, c.y, 0), tiles[m][v]);
        }
    }

    /// <summary>Cree un objet avec un SpriteRenderer.</summary>
    static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, int order, Vector3 pos)
    {
        var go = new GameObject(name);

        if (parent != null) go.transform.SetParent(parent, false);

        go.transform.localPosition = pos;
        var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sortingOrder = order;

        return r;
    }

    /// <summary>Instancie une bande de piques (rotation Z en degres, nombre de piques).</summary>
    static void Strip(GameObject prefab, string name, Vector3 pos, float angle, int count)
    {
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name; go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, angle));
        go.GetComponent<SpikeStrip>().SetCount(count);
    }
}
