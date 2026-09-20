using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin
{
    /// <summary>A material-handling plant that considers the cabin's occupant a reusable sensor.</summary>
    public sealed class SortingStationSetpiece : MonoBehaviour
    {
        public bool Active { get; private set; }
        public float Elapsed { get; private set; }
        public Transform GeneratedRoot { get; private set; }
        public int ProbeCount { get { return probes.Count; } }
        public bool StampApplied { get; private set; }
        public bool RecycledProbeRouted { get; private set; }
        public bool ReleasedForRedeployment { get; private set; }
        public Transform StampArm { get; private set; }
        public bool ScanActive { get { return Active && Elapsed >= 3f && Elapsed < 6.4f; } }

        const int ExteriorLayer = 26;
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> probes = new List<Transform>();
        readonly List<Vector3> probeOrigins = new List<Vector3>();
        readonly List<Transform> flowLamps = new List<Transform>();
        readonly List<Transform> clampShoulders = new List<Transform>();
        readonly List<Transform> clampUpper = new List<Transform>();
        readonly List<Transform> clampLower = new List<Transform>();
        readonly List<Transform> clampElbows = new List<Transform>();
        readonly List<Transform> clampHands = new List<Transform>();
        readonly List<Transform> recyclingTeeth = new List<Transform>();
        SetpieceDirector director;
        Transform environment, recycledPod, acceptedPod, scannerRig, scanBar, scanVolume, stampShoulder, stampElbow, stampUpper, stampLower, stampHand, stampPlate;
        Mesh podMesh;
        Font worldFont;
        Material lettering, steel, dark, olive, copper, paper, amber, cyan, green, red, scanMaterial;
        bool clampCue, scannerCue, departureCue, releaseCue;

        public void Initialize(SetpieceDirector owner) { director = owner; }

        public void Show()
        {
            Clear();
            GeneratedRoot = new GameObject("SETPIECE_09 | Biological sensor sorting plant").transform;
            GeneratedRoot.gameObject.layer = ExteriorLayer;
            GeneratedRoot.position = new Vector3(0f, 1.5f, 0f);
            GeneratedRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
            Active = true;
            steel = Mat("Oxidised structural steel", new Color(.27f, .31f, .29f), .62f, .1f);
            dark = Mat("Soot-black recesses", new Color(.028f, .039f, .038f), .22f, .06f);
            olive = Mat("Identical expendable cabin paint", new Color(.36f, .39f, .22f), .5f, .16f);
            copper = Mat("Worn identification bands", new Color(.53f, .29f, .08f), .72f, .1f);
            paper = Mat("Reusable sensor inspection tag", new Color(.86f, .85f, .59f), 0f, .65f);
            amber = Mat("Inspection amber", new Color(1f, .50f, .07f), .1f, 1.5f);
            cyan = Mat("Scanner phosphor", new Color(.05f, .75f, .9f), .1f, 2f);
            green = Mat("Return-to-service green", new Color(.24f, .95f, .35f), .1f, 1.4f);
            red = Mat("Recycler warning red", new Color(.92f, .10f, .025f), .1f, 1.2f);
            scanMaterial = new Material(Shader.Find("Astra/Setpieces/SortingScan")) { name = "Sorting / visible inspection volume" };
            materials.Add(scanMaterial);
            worldFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 64);
            lettering = new Material(Shader.Find("Astra/Setpieces/WorldText")) { name = "Sorting / depth-tested Chinese signage" };
            materials.Add(lettering);
            Font.textureRebuilt += RefreshFontAtlas;
            podMesh = CreatePodMesh();
            environment = Group("Moving station / cabin held in conveyor", GeneratedRoot, new Vector3(0f, 0f, 5f));
            BuildConcourse();
            BuildCargo();
            BuildClamp();
            BuildScanner();
            BuildStampArm();
            StageLight("Inspection flood", environment, new Vector3(-3f, 3f, 5.5f), new Color(.8f, .90f, .85f), 3.2f, 27f);
            StageLight("Amber plant flood", environment, new Vector3(7f, 6f, 15f), new Color(1f, .59f, .23f), 4f, 36f);
            StageLight("Cold background fill", environment, new Vector3(-9f, 5f, 28f), new Color(.28f, .55f, .8f), 4f, 42f);
            StageLight("Tag work light", GeneratedRoot, new Vector3(1f, 1.8f, 3f), new Color(.8f, .95f, .88f), 1.2f, 7f);
            if (director)
            {
                director.SetAlert("抵达自动分拣站", "接管运输中 / 无需人工操作\n检测对象：生物传感器 ×1");
                director.PlayCue("conveyor", .46f);
            }
            Animate(0f);
        }

        void BuildConcourse()
        {
            Box("Main conveyor floor", environment, new Vector3(0f, -2.6f, 26f), new Vector3(17f, .6f, 47f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Rod("Longitudinal transport rail", environment, new Vector3(side * 1.55f, -1.7f, 3.8f), new Vector3(side * 1.55f, -1.7f, 49f), .09f, steel);
                Rod("Cargo overhead rail", environment, new Vector3(side * 5.3f, 5.3f, 4f), new Vector3(side * 5.3f, 5.3f, 49f), .12f, copper);
                for (int j = 0; j < 4; j++)
                {
                    float z = 7f + j * 12f;
                    Box("Gantry vertical support", environment, new Vector3(side * 7.5f, .9f, z), new Vector3(.52f, 7.5f, .65f), steel);
                    Box("Support base", environment, new Vector3(side * 7.5f, -2.2f, z), new Vector3(1.3f, .8f, 1.4f), copper);
                    Rod("Gantry cross brace", environment, new Vector3(side * 7.5f, -.8f, z), new Vector3(side * 7.5f, 4.4f, z + 7f), .10f, copper);
                    Box("Gantry hazard strip", environment, new Vector3(side * 7.49f, 1.2f, z - .34f), new Vector3(.33f, 1.6f, .04f), amber);
                }
                for (int j = 0; j < 22; j++)
                {
                    float z = 4f + j * 2f;
                    Transform lamp = Box("Conveyor running light", environment, new Vector3(side * 1.65f, -1.58f, z), new Vector3(.12f, .04f, .7f), green);
                    flowLamps.Add(lamp);
                    Box("Cargo rack upright", environment, new Vector3(side * 6f, .3f, z), new Vector3(.18f, 5.1f, .20f), dark);
                }
                Box("Storage shelf upper", environment, new Vector3(side * 5.3f, 2.45f, 28f), new Vector3(3.8f, .16f, 42f), steel);
                Box("Storage shelf lower", environment, new Vector3(side * 5.3f, -1.15f, 28f), new Vector3(3.8f, .16f, 42f), steel);
            }
            for (int j = 0; j < 4; j++)
            {
                float z = 7f + j * 12f;
                Box("Overhead bridge girder", environment, new Vector3(0f, 4.8f, z), new Vector3(15.5f, .50f, .72f), steel);
                for (int x = -6; x <= 6; x += 2)
                {
                    Rod("Bridge lattice", environment, new Vector3(x - 1f, 4.55f, z), new Vector3(x, 5.6f, z), .07f, copper);
                    Rod("Bridge lattice", environment, new Vector3(x, 5.6f, z), new Vector3(x + 1f, 4.55f, z), .07f, copper);
                }
                Rod("Bridge top beam", environment, new Vector3(-7f, 5.6f, z), new Vector3(7f, 5.6f, z), .08f, steel);
            }
            Box("Facility name board", environment, new Vector3(0f, 4.04f, 21f), new Vector3(10.3f, 1.2f, .18f), dark);
            Label("生物传感器检测站", environment, new Vector3(0f, 4.14f, 20.89f), .66f, new Color(.92f, .83f, .54f));
            Label("BIOLOGICAL SENSORS // QUALITY ASSURANCE", environment, new Vector3(0f, 3.62f, 20.88f), .18f, new Color(.65f, .74f, .63f));
            BuildBay("RECYCLE", new Vector3(-6.5f, -.15f, 23f), red, "回收", "RESOURCE RECOVERY");
            BuildBay("REDEPLOY", new Vector3(4.2f, -.15f, 27f), green, "再次投放", "RETURN TO SERVICE");
            Box("Inspection warning board", environment, new Vector3(-3.5f, 1.1f, 10f), new Vector3(2.4f, .8f, .09f), dark);
            Label("请勿离开包装", environment, new Vector3(-3.5f, 1.22f, 9.94f), .25f, new Color(1f, .65f, .22f));
            Label("DO NOT UNPACK THE SENSOR", environment, new Vector3(-3.5f, .9f, 9.94f), .088f, new Color(.78f, .74f, .57f));
        }

        void BuildBay(string name, Vector3 origin, Material signal, string title, string subtitle)
        {
            Transform bay = Group(name + " sorting aperture", environment, origin);
            Box("Dark bay interior", bay, new Vector3(0f, 0f, 1.7f), new Vector3(4.2f, 3.7f, 2.8f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Bay side", bay, new Vector3(side * 2.25f, .25f, 0f), new Vector3(.5f, 4.8f, 1.7f), steel);
                Box("Bay vertical status light", bay, new Vector3(side * 2.27f, .25f, -.9f), new Vector3(.13f, 4.5f, .06f), signal);
            }
            Box("Bay lintel", bay, new Vector3(0f, 2.35f, 0f), new Vector3(4.95f, .74f, 1.75f), steel);
            Box("Bay sign plate", bay, new Vector3(0f, 2.5f, -.92f), new Vector3(4.5f, .87f, .04f), dark);
            Label(title, bay, new Vector3(0f, 2.69f, -.95f), .53f, signal.color);
            Label(subtitle, bay, new Vector3(0f, 2.17f, -.95f), .13f, signal.color);
            if (name == "RECYCLE")
                for (int i = 0; i < 8; i++)
                {
                    Transform roller = Primitive("Recycler toothed drum", PrimitiveType.Cylinder, bay,
                        new Vector3(-1.76f + i * .50f, -1.10f, -.2f), new Vector3(.46f, .58f, .46f), copper);
                    roller.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    recyclingTeeth.Add(roller);
                }
        }

        void BuildCargo()
        {
            // Repeating cabins, not assorted ships: every frame carries exactly the same instrument package.
            for (int i = 0; i < 24; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                int rank = i / 4;
                bool upper = (i / 2) % 2 == 1;
                Vector3 p = new Vector3(side * (5.25f + .1f * Mathf.Sin(i)), upper ? 3.64f : .05f, 13f + rank * 5.4f);
                Transform pod = CreateProbe("PACKAGED SENSOR / " + (i + 1).ToString("000"), environment, p, true, .78f);
                probes.Add(pod); probeOrigins.Add(p);
                Rod("Cargo suspension trolley", environment, p + new Vector3(0f, .96f, 0f), p + new Vector3(0f, 1.65f, 0f), .07f, steel);
            }
            recycledPod = CreateProbe("FAILED SENSOR / dark cabin routed to recovery", environment, new Vector3(-2.8f, -.05f, 8.7f), false, 1.12f);
            probes.Add(recycledPod); probeOrigins.Add(recycledPod.localPosition);
            acceptedPod = CreateProbe("ACCEPTED SENSOR / following us for redeployment", environment, new Vector3(2.5f, .0f, 11.5f), true, 1.05f);
            probes.Add(acceptedPod); probeOrigins.Add(acceptedPod.localPosition);
            Label("OFFLINE", recycledPod, new Vector3(0f, -.79f, -.71f), .20f, new Color(.72f, .21f, .10f));
            Label("REUSABLE", acceptedPod, new Vector3(0f, -.79f, -.71f), .17f, new Color(.4f, 1f, .4f));
        }

        Transform CreateProbe(string name, Transform parent, Vector3 position, bool powered, float scale)
        {
            Transform pod = Group(name, parent, position); pod.localScale = Vector3.one * scale;
            Transform hull = Group("Pinecone pressure shell", pod, Vector3.zero);
            hull.gameObject.AddComponent<MeshFilter>().sharedMesh = podMesh;
            MeshRenderer hullRenderer = hull.gameObject.AddComponent<MeshRenderer>(); hullRenderer.sharedMaterial = olive;
            hullRenderer.shadowCastingMode = ShadowCastingMode.Off; hullRenderer.receiveShadows = false;
            for (int j = 0; j < 3; j++)
            {
                float y = -.65f + j * .55f;
                float diameter = j == 1 ? 1.47f : j == 2 ? 1.21f : 1.42f;
                Primitive("Pressure-shell cinch band", PrimitiveType.Cylinder, pod, new Vector3(0f, y, 0f), new Vector3(diameter, .026f, diameter), copper);
            }
            Box("Observation port frame", pod, new Vector3(0f, .22f, -.684f), new Vector3(.95f, .57f, .17f), dark);
            Box("Observation port", pod, new Vector3(0f, .24f, -.776f), new Vector3(.79f, .41f, .035f), powered ? cyan : dark);
            if (powered)
            {
                Primitive("Occupant silhouette head", PrimitiveType.Sphere, pod, new Vector3(.1f, .25f, -.799f), new Vector3(.115f, .15f, .025f), dark);
                Box("Occupant silhouette shoulders", pod, new Vector3(.1f, .12f, -.80f), new Vector3(.23f, .12f, .023f), dark);
            }
            Primitive("Tip service plug", PrimitiveType.Cylinder, pod, new Vector3(0f, 1.04f, 0f), new Vector3(.34f, .13f, .34f), steel);
            Primitive("Aft ballast flange", PrimitiveType.Cylinder, pod, new Vector3(0f, -1.09f, 0f), new Vector3(.55f, .12f, .55f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Transport bracket", pod, new Vector3(side * .81f, -.31f, .08f), new Vector3(.21f, .73f, .29f), steel);
                Box("Sensor serial marking", pod, new Vector3(side * .43f, -.5f, -.65f), new Vector3(.10f, .28f, .035f), paper);
            }
            return pod;
        }

        void BuildClamp()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Transform shoulder = Group("Cabin clamp shoulder", GeneratedRoot, new Vector3(side * 3.9f, .6f, 5.6f));
                Primitive("Shoulder gearbox", PrimitiveType.Cylinder, shoulder, Vector3.zero, new Vector3(.88f, .28f, .88f), copper).localRotation = Quaternion.Euler(0f, 0f, 90f);
                Transform upper = Rod("Clamp upper hydraulic arm", GeneratedRoot, shoulder.localPosition, shoulder.localPosition + Vector3.down, .15f, steel);
                Transform elbow = Primitive("Clamp elbow swivel", PrimitiveType.Sphere, GeneratedRoot, Vector3.zero, Vector3.one * .47f, copper);
                Transform lower = Rod("Clamp lower hydraulic arm", GeneratedRoot, Vector3.zero, Vector3.one, .11f, steel);
                Transform hand = Group("Padded cabin restraint", GeneratedRoot, Vector3.zero);
                Box("Clamp pad housing", hand, Vector3.zero, new Vector3(.28f, .83f, .37f), copper);
                Box("Rubber contact pad", hand, new Vector3(-side * .18f, 0f, 0f), new Vector3(.10f, .60f, .30f), dark);
                Box("Clamp engaged indicator", hand, new Vector3(0f, .49f, -.15f), new Vector3(.14f, .11f, .07f), amber);
                clampShoulders.Add(shoulder); clampUpper.Add(upper); clampElbows.Add(elbow); clampLower.Add(lower); clampHands.Add(hand);
            }
        }

        void BuildScanner()
        {
            scannerRig = Group("Inspection scanner station", GeneratedRoot, Vector3.zero);
            scanBar = Group("Scanning light / traverses the real window", scannerRig, new Vector3(0f, .61f, 2.03f));
            Box("Narrow scanning line", scanBar, Vector3.zero, new Vector3(1.9f, .010f, .012f), cyan);
            scanVolume = Box("Translucent scanner light sheet", scanBar, new Vector3(0f, .018f, 1.55f), new Vector3(1.88f, .029f, 3.15f), scanMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Scanner emitter housing", scannerRig, new Vector3(side * 1.13f, .19f, 2.4f), new Vector3(.12f, 1.5f, .24f), dark);
                Box("Scanner emitter light", scannerRig, new Vector3(side * 1.12f, .19f, 2.265f), new Vector3(.045f, 1.30f, .012f), cyan);
            }
            scanBar.gameObject.SetActive(false);
        }

        void BuildStampArm()
        {
            Transform stampRig = Group("LABEL APPLICATOR / jointed inspection robot", GeneratedRoot, Vector3.zero);
            stampShoulder = Group("Applicator shoulder", stampRig, new Vector3(3.15f, .93f, 4.8f));
            Primitive("Shoulder motor", PrimitiveType.Cylinder, stampShoulder, Vector3.zero, new Vector3(.56f, .24f, .56f), copper).localRotation = Quaternion.Euler(90f, 0f, 0f);
            stampUpper = Rod("Applicator upper linkage", stampRig, Vector3.zero, Vector3.up, .095f, steel);
            stampElbow = Primitive("Applicator elbow bearing", PrimitiveType.Sphere, stampRig, Vector3.zero, Vector3.one * .32f, copper);
            stampLower = Rod("Applicator forearm", stampRig, Vector3.zero, Vector3.up, .066f, steel);
            stampHand = Group("Stamp wrist / vacuum gripper", stampRig, Vector3.zero);
            StampArm = stampHand;
            Box("Wrist crossbar", stampHand, Vector3.zero, new Vector3(.50f, .17f, .14f), copper);
            for (int side = -1; side <= 1; side += 2)
                Primitive("Vacuum pad", PrimitiveType.Cylinder, stampHand, new Vector3(side * .20f, 0f, -.10f), new Vector3(.13f, .06f, .13f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
            stampPlate = Group("Permanent inspection tag / attached outside window", GeneratedRoot, new Vector3(2.6f, .45f, 3.94f));
            Box("Metal-backed paper label", stampPlate, Vector3.zero, new Vector3(1.02f, .40f, .022f), paper);
            Box("Label upper black rule", stampPlate, new Vector3(0f, .166f, -.014f), new Vector3(.92f, .013f, .009f), dark);
            Label("生物传感器 ×1", stampPlate, new Vector3(0f, .075f, -.019f), .081f, new Color(.065f, .09f, .055f));
            Label("状态：可继续使用", stampPlate, new Vector3(0f, -.039f, -.019f), .073f, new Color(.065f, .09f, .055f));
            for (int i = 0; i < 26; i++)
                Box("Inspection barcode", stampPlate, new Vector3(-.41f + i * .021f, -.143f, -.016f), new Vector3(i % 3 == 0 ? .013f : .005f, .047f, .008f), dark);
            Label("P / 04", stampPlate, new Vector3(.335f, -.145f, -.020f), .045f, new Color(.07f, .10f, .07f));
            stampPlate.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Active || !GeneratedRoot) return;
            RefreshFontAtlas(worldFont);
            if (Time.deltaTime <= 0f || (director && director.controller && director.controller.IsPaused)) return;
            Elapsed += Time.deltaTime;
            Animate(Elapsed);
        }

        void Animate(float t)
        {
            float arrival = Smooth(0f, 3f, t);
            float unclamp = Smooth(18f, 20.5f, t);
            float departureDistance = 8f * Smooth(20f, 24f, t);
            // All travel is evaluated from absolute vignette time, never integrated into last frame's position.
            // The station, scanner and manipulators recede, while the player and inspection tag stay still.
            float driftTime = Mathf.Max(0f, t - 24f);
            environment.localPosition = new Vector3(Mathf.Sin(driftTime * .1f) * .05f, Mathf.Sin(t * .23f) * .025f,
                Mathf.Lerp(5f, 0f, arrival) + departureDistance + Mathf.Sin(driftTime * .1f) * .24f);
            Vector3 departureOffset = Vector3.forward * departureDistance;
            scannerRig.localPosition = departureOffset;
            stampShoulder.parent.localPosition = departureOffset;
            for (int i = 0; i < clampHands.Count; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 shoulder = new Vector3(side * 3.9f, .6f, 5.6f) + departureOffset;
                Vector3 elbow = new Vector3(side * Mathf.Lerp(3.65f, 2.8f, arrival), Mathf.Lerp(-.25f, .10f, arrival), Mathf.Lerp(4.65f, 3.4f, arrival));
                Vector3 hand = new Vector3(side * Mathf.Lerp(2.9f, 1.31f, arrival), -.16f, Mathf.Lerp(4.2f, 2.25f, arrival));
                elbow = Vector3.Lerp(elbow, new Vector3(side * 3.65f, -.25f, 4.65f), unclamp) + departureOffset;
                hand = Vector3.Lerp(hand, new Vector3(side * 2.9f, -.16f, 4.2f), unclamp) + departureOffset;
                SetRod(clampUpper[i], shoulder, elbow, .15f); SetRod(clampLower[i], elbow, hand, .11f);
                clampShoulders[i].localPosition = shoulder;
                clampElbows[i].localPosition = elbow; clampHands[i].localPosition = hand;
                clampShoulders[i].localRotation = Quaternion.Euler(0f, side * arrival * 18f * (1f - unclamp), -side * arrival * 24f * (1f - unclamp));
            }
            ReleasedForRedeployment = t >= 20.5f;
            if (!releaseCue && t >= 18f)
            {
                releaseCue = true;
                if (director) { director.PlayCue("conveyor", .42f); director.Shake(.12f, .45f); }
            }
            if (!clampCue && t >= 2.65f)
            {
                clampCue = true;
                if (director) { director.PlayCue("stamp", .38f); director.Shake(.22f, .5f); }
            }
            bool scanning = t >= 3f && t < 6.4f;
            scanBar.gameObject.SetActive(scanning);
            if (scanning)
            {
                float scan = (t - 3f) / 3.4f;
                scanBar.localPosition = new Vector3(0f, Mathf.Lerp(.65f, -.29f, scan), 2.03f);
                scanMaterial.SetFloat("_Pulse", .65f + Mathf.Sin(t * 11f) * .15f);
            }
            if (!scannerCue && t >= 3f)
            {
                scannerCue = true;
                if (director) { director.PlayCue("scanner", .50f); director.SetAlert("正在检测生物传感器", "生命体征稳定 / 认知功能残余正常\n正在评估剩余使用价值……"); }
            }
            AnimateStamp(t);
            for (int i = 0; i < 24; i++)
            {
                Vector3 p = probeOrigins[i];
                probes[i].localPosition = p + new Vector3(0f, Mathf.Sin(t * .40f + i * .7f) * .035f, Mathf.Sin(t * .18f + i * .49f) * .38f);
                probes[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(t * .22f + i) * 4f, 0f);
            }
            float rejection = Smooth(9.5f, 14f, t);
            Vector3 rejectedStart = new Vector3(-2.8f, -.05f, 8.7f), rejectedEnd = new Vector3(-6.5f, -.15f, 23.9f);
            recycledPod.localPosition = Vector3.Lerp(rejectedStart, rejectedEnd, rejection) + Vector3.up * Mathf.Sin(rejection * Mathf.PI) * .4f;
            recycledPod.localRotation = Quaternion.Euler(0f, -rejection * 22f, -rejection * 10f);
            recycledPod.gameObject.SetActive(rejection < .985f);
            RecycledProbeRouted = t >= 14f;
            float redeploy = Smooth(13.4f, 18f, t);
            acceptedPod.localPosition = Vector3.Lerp(new Vector3(2.5f, 0f, 11.5f), new Vector3(4.2f, -.15f, 29f), redeploy);
            acceptedPod.localRotation = Quaternion.Euler(0f, 13f * redeploy, 0f);
            acceptedPod.gameObject.SetActive(redeploy < .99f);
            for (int i = 0; i < recyclingTeeth.Count; i++)
                recyclingTeeth[i].localRotation = Quaternion.Euler(90f, 0f, t * (i % 2 == 0 ? 57f : -57f));
            for (int i = 0; i < flowLamps.Count; i++)
            {
                Vector3 p = flowLamps[i].localPosition;
                p.z = 4f + Mathf.Repeat((i % 22) * 2f - t * .72f, 44f);
                flowLamps[i].localPosition = p;
            }
            if (!departureCue && t >= 14f)
            {
                departureCue = true;
                if (director)
                {
                    director.SetAlert("检测通过 / 再次投放", "生物传感器 ×1 / 状态：可继续使用\n包装无需更换。感谢您的持续服役。");
                    director.PlayCue("conveyor", .38f);
                }
            }
            // Distant freight motion, travelling service lights and recycling rollers continue after departure.
        }

        void AnimateStamp(float t)
        {
            Vector3 parked = new Vector3(2.6f, .45f, 4.08f);
            Vector3 contact = new Vector3(.43f, .09f, 2.19f);
            float reach = Smooth(6.35f, 8.3f, t);
            float retract = Smooth(8.75f, 10.8f, t);
            Vector3 wrist = Vector3.Lerp(Vector3.Lerp(parked, contact, reach), parked, retract);
            Vector3 elbow = Vector3.Lerp(new Vector3(3.55f, -.35f, 4.15f), new Vector3(2.05f, .8f, 3.38f), reach * (1f - retract));
            stampElbow.localPosition = elbow;
            stampHand.localPosition = wrist;
            stampHand.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, 0f, reach) * (1f - retract));
            stampShoulder.localRotation = Quaternion.Euler(0f, reach * (1f - retract) * -31f, 0f);
            SetRod(stampUpper, stampShoulder.localPosition, elbow, .095f); SetRod(stampLower, elbow, wrist, .066f);
            stampPlate.gameObject.SetActive(t >= 6.35f);
            if (!StampApplied)
            {
                stampPlate.localPosition = wrist + new Vector3(0f, 0f, -.14f);
                stampPlate.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, 0f, reach));
            }
            if (!StampApplied && t >= 8.3f)
            {
                StampApplied = true;
                stampPlate.localPosition = contact + new Vector3(0f, 0f, -.14f); stampPlate.localRotation = Quaternion.identity;
                if (director)
                {
                    director.PlayCue("stamp", .60f); director.Shake(.12f, .3f);
                    director.SetAlert("生物传感器 ×1", "状态：可继续使用\n自动分流目的地：再次投放");
                }
            }
        }

        Mesh CreatePodMesh()
        {
            const int rings = 20, sides = 48;
            Vector3[] vertices = new Vector3[(rings + 1) * (sides + 1)];
            Vector2[] uv = new Vector2[vertices.Length]; int[] triangles = new int[rings * sides * 6];
            int k = 0;
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                float y = Mathf.Lerp(-1.08f, 1.05f, t);
                float radius = .79f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), .48f) * Mathf.Lerp(1.13f, .65f, t);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    float ridges = 1f + .035f * Mathf.Cos(a * 12f) + .026f * Mathf.Cos(t * Mathf.PI * 14f + a * 6f);
                    int index = j * (sides + 1) + i;
                    vertices[index] = new Vector3(Mathf.Cos(a) * radius * ridges, y, Mathf.Sin(a) * radius * ridges);
                    uv[index] = new Vector2(i / (float)sides, t);
                    if (j < rings && i < sides)
                    {
                        triangles[k++] = index; triangles[k++] = index + sides + 1; triangles[k++] = index + 1;
                        triangles[k++] = index + 1; triangles[k++] = index + sides + 1; triangles[k++] = index + sides + 2;
                    }
                }
            }
            Mesh mesh = new Mesh { name = "Reusable pinecone capsule / common stamped hull", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh); return mesh;
        }

        public void Clear()
        {
            Active = false;
            if (GeneratedRoot) { GeneratedRoot.gameObject.SetActive(false); Release(GeneratedRoot.gameObject); }
            GeneratedRoot = null; StampArm = null;
            Font.textureRebuilt -= RefreshFontAtlas;
            for (int i = 0; i < materials.Count; i++) if (materials[i]) Release(materials[i]);
            for (int i = 0; i < meshes.Count; i++) if (meshes[i]) Release(meshes[i]);
            if (worldFont) Release(worldFont);
            materials.Clear(); meshes.Clear(); probes.Clear(); probeOrigins.Clear(); flowLamps.Clear();
            clampShoulders.Clear(); clampUpper.Clear(); clampLower.Clear(); clampElbows.Clear(); clampHands.Clear(); recyclingTeeth.Clear();
            worldFont = null; lettering = null; podMesh = null;
            Elapsed = 0f; StampApplied = RecycledProbeRouted = ReleasedForRedeployment = clampCue = scannerCue = departureCue = releaseCue = false;
        }
        void OnDestroy() { Clear(); }
        static float Smooth(float start, float end, float t) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, t)); }
        static void Release(Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        Material Mat(string name, Color color, float metal, float emission)
        {
            Material m = new Material(Shader.Find("Standard")) { name = "Sorting / " + name, color = color };
            m.SetFloat("_Metallic", metal); m.SetFloat("_Glossiness", .26f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission);
            materials.Add(m); return m;
        }
        static Transform Group(string name, Transform parent, Vector3 position)
        {
            Transform t = new GameObject(name).transform; t.gameObject.layer = ExteriorLayer; t.SetParent(parent, false); t.localPosition = position; return t;
        }
        static Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.layer = ExteriorLayer;
            Transform t = go.transform; t.SetParent(parent, false); t.localPosition = position; t.localScale = scale;
            Collider c = go.GetComponent<Collider>(); if (c) { c.enabled = false; Release(c); }
            Renderer r = go.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; return t;
        }
        static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        { return Primitive(name, PrimitiveType.Cube, parent, position, scale, material); }
        static Transform Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material material)
        {
            Transform t = Primitive(name, PrimitiveType.Cylinder, parent, Vector3.zero, Vector3.one, material); SetRod(t, a, b, radius); return t;
        }
        static void SetRod(Transform t, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a; t.localPosition = (a + b) * .5f; t.localScale = new Vector3(radius * 2f, d.magnitude * .5f, radius * 2f);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        }
        void Label(string text, Transform parent, Vector3 position, float height, Color color)
        {
            Transform t = Group(text, parent, position); TextMesh tm = t.gameObject.AddComponent<TextMesh>();
            tm.font = worldFont; tm.fontSize = 64; tm.characterSize = height * .16f; tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
            worldFont.RequestCharactersInTexture(text, 64, FontStyle.Normal); RefreshFontAtlas(worldFont);
            Renderer r = tm.GetComponent<Renderer>(); r.sharedMaterial = lettering; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }
        void RefreshFontAtlas(Font changedFont)
        {
            if (lettering && worldFont && changedFont == worldFont && changedFont.material)
                lettering.mainTexture = changedFont.material.mainTexture;
        }
        static void StageLight(string name, Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            Light light = Group(name, parent, position).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range;
            light.shadows = LightShadows.None; light.cullingMask = 1 << ExteriorLayer;
        }
    }
}
