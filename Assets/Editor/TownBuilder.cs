using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// Builds the town of Minnalpatti from the sprites in Assets/Art.
// Re-run it any time from the menu: Current Pochu > Rebuild Town.
//
// The map, in tiles (1 tile = 1 unit). Three streets run east-west and three
// roads run north-south, with buildings along the north side of each street:
//
//        y=22  Bazaar Street      wedding hall, tea stall, shops, EB office
//        y=-2  Kamarajar Street   houses (home is here), cricket ground behind
//        y=-26 Tank Road          houses, banyan tree, temple tank and shrine
//
// East of town (x 66 to 148) are the paddy fields of Chapter 2. The flooded
// paddies can't be walked on, so the raised dirt bunds between them are a maze.
public static class TownBuilder
{
    const string ScenePath = "Assets/Scenes/Town.unity";
    const string Art = "Assets/Art/";

    // Sorting orders. Everything at order 0 is sorted by its Y position.
    const int GroundOrder = -100, DecalOrder = -50, GlowOrder = 10;

    const int HalfWidth = 64, HalfHeight = 40; // how far players can walk from the centre
    static readonly int[] Streets = { 22, -2, -26 }; // bottom row of each 4-tile-wide street
    static readonly int[] Roads = { -58, -2, 54 }; // left column of each 4-tile-wide road
    static readonly RectInt Tank = new(14, -20, 27, 10);
    static readonly RectInt Pitch = new(26, 9, 4, 10);

    const int FieldsStart = 66, FieldsEnd = 148;

    // North of town, up the road past the bazaar, is the walled yard of Raja
    // Talkies (Chapter 3). The gate is in the middle of its south wall.
    const int NorthEnd = 96;

    // West of town, past the level crossing on Tank Road, is the goods yard
    // (Chapter 4): gravel, with three railway tracks running east-west.
    const int WestEnd = -150;
    static readonly RectInt GoodsYard = Area(-148, -38, -70, -6);
    static readonly int[] Tracks = { -14, -21, -32 };

    static string YardAt(int x, int y)
    {
        if (y >= Streets[2] && y <= Streets[2] + 3 && x > GoodsYard.xMax - 1) return "road";
        if (!GoodsYard.Contains(new Vector2Int(x, y))) return "grass";
        return System.Array.IndexOf(Tracks, y) >= 0 ? "rail" : "gravel";
    }
    static readonly RectInt Yard = new(-24, 58, 49, 35);

    // Inclusive corners, which is easier to read for paths than x/y/width/height.
    static RectInt Area(int x0, int y0, int x1, int y1) => new(x0, y0, x1 - x0 + 1, y1 - y0 + 1);

    static readonly RectInt Canal = Area(101, -60, 104, 60);
    static readonly RectInt Island = Area(128, -7, 146, 8); // dry ground around the pump-house
    static readonly RectInt[] Bunds =
    {
        Area(66, -1, 100, 0),    // the cart track from town to the canal
        Area(84, -23, 85, 22),   // long bund running north-south
        Area(84, 10, 99, 11),    // east to the canal-side valve
        Area(70, -12, 96, -11), Area(70, -12, 71, -1), // a loop to get lost on
        Area(83, 20, 86, 22), Area(83, -23, 86, -21), Area(97, 9, 99, 12), // valve platforms
        Area(105, -1, 128, 0),   // across the canal to the pump-house
        Area(116, 0, 117, 18), Area(116, 17, 136, 18), Area(133, 16, 136, 19), // to the scarecrow
        Area(110, -14, 111, -1), // dead end
    };

    static readonly Color Warm = new(1f, 0.8f, 0.5f);

    static readonly List<Rect> blocked = new(); // footprints of buildings, to keep trees off them
    static readonly List<GameObject> powerLights = new(); // lit windows, off until the power returns
    static GameObject props;

