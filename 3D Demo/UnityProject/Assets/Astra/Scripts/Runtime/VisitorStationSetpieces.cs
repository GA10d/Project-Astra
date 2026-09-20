using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin
{
    /// <summary>Real, editable runtime geometry outside the D-wall aperture. No skybox pictures.</summary>
    public sealed class VisitorStationSetpieces : MonoBehaviour
    {
        public bool VisitorActive { get; private set; }
        public bool StationActive { get; private set; }
        public Transform VisitorPilot { get; private set; }
        public Transform VisitorWaveHand { get; private set; }
        public int QueuedShipCount { get { return queueShips.Count; } }
        public Transform GeneratedRoot { get; private set; }

        const int ExteriorLayer = 26;
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> queueShips = new List<Transform>();
        readonly List<Vector3> queueOrigins = new List<Vector3>();
        readonly List<Renderer> runningLights = new List<Renderer>();
        SetpieceDirector director;
        Transform saucer, wavePivot, pilotHead, beacon;
        Material silver, dark, brass, white, red, blue, amber, cyan, green, black, skin, orange, glass;
        Material worldTextMaterial;
        Font worldTextFont;
        float elapsed;

        public void Initialize(SetpieceDirector owner) { director = owner; }

        public void Clear()
        {
            VisitorActive = false;
            StationActive = false;
            VisitorPilot = null; VisitorWaveHand = null;
            saucer = wavePivot = pilotHead = beacon = null;
            // Disable immediately: Destroy is end-of-frame in a running player.
            if (GeneratedRoot) { GeneratedRoot.gameObject.SetActive(false); Release(GeneratedRoot.gameObject); }
            GeneratedRoot = null;
            Font.textureRebuilt -= RefreshWorldFontTexture;
            worldTextMaterial = null; worldTextFont = null;
            for (int i = 0; i < materials.Count; i++) if (materials[i]) Release(materials[i]);
            for (int i = 0; i < meshes.Count; i++) if (meshes[i]) Release(meshes[i]);
            materials.Clear(); meshes.Clear(); queueShips.Clear(); queueOrigins.Clear(); runningLights.Clear();
            elapsed = 0f;
        }

        void OnDestroy() { Clear(); }
        static void Release(Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }

        void Begin(string name)
        {
            Clear();
            GeneratedRoot = new GameObject(name).transform;
            GeneratedRoot.gameObject.layer = ExteriorLayer;
            silver = Mat("Brushed titanium", new Color(.34f, .43f, .44f), .72f, .44f, .035f);
            dark = Mat("Graphite seams", new Color(.045f, .07f, .075f), .5f, .25f, .05f);
            brass = Mat("Aged docking brass", new Color(.52f, .32f, .09f), .7f, .34f, .035f);
            white = Mat("Ivory hull", new Color(.70f, .76f, .73f), .32f, .43f, .035f);
            red = Mat("Cargo vermilion", new Color(.62f, .075f, .035f), .4f, .3f, .07f);
            blue = Mat("Passenger cobalt", new Color(.08f, .27f, .49f), .45f, .42f, .06f);
            amber = Glow("Safety amber", new Color(1f, .46f, .055f), 1.4f);
            cyan = Glow("Ion cyan", new Color(.075f, .77f, 1f), 1.2f);
            green = Glow("Dock ready", new Color(.12f, .85f, .40f), .85f);
            black = Mat("Pilot eyes", new Color(.009f, .022f, .023f), .08f, .9f, .03f);
            skin = Mat("Visitor jade skin", new Color(.24f, .64f, .29f), .08f, .38f, .15f);
            orange = Mat("Pilot flight suit", new Color(.72f, .27f, .055f), .12f, .3f, .06f);
            glass = Mat("Pale cyan canopy", new Color(.12f, .5f, .54f, .10f), .05f, .75f, .02f);
            glass.SetFloat("_Mode", 3f);
            glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.DisableKeyword("_ALPHATEST_ON"); glass.EnableKeyword("_ALPHABLEND_ON");
            glass.DisableKeyword("_ALPHAPREMULTIPLY_ON"); glass.renderQueue = 3000;
        }

        public void ShowVisitor()
        {
            Begin("SETPIECE_02_Visitor");
            VisitorActive = true;
            saucer = Group("UFO / transparent cockpit", GeneratedRoot, new Vector3(5.15f, 1.20f, 0f));
            saucer.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Primitive("Saucer lower shell", PrimitiveType.Sphere, saucer, Vector3.zero, new Vector3(3.25f, .39f, 2.8f), silver);
            Primitive("Saucer equatorial seam", PrimitiveType.Sphere, saucer, new Vector3(0f, .1f, 0f), new Vector3(3.4f, .15f, 2.95f), dark);
            Primitive("Saucer upper shell", PrimitiveType.Sphere, saucer, new Vector3(0f, .17f, .1f), new Vector3(3.0f, .43f, 2.55f), silver);
            Transform rim = Torus("Unbroken cyan drive rim", saucer, 1.58f, .035f, new Vector3(0f, .12f, 0f), new Vector3(90f, 0f, 0f), cyan);
            rim.localScale = new Vector3(1f, .86f, 1f);
            Primitive("Ventral engine", PrimitiveType.Cylinder, saucer, new Vector3(0f, -.22f, 0f), new Vector3(1.25f, .08f, 1.25f), dark);
            Primitive("Ventral engine glow", PrimitiveType.Cylinder, saucer, new Vector3(0f, -.305f, 0f), new Vector3(.84f, .02f, .84f), cyan);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                Vector3 p = new Vector3(Mathf.Sin(a) * 1.48f, .265f, Mathf.Cos(a) * 1.28f);
                Transform panel = Primitive("Radial hull plate " + i, PrimitiveType.Cube, saucer, p, new Vector3(.11f, .035f, .26f), i % 4 == 0 ? brass : dark);
                panel.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                if (i % 2 == 0) runningLights.Add(Primitive("Navigation lamp " + i, PrimitiveType.Sphere, saucer, p + Vector3.up * .04f, Vector3.one * .07f, i % 4 == 0 ? amber : cyan).GetComponent<Renderer>());
            }
            // The front canopy is genuinely transparent; thin ribs sit to either side of the face.
            Primitive("Clear cockpit dome", PrimitiveType.Sphere, saucer, new Vector3(0f, .83f, -.08f), new Vector3(2.08f, 1.62f, 1.8f), glass);
            Primitive("Cockpit rear wall", PrimitiveType.Sphere, saucer, new Vector3(0f, .78f, .46f), new Vector3(1.83f, 1.2f, .23f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 last = new Vector3(side * .86f, .28f, -.54f);
                for (int n = 1; n <= 12; n++)
                {
                    float t = n / 12f;
                    Vector3 p = new Vector3(side * Mathf.Lerp(.86f, .60f, Mathf.Sin(t * Mathf.PI)), .28f + Mathf.Sin(t * Mathf.PI) * 1.27f, -.54f + 1.12f * t);
                    Rod("Canopy side rib", saucer, last, p, .035f, brass); last = p;
                }
            }
            Primitive("Pilot chair", PrimitiveType.Cube, saucer, new Vector3(0f, .67f, .12f), new Vector3(.57f, .8f, .2f), dark);
            Primitive("Console", PrimitiveType.Cube, saucer, new Vector3(0f, .43f, -.7f), new Vector3(1.40f, .18f, .38f), dark);
            for (int i = 0; i < 7; i++) Primitive("Alien console key", PrimitiveType.Cube, saucer, new Vector3(-.48f + i * .16f, .53f, -.7f), new Vector3(.075f, .025f, .09f), i % 2 == 0 ? cyan : amber);
            CreatePilot();
            Label("TRANSPONDER : PEACE", saucer, new Vector3(0f, .07f, -1.415f), .080f, new Color(.24f, 1f, .78f));
            StageLight("Cockpit soft light", saucer, new Vector3(-.65f, 1.1f, -1.7f), new Color(.76f, 1f, .87f), 2f, 5.5f);
            StageLight("Hull cool fill", saucer, new Vector3(1.8f, .2f, -2f), new Color(.25f, .64f, 1f), 2f, 7f);
            if (director) director.PlayCue("ufo", .75f);
        }

        void CreatePilot()
        {
            VisitorPilot = Group("PILOT / articulated visitor", saucer, new Vector3(0f, .37f, -.24f));
            Primitive("Pressure suit torso", PrimitiveType.Capsule, VisitorPilot, new Vector3(0f, .23f, 0f), new Vector3(.43f, .30f, .31f), orange);
            Primitive("Suit chest panel", PrimitiveType.Cube, VisitorPilot, new Vector3(0f, .27f, -.16f), new Vector3(.2f, .2f, .04f), dark);
            Primitive("Suit insignia", PrimitiveType.Sphere, VisitorPilot, new Vector3(-.12f, .36f, -.155f), new Vector3(.07f, .07f, .023f), cyan);
            Primitive("Neck collar", PrimitiveType.Cylinder, VisitorPilot, new Vector3(0f, .46f, 0f), new Vector3(.26f, .05f, .26f), brass);
            pilotHead = Group("Head / gaze toward cabin", VisitorPilot, new Vector3(0f, .74f, -.015f));
            Primitive("Large jade head", PrimitiveType.Sphere, pilotHead, Vector3.zero, new Vector3(.61f, .65f, .48f), skin);
            Primitive("Tapered chin", PrimitiveType.Sphere, pilotHead, new Vector3(0f, -.22f, -.09f), new Vector3(.27f, .20f, .22f), skin);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform eye = Primitive("Black almond eye", PrimitiveType.Sphere, pilotHead, new Vector3(side * .155f, .035f, -.207f), new Vector3(.205f, .275f, .084f), black);
                eye.localRotation = Quaternion.Euler(0f, side * -15f, side * -23f);
                Primitive("Eye highlight", PrimitiveType.Sphere, pilotHead, new Vector3(side * .153f - .026f, .095f, -.254f), new Vector3(.032f, .045f, .013f), white);
            }
            Primitive("Small nose", PrimitiveType.Sphere, pilotHead, new Vector3(0f, -.08f, -.248f), new Vector3(.055f, .065f, .035f), skin);
            Primitive("Quiet smile", PrimitiveType.Cube, pilotHead, new Vector3(0f, -.18f, -.225f), new Vector3(.1f, .013f, .015f), dark);
            Rod("Left upper sleeve", VisitorPilot, new Vector3(-.2f, .38f, 0f), new Vector3(-.40f, .20f, -.12f), .075f, orange);
            Rod("Left forearm", VisitorPilot, new Vector3(-.40f, .20f, -.12f), new Vector3(-.46f, .18f, -.43f), .05f, skin);
            Primitive("Resting hand", PrimitiveType.Sphere, VisitorPilot, new Vector3(-.46f, .18f, -.43f), new Vector3(.12f, .07f, .16f), skin);
            Rod("Right upper sleeve", VisitorPilot, new Vector3(.20f, .38f, 0f), new Vector3(.46f, .48f, -.17f), .072f, orange);
            Primitive("Right elbow", PrimitiveType.Sphere, VisitorPilot, new Vector3(.46f, .48f, -.17f), Vector3.one * .13f, skin);
            wavePivot = Group("WAVE / elbow joint", VisitorPilot, new Vector3(.46f, .48f, -.17f));
            Rod("Raised forearm", wavePivot, Vector3.zero, new Vector3(0f, .33f, -.035f), .052f, skin);
            VisitorWaveHand = Group("WAVE / wrist and open palm", wavePivot, new Vector3(0f, .42f, -.035f));
            Primitive("Open palm", PrimitiveType.Sphere, VisitorWaveHand, Vector3.zero, new Vector3(.15f, .20f, .064f), skin);
            for (int finger = 0; finger < 3; finger++)
            {
                Vector3 start = new Vector3((finger - 1) * .052f, .06f, 0f);
                Vector3 end = start + new Vector3((finger - 1) * .018f, .15f - Mathf.Abs(finger - 1) * .024f, 0f);
                Rod("Waving finger " + finger, VisitorWaveHand, start, end, .019f, skin);
                Primitive("Fingertip", PrimitiveType.Sphere, VisitorWaveHand, end, Vector3.one * .039f, skin);
            }
            Rod("Waving thumb", VisitorWaveHand, new Vector3(-.05f, 0f, 0f), new Vector3(-.14f, .09f, 0f), .022f, skin);
        }

        public void ShowStation()
        {
            Begin("SETPIECE_06_OrbitalFuelStation");
            StationActive = true;
            GeneratedRoot.position = new Vector3(11.6f, 1.15f, 0f);
            GeneratedRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
            Transform station = Group("NEREID / refuelling concourse", GeneratedRoot, Vector3.zero);
            Primitive("Main concourse deck", PrimitiveType.Cube, station, new Vector3(0f, -1.6f, 2.8f), new Vector3(19f, .6f, 4f), dark);
            Primitive("Concourse upper skin", PrimitiveType.Cube, station, new Vector3(0f, -1.27f, 2.8f), new Vector3(19f, .07f, 4f), silver);
            Primitive("Illuminated deck edge", PrimitiveType.Cube, station, new Vector3(0f, -1.38f, .76f), new Vector3(18.8f, .09f, .055f), amber);
            Primitive("Rear service spine", PrimitiveType.Cube, station, new Vector3(0f, 1.9f, 6f), new Vector3(18f, .34f, .6f), silver);
            Primitive("Spine dark inset", PrimitiveType.Cube, station, new Vector3(0f, 1.93f, 5.66f), new Vector3(17.8f, .15f, .07f), dark);
            for (int x = -8; x <= 8; x += 2)
            {
                Rod("Spine diagonal truss", station, new Vector3(x, 1.68f, 6f), new Vector3(x + 1f, 2.6f, 6f), .065f, brass);
                Rod("Spine diagonal truss", station, new Vector3(x + 1f, 2.6f, 6f), new Vector3(x + 2f, 1.68f, 6f), .065f, brass);
                Primitive("Truss marker", PrimitiveType.Sphere, station, new Vector3(x, 1.92f, 5.62f), Vector3.one * .13f, cyan);
            }
            Rod("Top truss rail", station, new Vector3(-8f, 2.6f, 6f), new Vector3(10f, 2.6f, 6f), .07f, silver);
            Primitive("Station sign board", PrimitiveType.Cube, station, new Vector3(0f, 3.28f, 5.9f), new Vector3(9.3f, .95f, .18f), dark);
            Label("NEREID // ORBITAL FUEL", station, new Vector3(0f, 3.36f, 5.79f), .39f, new Color(.30f, .89f, 1f));
            Label("DOCK 04     HE-3 / H2     TRANSIT OPEN", station, new Vector3(0f, 2.95f, 5.76f), .145f, new Color(1f, .72f, .28f));
            for (int side = -1; side <= 1; side += 2)
            {
                Transform tank = Group("Cryogenic tank array", station, new Vector3(side * 8.4f, .05f, 3.4f));
                for (int j = 0; j < 3; j++)
                {
                    Primitive("Fuel vessel", PrimitiveType.Capsule, tank, new Vector3(0f, 0f, j * 1.1f), new Vector3(.95f, 1.7f, .95f), white);
                    Torus("Tank band", tank, .50f, .065f, new Vector3(0f, .5f, j * 1.1f), new Vector3(90f, 0f, 0f), amber);
                    Rod("Fuel main", tank, new Vector3(-.45f, -.6f, j * 1.1f), new Vector3(-.45f, -.6f, j * 1.1f + 1f), .11f, brass);
                }
            }
            // Three actual docked spacecraft, each physically attached to an articulated fuel pipe.
            for (int bay = 0; bay < 3; bay++)
            {
                float x = (bay - 1) * 4.4f;
                Vector3 shipPosition = new Vector3(x, -.2f, .15f + (bay == 1 ? -.9f : 0f));
                CreateShip("Docked craft " + (bay + 1), station, shipPosition, bay, 1.02f, false);
                Primitive("Dock support", PrimitiveType.Cube, station, new Vector3(x, -.85f, 2.3f), new Vector3(.55f, .9f, 4f), silver);
                Rod("Fuel tower", station, new Vector3(x + 1.55f, -1.3f, 3.2f), new Vector3(x + 1.55f, 2.4f, 3.2f), .14f, silver);
                Primitive("Service light box", PrimitiveType.Cube, station, new Vector3(x + 1.55f, 2.48f, 3.2f), new Vector3(.4f, .22f, .42f), amber);
                Vector3 a = new Vector3(x + 1.55f, 2.2f, 3.2f), b = new Vector3(x + .60f, 2.2f, .8f), c = shipPosition + new Vector3(.56f, .37f, .2f);
                Rod("Articulated fuel arm", station, a, b, .11f, brass);
                Primitive("Fuel elbow", PrimitiveType.Sphere, station, b, Vector3.one * .30f, dark);
                Vector3 last = b;
                for (int n = 1; n <= 10; n++)
                {
                    float t = n / 10f;
                    Vector3 p = Vector3.Lerp(b, c, t) + new Vector3(.23f * Mathf.Sin(Mathf.PI * t), 0f, -.14f * Mathf.Sin(Mathf.PI * t));
                    Rod("Connected flexible fuel hose", station, last, p, .06f, amber); last = p;
                }
                Primitive("Fuel connector", PrimitiveType.Sphere, station, c, Vector3.one * .22f, silver);
                Label("0" + (bay + 1) + "  FUELLING", station, new Vector3(x, -1.25f, .62f), .18f, new Color(.27f, 1f, .52f));
                for (int mark = 0; mark < 5; mark++) Primitive("Dock alignment light", PrimitiveType.Cube, station, new Vector3(x - .75f, -1.18f, 1.1f + mark * .6f), new Vector3(.12f, .035f, .20f), green);
            }
            // Queue is laid out diagonally beyond the dock, so hull silhouettes do not overlap.
            for (int i = 0; i < 8; i++)
            {
                float x = -8.3f + i * 2.35f;
                // Keep the holding lane clearly above the concourse sign and service truss.
                Vector3 p = new Vector3(x, 5.85f + (i % 2) * .70f, 8f + Mathf.Abs(i - 3.5f) * 1.15f);
                Transform ship = CreateShip("QUEUE " + (i + 1).ToString("00"), station, p, i % 4, .66f + (i % 3) * .12f, true);
                ship.localRotation = Quaternion.Euler(0f, -23f, 0f);
                queueShips.Add(ship); queueOrigins.Add(p);
            }
            // Nearby structures frame the view without putting geometry inside the cabin.
            for (int side = -1; side <= 1; side += 2)
            {
                Rod("Foreground docking rail", station, new Vector3(side * 3.8f, -1.7f, -5.3f), new Vector3(side * 5.2f, -1.7f, .8f), .10f, silver);
                for (int i = 0; i < 6; i++) Primitive("Approach guidance beacon", PrimitiveType.Sphere, station, new Vector3(side * Mathf.Lerp(3.8f, 5.2f, i / 5f), -1.53f, -5.3f + i * 1.22f), Vector3.one * .16f, cyan);
            }
            beacon = Group("Rotating tower beacon", station, new Vector3(-7f, 3f, 5.2f));
            Rod("Beacon mast", station, new Vector3(-7f, 2.1f, 5.2f), new Vector3(-7f, 3.5f, 5.2f), .06f, dark);
            Primitive("Beacon bar", PrimitiveType.Cube, beacon, Vector3.zero, new Vector3(.85f, .12f, .12f), amber);
            StageLight("Dock cold flood", station, new Vector3(-4f, 4f, -1f), new Color(.51f, .77f, 1f), 4f, 22f);
            StageLight("Dock warm flood", station, new Vector3(6f, 4f, 1f), new Color(1f, .66f, .33f), 3.5f, 20f);
            if (director) director.PlayCue("engine", .52f);
        }

        Transform CreateShip(string name, Transform parent, Vector3 position, int variant, float scale, bool engineOn)
        {
            Transform ship = Group(name, parent, position);
            ship.localScale = Vector3.one * scale;
            Material paint = variant == 0 ? red : variant == 1 ? blue : variant == 2 ? white : brass;
            Primitive("Pressure hull", PrimitiveType.Capsule, ship, Vector3.zero, new Vector3(.94f, 1.55f, .78f), paint).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive("Armoured nose", PrimitiveType.Sphere, ship, new Vector3(0f, .05f, -1.30f), new Vector3(.82f, .61f, .94f), paint);
            Primitive("Cockpit dark surround", PrimitiveType.Sphere, ship, new Vector3(0f, .24f, -.88f), new Vector3(.75f, .40f, .76f), dark);
            Primitive("Cockpit luminous pane", PrimitiveType.Sphere, ship, new Vector3(0f, .27f, -1.02f), new Vector3(.56f, .25f, .42f), cyan);
            Primitive("Keel", PrimitiveType.Cube, ship, new Vector3(0f, -.36f, .15f), new Vector3(.55f, .12f, 1.8f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                float spread = variant == 1 ? 1.25f : .91f;
                Primitive("Wing spar", PrimitiveType.Cube, ship, new Vector3(side * spread * .64f, -.06f, .2f), new Vector3(spread, .10f, .80f), paint).localRotation = Quaternion.Euler(0f, side * -18f, 0f);
                Primitive("Engine nacelle", PrimitiveType.Capsule, ship, new Vector3(side * spread, -.04f, .53f), new Vector3(.38f, .72f, .38f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Primitive("Engine nozzle", PrimitiveType.Cylinder, ship, new Vector3(side * spread, -.04f, 1.26f), new Vector3(.32f, .09f, .32f), brass).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Primitive("Engine core", PrimitiveType.Sphere, ship, new Vector3(side * spread, -.04f, 1.35f), new Vector3(.20f, .20f, engineOn ? .55f : .075f), cyan);
                Primitive("Wing marker", PrimitiveType.Sphere, ship, new Vector3(side * (spread + .1f), .10f, -.04f), Vector3.one * .09f, side < 0 ? amber : green);
            }
            if (variant == 0)
                for (int i = 0; i < 3; i++) Primitive("Cargo container", PrimitiveType.Cube, ship, new Vector3(0f, .46f, -.1f + i * .51f), new Vector3(.85f, .45f, .46f), i == 1 ? brass : red);
            else if (variant == 1)
                for (int i = 0; i < 4; i++) Primitive("Passenger porthole", PrimitiveType.Sphere, ship, new Vector3(-.435f, .10f, -.18f + i * .32f), new Vector3(.045f, .1f, .1f), cyan);
            else if (variant == 2)
                Primitive("Survey antenna", PrimitiveType.Cylinder, ship, new Vector3(0f, .76f, .35f), new Vector3(.035f, .45f, .035f), silver);
            else
                for (int side = -1; side <= 1; side += 2) Primitive("Tanker auxiliary pod", PrimitiveType.Capsule, ship, new Vector3(side * .67f, .29f, .35f), new Vector3(.42f, .69f, .42f), brass).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive("Tail fin", PrimitiveType.Cube, ship, new Vector3(0f, .49f, .98f), new Vector3(.09f, .55f, .62f), paint);
            return ship;
        }

        void Update()
        {
            if (worldTextFont) RefreshWorldFontTexture(worldTextFont);
            float dt = Time.deltaTime;
            if (dt <= 0f || (!VisitorActive && !StationActive)) return;
            elapsed += dt;
            if (VisitorActive && saucer)
            {
                saucer.localPosition = new Vector3(5.15f, 1.20f + Mathf.Sin(elapsed * .84f) * .045f, Mathf.Sin(elapsed * .49f) * .055f);
                saucer.localRotation = Quaternion.Euler(Mathf.Sin(elapsed * .57f) * 1f, 90f, Mathf.Sin(elapsed * .76f) * .65f);
                if (wavePivot) wavePivot.localRotation = Quaternion.Euler(0f, 0f, -8f + Mathf.Sin(elapsed * 3.5f) * 26f);
                if (VisitorWaveHand) VisitorWaveHand.localRotation = Quaternion.Euler(0f, Mathf.Sin(elapsed * 3.5f + .5f) * 10f, Mathf.Sin(elapsed * 3.5f + .6f) * 12f);
                if (pilotHead) pilotHead.localRotation = Quaternion.Euler(Mathf.Sin(elapsed * 1.6f) * 3f, Mathf.Sin(elapsed * .7f) * 4f, -5f);
                for (int i = 0; i < runningLights.Count; i++) if (runningLights[i]) runningLights[i].enabled = Mathf.Sin(elapsed * 2.1f + i * .65f) > -.60f;
            }
            if (StationActive)
            {
                for (int i = 0; i < queueShips.Count; i++)
                {
                    // A slow approach-and-hold loop never teleports a visible ship across the aperture.
                    float phase = elapsed * .16f + i * .83f;
                    queueShips[i].localPosition = queueOrigins[i] + new Vector3(Mathf.Sin(phase) * .28f, Mathf.Sin(phase * 1.4f) * .055f, Mathf.Sin(phase * .7f) * .42f);
                }
                if (beacon) beacon.localRotation = Quaternion.Euler(0f, elapsed * 48f, 0f);
            }
        }

        Material Mat(string name, Color color, float metallic, float gloss, float emission)
        {
            Material m = new Material(Shader.Find("Standard")); m.name = "Setpiece / " + name;
            m.color = color; m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", gloss);
            if (emission > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * emission); }
            materials.Add(m); return m;
        }
        Material Glow(string name, Color color, float strength)
        {
            // Standard's self-emission remains visible even during the cabin blackout.
            return Mat(name, color, .15f, .6f, strength);
        }
        static Transform Group(string name, Transform parent, Vector3 localPosition)
        {
            Transform t = new GameObject(name).transform; t.gameObject.layer = ExteriorLayer;
            t.SetParent(parent, false); t.localPosition = localPosition; return t;
        }
        static Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.layer = ExteriorLayer;
            Transform t = go.transform; t.SetParent(parent, false); t.localPosition = position; t.localScale = scale;
            Collider collider = go.GetComponent<Collider>(); if (collider) { collider.enabled = false; Release(collider); }
            Renderer r = go.GetComponent<Renderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return t;
        }
        static Transform Rod(string name, Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 d = to - from;
            Transform t = Primitive(name, PrimitiveType.Cylinder, parent, (from + to) * .5f, new Vector3(radius * 2f, d.magnitude * .5f, radius * 2f), material);
            if (d.sqrMagnitude > .000001f) t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            return t;
        }
        Transform Torus(string name, Transform parent, float radius, float tube, Vector3 position, Vector3 rotation, Material material)
        {
            const int segments = 56, sides = 8;
            Vector3[] vertices = new Vector3[(segments + 1) * (sides + 1)];
            Vector3[] normals = new Vector3[vertices.Length]; int[] indices = new int[segments * sides * 6];
            int k = 0;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments; Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= sides; j++)
                {
                    float b = Mathf.PI * 2f * j / sides; int n = i * (sides + 1) + j;
                    normals[n] = radial * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    vertices[n] = radial * radius + normals[n] * tube;
                    if (i < segments && j < sides)
                    {
                        indices[k++] = n; indices[k++] = n + sides + 1; indices[k++] = n + 1;
                        indices[k++] = n + 1; indices[k++] = n + sides + 1; indices[k++] = n + sides + 2;
                    }
                }
            }
            Mesh mesh = new Mesh { name = name + " mesh", vertices = vertices, normals = normals, triangles = indices };
            mesh.RecalculateBounds(); meshes.Add(mesh);
            Transform t = Group(name, parent, position); t.localRotation = Quaternion.Euler(rotation);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; return t;
        }
        void Label(string text, Transform parent, Vector3 position, float size, Color color)
        {
            Transform t = Group(text, parent, position);
            // TextMesh faces local -Z by default, toward the cabin after the parent's Y=90 rotation.
            TextMesh tm = t.gameObject.AddComponent<TextMesh>(); tm.text = text; tm.fontSize = 64; tm.characterSize = size * .16f;
            if (!worldTextFont)
            {
                worldTextFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                worldTextMaterial = new Material(Shader.Find("Astra/Setpieces/WorldText")) { name = "Setpiece / depth-tested world lettering" };
                materials.Add(worldTextMaterial);
                Font.textureRebuilt += RefreshWorldFontTexture;
            }
            tm.font = worldTextFont;
            worldTextFont.RequestCharactersInTexture(text, 64, FontStyle.Normal);
            RefreshWorldFontTexture(worldTextFont);
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
            Renderer r = tm.GetComponent<Renderer>();
            r.sharedMaterial = worldTextMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }
        void RefreshWorldFontTexture(Font changedFont)
        {
            if (worldTextMaterial && changedFont && changedFont == worldTextFont && changedFont.material)
                worldTextMaterial.mainTexture = changedFont.material.mainTexture;
        }
        static void StageLight(string name, Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            Light light = Group(name, parent, position).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range;
            light.shadows = LightShadows.None; light.cullingMask = 1 << ExteriorLayer;
        }
    }
}
