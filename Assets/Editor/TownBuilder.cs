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

    static readonly Color Warm = new(1f, 0.8f, 0.5f);

    static readonly List<Rect> blocked = new(); // footprints of buildings, to keep trees off them
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

        props = new GameObject("Props");
        BuildGround();
        BuildKamarajarStreet();
        BuildBazaar();
        BuildTankRoad();
        BuildExits();
        BuildTrees();
        BuildLighting();
        BuildCamera();
        BuildBounds();

        new GameObject("SpawnPoint").transform.position = new Vector3(-9f, 0.5f, 0f);
        var dogSpot = new GameObject("DogSpot").transform;
        dogSpot.position = new Vector3(-5f, 2.8f, 0f);
        BuildNetwork(playerPrefab, minminiPrefab, dogPrefab, BuildMinminiSpots(), dogSpot);
        new GameObject("GameUI").AddComponent<GameUI>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.DeleteAsset("Assets/Scenes/Street.unity");
        AssetDatabase.SaveAssets();
        return $"built {ScenePath}: {StreetLightCount()} streetlights";
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

        var controller = new SerializedObject(root.AddComponent<PlayerController>());
        controller.FindProperty("body").objectReferenceValue = body;
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

    static string GroundAt(int x, int y)
    {
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

        for (int x = -HalfWidth - 16; x < HalfWidth + 16; x++)
        for (int y = -HalfHeight - 10; y < HalfHeight + 10; y++)
        {
            int variant = Mathf.Abs(x * 7 + y * 13 + x * y) % 3;
            tilemap.SetTile(new Vector3Int(x, y, 0), TileFor($"{GroundAt(x, y)}_{variant}"));
        }

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
        Person("Paati", "Characters/paati", new Vector3(-9.3f, 3.5f, 0f),
            "Paati|No current, no serial, no cricket. Thirty years I have paid that Electricity Board.",
            "Paati|Go and find Murugesan, kanna. He is hiding by the streetlight, I can see his helmet from here.",
            "Paati|And take Battery with you. That dog eats more than you do, let him work for it.");

        Solid(Prop("Cycle", "Props/cycle", new Vector3(-13.4f, 2.7f, 0f)), 1.4f, 0.4f);
        Person("Lineman Murugesan", "Characters/lineman", new Vector3(-12.1f, 3f, 0f),
            "Lineman Murugesan|It is NOT the fuse. I checked the fuse. I checked it four times.",
            "Lineman Murugesan|The lines are empty, thambi. Like somebody drank the current with a straw.",
            "Lineman Murugesan|See those little glowing bugs? Minminis. Each one is carrying a sip of it.",
            "Lineman Murugesan|Shine your torch at them and they follow you. Walk three of them to a dead streetlight and it wakes up. Don't ask me why. I only work here.");

        // The cricket ground behind the eastern houses.
        Solid(Prop("Stumps", "Props/stumps", new Vector3(28f, 17.6f, 0f)), 0.4f, 0.3f);
        Solid(Prop("Stumps", "Props/stumps", new Vector3(28f, 9.6f, 0f)), 0.4f, 0.3f);
        Person("Umpire Ravi", "Characters/abdul_idle", new Vector3(30.6f, 13.5f, 0f),
            "Umpire Ravi|We were playing our own final. Then the real final went dark and everybody ran home.",
            "Umpire Ravi|34 off 24. If the current doesn't come back we will never know. NEVER.");

        Solid(Prop("Well", "Props/well", new Vector3(-30f, 14f, 0f)), 1.4f, 0.9f);
        blocked.Add(new Rect(-32f, 12f, 4f, 4f));
    }

    static void BuildBazaar()
    {
        float doorstep = Streets[0] + 6;

        Building("Wedding Hall", "Props/wedding_hall", -46f, doorstep);
        Person("The Chairman", "Characters/chairman", new Vector3(-41f, doorstep - 0.7f, 0f),
            "The Chairman|I have a speech at nine. NINE! How will the people see my face in the dark?",
            "The Chairman|...Don't answer that.");

        var stall = Building("Tea Stall", "Props/tea_stall", -34f, doorstep);
        var stove = Child(stall, "Stove", new Vector3(0.85f, 1.25f, 0f));
        Light(stove, new Color(1f, 0.55f, 0.25f), 1.2f, 0.2f, 2.6f);
        stove.AddComponent<FlickerLight>();
        Glow(stall, new Vector3(0.85f, 1.3f, 0f), new Color(1f, 0.6f, 0.3f), 0.35f);
        Person("Tea Master Selvam", "Characters/teamaster", new Vector3(-31f, doorstep - 0.8f, 0f),
            "Tea Master Selvam|No current, no mixie, no fridge. But tea? Tea runs on firewood, thambi.",
            "Tea Master Selvam|Light up the bazaar and your first tea is free. The second one is full price.");

        string[] west = { "shop_stores", "shop_tailor", "shop_medical" };
        for (int i = 0; i < west.Length; i++)
            Building("Shop", $"Props/{west[i]}", -26f + i * 6f, doorstep);

        for (int i = 0; i < 5; i++)
            House(8f + i * 6f, doorstep, 20 + i);

        Building("EB Office", "Props/eb_office", 43f, doorstep);
        var transformer = Prop("Transformer", "Props/transformer", new Vector3(48.5f, doorstep - 0.2f, 0f));
        Solid(transformer, 2.2f, 0.6f);
        transformer.AddComponent<ShadowCaster2D>().selfShadows = false;
        Talk(transformer, "Read", "|The transformer is stone cold. A sign says: DANGER, 11000 VOLTS. Tonight it should say: DANGER, 0 VOLTS.");

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
    static void BuildExits()
    {
        Barricade(new Vector3(61f, 0f, 0f), true,
            "|ROAD CLOSED. The line to the paddy fields is down.   (Chapter 2: The Pump-set)");
        Barricade(new Vector3(0f, 37f, 0f), false,
            "|ROAD CLOSED. Beyond here is the old Raja Talkies. Nobody goes there after dark.   (Chapter 3)");
        Barricade(new Vector3(-61f, -24f, 0f), true,
            "|ROAD CLOSED. The goods yard is past the level crossing.   (Chapter 4)");
    }

    static void Barricade(Vector3 centre, bool acrossHorizontalRoad, string sign)
    {
        for (int i = 0; i < 2; i++)
        {
            var offset = acrossHorizontalRoad ? new Vector3(i * 0.5f, -1.9f + i * 2f, 0f) : new Vector3(-1f + i * 2f, 0f, 0f);
            Talk(Prop("Barricade", "Props/barricade", centre + offset), "Read", sign);
        }
        var wall = new GameObject("Closed Road").AddComponent<BoxCollider2D>();
        wall.transform.position = centre;
        wall.size = acrossHorizontalRoad ? new Vector2(0.6f, 5f) : new Vector2(5f, 0.6f);
    }

    static void BuildTrees()
    {
        var random = new System.Random(11);
        int planted = 0;
        for (int attempt = 0; attempt < 2500 && planted < 150; attempt++)
        {
            float x = random.Next(-HalfWidth - 6, HalfWidth + 6) + (float)random.NextDouble();
            float y = random.Next(-HalfHeight - 4, HalfHeight + 4) + (float)random.NextDouble();
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
        return spots;
    }

    static void BuildBounds()
    {
        var edge = new GameObject("World Bounds").AddComponent<EdgeCollider2D>();
        edge.points = new[]
        {
            new Vector2(-HalfWidth, -HalfHeight), new Vector2(HalfWidth, -HalfHeight),
            new Vector2(HalfWidth, HalfHeight), new Vector2(-HalfWidth, HalfHeight),
            new Vector2(-HalfWidth, -HalfHeight),
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

    static void Person(string name, string sprite, Vector3 position, params string[] lines)
    {
        var person = Prop(name, sprite, position);
        Solid(person, 0.6f, 0.4f);
        Talk(person, "Talk", lines);
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

    static void BuildNetwork(GameObject player, GameObject minmini, GameObject dog, Transform minminiSpots, Transform dogSpot)
    {
        var go = new GameObject("NetworkManager");
        var manager = go.AddComponent<NetworkManager>();
        manager.NetworkConfig.NetworkTransport = go.AddComponent<UnityTransport>();
        manager.NetworkConfig.PlayerPrefab = player;

        // Everything the server may spawn has to be on this list on every machine.
        const string listPath = "Assets/Prefabs/SpawnablePrefabs.asset";
        AssetDatabase.DeleteAsset(listPath);
        var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
        foreach (var prefab in new[] { player, minmini, dog })
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
