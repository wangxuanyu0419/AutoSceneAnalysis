using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AutoSceneVR
{
    public static class RoomFactory
    {
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Transform room;
        static Material wood, cream, sage, dark, brass, teal, glass, ivory, skin, robot, blue;

        public static SimulationController Create()
        {
            materials.Clear();
            room = new GameObject("Photo room — authored approximation").transform;
            wood = Mat("Oak", new Color(.43f, .25f, .12f));
            cream = Mat("Warm plaster", new Color(.87f, .79f, .62f));
            sage = Mat("Sage wall", new Color(.24f, .38f, .31f));
            dark = Mat("Charcoal", new Color(.055f, .07f, .085f));
            brass = Mat("Brass", new Color(.61f, .44f, .20f), .55f);
            teal = Mat("Teal ceramic", new Color(.13f, .65f, .55f));
            glass = Mat("Daylight glass", new Color(.59f, .77f, .82f));
            ivory = Mat("Ivory", new Color(.93f, .90f, .79f));
            skin = Mat("Virtual skin", new Color(.73f, .43f, .28f));
            robot = Mat("Robot shell", new Color(.16f, .38f, .52f), .5f);
            blue = Mat("Trial blue", new Color(.18f, .42f, .69f));
            BuildArchitecture();
            BuildDining();
            BuildTv();
            BuildKitchen();
            BuildFireplace();
            BuildLighting();
            var simulationRoot = new GameObject("Simulation and input boundary");
            var sim = simulationRoot.AddComponent<SimulationController>();
            var viewerRoot = new GameObject("Seated viewer origin");
            viewerRoot.transform.position = new Vector3(-.55f, 1.25f, -.25f);
            var viewer = viewerRoot.AddComponent<ViewerController>();
            var cameraGo = new GameObject("Participant camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(viewerRoot.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 78;
            camera.nearClipPlane = .025f;
            camera.farClipPlane = 35;
            camera.backgroundColor = new Color(.51f, .65f, .73f);
            cameraGo.AddComponent<AudioListener>();
            var overviewGo = new GameObject("Room overview camera");
            overviewGo.transform.position = new Vector3(3.2f, 5.8f, -7.3f);
            overviewGo.transform.LookAt(new Vector3(0, .65f, .75f));
            var overview = overviewGo.AddComponent<Camera>();
            overview.fieldOfView = 49;
            overview.farClipPlane = 40;
            overview.cullingMask = ~(1 << 31);
            overviewGo.SetActive(false);
            viewer.viewCamera = camera; viewer.overviewCamera = overview; viewer.simulation = sim;
            sim.viewer = viewer;
            sim.arm = BuildArm(viewer.transform, skin, robot);
            var file = simulationRoot.AddComponent<BciFileInput>(); file.simulation = sim;
            file.enabledForSession = true;
            var hud = simulationRoot.AddComponent<SimulationHud>(); hud.simulation = sim; hud.fileInput = file;
            WorldPanel(sim);
            var reticle = Shape("Gaze dot", PrimitiveType.Sphere, cameraGo.transform, new Vector3(0, 0, .6f), Vector3.one * .004f, ivory);
            RemoveCollider(reticle);
            sim.gameObject.AddComponent<RuntimeValidation>();
            return sim;
        }

        static void BuildArchitecture()
        {
            Box("Floor base", room, new Vector3(0, -.10f, 0), new Vector3(8.4f, .2f, 7.4f), wood);
            // Individual boards keep the room readable without external textures.
            for (int row = 0; row < 25; row++) {
                var shade = Mat("Floor " + row % 5, new Color(.30f + (row % 5) * .028f, .17f + (row % 5) * .018f, .085f + (row % 5) * .009f));
                Box("Oak board course " + row, room, new Vector3(0, .004f, -3.55f + row * .295f), new Vector3(8.36f, .012f, .285f), shade);
            }
            Box("Back plaster wall", room, new Vector3(0, 1.45f, 3.72f), new Vector3(8.5f, 2.9f, .14f), cream);
            Box("Left plaster wall", room, new Vector3(-4.22f, 1.45f, 0), new Vector3(.14f, 2.9f, 7.5f), cream);
            Box("Right feature wall", room, new Vector3(4.22f, 1.45f, 0), new Vector3(.14f, 2.9f, 7.5f), sage);
            var ceiling = Box("Ceiling", room, new Vector3(0, 2.96f, 0), new Vector3(8.4f, .12f, 7.4f), ivory);
            ceiling.layer = 31;
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box("Back skirting", room, new Vector3(0, .08f, 3.61f), new Vector3(8.35f, .16f, .06f), ivory);
            Box("Left skirting", room, new Vector3(-4.11f, .08f, 0), new Vector3(.06f, .16f, 7.35f), ivory);
            Box("Right skirting", room, new Vector3(4.11f, .08f, 0), new Vector3(.06f, .16f, 7.35f), ivory);
            for (int i = 0; i < 3; i++) {
                float x = -2.9f + i * 1.35f;
                Box("Window frame " + i, room, new Vector3(x, 1.75f, 3.59f), new Vector3(1.07f, 1.75f, .10f), ivory);
                Box("Window daylight " + i, room, new Vector3(x, 1.76f, 3.52f), new Vector3(.89f, 1.55f, .025f), glass);
                Box("Window mullion " + i, room, new Vector3(x, 1.76f, 3.49f), new Vector3(.035f, 1.55f, .025f), ivory);
                Box("Window sill " + i, room, new Vector3(x, .9f, 3.42f), new Vector3(1.2f, .065f, .38f), wood);
                Plant("Sill plant " + i, room, new Vector3(x + .18f, .94f, 3.42f), .16f);
            }
            Box("Radiator", room, new Vector3(-2.4f, .47f, 3.40f), new Vector3(1.65f, .72f, .17f), ivory);
            for (int i = 0; i < 18; i++) Box("Radiator rib", room, new Vector3(-3.15f + i * .085f, .47f, 3.29f), new Vector3(.025f, .68f, .025f), cream);
            var rug = Mat("Woven sand", new Color(.64f, .63f, .50f));
            Box("Foreground rug", room, new Vector3(1.2f, .018f, -2.25f), new Vector3(2.7f, .016f, 1.65f), rug);
        }

        static void BuildDining()
        {
            var table = Target("table_01", "Dining table", new Vector3(-.6f, 0, .9f), new[] { "inspect" });
            Box("Tabletop", table.transform, new Vector3(0, .75f, 0), new Vector3(1.65f, .085f, 1.30f), wood);
            foreach (float x in new[] { -.67f, .67f }) foreach (float z in new[] { -.48f, .48f })
                Box("Table leg", table.transform, new Vector3(x, .36f, z), new Vector3(.075f, .72f, .075f), dark);
            Chair(new Vector3(-1.85f, 0, .9f), 90);
            Chair(new Vector3(.65f, 0, .9f), -90);
            Chair(new Vector3(-.6f, 0, 1.95f), 180);
            var mug = Target("mug_01", "Training mug", new Vector3(-.47f, .795f, .37f), new[] { "inspect", "reach", "grasp", "release" }, true);
            mug.provenance = "added experiment prop";
            mug.graspOffset = new Vector3(.105f, .08f, 0);
            Cylinder("Mug body", mug.transform, new Vector3(0, .065f, 0), new Vector3(.105f, .065f, .105f), teal);
            Cylinder("Mug interior", mug.transform, new Vector3(0, .131f, 0), new Vector3(.085f, .002f, .085f), dark);
            Box("Mug handle outer", mug.transform, new Vector3(.078f, .072f, 0), new Vector3(.022f, .075f, .025f), teal);
            Box("Mug handle top", mug.transform, new Vector3(.061f, .109f, 0), new Vector3(.05f, .02f, .025f), teal);
            Box("Mug handle bottom", mug.transform, new Vector3(.061f, .035f, 0), new Vector3(.05f, .02f, .025f), teal);
            AddPhysics(mug, .2f);
            var book = Target("book_01", "Training book", new Vector3(-.9f, .80f, .44f), new[] { "inspect", "reach", "grasp", "release" }, true);
            book.provenance = "added experiment prop";
            book.graspOffset = new Vector3(0, .025f, 0);
            Box("Book cover", book.transform, new Vector3(0, .022f, 0), new Vector3(.22f, .044f, .16f), blue);
            Box("Book pages", book.transform, new Vector3(.008f, .023f, 0), new Vector3(.21f, .025f, .15f), ivory);
            AddPhysics(book, .3f);
            var tray = Target("tray_01", "Placement tray", new Vector3(-.08f, .802f, .62f), new[] { "inspect" });
            tray.provenance = "added experiment destination";
            Box("Tray base", tray.transform, Vector3.zero, new Vector3(.27f, .018f, .26f), brass);
            foreach (float x in new[] { -.14f, .14f }) Box("Tray rim", tray.transform, new Vector3(x, .02f, 0), new Vector3(.013f, .04f, .28f), brass);
            foreach (float z in new[] { -.14f, .14f }) Box("Tray rim", tray.transform, new Vector3(0, .02f, z), new Vector3(.28f, .04f, .013f), brass);
            Label("PLACE", room, new Vector3(-.22f, .83f, .47f), .006f, Color.white);
        }

        static void BuildTv()
        {
            var tv = Target("tv_01", "Television", new Vector3(-4.03f, 1.08f, -.35f), new[] { "inspect", "switch_on", "switch_off" });
            tv.transform.localRotation = Quaternion.Euler(0, -90, 0);
            Box("TV frame", tv.transform, Vector3.zero, new Vector3(1.5f, .88f, .085f), dark);
            tv.stateRenderer = Box("TV screen", tv.transform, new Vector3(0, 0, -.052f), new Vector3(1.39f, .77f, .018f), Mat("Screen blue", new Color(.09f, .18f, .22f))).GetComponent<Renderer>();
            Box("TV stand", room, new Vector3(-3.98f, .42f, -.35f), new Vector3(.42f, .72f, 2.25f), ivory);
            Box("Console top", room, new Vector3(-3.96f, .80f, -.35f), new Vector3(.49f, .06f, 2.38f), dark);
            var remote = Target("remote_01", "Remote control", new Vector3(-3.70f, .85f, -.6f), new[] { "inspect", "press" });
            Box("Remote", remote.transform, Vector3.zero, new Vector3(.07f, .025f, .19f), dark);
            var mirror = new GameObject("Wall mirror").transform; mirror.SetParent(room);
            mirror.position = new Vector3(-4.10f, 2.05f, -.25f); mirror.rotation = Quaternion.Euler(0, -90, 0);
            Box("Mirror frame", mirror, Vector3.zero, new Vector3(1.65f, .70f, .07f), dark);
            Box("Mirror surface", mirror, new Vector3(0, 0, -.045f), new Vector3(1.54f, .60f, .02f), glass);
        }

        static void BuildKitchen()
        {
            Box("Kitchen base", room, new Vector3(2.47f, .44f, 3.28f), new Vector3(2.75f, .88f, .67f), ivory);
            Box("Countertop", room, new Vector3(2.47f, .91f, 3.24f), new Vector3(2.90f, .075f, .82f), dark);
            for (int i = 0; i < 4; i++) {
                Box("Cabinet door", room, new Vector3(1.48f + i * .65f, .44f, 2.925f), new Vector3(.62f, .81f, .025f), cream);
                Box("Cabinet handle", room, new Vector3(1.48f + i * .65f, .65f, 2.895f), new Vector3(.2f, .018f, .025f), brass);
            }
            var drawer = Target("drawer_01", "Kitchen drawer", new Vector3(1.48f, .78f, 2.89f), new[] { "inspect", "open", "close" });
            var drawerPart = new GameObject("Sliding drawer").transform; drawerPart.SetParent(drawer.transform, false);
            Box("Drawer front", drawerPart, Vector3.zero, new Vector3(.61f, .17f, .05f), wood);
            Box("Drawer handle", drawerPart, new Vector3(0, 0, -.045f), new Vector3(.22f, .022f, .035f), brass);
            Box("Drawer floor", drawerPart, new Vector3(0, -.04f, .22f), new Vector3(.57f, .03f, .45f), wood);
            drawer.animatedPart = drawerPart; drawer.openOffset = new Vector3(0, 0, -.40f);
            var door = Target("cabinet_01", "Upper cabinet", new Vector3(2.6f, 1.91f, 3.31f), new[] { "inspect", "open", "close" });
            Box("Cabinet case", door.transform, Vector3.zero, new Vector3(1.05f, .8f, .42f), wood);
            var hinge = new GameObject("Cabinet hinge").transform; hinge.SetParent(door.transform, false); hinge.localPosition = new Vector3(-.52f, 0, -.235f);
            Box("Hinged door", hinge, new Vector3(.52f, 0, 0), new Vector3(1.04f, .79f, .035f), cream);
            Box("Door handle", hinge, new Vector3(.94f, -.10f, -.04f), new Vector3(.02f, .19f, .025f), brass);
            door.animatedPart = hinge; door.openEuler = new Vector3(0, -85, 0);
            Cylinder("Sink basin", room, new Vector3(3.16f, .961f, 3.17f), new Vector3(.42f, .015f, .36f), Mat("Steel", new Color(.35f, .43f, .46f), .7f));
            Box("Tap", room, new Vector3(3.16f, 1.08f, 3.43f), new Vector3(.035f, .25f, .035f), brass);
            Box("Tap spout", room, new Vector3(3.16f, 1.20f, 3.34f), new Vector3(.035f, .035f, .20f), brass);
        }

        static void BuildFireplace()
        {
            Box("Fireplace surround", room, new Vector3(4.02f, .65f, .72f), new Vector3(.24f, 1.18f, 1.38f), ivory);
            Box("Fireplace opening", room, new Vector3(3.875f, .65f, .72f), new Vector3(.04f, 1.0f, 1.17f), dark);
            Box("Fireplace glow", room, new Vector3(3.846f, .40f, .72f), new Vector3(.012f, .32f, .80f), Mat("Fire amber", new Color(.78f, .22f, .04f)));
            Cylinder("Foreground round table", room, new Vector3(2.9f, .52f, -2.0f), new Vector3(1.0f, .035f, 1.0f), wood);
            Cylinder("Round table leg", room, new Vector3(2.9f, .25f, -2.0f), new Vector3(.10f, .25f, .10f), dark);
            var vase = Target("vase_01", "Green vase", new Vector3(2.9f, .56f, -2.0f), new[] { "inspect" });
            Shape("Vase belly", PrimitiveType.Sphere, vase.transform, new Vector3(0, .15f, 0), new Vector3(.20f, .28f, .20f), Mat("Vase green", new Color(.48f, .65f, .11f)));
            Cylinder("Vase neck", vase.transform, new Vector3(0, .30f, 0), new Vector3(.065f, .10f, .065f), Mat("Vase green", Color.green));
            Flower(vase.transform, new Vector3(0, .40f, 0), .50f);
            Plant("Corner plant", room, new Vector3(3.65f, 0, 2.76f), .55f);
        }

        static void BuildLighting()
        {
            var sun = new GameObject("Soft daylight"); sun.transform.rotation = Quaternion.Euler(46, -35, 0);
            var light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.8f;
            light.color = new Color(1, .91f, .76f); light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.70f, .78f, .85f);
            RenderSettings.ambientEquatorColor = new Color(.46f, .43f, .36f);
            RenderSettings.ambientGroundColor = new Color(.25f, .23f, .20f);
            var lamp = Target("lamp_01", "Pendant lamp", new Vector3(-.6f, 2.30f, .9f), new[] { "inspect", "switch_on", "switch_off", "press" });
            Cylinder("Lamp stem", lamp.transform, new Vector3(0, .28f, 0), new Vector3(.015f, .30f, .015f), dark);
            Shape("Lamp shade", PrimitiveType.Sphere, lamp.transform, Vector3.zero, new Vector3(.42f, .20f, .42f), ivory);
            var point = new GameObject("Warm lamp light"); point.transform.SetParent(lamp.transform, false); point.transform.localPosition = new Vector3(0, -.15f, 0);
            lamp.controlledLight = point.AddComponent<Light>(); lamp.controlledLight.type = LightType.Point;
            lamp.controlledLight.range = 4; lamp.controlledLight.intensity = 1.1f; lamp.controlledLight.color = new Color(1, .78f, .47f);
            var button = Target("switch_01", "Wall light switch", new Vector3(-4.09f, 1.12f, 1.25f), new[] { "inspect", "press", "switch_on", "switch_off" });
            Box("Switch plate", button.transform, Vector3.zero, new Vector3(.045f, .13f, .10f), ivory);
            button.stateOwner = lamp;
        }

        static ArmController BuildArm(Transform viewer, Material skinMat, Material robotMat)
        {
            var root = new GameObject("Virtual right arm");
            var arm = root.AddComponent<ArmController>(); arm.viewer = viewer;
            arm.handMaterial = skinMat; arm.robotMaterial = robotMat;
            arm.upperArm = Cylinder("Upper arm", root.transform, Vector3.zero, Vector3.one, skinMat).transform;
            arm.forearm = Cylinder("Forearm", root.transform, Vector3.zero, Vector3.one, skinMat).transform;
            arm.shoulderJoint = Joint("Shoulder", root.transform, .09f, skinMat);
            arm.elbowJoint = Joint("Elbow", root.transform, .08f, skinMat);
            arm.wristJoint = Joint("Wrist", root.transform, .065f, skinMat);
            var palm = new GameObject("Endpoint palm").transform; palm.SetParent(root.transform);
            arm.palm = palm;
            var palmShape = Box("Palm", palm, Vector3.zero, new Vector3(.085f, .025f, .10f), skinMat); RemoveCollider(palmShape);
            var fingers = new List<Transform>();
            for (int i = 0; i < 5; i++) {
                var finger = new GameObject("Finger " + i).transform; finger.SetParent(palm, false);
                finger.localPosition = new Vector3(-.041f + i * .019f, 0, .04f);
                var part = Box("Finger segment", finger, new Vector3(0, 0, .033f), new Vector3(.013f, .018f, i == 0 ? .045f : .065f), skinMat);
                RemoveCollider(part); fingers.Add(finger);
            }
            arm.fingers = fingers.ToArray();
            foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return arm;
        }

        static void WorldPanel(SimulationController sim)
        {
            var root = new GameObject("Gaze interaction panel").transform; root.position = new Vector3(-1.62f, 1.30f, .52f);
            Box("Panel", root, Vector3.zero, new Vector3(1.04f, .88f, .035f), dark);
            Label("OBJECT  /  ACTION", root, new Vector3(-.46f, .35f, -.025f), .013f, Color.white);
            string[] labels = { "MUG", "REACH", "GRASP", "RELEASE", "RESET", "PAUSE", "RESUME", "HAND / BOT", "RECENTER", "LENS QR" };
            string[] values = { "mug_01", "reach", "grasp", "release", "", "", "", "", "", "" };
            string[] commands = { "target", "action", "action", "action", "reset", "stop", "resume", "embodiment", "recenter", "profile" };
            for (int i = 0; i < labels.Length; i++) {
                Vector3 p = new Vector3(-.34f + i % 3 * .34f, .17f - i / 3 * .15f, -.04f);
                var go = Box("Gaze " + labels[i], root, p, new Vector3(.30f, .115f, .035f), i == 5 ? Mat("Pause red", new Color(.55f, .18f, .14f)) : robot);
                var button = go.AddComponent<WorldCommandButton>(); button.label = labels[i]; button.value = values[i]; button.command = commands[i]; button.simulation = sim;
                var text = Label(labels[i], go.transform, new Vector3(0, 0, -.55f), .010f, Color.white);
                text.transform.localScale = new Vector3(1 / .30f, 1 / .115f, 1 / .035f);
                text.anchor = TextAnchor.MiddleCenter;
            }
        }

        static void Chair(Vector3 position, float yaw)
        {
            var root = new GameObject("Dining chair").transform; root.SetParent(room); root.position = position; root.rotation = Quaternion.Euler(0, yaw, 0);
            var fabric = Mat("Chair fabric", new Color(.40f, .20f, .14f));
            Box("Seat", root, new Vector3(0, .46f, 0), new Vector3(.46f, .07f, .46f), fabric);
            Box("Back", root, new Vector3(0, .76f, .20f), new Vector3(.46f, .60f, .055f), fabric);
            foreach (float x in new[] { -.18f, .18f }) foreach (float z in new[] { -.18f, .18f })
                Box("Chair leg", root, new Vector3(x, .22f, z), new Vector3(.035f, .44f, .035f), dark);
        }

        static void Plant(string name, Transform parent, Vector3 p, float size)
        {
            var root = new GameObject(name).transform; root.SetParent(parent, false); root.localPosition = p;
            Cylinder("Plant pot", root, new Vector3(0, size * .5f, 0), new Vector3(size, size * .5f, size), cream);
            Flower(root, new Vector3(0, size, 0), size * 1.6f);
        }

        static void Flower(Transform parent, Vector3 p, float size)
        {
            Cylinder("Stem", parent, p + Vector3.up * size * .5f, new Vector3(.012f, size * .5f, .012f), sage);
            for (int i = 0; i < 5; i++) {
                float angle = i * Mathf.PI * .4f;
                Shape("Flower petal", PrimitiveType.Sphere, parent, p + Vector3.up * size + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .04f,
                    new Vector3(.07f, .04f, .07f), Mat("Flower pink", new Color(.76f, .25f, .38f)));
            }
        }

        static RoomTarget Target(string id, string label, Vector3 position, string[] actions, bool movable = false)
        {
            var go = new GameObject(label); go.transform.SetParent(room); go.transform.position = position;
            var target = go.AddComponent<RoomTarget>(); target.targetId = id; target.displayName = label; target.actions = actions; target.movable = movable;
            return target;
        }

        static void AddPhysics(RoomTarget target, float mass)
        {
            var body = target.gameObject.AddComponent<Rigidbody>(); body.mass = mass; body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        static Transform Joint(string name, Transform parent, float size, Material material)
            => Shape(name, PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * size, material).transform;
        static GameObject Box(string name, Transform parent, Vector3 p, Vector3 scale, Material material)
            => Shape(name, PrimitiveType.Cube, parent, p, scale, material);
        static GameObject Cylinder(string name, Transform parent, Vector3 p, Vector3 scale, Material material)
            => Shape(name, PrimitiveType.Cylinder, parent, p, scale, material);

        static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 p, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = p; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void RemoveCollider(GameObject go) { var c = go.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c); }

        static TextMesh Label(string text, Transform parent, Vector3 p, float size, Color color)
        {
            var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.localPosition = p;
            var mesh = go.AddComponent<TextMesh>(); mesh.text = text; mesh.characterSize = size; mesh.fontSize = 64; mesh.color = color;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            return mesh;
        }

        static Material Mat(string name, Color color, float metallic = 0)
        {
            if (materials.TryGetValue(name, out var existing)) return existing;
            Material material = null;
#if UNITY_EDITOR
            const string folder = "Assets/AutoScene/Art";
            if (!System.IO.Directory.Exists(folder)) { System.IO.Directory.CreateDirectory(folder); AssetDatabase.Refresh(); }
            string path = folder + "/" + name.Replace('/', '-') + ".mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
#else
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
#endif
            material.name = name; material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", metallic > 0 ? .45f : .28f);
            materials[name] = material; return material;
        }
    }
}
