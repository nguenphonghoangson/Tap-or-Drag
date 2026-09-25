using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Percas.TapOrDrag.Editor
{
    /// <summary>
    /// Builds the art pipeline pieces from Assets/Arts/Sprites:
    /// sorting layers, low-res render target, prefabs, Game + Showcase scenes.
    /// Safe to re-run; everything is regenerated in place.
    ///
    /// The game renders into a 108x216 point-filtered RenderTexture which a RawImage
    /// upscales to the screen, so rotation and particles snap to the low-res pixel grid.
    /// </summary>
    public static class ProjectSetup
    {
        const int PPU = PixelSpriteImporter.PixelsPerUnit;
        const int ScreenW = 108, ScreenH = 216;

        const string PrefabDir = "Assets/Prefabs";
        const string RenderingDir = "Assets/Arts/Rendering";
        const string RtPath = RenderingDir + "/LowRes_108x216.renderTexture";
        const string FxMaterialPath = RenderingDir + "/FX_Pixel.mat";
        const string GameScenePath = "Assets/Scenes/Game.unity";
        const string ShowcaseScenePath = "Assets/Scenes/Showcase.unity";

        const string LayerBackground = "Background", LayerObstacles = "Obstacles",
            LayerPickups = "Pickups", LayerPlayer = "Player", LayerFx = "FX";
        static readonly string[] SortingLayers = { LayerBackground, LayerObstacles, LayerPickups, LayerPlayer, LayerFx };

        // Pipe geometry in game pixels, measured from the gap centre.
        const int GapHalf = 22, CapH = 5, BodyH = 216;

        static readonly Color Letterbox = Hex("#1e1b4b");
        static readonly Color SkyTop = Hex("#b4b9f5");
        static readonly Color ShowcaseBg = Hex("#f7f5ff");

        [MenuItem("Tap Or Drag/Setup Art, Prefabs and Scenes")]
        public static void Run()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(RenderingDir);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(PixelSpriteImporter.SpriteFolder,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            EnsureSortingLayers();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            var rt = EnsureLowResTarget();
            var fxMat = EnsureFxMaterial();
            var prefabs = BuildPrefabs(fxMat);
            BuildGameScene(rt, prefabs);
            BuildShowcaseScene(prefabs);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScenePath, true) };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(GameScenePath);
            Debug.Log("[TapOrDrag] Setup complete.");
        }

        /// <summary>Batchmode entry: setup + preview PNGs into ConceptArt/.</summary>
        public static void RunBatch()
        {
            Run();
            RenderPreviews();
        }

        // ------------------------------------------------------------------ project settings

        static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            foreach (var layerName in SortingLayers)
            {
                var exists = false;
                for (var i = 0; i < layers.arraySize; i++)
                    exists |= layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layerName;
                if (exists) continue;

                layers.InsertArrayElementAtIndex(layers.arraySize);
                var entry = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                entry.FindPropertyRelative("name").stringValue = layerName;
                entry.FindPropertyRelative("uniqueID").longValue = (uint)Animator.StringToHash("SortingLayer." + layerName);
                entry.FindPropertyRelative("locked").boolValue = false;
            }
            tagManager.ApplyModifiedProperties();
        }

        static RenderTexture EnsureLowResTarget()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (rt == null)
            {
                rt = new RenderTexture(ScreenW, ScreenH, 16, RenderTextureFormat.ARGB32);
                AssetDatabase.CreateAsset(rt, RtPath);
            }
            rt.filterMode = FilterMode.Point;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.useMipMap = false;
            rt.antiAliasing = 1;
            EditorUtility.SetDirty(rt);
            return rt;
        }

        static Material EnsureFxMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FxMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, FxMaterialPath);
            }
            mat.mainTexture = Spr("fx_px").texture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ prefabs

        class Prefabs
        {
            public GameObject Player, Background, PipeStatic, PipeMoving, GatePortal, GateLaser, GateDouble, Orb, Coin;
            public GameObject[] Fx;
        }

        static Prefabs BuildPrefabs(Material fxMat)
        {
            var p = new Prefabs
            {
                Player = Save(BuildPlayer(), "Player"),
                Background = Save(BuildBackground(), "Background"),
                PipeStatic = Save(BuildPipe("Pipe_Static", "pipe", false), "Obstacles/Pipe_Static"),
                PipeMoving = Save(BuildPipe("Pipe_Moving", "pipe_mv", true), "Obstacles/Pipe_Moving"),
                GatePortal = Save(BuildGate("Gate_Portal", "gate_portal"), "Obstacles/Gate_Portal"),
                GateLaser = Save(BuildGate("Gate_Laser", "gate_laser"), "Obstacles/Gate_Laser"),
                Orb = Save(BuildOrb(), "Obstacles/Orb"),
                Coin = Save(BuildCoin(), "Pickups/Coin"),
            };

            // Double gate nests the two single gates so their art stays in one place.
            var dbl = new GameObject("Gate_Double");
            Place(p.GatePortal, dbl.transform, -16, 0);
            Place(p.GateLaser, dbl.transform, 16, 0);
            p.GateDouble = Save(dbl, "Obstacles/Gate_Double");

            p.Fx = new[]
            {
                Save(BuildBurst("FX_Burst_Pipe", fxMat, "#f59e0b", "#fbbf24", "#b45309"), "FX/FX_Burst_Pipe"),
                Save(BuildBurst("FX_Burst_Gate", fxMat, "#22d3ee", "#f472b6", "#a5f3fc", "#fbcfe8"), "FX/FX_Burst_Gate"),
                Save(BuildBurst("FX_Burst_Orb", fxMat, "#7c3aed", "#a78bfa", "#5b21b6"), "FX/FX_Burst_Orb"),
                Save(BuildBurst("FX_Burst_Coin", fxMat, "#fde047", "#fef9c3", "#ca8a04"), "FX/FX_Burst_Coin"),
            };
            return p;
        }

        static GameObject BuildPlayer()
        {
            var root = new GameObject("Player");
            SpriteNode("Body", root.transform, "bird_mid", LayerPlayer, 1);
            SpriteNode("DashTrail", root.transform, "bird_dash_trail", LayerPlayer, 0).gameObject.SetActive(false);
            root.AddComponent<CircleCollider2D>().radius = 5f / PPU;
            return root;
        }

        static GameObject BuildBackground()
        {
            var root = new GameObject("Background");
            SpriteNode("Sky", root.transform, "bg_sky", LayerBackground, 0);
            SpriteNode("HillsFar", root.transform, "bg_hills_far", LayerBackground, 1, 0, -ScreenH / 2);
            SpriteNode("HillsNear", root.transform, "bg_hills_near", LayerBackground, 2, 0, -ScreenH / 2);
            var clouds = Node("Clouds", root.transform);
            SpriteNode("Cloud_A", clouds.transform, "cloud_big", LayerBackground, 3, -46, 75);
            SpriteNode("Cloud_B", clouds.transform, "cloud_big", LayerBackground, 3, 8, 53);
            SpriteNode("Cloud_C", clouds.transform, "cloud_small", LayerBackground, 3, -24, 25);
            SpriteNode("Cloud_D", clouds.transform, "cloud_small", LayerBackground, 3, 35, 92);
            return root;
        }

        /// <summary>Root sits at the gap centre; each half is body + cap with its own collider.</summary>
        static GameObject BuildPipe(string name, string key, bool moving)
        {
            var root = new GameObject(name);
            foreach (var (half, up) in new[] { ("Top", true), ("Bottom", false) })
            {
                var h = Node(half, root.transform);
                var cap = SpriteNode("Cap", h.transform, key + "_cap", LayerObstacles, 1, 0, up ? GapHalf : -GapHalf - CapH);
                var body = SpriteNode("Body", h.transform, key + "_body", LayerObstacles, 0, 0,
                    up ? GapHalf + CapH : -GapHalf - CapH - BodyH);
                cap.gameObject.AddComponent<BoxCollider2D>();
                body.gameObject.AddComponent<BoxCollider2D>();
            }

            if (moving)
            {
                // Arrows beside each gap edge + faint after-lines inside the gap hint at the up/down drift.
                var hint = Node("MotionHint", root.transform);
                SpriteNode("ArrowTop", hint.transform, "pipe_mv_hint", LayerObstacles, 2, 17, GapHalf - 3);
                SpriteNode("ArrowBottom", hint.transform, "pipe_mv_hint", LayerObstacles, 2, 17, -GapHalf - 4);
                SpriteNode("GhostTop", hint.transform, "pipe_mv_ghost", LayerObstacles, 2, 0, GapHalf - 5);
                var ghostBottom = SpriteNode("GhostBottom", hint.transform, "pipe_mv_ghost", LayerObstacles, 2, 0, -GapHalf + 5);
                ghostBottom.transform.localScale = new Vector3(1f, -1f, 1f);
            }
            return root;
        }

        static GameObject BuildGate(string name, string sprite)
        {
            var root = new GameObject(name);
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = Spr(sprite);
            SetLayer(sr, LayerObstacles, 0);
            root.AddComponent<BoxCollider2D>().isTrigger = true;
            return root;
        }

        static GameObject BuildOrb()
        {
            var root = new GameObject("Orb");
            SpriteNode("Body", root.transform, "orb", LayerObstacles, 2);
            var trail = Node("BobTrail", root.transform);
            SpriteNode("Ghost_1", trail.transform, "orb", LayerObstacles, 1, 0, 2).color = new Color(1f, 1f, 1f, 0.3f);
            SpriteNode("Ghost_2", trail.transform, "orb", LayerObstacles, 1, 0, 4).color = new Color(1f, 1f, 1f, 0.15f);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 4f / PPU;
            col.offset = Px(0.5f, 0.5f);
            return root;
        }

        static GameObject BuildCoin()
        {
            var root = new GameObject("Coin");
            SpriteNode("Body", root.transform, "coin", LayerPickups, 0);
            var sparkles = Node("Sparkles", root.transform);
            SpriteNode("Sparkle_A", sparkles.transform, "sparkle_small", LayerPickups, 1, -5, 4);
            SpriteNode("Sparkle_B", sparkles.transform, "sparkle_big", LayerPickups, 1, 5, 5);
            SpriteNode("Sparkle_C", sparkles.transform, "sparkle_small", LayerPickups, 1, 4, -5).color = new Color(1f, 1f, 1f, 0.7f);
            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 4f / PPU;
            col.offset = Px(0.5f, 0f);
            return root;
        }

        /// <summary>One-shot square-pixel burst: 12 particles, 2px shrinking to 1px, fading out.</summary>
        static GameObject BuildBurst(string name, Material mat, params string[] colors)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(40f / PPU, 90f / PPU);
            main.startSize = 2f / PPU;
            main.startColor = new ParticleSystem.MinMaxGradient(FixedGradient(colors)) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f / PPU;

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 1000f;
            drag.drag = 4f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(0f, 1f) });
            fade.color = alpha;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortingLayerName = LayerFx;
            return go;
        }

        // ------------------------------------------------------------------ scenes

        static void BuildGameScene(RenderTexture rt, Prefabs p)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = NewCamera("GameCamera", SkyTop);
            cam.tag = "MainCamera";
            cam.targetTexture = rt;
            cam.gameObject.AddComponent<AudioListener>();

            var display = NewCamera("DisplayCamera", Letterbox);
            display.cullingMask = 0;
            display.depth = 1;

            var canvasGo = new GameObject("Screen", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2160);
            scaler.matchWidthOrHeight = 0.5f;

            var view = new GameObject("LowResView", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            view.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)view.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var raw = view.GetComponent<RawImage>();
            raw.texture = rt;
            raw.raycastTarget = false;
            var fitter = view.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)ScreenW / ScreenH;

            var world = new GameObject("World").transform;
            Place(p.Background, world, 0, 0);
            var obstacles = Node("Obstacles", world).transform;
            Place(p.PipeStatic, obstacles, 20, 16);
            Place(p.Orb, obstacles, -20, -46);
            Place(p.GateLaser, obstacles, 45, 0);
            var pickups = Node("Pickups", world).transform;
            Place(p.Coin, pickups, 20, 16);
            var player = Place(p.Player, world, -28, 8, 30f);
            player.transform.Find("Body").GetComponent<SpriteRenderer>().sprite = Spr("bird_up");

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        static void BuildShowcaseScene(Prefabs p)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = NewCamera("ShowcaseCamera", ShowcaseBg);
            cam.tag = "MainCamera";

            var root = new GameObject("Showcase").transform;
            Place(p.PipeStatic, root, -140, 0);
            Place(p.PipeMoving, root, -100, 0);
            Place(p.GatePortal, root, -58, 0);
            Place(p.GateLaser, root, -34, 0);
            Place(p.GateDouble, root, 2, 0);

            var up = Place(p.Player, root, 50, 64, 30f);
            up.transform.Find("Body").GetComponent<SpriteRenderer>().sprite = Spr("bird_up");
            var fall = Place(p.Player, root, 80, 64, -45f);
            fall.transform.Find("Body").GetComponent<SpriteRenderer>().sprite = Spr("bird_down");
            var dash = Place(p.Player, root, 128, 64);
            dash.transform.Find("Body").GetComponent<SpriteRenderer>().sprite = Spr("bird_dash");
            dash.transform.Find("DashTrail").gameObject.SetActive(true);

            Place(p.Orb, root, 55, 12);
            Place(p.Coin, root, 85, 12);
            for (var i = 0; i < p.Fx.Length; i++)
                Place(p.Fx[i], root, 45 + i * 30, -50);

            EditorSceneManager.SaveScene(scene, ShowcaseScenePath);
        }

        // ------------------------------------------------------------------ previews

        [MenuItem("Tap Or Drag/Render Preview PNGs")]
        public static void RenderPreviews()
        {
            EditorSceneManager.OpenScene(GameScenePath);
            var game = Object.FindObjectsOfType<Camera>().First(c => c.targetTexture != null);
            SaveCameraPng(game, game.targetTexture, "ConceptArt/preview_game.png", 5);

            EditorSceneManager.OpenScene(ShowcaseScenePath);
            foreach (var ps in Object.FindObjectsOfType<ParticleSystem>())
                ps.Simulate(0.12f, true, true);
            var showcase = Object.FindObjectsOfType<Camera>().First();
            var tmp = new RenderTexture(ScreenW * 3, ScreenH, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            SaveCameraPng(showcase, tmp, "ConceptArt/preview_showcase.png", 4);
            tmp.Release();

            EditorSceneManager.OpenScene(GameScenePath);
        }

        static void SaveCameraPng(Camera cam, RenderTexture rt, string relPath, int scale)
        {
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var src = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            src.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            src.Apply();
            RenderTexture.active = prevActive;
            cam.targetTexture = prevTarget;

            var w = rt.width * scale;
            var h = rt.height * scale;
            var from = src.GetPixels32();
            var to = new Color32[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                to[y * w + x] = from[y / scale * rt.width + x / scale];
            var big = new Texture2D(w, h, TextureFormat.RGBA32, false);
            big.SetPixels32(to);
            big.Apply();

            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath), relPath);
            File.WriteAllBytes(path, big.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(big);
            Debug.Log($"[TapOrDrag] Preview written: {path}");
        }

        // ------------------------------------------------------------------ helpers

        static Camera NewCamera(string name, Color clear)
        {
            var cam = new GameObject(name).AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic = true;
            cam.orthographicSize = ScreenH / 2f / PPU;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = clear;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            return cam;
        }

        static GameObject Save(GameObject go, string relPath)
        {
            var path = $"{PrefabDir}/{relPath}.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject Place(GameObject prefab, Transform parent, float xPx, float yPx, float rotZ = 0f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = Px(xPx, yPx);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            return go;
        }

        static GameObject Node(string name, Transform parent, float xPx = 0, float yPx = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Px(xPx, yPx);
            return go;
        }

        static SpriteRenderer SpriteNode(string name, Transform parent, string sprite, string layer, int order,
            float xPx = 0, float yPx = 0)
        {
            var sr = Node(name, parent, xPx, yPx).AddComponent<SpriteRenderer>();
            sr.sprite = Spr(sprite);
            SetLayer(sr, layer, order);
            return sr;
        }

        static void SetLayer(Renderer r, string layer, int order)
        {
            var id = SortingLayer.NameToID(layer);
            if (!SortingLayer.IsValid(id)) throw new InvalidOperationException($"Sorting layer '{layer}' missing");
            r.sortingLayerID = id;
            r.sortingOrder = order;
        }

        static Sprite Spr(string name)
        {
            var path = $"{PixelSpriteImporter.SpriteFolder}/{name}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException($"Sprite not found: {path}");
            return sprite;
        }

        static Vector3 Px(float x, float y) => new Vector3(x / PPU, y / PPU, 0f);

        static Gradient FixedGradient(string[] colors)
        {
            var g = new Gradient { mode = GradientMode.Fixed };
            g.SetKeys(colors.Select((c, i) => new GradientColorKey(Hex(c), (i + 1f) / colors.Length)).ToArray(),
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