    [MenuItem("Current Pochu/Rebuild Town")]
    public static string Build()
    {
        ConfigureProject();
        blocked.Clear();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var playerPrefab = BuildPlayerPrefab();
        var minminiPrefab = BuildMinminiPrefab();
        var dogPrefab = BuildDogPrefab();
        var bandicootPrefab = BuildBandicootPrefab();
        powerLights.Clear();

        props = new GameObject("Props");
        BuildGround();
        BuildKamarajarStreet();
        BuildBazaar();
        BuildTankRoad();
        BuildTrees();
        BuildLighting();
        BuildCamera();
        BuildBounds();

        new GameObject("SpawnPoint").transform.position = new Vector3(-9f, 0.5f, 0f);
        var dogSpot = new GameObject("DogSpot").transform;
        dogSpot.position = new Vector3(-5f, 2.8f, 0f);
        var (east, north, west) = BuildExits();
        BuildQuestItems();
        var network = BuildNetwork(playerPrefab, minminiPrefab, dogPrefab, bandicootPrefab, BuildMinminiSpots(), dogSpot);
        Set(network.GetComponent<WorldSpawner>(), "bandicootPrefab", bandicootPrefab.GetComponent<NetworkObject>());
        Set(network.GetComponent<WorldSpawner>(), "bandicootSpots", BuildBandicootSpots());

        Set(network.GetComponent<WorldSpawner>(), "minminiCount", 72);

        var quests = new GameObject("Quests");
        quests.AddComponent<NetworkObject>();
        var questState = new SerializedObject(quests.AddComponent<Quests>());
        BuildFields(questState);
        BuildCinema(questState, north, network.GetComponent<WorldSpawner>());
        BuildGoodsYard(questState, west);
        Fill(questState.FindProperty("powerOn"), powerLights);
        Fill(questState.FindProperty("powerOff"), new List<GameObject> { east });
        questState.ApplyModifiedPropertiesWithoutUndo();
        new GameObject("GameUI").AddComponent<GameUI>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();

        // Netcode gives every networked object placed in a scene an ID, but it can
        // only work one out once the scene exists on disk. So load the saved
        // scene again, let it assign the IDs, and save once more.
        scene = EditorSceneManager.OpenScene(ScenePath);
        var ids = new HashSet<uint>();
        int networked = 0;
        foreach (var networkObject in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
        {
            var serialized = new SerializedObject(networkObject);
            ids.Add(serialized.FindProperty("GlobalObjectIdHash").uintValue);
            EditorUtility.SetDirty(networkObject);
            networked++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"built {ScenePath}: {StreetLightCount()} streetlights, {networked} networked objects, {ids.Count} unique ids";
    }

    static int StreetLightCount() => Object.FindObjectsByType<StreetLight>(FindObjectsSortMode.None).Length;

    static void ConfigureProject()
    {
        PlayerSettings.companyName = "Segar Games";
        PlayerSettings.productName = "Current Pochu!";
        PlayerSettings.runInBackground = true; // keep running when the window loses focus
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;

        // Sort sprites by Y so you can walk in front of and behind things.
        var renderer = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Renderer2D.asset"));
        renderer.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
        renderer.FindProperty("m_TransparencySortAxis").vector3Value = Vector3.up;
        renderer.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------ prefabs

    static GameObject BuildPlayerPrefab()
    {
        var root = new GameObject("Player");
        Body(root, 0.28f);

        var body = Child(root, "Body", Vector3.zero).AddComponent<SpriteRenderer>();
        body.sprite = Load("Characters/kavin_idle");
        body.spriteSortPoint = SpriteSortPoint.Pivot;

        var torchPivot = Child(root, "TorchPivot", new Vector3(0f, 0.5f, 0f));
        var torch = Child(torchPivot, "Torch", Vector3.zero).AddComponent<Light2D>();
        torch.lightType = Light2D.LightType.Point;
        torch.color = new Color(1f, 0.92f, 0.72f);
        torch.intensity = 1.9f;
        torch.pointLightInnerRadius = 0.5f;
        torch.pointLightOuterRadius = 7.5f;
        torch.pointLightInnerAngle = 35f;
        torch.pointLightOuterAngle = 88f;
        torch.falloffIntensity = 0.6f;
        torch.shadowsEnabled = true;
        torch.shadowIntensity = 0.75f;

        // A faint glow so you can always see yourself.
        Light(Child(root, "Aura", new Vector3(0f, 0.5f, 0f)), new Color(1f, 0.9f, 0.75f), 0.55f, 0.2f, 2.4f);

        root.AddComponent<NetworkObject>();
        var networkTransform = SyncedPosition(root);
        networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner; // each player moves themselves
        root.AddComponent<NetworkRigidbody2D>();

        var carry = Child(root, "Carrying", new Vector3(0f, 1.5f, 0f)).AddComponent<SpriteRenderer>();
        carry.sharedMaterial = GlowMaterial(); // unlit, so you can see it in the dark
        carry.sortingOrder = GlowOrder;

        var controller = new SerializedObject(root.AddComponent<PlayerController>());
        controller.FindProperty("body").objectReferenceValue = body;
        controller.FindProperty("carryIcon").objectReferenceValue = carry;
        var items = controller.FindProperty("itemSprites");
        string[] itemNames = { null, "glasses", "leaf", "ice", "belt" };
        items.arraySize = itemNames.Length;
        for (int i = 1; i < itemNames.Length; i++)
            items.GetArrayElementAtIndex(i).objectReferenceValue = Load($"Items/{itemNames[i]}");
        controller.FindProperty("torchPivot").objectReferenceValue = torchPivot.transform;
        var kids = new[] { "kavin", "yazhini", "abdul", "mercy" };
        var looks = controller.FindProperty("looks");
        looks.arraySize = kids.Length;
        for (int i = 0; i < kids.Length; i++)
        {
            var look = looks.GetArrayElementAtIndex(i);
            look.FindPropertyRelative("idle").objectReferenceValue = Load($"Characters/{kids[i]}_idle");
            look.FindPropertyRelative("walk1").objectReferenceValue = Load($"Characters/{kids[i]}_walk1");
            look.FindPropertyRelative("walk2").objectReferenceValue = Load($"Characters/{kids[i]}_walk2");
        }
        controller.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(root);
    }

    static GameObject BuildMinminiPrefab()
    {
        var root = new GameObject("Minmini");
        var visual = Child(root, "Visual", new Vector3(0f, 0.35f, 0f));
        var dot = visual.AddComponent<SpriteRenderer>();
        dot.sprite = Load("Decals/glow");
        dot.sharedMaterial = GlowMaterial();
        dot.color = new Color(0.8f, 1f, 0.4f);
        dot.sortingOrder = GlowOrder;
        Light(visual, new Color(0.75f, 1f, 0.4f), 0.9f, 0.1f, 1.5f);

        root.AddComponent<NetworkObject>();
        SyncedPosition(root); // moved by the server
        Set(root.AddComponent<Minmini>(), "visual", visual.transform);
        return SavePrefab(root);
    }

    static GameObject BuildBandicootPrefab()
    {
        var root = new GameObject("Bandicoot");
        var body = root.AddComponent<SpriteRenderer>();
        body.sprite = Load("Characters/bandicoot_1");
        body.spriteSortPoint = SpriteSortPoint.Pivot;
        root.AddComponent<NetworkObject>();
        SyncedPosition(root);
        var bandicoot = root.AddComponent<Bandicoot>();
        Set(bandicoot, "body", body);
        Set(bandicoot, "frame1", Load("Characters/bandicoot_1"));
        Set(bandicoot, "frame2", Load("Characters/bandicoot_2"));
        return SavePrefab(root);
    }

    static GameObject BuildDogPrefab()
    {
        var root = new GameObject("Battery");
        var body = root.AddComponent<SpriteRenderer>();
        body.sprite = Load("Characters/dog_idle");
        body.spriteSortPoint = SpriteSortPoint.Pivot;
        Body(root, 0.2f).mass = 0.2f;

        root.AddComponent<NetworkObject>();
        SyncedPosition(root);
        root.AddComponent<NetworkRigidbody2D>();

        var dog = root.AddComponent<Dog>();
        Set(dog, "body", body);
        Set(dog, "idle", Load("Characters/dog_idle"));
        Set(dog, "walk1", Load("Characters/dog_walk1"));
        Set(dog, "walk2", Load("Characters/dog_walk2"));

        var pet = root.AddComponent<Interactable>();
        pet.verb = "Pet";
        pet.lines = new[]
        {
            "Battery|Woof!",
            "|(He looks at your torch like it might be a snack.)",
        };
        return SavePrefab(root);
    }

    static Rigidbody2D Body(GameObject root, float radius)
    {
        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var feet = root.AddComponent<CircleCollider2D>();
        feet.radius = radius;
        feet.offset = new Vector2(0f, radius * 0.8f);
        return rb;
    }

    // Only X and Y ever change, so that is all we send over the network.
    static NetworkTransform SyncedPosition(GameObject root)
    {
        var networkTransform = root.AddComponent<NetworkTransform>();
        networkTransform.SyncPositionZ = false;
        networkTransform.SyncRotAngleX = networkTransform.SyncRotAngleY = networkTransform.SyncRotAngleZ = false;
        networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
        return networkTransform;
    }

    static GameObject SavePrefab(GameObject root)
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Prefabs/{root.name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------ ground

    static bool OnStreet(int y)
    {
        foreach (int street in Streets)
            if (y >= street && y <= street + 3) return true;
        return false;
    }

    static bool OnRoad(int x)
    {
        foreach (int road in Roads)
            if (x >= road && x <= road + 3) return true;
        return false;
    }

    static string FieldAt(int x, int y)
    {
        var cell = new Vector2Int(x, y);
        if (Canal.Contains(cell)) return y == -1 || y == 0 ? "steps" : "water"; // the bridge crossing
        foreach (var bund in Bunds)
            if (bund.Contains(cell)) return "dirt";
        return Island.Contains(cell) ? "grass" : "paddy";
    }

    static bool Blocks(string ground) => ground == "paddy" || ground == "water" || ground == "wall";

    static string CinemaAt(int x, int y)
    {
        if (!Yard.Contains(new Vector2Int(x, y))) return "grass";
        bool edge = x == Yard.xMin || x == Yard.xMax - 1 || y == Yard.yMin || y == Yard.yMax - 1;
        bool gate = y == Yard.yMin && x >= Roads[1] && x <= Roads[1] + 3;
        return edge && !gate ? "wall" : "pitch";
    }

    static string GroundAt(int x, int y)
    {
        if (x >= FieldsStart) return FieldAt(x, y);
        if (y >= Yard.yMin) return CinemaAt(x, y);
        if (x < -65) return YardAt(x, y);

        bool inTown = x >= Roads[0] && x <= Roads[2] + 3 && y >= Streets[2] && y <= Streets[0] + 3;

        // The three ways out of town: east along Kamarajar Street, north past
        // the bazaar, and west along Tank Road.
        if (y >= Streets[1] && y <= Streets[1] + 3 && x > Roads[2]) return "road";
        if (x >= Roads[1] && x <= Roads[1] + 3 && y > Streets[0]) return "road";
        if (y >= Streets[2] && y <= Streets[2] + 3 && x < Roads[0]) return "road";

        if (inTown && (OnStreet(y) || OnRoad(x))) return "road";
        if (y < Streets[2] - 3 || x > Roads[2] + 6 || x < Roads[0] - 3) return "paddy";

        if (Tank.Contains(new Vector2Int(x, y))) return "water";
        var steps = new RectInt(Tank.x - 1, Tank.y - 1, Tank.width + 2, Tank.height + 2);
        if (steps.Contains(new Vector2Int(x, y))) return "steps";
        if (Pitch.Contains(new Vector2Int(x, y))) return "pitch";

        if (x > Roads[0] + 3 && x < Roads[2] && !OnRoad(x))
        {
            foreach (int street in Streets)
            {
                if (y == street + 4 || y == street + 5) return "cement"; // doorsteps
                if (y == street - 1) return "dirt"; // dusty shoulder
            }
        }
        return "grass";
    }

    static void BuildGround()
    {
        var grid = new GameObject("Grid").AddComponent<Grid>();
        var tilemap = Child(grid.gameObject, "Ground", Vector3.zero).AddComponent<Tilemap>();
        tilemap.gameObject.AddComponent<TilemapRenderer>().sortingOrder = GroundOrder;

        // Flooded paddy and open water go on a second layer that has a collider,
        // so nobody can wade through them.
        var wet = Child(grid.gameObject, "Water and Paddy", Vector3.zero).AddComponent<Tilemap>();
        wet.gameObject.AddComponent<TilemapRenderer>().sortingOrder = GroundOrder;
        wet.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        wet.gameObject.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        wet.gameObject.AddComponent<CompositeCollider2D>();

        for (int x = WestEnd - 16; x < FieldsEnd + 16; x++)
        for (int y = -HalfHeight - 10; y < NorthEnd + 10; y++)
        {
            int variant = Mathf.Abs(x * 7 + y * 13 + x * y) % 3;
            string ground = GroundAt(x, y);
            (Blocks(ground) ? wet : tilemap).SetTile(new Vector3Int(x, y, 0), TileFor($"{ground}_{variant}"));
        }
        wet.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        var merged = wet.GetComponent<CompositeCollider2D>();
        merged.geometryType = CompositeCollider2D.GeometryType.Polygons; // solid all the way through
        merged.GenerateGeometry();

        // You can't walk into the temple tank.
        var water = new GameObject("Tank Water").AddComponent<BoxCollider2D>();
        water.transform.position = new Vector3(Tank.center.x, Tank.center.y, 0f);
        water.size = new Vector2(Tank.width - 0.4f, Tank.height - 0.4f);
        blocked.Add(new Rect(Tank.x - 2, Tank.y - 2, Tank.width + 4, Tank.height + 4));
        blocked.Add(new Rect(Pitch.x - 6, Pitch.y - 2, Pitch.width + 12, Pitch.height + 4));
    }

    // ------------------------------------------------------------ districts

    static void BuildKamarajarStreet()
    {
        float doorstep = Streets[1] + 6;
        int index = 0;
        foreach (float start in new[] { -50f, 8f })
            for (int i = 0; i < 8; i++, index++)
            {
                float x = start + i * 6f;
                var house = House(x, doorstep, index);
                if (Mathf.Approximately(x, -8f) || index % 5 == 2) Candle(house, new Vector3(-1.1f, 1.7f, 0f));
            }

        foreach (float x in new[] { -47f, -29f, -11f, 11f, 29f, 47f })
            Streetlight(x, doorstep - 1.7f, lit: Mathf.Approximately(x, -11f));

        // Home: Paati on her doorstep, and the lineman by the one working lamp.
        Person("Paati", "Characters/paati", new Vector3(-9.3f, 3.5f, 0f), "paati");

        Solid(Prop("Cycle", "Props/cycle", new Vector3(-13.4f, 2.7f, 0f)), 1.4f, 0.4f);
        Person("Lineman Murugesan", "Characters/lineman", new Vector3(-12.1f, 3f, 0f), "lineman");

        // The cricket ground behind the eastern houses.
        Solid(Prop("Stumps", "Props/stumps", new Vector3(28f, 17.6f, 0f)), 0.4f, 0.3f);
        Solid(Prop("Stumps", "Props/stumps", new Vector3(28f, 9.6f, 0f)), 0.4f, 0.3f);
        Person("Umpire Ravi", "Characters/abdul_idle", new Vector3(30.6f, 13.5f, 0f), "ravi");

        Solid(Prop("Well", "Props/well", new Vector3(-30f, 14f, 0f)), 1.4f, 0.9f);
        blocked.Add(new Rect(-32f, 12f, 4f, 4f));
    }

    static void BuildBazaar()
    {
        float doorstep = Streets[0] + 6;

        Building("Wedding Hall", "Props/wedding_hall", -46f, doorstep);
        Person("The Chairman", "Characters/chairman", new Vector3(-41f, doorstep - 0.7f, 0f), "chairman");
        var bananas = Child(props, "Banana Leaves", new Vector3(-46f, doorstep - 0.4f, 0f));
        Act(bananas, "Take a leaf", "banana");

        var stall = Building("Tea Stall", "Props/tea_stall", -34f, doorstep);
        var stove = Child(stall, "Stove", new Vector3(0.85f, 1.25f, 0f));
        Light(stove, new Color(1f, 0.55f, 0.25f), 1.2f, 0.2f, 2.6f);
        stove.AddComponent<FlickerLight>();
        Glow(stall, new Vector3(0.85f, 1.3f, 0f), new Color(1f, 0.6f, 0.3f), 0.35f);
        Person("Tea Master Selvam", "Characters/teamaster", new Vector3(-31f, doorstep - 0.8f, 0f), "teamaster");

        string[] west = { "shop_stores", "shop_tailor", "shop_medical" };
        for (int i = 0; i < west.Length; i++)
            Building("Shop", $"Props/{west[i]}", -26f + i * 6f, doorstep);

        for (int i = 0; i < 5; i++)
            House(8f + i * 6f, doorstep, 20 + i);

        Building("EB Office", "Props/eb_office", 43f, doorstep);
        var transformer = Prop("Transformer", "Props/transformer", new Vector3(48.5f, doorstep - 0.2f, 0f));
        Solid(transformer, 2.2f, 0.6f);
        transformer.AddComponent<ShadowCaster2D>().selfShadows = false;
        Act(transformer, "Inspect", "transformer");

        foreach (float x in new[] { -39f, -17f, 11f, 38f })
            Streetlight(x, doorstep - 1.7f, lit: false);
    }

    static void BuildTankRoad()
    {
        float doorstep = Streets[2] + 6;
        for (int i = 0; i < 8; i++)
        {
            var house = House(-50f + i * 6f, doorstep, 30 + i);
            if (i % 3 == 1) Candle(house, new Vector3(1.2f, 1.7f, 0f));
        }

        var banyan = Prop("Banyan Tree", "Props/banyan", new Vector3(7f, -18f, 0f));
        var trunk = banyan.AddComponent<CircleCollider2D>();
        trunk.radius = 1.3f;
        trunk.offset = new Vector2(0f, 0.9f);
        blocked.Add(new Rect(2f, -20f, 10f, 9f));

        var shrine = Building("Shrine", "Props/shrine", 27.5f, -9f);
        var lamp = Child(shrine, "Oil Lamp", new Vector3(0f, 0.9f, 0f));
        Light(lamp, new Color(1f, 0.7f, 0.35f), 1.3f, 0.2f, 3.2f);
        lamp.AddComponent<FlickerLight>();
        Glow(shrine, new Vector3(0f, 0.9f, 0f), new Color(1f, 0.75f, 0.4f), 0.3f);

        foreach (float x in new[] { -47f, -29f, -11f, 8f })
            Streetlight(x, doorstep - 1.7f, lit: false);
    }

    // The roads out of town are closed until later chapters.
    // Returns the east and north barricades, which come down as chapters are finished.
    static (GameObject east, GameObject north, GameObject west) BuildExits()
    {
        var east = Barricade(new Vector3(61f, 0f, 0f), true,
            "|ROAD CLOSED. The line to the paddy fields is down. Get the power back on first.");
        var north = Barricade(new Vector3(0f, 37f, 0f), false,
            "|ROAD CLOSED. Beyond here is the old Raja Talkies. Nobody goes there after dark.");
        var west = Barricade(new Vector3(-61f, -24f, 0f), true,
            "|LEVEL CROSSING CLOSED. The goods yard is beyond. No entry after dark.");
        return (east, north, west);
    }

    static GameObject Barricade(Vector3 centre, bool acrossHorizontalRoad, string sign)
    {
        var root = Child(props, "Closed Road", centre);
        for (int i = 0; i < 2; i++)
        {
            var offset = acrossHorizontalRoad ? new Vector3(i * 0.5f, -1.9f + i * 2f, 0f) : new Vector3(-1f + i * 2f, 0f, 0f);
            var board = Prop("Barricade", "Props/barricade", centre + offset);
            board.transform.SetParent(root.transform, true);
            Talk(board, "Read", sign);
        }
        var wall = root.AddComponent<BoxCollider2D>();
        wall.size = acrossHorizontalRoad ? new Vector2(0.6f, 5f) : new Vector2(5f, 0.6f);
        return root;
    }

    // Everything the four fuse tasks need: the lost glasses, the goat and her
    // pen, the ice cart, and six cricket balls hidden around town.
    static void BuildQuestItems()
    {
        Pickup("Paati's Glasses", "Items/glasses", new Vector3(-28.3f, 13.3f, 0f), "glasses");

        Vector3[] balls =
        {
            new(-34f, 16f, 0f), new(10.5f, -15f, 0f), new(31f, -21.6f, 0f),
            new(51f, 27.2f, 0f), new(-50.5f, -12f, 0f), new(41f, 15f, 0f),
        };
        for (int i = 0; i < balls.Length; i++)
            Pickup("Cricket Ball", "Items/ball", balls[i], $"ball{i}");

        var cart = Prop("Ice Cart", "Props/ice_cart", new Vector3(48f, -3.9f, 0f));
        Solid(cart, 1.9f, 0.6f);
        Act(cart, "Take ice", "icecart");

        var pen = Prop("Goat Pen", "Props/pen", new Vector3(-33f, 20.9f, 0f));
        var stall = Child(pen, "Stall", new Vector3(0f, -0.8f, 0f)).transform;

        var goat = Prop("Lakshmi", "Characters/goat_idle", new Vector3(12f, -21.4f, 0f));
        Body(goat, 0.22f);
        goat.AddComponent<NetworkObject>();
        SyncedPosition(goat);
        goat.AddComponent<NetworkRigidbody2D>();
        var brain = goat.AddComponent<Goat>();
        Set(brain, "body", goat.GetComponent<SpriteRenderer>());
        Set(brain, "idle", Load("Characters/goat_idle"));
        Set(brain, "walk1", Load("Characters/goat_walk1"));
        Set(brain, "walk2", Load("Characters/goat_walk2"));
        Set(brain, "pen", stall);
        Talk(goat, "Pet", "Lakshmi|Meh-eh-eh.", "|(She eyes your pockets for banana leaves.)");
    }

    // Chapter 2: the sluice bridge and its valves, the scarecrow, the farmer
    // and the pump-house. Hands the pieces Quests needs to control over to it.
    static void BuildFields(SerializedObject quests)
    {
        // The crossing: blocked by a raised gate until all three valves are open.
        var blocker = Child(props, "Sluice Blocker", new Vector3(103f, 0f, 0f));
        blocker.AddComponent<BoxCollider2D>().size = new Vector2(4f, 2.4f);
        var gate = Prop("Sluice Gate", "Props/sluice_gate", new Vector3(100.6f, -1.2f, 0f));
        Act(gate, "Read", "sluice");
        var plank = Prop("Plank Bridge", "Decals/plank_bridge", new Vector3(103f, 0f, 0f));
        plank.GetComponent<SpriteRenderer>().sortingOrder = DecalOrder;

        Vector3[] valveSpots = { new(84.5f, 21.2f, 0f), new(84.5f, -22.6f, 0f), new(98.6f, 11.3f, 0f) };
        var valves = quests.FindProperty("valves");
        valves.arraySize = valveSpots.Length;
        for (int i = 0; i < valveSpots.Length; i++)
        {
            var valve = Prop("Valve", "Props/valve_shut", valveSpots[i]);
            Solid(valve, 0.5f, 0.3f);
            Act(valve, "Turn the wheel", $"valve{i}");
            Glow(valve, new Vector3(0f, 1f, 0f), new Color(1f, 0.5f, 0.4f, 0.6f), 0.3f);
            valves.GetArrayElementAtIndex(i).objectReferenceValue = valve.GetComponent<SpriteRenderer>();
        }

        var scarecrow = Prop("Scarecrow", "Props/scarecrow", new Vector3(134.5f, 17.6f, 0f));
        Solid(scarecrow, 0.4f, 0.3f);
        Act(scarecrow, "Search", "scarecrow");

        var farmer = Prop("Farmer Periyasamy", "Characters/farmer", new Vector3(131.5f, 2.4f, 0f));
        Solid(farmer, 0.6f, 0.4f);
        Act(farmer, "Talk", "farmer");
        var lantern = Child(farmer, "Lantern", new Vector3(0.5f, 0.6f, 0f));
        Light(lantern, new Color(1f, 0.7f, 0.35f), 1.2f, 0.3f, 3.5f);
        lantern.AddComponent<FlickerLight>();
        Glow(farmer, new Vector3(0.5f, 0.6f, 0f), new Color(1f, 0.75f, 0.4f), 0.3f);

        // The pump is powered by minminis exactly like a streetlight, but hungrier.
        var pumpHouse = Building("Pump-house", "Props/pump_off", 137f, 3f);
        var pool = Light(Child(pumpHouse, "Pool", new Vector3(-0.8f, -0.6f, 0f)), Warm, 1.4f, 1.2f, 6.5f);
        var glow = Glow(pumpHouse, new Vector3(0.55f, 1.5f, 0f), new Color(1f, 0.93f, 0.7f), 0.7f);
        pumpHouse.AddComponent<NetworkObject>();
        var pump = pumpHouse.AddComponent<StreetLight>();
        Set(pump, "needed", 5);
        Set(pump, "isStreetlight", false);
        Set(pump, "pole", pumpHouse.GetComponent<SpriteRenderer>());
        Set(pump, "litSprite", Load("Props/pump_on"));
        Set(pump, "deadSprite", Load("Props/pump_off"));
        Set(pump, "pool", pool);
        Set(pump, "glow", glow);

        var minnal = Prop("Minnal", "Characters/minnal", new Vector3(137f, 6.4f, 0f));
        var minnalSprite = minnal.GetComponent<SpriteRenderer>();
        minnalSprite.sharedMaterial = GlowMaterial();
        minnalSprite.sortingOrder = GlowOrder;
        Set(minnal.AddComponent<MinnalCameo>(), "glow", Light(minnal, new Color(1f, 0.95f, 0.6f), 3f, 0.3f, 5f));
        minnal.SetActive(false);

        foreach (var spot in new[] { new Vector3(130f, -4.5f, 0f), new Vector3(142f, -3f, 0f), new Vector3(144f, 6f, 0f), new Vector3(129.5f, 7f, 0f) })
        {
            var tree = Prop("Coconut Tree", "Props/coconut_1", spot);
            tree.AddComponent<CircleCollider2D>().radius = 0.25f;
        }

        quests.FindProperty("bridgeBlocker").objectReferenceValue = blocker;
        quests.FindProperty("bridgeRaised").objectReferenceValue = gate;
        quests.FindProperty("bridgeLowered").objectReferenceValue = plank;
        quests.FindProperty("valveShut").objectReferenceValue = Load("Props/valve_shut");
        quests.FindProperty("valveOpen").objectReferenceValue = Load("Props/valve_open");
        quests.FindProperty("pump").objectReferenceValue = pump;
        quests.FindProperty("minnal").objectReferenceValue = minnal;
    }

    // Chapter 3: the cinema yard. A projector, three mirrors to bounce its beam
    // onto the screen, three lost film reels, the watchman, and the ghosts.
    static void BuildCinema(SerializedObject quests, GameObject northBarricade, WorldSpawner spawner)
    {
        blocked.Add(new Rect(Yard.xMin - 2, Yard.yMin - 2, Yard.width + 4, Yard.height + 4));

        var screen = Prop("Cinema Screen", "Props/cinema_screen", new Vector3(12f, 84f, 0f));
        Solid(screen, 9.6f, 0.5f);
        var target = Child(screen, "Beam Target", new Vector3(0f, 2f, 0f));
        var screenGlow = Child(screen, "Film Light", new Vector3(0f, 0.5f, 0f));
        Light(screenGlow, new Color(0.95f, 0.95f, 1f), 1.5f, 3f, 16f);
        screenGlow.AddComponent<FlickerLight>();
        screenGlow.SetActive(false);

        var booth = Building("Projector Booth", "Props/projector_off", -12f, 64.5f);
        Act(booth, "Inspect", "projector");
        var origin = Child(booth, "Beam Origin", new Vector3(1.5f, 1.72f, 0f));
        var pool = Light(Child(booth, "Pool", new Vector3(0.5f, -0.6f, 0f)), Warm, 1.3f, 1f, 5.5f);
        var glow = Glow(booth, new Vector3(1.4f, 1.72f, 0f), new Color(1f, 1f, 0.9f), 0.5f);
        booth.AddComponent<NetworkObject>();
        var projector = booth.AddComponent<StreetLight>();
        Set(projector, "needed", 4);
        Set(projector, "isStreetlight", false);
        Set(projector, "pole", booth.GetComponent<SpriteRenderer>());
        Set(projector, "litSprite", Load("Props/projector_on"));
        Set(projector, "deadSprite", Load("Props/projector_off"));
        Set(projector, "pool", pool);
        Set(projector, "glow", glow);

        // The beam leaves the booth heading east at this height; mirrors stand
        // so their glass (0.7 above their feet) is exactly in its path.
        float beamY = origin.transform.position.y;
        Vector3[] mirrorSpots = { new(0f, beamY - 0.7f, 0f), new(0f, beamY + 11.3f, 0f), new(12f, beamY + 11.3f, 0f) };
        var mirrors = quests.FindProperty("mirrors");
        mirrors.arraySize = mirrorSpots.Length;
        for (int i = 0; i < mirrorSpots.Length; i++)
        {
            var mirror = Prop("Mirror", "Props/mirror_backslash", mirrorSpots[i]);
            Solid(mirror, 0.5f, 0.3f);
            Act(mirror, "Turn the mirror", $"mirror{i}");
            mirrors.GetArrayElementAtIndex(i).objectReferenceValue = mirror.GetComponent<SpriteRenderer>();
        }

        var beam = new GameObject("Projector Beam").AddComponent<LineRenderer>();
        beam.sharedMaterial = GlowMaterial();
        beam.startWidth = beam.endWidth = 0.3f;
        beam.startColor = beam.endColor = new Color(1f, 0.96f, 0.7f, 0.9f);
        beam.sortingOrder = GlowOrder;
        beam.enabled = false;

        foreach (float y in new[] { 69f, 72f, 75f })
        foreach (float x in new[] { -19f, -15f, -9f, -5f, 5f, 9f, 16f, 20f })
            Solid(Prop("Bench", "Props/bench", new Vector3(x, y, 0f)), 1.9f, 0.4f);

        Vector3[] reels = { new(-19f, 88f, 0f), new(20.5f, 61f, 0f), new(18f, 80f, 0f) };
        for (int i = 0; i < reels.Length; i++)
            Pickup("Film Reel", "Items/reel", reels[i], $"reel{i}");

        var watchman = Prop("Watchman Kannan", "Characters/watchman", new Vector3(3.4f, 56.4f, 0f));
        Solid(watchman, 0.6f, 0.4f);
        Act(watchman, "Talk", "watchman");
        var lantern = Child(watchman, "Lantern", new Vector3(0.5f, 0.6f, 0f));
        Light(lantern, new Color(1f, 0.7f, 0.35f), 1.2f, 0.3f, 3.5f);
        lantern.AddComponent<FlickerLight>();
        Glow(watchman, new Vector3(0.5f, 0.6f, 0f), new Color(1f, 0.75f, 0.4f), 0.3f);

        var minnal = Prop("Minnal On Screen", "Characters/minnal", new Vector3(12f, 86.2f, 0f));
        var minnalSprite = minnal.GetComponent<SpriteRenderer>();
        minnalSprite.sharedMaterial = GlowMaterial();
        minnalSprite.sortingOrder = GlowOrder;
        var cameo = minnal.AddComponent<MinnalCameo>();
        Set(cameo, "glow", Light(minnal, new Color(1f, 0.95f, 0.6f), 3f, 0.3f, 5f));
        var escape = new SerializedObject(cameo);
        escape.FindProperty("escape").vector3Value = new Vector3(-34f, -6f, 0f);
        escape.ApplyModifiedPropertiesWithoutUndo();
        minnal.SetActive(false);

        // The ghosts are spawned by the server when a game starts.
        var ghost = new GameObject("Ghost");
        var sheet = Child(ghost, "Sheet", new Vector3(0f, 0.2f, 0f)).AddComponent<SpriteRenderer>();
        sheet.sprite = Load("Characters/ghost");
        sheet.sharedMaterial = GlowMaterial(); // faintly visible even in the dark
        sheet.color = new Color(0.8f, 0.85f, 1f, 0.9f);
        sheet.sortingOrder = GlowOrder;
        ghost.AddComponent<NetworkObject>();
        SyncedPosition(ghost);
        Set(ghost.AddComponent<Ghost>(), "body", sheet);
        var ghostPrefab = SavePrefab(ghost);
        AddSpawnable(ghostPrefab);

        var haunts = new GameObject("GhostSpots");
        foreach (var spot in new[] { new Vector3(-15f, 84f, 0f), new Vector3(17f, 66f, 0f), new Vector3(14f, 79f, 0f) })
            Child(haunts, "Haunt", spot);
        Set(spawner, "ghostPrefab", ghostPrefab.GetComponent<NetworkObject>());
        Set(spawner, "ghostSpots", haunts.transform);

        var spook = new GameObject("Spook Point");
        spook.transform.position = new Vector3(0f, 54.5f, 0f);

        Fill(quests.FindProperty("pumpOff"), new List<GameObject> { northBarricade });
        quests.FindProperty("projector").objectReferenceValue = projector;
        quests.FindProperty("beamOrigin").objectReferenceValue = origin.transform;
        quests.FindProperty("screenTarget").objectReferenceValue = target.transform;
        quests.FindProperty("spookPoint").objectReferenceValue = spook.transform;
        quests.FindProperty("mirrorSlash").objectReferenceValue = Load("Props/mirror_slash");
        quests.FindProperty("mirrorBackslash").objectReferenceValue = Load("Props/mirror_backslash");
        quests.FindProperty("beam").objectReferenceValue = beam;
        quests.FindProperty("screenGlow").objectReferenceValue = screenGlow;
        quests.FindProperty("minnalOnScreen").objectReferenceValue = minnal;
    }

    // Chapter 4: the goods yard. Three engines shunting on three tracks, four
    // lost lanterns, three point levers, and the signal cabin to power.
    static void BuildGoodsYard(SerializedObject quests, GameObject westBarricade)
    {
        (float x, float speed)[] engines = { (-100f, 9f), (-125f, 13f), (-90f, 7f) };
        for (int i = 0; i < Tracks.Length; i++)
        {
            var engine = Prop("Shunting Engine", "Props/engine", new Vector3(engines[i].x, Tracks[i] + 0.2f, 0f));
            Light(Child(engine, "Headlamp", new Vector3(0f, 1.3f, 0f)), new Color(1f, 0.95f, 0.75f), 1.4f, 1f, 6f);
            Glow(engine, new Vector3(0f, 1.4f, 0f), new Color(1f, 0.95f, 0.7f), 0.5f);
            engine.AddComponent<NetworkObject>();
            SyncedPosition(engine);
            var train = new SerializedObject(engine.AddComponent<Train>());
            train.FindProperty("body").objectReferenceValue = engine.GetComponent<SpriteRenderer>();
            train.FindProperty("speed").floatValue = engines[i].speed;
            train.ApplyModifiedPropertiesWithoutUndo();
        }

        var cabinHouse = Building("Signal Cabin", "Props/cabin_off", -136f, -10.6f);
        Act(cabinHouse, "Inspect", "cabin");
        var pool = Light(Child(cabinHouse, "Pool", new Vector3(0f, -0.6f, 0f)), Warm, 1.4f, 1.2f, 6.5f);
        var glow = Glow(cabinHouse, new Vector3(1.7f, 4f, 0f), new Color(0.5f, 1f, 0.6f), 0.6f);
        cabinHouse.AddComponent<NetworkObject>();
        var cabin = cabinHouse.AddComponent<StreetLight>();
        Set(cabin, "needed", 5);
        Set(cabin, "isStreetlight", false);
        Set(cabin, "pole", cabinHouse.GetComponent<SpriteRenderer>());
        Set(cabin, "litSprite", Load("Props/cabin_on"));
        Set(cabin, "deadSprite", Load("Props/cabin_off"));
        Set(cabin, "pool", pool);
        Set(cabin, "glow", glow);

        var levers = quests.FindProperty("levers");
        levers.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var lever = Prop("Point Lever", "Props/lever_down", new Vector3(-130f + i * 2f, -12.4f, 0f));
            Solid(lever, 0.5f, 0.3f);
            Act(lever, "Pull the lever", $"lever{i}");
            levers.GetArrayElementAtIndex(i).objectReferenceValue = lever.GetComponent<SpriteRenderer>();
        }
        var board = Prop("Notice Board", "Props/notice_board", new Vector3(-123f, -12.3f, 0f));
        Solid(board, 1.2f, 0.3f);
        Act(board, "Read", "points");

        Vector3[] lanterns = { new(-92f, -17.5f, 0f), new(-118f, -27f, 0f), new(-104f, -35.5f, 0f), new(-143f, -18f, 0f) };
        for (int i = 0; i < lanterns.Length; i++)
            Pickup("Signal Lantern", "Items/lantern", lanterns[i], $"lantern{i}");

        var rani = Prop("Signal Rani", "Characters/rani", new Vector3(-72.5f, -27.6f, 0f));
        Solid(rani, 0.6f, 0.4f);
        Act(rani, "Talk", "rani");
        var lamp = Child(rani, "Lantern", new Vector3(0.5f, 0.6f, 0f));
        Light(lamp, new Color(1f, 0.7f, 0.35f), 1.2f, 0.3f, 3.5f);
        lamp.AddComponent<FlickerLight>();
        Glow(rani, new Vector3(0.5f, 0.6f, 0f), new Color(1f, 0.75f, 0.4f), 0.3f);

        var entrance = new GameObject("Yard Entrance");
        entrance.transform.position = new Vector3(-67.5f, -24.5f, 0f);

        var minnal = Prop("Minnal On Signal", "Characters/minnal", new Vector3(-134.3f, -6.2f, 0f));
        var minnalSprite = minnal.GetComponent<SpriteRenderer>();
        minnalSprite.sharedMaterial = GlowMaterial();
        minnalSprite.sortingOrder = GlowOrder;
        var cameo = minnal.AddComponent<MinnalCameo>();
        Set(cameo, "glow", Light(minnal, new Color(1f, 0.95f, 0.6f), 3f, 0.3f, 5f));
        var escape = new SerializedObject(cameo);
        escape.FindProperty("escape").vector3Value = new Vector3(-30f, 14f, 0f);
        escape.ApplyModifiedPropertiesWithoutUndo();
        minnal.SetActive(false);

        Fill(quests.FindProperty("filmOff"), new List<GameObject> { westBarricade });
        quests.FindProperty("cabin").objectReferenceValue = cabin;
        quests.FindProperty("leverUp").objectReferenceValue = Load("Props/lever_up");
        quests.FindProperty("leverDown").objectReferenceValue = Load("Props/lever_down");
        quests.FindProperty("yardEntrance").objectReferenceValue = entrance.transform;
        quests.FindProperty("minnalOnSignal").objectReferenceValue = minnal;
    }

    // Adds a prefab to the list of things the server is allowed to spawn.
    static void AddSpawnable(GameObject prefab)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/Prefabs/SpawnablePrefabs.asset");
        list.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(list);
    }

    // A small thing lying on the ground, with a faint glint so a torch can find it.
    static void Pickup(string name, string sprite, Vector3 position, string action)
    {
        var item = Prop(name, sprite, position);
        Glow(item, new Vector3(0f, 0.15f, 0f), new Color(1f, 1f, 0.9f, 0.5f), 0.22f);
        Act(item, "Pick up", action);
    }

    static Transform BuildBandicootSpots()
    {
        var spots = new GameObject("BandicootSpots").transform;
        var random = new System.Random(5);
        while (spots.childCount < 9)
        {
            float x = random.Next(-52, 52), y = random.Next(-28, 30);
            if (GroundAt((int)x, (int)y) != "grass") continue;
            if (Vector2.Distance(new Vector2(x, y), new Vector2(-9f, 0.5f)) < 16f) continue; // a calm start
            Child(spots.gameObject, "Lair", new Vector3(x, y, 0f));
        }
        foreach (var lair in new[] { new Vector3(90f, -11.5f, 0f), new Vector3(84.5f, 16f, 0f), new Vector3(116.5f, 9f, 0f), new Vector3(110.5f, -10f, 0f) })
            Child(spots.gameObject, "Lair", lair);
        return spots;
    }

    static void BuildTrees()
    {
        var random = new System.Random(11);
        int planted = 0;
        for (int attempt = 0; attempt < 6000 && planted < 300; attempt++)
        {
            float x = random.Next(WestEnd, HalfWidth + 6) + (float)random.NextDouble();
            float y = random.Next(-HalfHeight - 4, NorthEnd + 4) + (float)random.NextDouble();
            var point = new Vector2(x, y);

            // Trees only grow on open grass, with a little room around the trunk.
            bool open = true;
            for (int dx = -1; dx <= 1 && open; dx++)
            for (int dy = -1; dy <= 1 && open; dy++)
                open = GroundAt(Mathf.FloorToInt(x) + dx, Mathf.FloorToInt(y) + dy) == "grass";
            foreach (var rect in blocked)
                if (rect.Contains(point)) open = false;
            if (!open) continue;

            var tree = Prop("Coconut Tree", $"Props/coconut_{random.Next(3)}", new Vector3(x, y, 0f));
            var trunk = tree.AddComponent<CircleCollider2D>();
            trunk.radius = 0.25f;
            trunk.offset = new Vector2(0f, 0.2f);
            blocked.Add(new Rect(x - 2.5f, y - 2f, 5f, 4f));
            planted++;
        }
    }

    static Transform BuildMinminiSpots()
    {
        var spots = new GameObject("MinminiSpots").transform;
        var random = new System.Random(23);
        while (spots.childCount < 90)
        {
            float x = random.Next(-56, 56) + (float)random.NextDouble();
            float y = random.Next(-30, 32) + (float)random.NextDouble();
            string ground = GroundAt(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
            if (ground == "water" || ground == "paddy") continue;
            if (Vector2.Distance(new Vector2(x, y), new Vector2(-9f, 0.5f)) < 7f) continue; // not right at the start
            Child(spots.gameObject, "Spot", new Vector3(x, y, 0f));
        }
        while (spots.childCount < 110) // some around the cinema
        {
            float x = random.Next(-22, 22) + 0.5f, y = random.Next(44, 90) + 0.5f;
            if (!Blocks(GroundAt((int)x, (int)y))) Child(spots.gameObject, "Spot", new Vector3(x, y, 0f));
        }
        while (spots.childCount < 132) // some in the goods yard
        {
            float x = random.Next(GoodsYard.xMin + 2, GoodsYard.xMax - 2) + 0.5f, y = random.Next(GoodsYard.yMin + 2, GoodsYard.yMax - 2) + 0.5f;
            Child(spots.gameObject, "Spot", new Vector3(x, y, 0f));
        }
        while (spots.childCount < 162) // and some out in the fields
        {
            float x = random.Next(FieldsStart, FieldsEnd - 2) + 0.5f, y = random.Next(-24, 23) + 0.5f;
            if (!Blocks(GroundAt((int)x, (int)y))) Child(spots.gameObject, "Spot", new Vector3(x, y, 0f));
        }
        return spots;
    }

    static void BuildBounds()
    {
        var edge = new GameObject("World Bounds").AddComponent<EdgeCollider2D>();
        edge.points = new[]
        {
            new Vector2(WestEnd, -HalfHeight), new Vector2(FieldsEnd, -HalfHeight),
            new Vector2(FieldsEnd, NorthEnd), new Vector2(WestEnd, NorthEnd),
            new Vector2(WestEnd, -HalfHeight),
        };
    }

    // ------------------------------------------------------------ building blocks

    static GameObject House(float x, float y, int index)
    {
        var house = Building($"House {index}", $"Props/house_{(index * 3 + 1) % 4}", x, y);
        var kolam = Prop("Kolam", "Decals/kolam", new Vector3(x, y - 1f, 0f));
        kolam.GetComponent<SpriteRenderer>().sortingOrder = DecalOrder;
        kolam.transform.localScale = Vector3.one * 0.85f;
        if (index % 3 == 1) Solid(Prop("Tulsi", "Props/tulsi", new Vector3(x + 2.75f, y - 0.35f, 0f)), 0.5f, 0.4f);

        // Lit windows, switched on when the power comes back.
        var power = Child(house, "Power", Vector3.zero);
        foreach (float side in new[] { -1.15f, 1.2f })
            Glow(power, new Vector3(side, 1.65f, 0f), new Color(1f, 0.88f, 0.55f), 0.5f);
        if (index % 2 == 0) Light(Child(power, "Window Light", new Vector3(0f, 1.2f, 0f)), Warm, 0.9f, 0.5f, 3.2f);
        power.SetActive(false);
        powerLights.Add(power);
        return house;
    }

    // A solid, shadow-casting building whose front door sits at (x, y).
    static GameObject Building(string name, string sprite, float x, float y)
    {
        var building = Prop(name, sprite, new Vector3(x, y, 0f));
        var size = building.GetComponent<SpriteRenderer>().sprite.bounds.size;
        var wall = building.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(size.x - 0.15f, size.y * 0.82f);
        wall.offset = new Vector2(0f, wall.size.y / 2f);
        building.AddComponent<ShadowCaster2D>().selfShadows = false;
        blocked.Add(new Rect(x - size.x / 2f - 1.5f, y - 1f, size.x + 3f, size.y + 2f));
        return building;
    }

    static void Streetlight(float x, float y, bool lit)
    {
        var lamp = Prop("Streetlight", lit ? "Props/streetlight_on" : "Props/streetlight_off", new Vector3(x, y, 0f));
        Solid(lamp, 0.3f, 0.3f);
        var pool = Light(Child(lamp, "Pool", new Vector3(0.5f, -0.9f, 0f)), Warm, 1.35f, 1.2f, 6f);
        var glow = Glow(lamp, new Vector3(0.28f, 3f, 0f), new Color(1f, 0.93f, 0.7f), 0.9f);

        lamp.AddComponent<NetworkObject>();
        var streetLight = lamp.AddComponent<StreetLight>();
        Set(streetLight, "startsLit", lit);
        Set(streetLight, "pole", lamp.GetComponent<SpriteRenderer>());
        Set(streetLight, "litSprite", Load("Props/streetlight_on"));
        Set(streetLight, "deadSprite", Load("Props/streetlight_off"));
        Set(streetLight, "pool", pool);
        Set(streetLight, "glow", glow);
    }

    static void Person(string name, string sprite, Vector3 position, string action)
    {
        var person = Prop(name, sprite, position);
        Solid(person, 0.6f, 0.4f);
        Act(person, "Talk", action);
    }

    // What is said and what happens is decided by Quests, using this action name.
    static void Act(GameObject go, string verb, string action)
    {
        var interactable = go.AddComponent<Interactable>();
        interactable.verb = verb;
        interactable.action = action;
    }

    static void Fill(SerializedProperty array, List<GameObject> objects)
    {
        array.arraySize = objects.Count;
        for (int i = 0; i < objects.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }

    static void Talk(GameObject go, string verb, params string[] lines)
    {
        var interactable = go.AddComponent<Interactable>();
        interactable.verb = verb;
        interactable.lines = lines;
    }

    static void Candle(GameObject house, Vector3 window)
    {
        var flame = Child(house, "Candle", window);
        Light(flame, new Color(1f, 0.62f, 0.3f), 1.1f, 0.2f, 2.2f);
        flame.AddComponent<FlickerLight>();
        Glow(house, window, new Color(1f, 0.75f, 0.4f), 0.45f);
    }

    // ------------------------------------------------------------ look

    static void BuildLighting()
    {
        // Moonlight: everything is dim and blue unless another light reaches it.
        var moon = new GameObject("Moonlight").AddComponent<Light2D>();
        moon.lightType = Light2D.LightType.Global;
        moon.color = new Color(0.38f, 0.46f, 0.85f);
        moon.intensity = 0.42f;

        const string profilePath = "Assets/Settings/NightProfile.asset";
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);

        var bloom = AddEffect<Bloom>(profile);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(1.1f);
        bloom.scatter.Override(0.72f);
        var vignette = AddEffect<Vignette>(profile);
        vignette.intensity.Override(0.34f);
        vignette.smoothness.Override(0.45f);
        var grade = AddEffect<ColorAdjustments>(profile);
        grade.contrast.Override(10f);
        grade.saturation.Override(8f);

        var volume = new GameObject("Night Look").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    static T AddEffect<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var effect = profile.Add<T>(true);
        AssetDatabase.AddObjectToAsset(effect, profile);
        return effect;
    }

    static void BuildCamera()
    {
        var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
        camera.transform.position = new Vector3(-8f, 3.5f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 6.75f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
        camera.gameObject.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        // Keeps pixels square and crisp at any window size: a 384x216 "screen".
        var pixelPerfect = camera.gameObject.AddComponent<PixelPerfectCamera>();
        pixelPerfect.assetsPPU = 16;
        pixelPerfect.refResolutionX = 384;
        pixelPerfect.refResolutionY = 216;

        camera.gameObject.AddComponent<CameraFollow>();

        // Ordinary fireflies drift around wherever the camera goes. They are
        // only decoration; the minminis you can lead are separate objects.
        var fireflies = Child(camera.gameObject, "Fireflies", new Vector3(0f, 0f, 10f)).AddComponent<ParticleSystem>();
        var main = fireflies.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        main.startColor = new Color(0.9f, 1f, 0.7f, 0.8f);
        main.maxParticles = 60;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = fireflies.emission;
        emission.rateOverTime = 6f;
        var shape = fireflies.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 18f, 0f);
        shape.randomDirectionAmount = 1f;
        var noise = fireflies.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.2f;

        var fade = fireflies.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.25f, 0.4f),
                new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0.3f, 0.8f), new GradientAlphaKey(0f, 1f),
            });
        fade.color = gradient;

        var renderer = fireflies.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GlowMaterial();
        renderer.sortingOrder = GlowOrder;
    }

    static GameObject BuildNetwork(GameObject player, GameObject minmini, GameObject dog, GameObject bandicoot,
        Transform minminiSpots, Transform dogSpot)
    {
        var go = new GameObject("NetworkManager");
        var manager = go.AddComponent<NetworkManager>();
        manager.NetworkConfig.NetworkTransport = go.AddComponent<UnityTransport>();
        manager.NetworkConfig.PlayerPrefab = player;

        // Everything the server may spawn has to be on this list on every machine.
        const string listPath = "Assets/Prefabs/SpawnablePrefabs.asset";
        AssetDatabase.DeleteAsset(listPath);
        var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
        foreach (var prefab in new[] { player, minmini, dog, bandicoot })
            list.Add(new NetworkPrefab { Prefab = prefab });
        AssetDatabase.CreateAsset(list, listPath);
        manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
        manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
        EditorUtility.SetDirty(manager);

        go.AddComponent<SessionManager>();
        var spawner = go.AddComponent<WorldSpawner>();
        Set(spawner, "minminiPrefab", minmini.GetComponent<NetworkObject>());
        Set(spawner, "dogPrefab", dog.GetComponent<NetworkObject>());
        Set(spawner, "minminiSpots", minminiSpots);
        Set(spawner, "dogSpot", dogSpot);
        return go;
    }

    // ------------------------------------------------------------ helpers

    static readonly Dictionary<string, Tile> tiles = new();

    static Tile TileFor(string name)
    {
        if (tiles.TryGetValue(name, out var cached) && cached != null) return cached;

        string path = $"{Art}Tiles/{name}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }
        tile.sprite = Load($"Tiles/{name}");
        tile.colliderType = Blocks(name.Split('_')[0]) ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tiles[name] = tile;
    }

    static Sprite Load(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}{name}.png");
        if (sprite == null) throw new System.IO.FileNotFoundException($"Missing sprite {Art}{name}.png");
        return sprite;
    }

    // Fills in a [SerializeField] on a component.
    static void Set(Object component, string field, object value)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(field);
        if (value is bool flag) property.boolValue = flag;
        else if (value is int number) property.intValue = number;
        else property.objectReferenceValue = (Object)value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject Child(GameObject parent, string name, Vector3 localPosition)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = localPosition;
        return child;
    }

    static GameObject Prop(string name, string sprite, Vector3 position)
    {
        var prop = Child(props, name, position);
        var renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = Load(sprite);
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        return prop;
    }

    static void Solid(GameObject prop, float width, float height)
    {
        var box = prop.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, height);
        box.offset = new Vector2(0f, height / 2f);
    }

    static Light2D Light(GameObject go, Color color, float intensity, float inner, float outer)
    {
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightInnerRadius = inner;
        light.pointLightOuterRadius = outer;
        light.falloffIntensity = 0.65f;
        return light;
    }

    static Material GlowMaterial()
    {
        const string path = Art + "Decals/Glow.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.mainTexture = Load("Decals/glow").texture;
        return material;
    }

    // A small unlit dot that the bloom effect turns into a soft halo.
    static SpriteRenderer Glow(GameObject parent, Vector3 localPosition, Color color, float size)
    {
        var glow = Child(parent, "Glow", localPosition).AddComponent<SpriteRenderer>();
        glow.sprite = Load("Decals/glow");
        glow.sharedMaterial = GlowMaterial();
        glow.color = color;
        glow.sortingOrder = GlowOrder;
        glow.transform.localScale = Vector3.one * size * 2f;
        return glow;
    }
}
