using System;
using System.Collections.Generic;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class FarewellStage:IDisposable
    {
        public Transform Root {get;private set;}public Transform Actor {get;private set;}public Transform Chest {get;private set;}
        public Camera Camera {get;private set;}public RenderTexture Video {get;private set;}
        public Transform Planet {get;private set;}public Transform Photo {get;private set;}public Texture2D Portrait {get;private set;}
        public bool Broken {get;private set;}public int FragmentCount {get{return pieces.Count;}}
        readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        readonly List<Fragment> pieces = new List<Fragment>();
        Transform callRoot, exterior, bodyEnergy, planetEnergy;
        Material bodyGlow, planetGlow, ribbonMaterial;
        Mesh energySphere, ribbonMesh;
        Transform[] chestRibbons, planetRibbons;
        readonly Transform[] remnants = new Transform[2];
        Animation animation;
        MihuoerCountdownSkin countdownSkin;
        Vector3 planetPosition, photoStart;
        Quaternion photoRotation;
        Camera cabin;
        float exteriorScale, concealedFractureTime;
        readonly ZylPresentationParameters settings;
        public FarewellStage(Camera source, ZylPresentationParameters settings)
        {
            this.settings = settings;
            try{Build(source);}catch{Dispose();throw;}
        }
        Material Mat(Color color,bool energy=false)
        {
            var shader=energy?Resources.Load<Shader>("Galaxies/Zyl/Farewell/FarewellEnergy"):Shader.Find("Standard");if(!shader)throw new InvalidOperationException("别离能量Shader缺失");
            var m=new Material(shader);m.color=color;
            if (energy)
            {
                m.SetFloat("_SurfaceWaveAmplitude", settings.farewellSurfaceWaveAmplitude * .01f);
                m.SetFloat("_InnerWaveHz", settings.farewellInnerWaveHz);
                m.SetFloat("_HaloWaveHz", settings.farewellHaloWaveHz);
                m.SetFloat("_HaloIntensity", settings.farewellHaloIntensity);
            }
            else m.SetFloat("_Glossiness", .12f);
            resources.Add(m);
            return m;
        }
        Transform Primitive(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material m)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.layer=26;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;UnityEngine.Object.Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=m;return go.transform;
        }
        void Build(Camera source)
        {
            cabin=source;Root=new GameObject("17 | 泰德野里_别离").transform;callRoot=new GameObject("Abandoned planet call set").transform;callRoot.SetParent(Root);callRoot.position=new Vector3(0,-300,0);
            var ground=Mat(new Color(.22f,.19f,.16f));Primitive("Barren ground",PrimitiveType.Cube,callRoot,new Vector3(0,-.12f,2),new Vector3(80,.2f,80),ground);
            var random=new System.Random(1707);for(int i=0;i<28;i++)
            {
                float x=(float)random.NextDouble()*30-15,z=3+(float)random.NextDouble()*25;var stone=Primitive("Ruined planet stone",PrimitiveType.Sphere,callRoot,new Vector3(x,.10f,z),new Vector3(1+(float)random.NextDouble()*3,.4f+(float)random.NextDouble()*2,1.8f),ground);
                stone.localRotation=Quaternion.Euler(i*17,i*43,i*29);if(i%5==0)Primitive("Broken structure",PrimitiveType.Cube,callRoot,new Vector3(x,1,z),new Vector3(.5f,2+(float)random.NextDouble()*3,.7f),ground);
            }
            var model=Resources.Load<GameObject>(StomachMihuoerActor.StandingResource);if(!model)throw new InvalidOperationException("别离缺少米霍尔站立模型");Actor=UnityEngine.Object.Instantiate(model,callRoot,false).transform;Actor.localPosition=Vector3.zero;Actor.localRotation=Quaternion.Euler(0,180,0);Actor.localScale=Vector3.one*1.5f;
            var surface=Resources.Load<Material>("Galaxies/Zyl/Encounter/MihuoerSurface");foreach(var r in Actor.GetComponentsInChildren<Renderer>())r.sharedMaterial=surface;foreach(var t in Actor.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=26;if(t.name=="chest")Chest=t;}
            if(!Chest)Chest=Actor;animation=Actor.GetComponentInChildren<Animation>();var talk=Resources.Load<GameObject>("Galaxies/Zyl/Standing/StandingTalk01");var talkAnim=talk?talk.GetComponentInChildren<Animation>():null;if(talkAnim&&talkAnim.GetClip("StandingTalk01"))animation.AddClip(talkAnim.GetClip("StandingTalk01"),"FarewellTalk");
            Camera=new GameObject("Farewell call camera").AddComponent<Camera>();Camera.transform.SetParent(callRoot,false);Camera.transform.localPosition=new Vector3(0,1.1f,-2.35f);Camera.transform.LookAt(callRoot.position+Vector3.up*.83f);Camera.cullingMask=1<<26;Camera.nearClipPlane=.03f;Camera.farClipPlane=120;Camera.fieldOfView=47;Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.08f,.065f,.06f);Camera.enabled=false;
            Video=new RenderTexture(1280,720,24){name="Farewell live video"};Video.Create();Camera.targetTexture=Video;
            var key=new GameObject("Farewell sunset fill").AddComponent<Light>();key.transform.SetParent(callRoot,false);key.transform.localPosition=new Vector3(-2,4,-3);key.type=LightType.Point;key.range=45;key.intensity=1.05f;key.color=new Color(1,.82f,.65f);key.cullingMask=1<<26;
            SampleActor(0, false);
            CapturePortrait();
            energySphere = CreateEnergySphere();
            ribbonMesh = CreateRibbon();
            var ribbonShader = Shader.Find("Astra/SetpieceGlow");
            if (!ribbonShader) throw new InvalidOperationException("别离光带Shader缺失");
            ribbonMaterial = new Material(ribbonShader) { color = new Color(.40f, .88f, 1, .95f) };
            resources.Add(ribbonMaterial);
            bodyGlow = Mat(new Color(.02f, .76f, 1, 1), true);
            bodyEnergy = CreateEnergy("Chest energy", callRoot, bodyGlow, out chestRibbons);
            exterior=new GameObject("Farewell exterior").transform;exterior.SetParent(Root);exterior.gameObject.SetActive(false);
            var rockShader = Resources.Load<Shader>("Galaxies/Zyl/Farewell/FarewellRock");
            if (!rockShader) throw new InvalidOperationException("别离岩石Shader缺失");
            var craterTexture = Resources.Load<Texture2D>("PortalCraterAlbedo");
            var planetMat = new Material(rockShader) { color = new Color(.92f, .74f, .62f), mainTexture = craterTexture };
            planetMat.SetColor("_RimColor", new Color(.20f, .43f, .63f));
            planetMat.SetFloat("_Ambient", .32f);
            resources.Add(planetMat);
            Planet = Primitive("Abandoned planet", PrimitiveType.Sphere, exterior, Vector3.zero, Vector3.one * 36, planetMat);
            planetGlow = Mat(new Color(.02f, .76f, 1, 1), true);
            planetEnergy = CreateEnergy("Planet local detonation", exterior, planetGlow, out planetRibbons);
            var stoneMat = new Material(rockShader) { color = new Color(.62f, .47f, .41f), mainTexture = craterTexture };
            stoneMat.SetFloat("_Ambient", .19f);
            resources.Add(stoneMat);
            BuildFragments(stoneMat);
            var photoMat=new Material(Shader.Find("Unlit/Transparent"));photoMat.mainTexture=Portrait;resources.Add(photoMat);Photo=Primitive("Mihuoer monochrome photograph",PrimitiveType.Quad,exterior,Vector3.zero,new Vector3(.7f,.86f,1),photoMat);Photo.gameObject.SetActive(false);
        }
        Mesh CreateEnergySphere()
        {
            const int rows = 40, columns = 72;
            var vertices = new Vector3[(rows + 1) * (columns + 1)];
            var normals = new Vector3[vertices.Length];
            var triangles = new List<int>(rows * columns * 6);
            for (int row = 0; row <= rows; row++)
            {
                float latitude = Mathf.PI * row / rows;
                for (int column = 0; column <= columns; column++)
                {
                    float longitude = 2 * Mathf.PI * column / columns;
                    int index = row * (columns + 1) + column;
                    var normal = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude),
                        Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    normals[index] = normal;
                    vertices[index] = normal * .5f;
                    if (row == rows || column == columns) continue;
                    int next = index + columns + 1;
                    triangles.Add(index);
                    triangles.Add(index + 1);
                    triangles.Add(next);
                    triangles.Add(index + 1);
                    triangles.Add(next + 1);
                    triangles.Add(next);
                }
            }
            var mesh = new Mesh { name = "Farewell layered energy sphere" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1.42f);
            resources.Add(mesh);
            return mesh;
        }

        Mesh CreateRibbon()
        {
            const int steps = 88;
            var vertices = new Vector3[(steps + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new List<int>(steps * 6);
            for (int step = 0; step <= steps; step++)
            {
                float progress = step / (float)steps;
                float angle = progress * 5.15f;
                float taper = Mathf.Max(0, Mathf.Sin(progress * Mathf.PI));
                float radius = .595f + .012f * Mathf.Sin(angle * 2.1f);
                int index = step * 2;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var center = radial * radius + Vector3.up * (.014f * Mathf.Sin(angle * 2.6f));
                var width = radial * (.022f * taper) + Vector3.up * (.012f * taper);
                vertices[index] = center - width;
                vertices[index + 1] = center + width;
                uv[index] = new Vector2(progress, 0);
                uv[index + 1] = new Vector2(progress, 1);
                colors[index] = colors[index + 1] = new Color(1, 1, 1, Mathf.Sqrt(taper));
                if (step == steps) continue;
                triangles.AddRange(new[] { index, index + 2, index + 1, index + 1, index + 2, index + 3 });
            }
            var mesh = new Mesh { name = "Farewell orbital light ribbon" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            resources.Add(mesh);
            return mesh;
        }

        Transform CreateEnergy(string name, Transform parent, Material material, out Transform[] ribbons)
        {
            var sphere = new GameObject(name).transform;
            sphere.gameObject.layer = 26;
            sphere.SetParent(parent, false);
            sphere.gameObject.AddComponent<MeshFilter>().sharedMesh = energySphere;
            sphere.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            ribbons = new Transform[3];
            for (int i = 0; i < ribbons.Length; i++)
            {
                var ribbon = new GameObject("Orbit ribbon " + (i + 1)).transform;
                ribbon.gameObject.layer = 26;
                ribbon.SetParent(sphere, false);
                ribbon.localScale = Vector3.one * (1 + i * .055f);
                ribbon.gameObject.AddComponent<MeshFilter>().sharedMesh = ribbonMesh;
                ribbon.gameObject.AddComponent<MeshRenderer>().sharedMaterial = ribbonMaterial;
                ribbons[i] = ribbon;
            }
            sphere.gameObject.SetActive(false);
            return sphere;
        }

        static void AnimateRibbons(Transform[] ribbons, float elapsed)
        {
            ribbons[0].localRotation = Quaternion.Euler(18, elapsed * 70, 14);
            ribbons[1].localRotation = Quaternion.Euler(-28, 120 - elapsed * 53, -23);
            ribbons[2].localRotation = Quaternion.Euler(52, 250 + elapsed * 43, 32);
        }


        public void AttachCountdown(MihuoerCountdown timer,ZylPresentationParameters p){countdownSkin=MihuoerCountdownSkin.Attach(Actor.gameObject,timer,p,true);}
        void CapturePortrait()
        {
            Camera.Render();var previous=RenderTexture.active;RenderTexture.active=Video;var tex=new Texture2D(1280,720,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();RenderTexture.active=previous;
            Portrait=new Texture2D(320,400,TextureFormat.RGBA32,false);for(int y=0;y<400;y++)for(int x=0;x<320;x++){bool border=x<13||x>=307||y<13||y>=387;var c=tex.GetPixelBilinear(.32f+x/320f*.36f,.03f+y/400f*.93f);float gray=c.grayscale;Portrait.SetPixel(x,y,border?new Color(.88f,.88f,.84f):new Color(gray,gray,gray,1));}Portrait.Apply();resources.Add(Portrait);UnityEngine.Object.Destroy(tex);
        }
        public void SampleActor(float time,bool talking)
        {
            if(!animation)return;string clip=talking&&animation.GetClip("FarewellTalk")?"FarewellTalk":StomachMihuoerActor.StandingClip;var state=animation[clip];if(state==null)return;animation.enabled=false;animation.Play(clip);state.speed=0;state.time=Mathf.Repeat(time,state.length);animation.Sample();
        }
        public void Render()
        {
            if (countdownSkin) countdownSkin.UpdateDisplay();
            Camera.Render();
        }
        Vector3 chestLocalOrigin;
        public Vector3 ChestOrigin => Chest.TransformPoint(chestLocalOrigin);

        public void AnchorChestBurst()
        {
            Vector3 surface = FindCountdownSurface();
            // Move from the posed skin into the torso. The chest bone carries the inset as the actor moves.
            Vector3 inside = Vector3.MoveTowards(surface, Chest.position, .038f * Actor.lossyScale.x);
            chestLocalOrigin = Chest.InverseTransformPoint(inside);
        }

        Vector3 FindCountdownSurface()
        {
            // Use the countdown shader's rest-space coordinates to locate the skin over the source.
            Vector2 displayCenter = new Vector2(-settings.countdownOffsetX, .795f + settings.countdownOffsetY);
            SkinnedMeshRenderer bestSkin = null;
            int bestIndex = 0;
            float bestDistance = float.PositiveInfinity;
            var rest = new List<Vector4>();
            foreach (var skin in Actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.sharedMesh) continue;
                rest.Clear();
                skin.sharedMesh.GetUVs(2, rest);
                for (int i = 0; i < rest.Count; i++)
                {
                    Vector4 point = rest[i];
                    if (point.z <= .015f || point.y < .69f || point.y > .90f || Mathf.Abs(point.x) > .17f)
                        continue;
                    float distance = (new Vector2(point.x, point.y) - displayCenter).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestSkin = skin;
                    bestIndex = i;
                    bestDistance = distance;
                }
            }
            if (!bestSkin) return Chest.position;

            var mesh = bestSkin.sharedMesh;
            var weights = mesh.boneWeights;
            if (bestIndex >= weights.Length) return Chest.position;
            var vertex = mesh.vertices[bestIndex];
            var bind = mesh.bindposes;
            var bones = bestSkin.bones;
            BoneWeight weight = weights[bestIndex];

            // Bone matrices already include the FBX hierarchy's scale. The sum is in world space.
            Vector3 WeightedPoint(int index, float amount)
            {
                if (amount == 0) return Vector3.zero;
                return bones[index].TransformPoint(bind[index].MultiplyPoint3x4(vertex)) * amount;
            }

            return WeightedPoint(weight.boneIndex0, weight.weight0)
                + WeightedPoint(weight.boneIndex1, weight.weight1)
                + WeightedPoint(weight.boneIndex2, weight.weight2)
                + WeightedPoint(weight.boneIndex3, weight.weight3);
        }

        public void SampleChestPreview(float elapsed)
        {
            if (elapsed <= 0)
            {
                bodyEnergy.gameObject.SetActive(false);
                Render();
                return;
            }
            DrawChestEnergy(elapsed, PreviewProgress(elapsed));
        }

        float PreviewProgress(float elapsed) =>
            .19f * Mathf.Clamp01(elapsed / Mathf.Max(.001f, settings.farewellChestLeadTime));

        public void SampleChest(float t, float shake, float previewElapsed)
        {
            Camera.transform.localPosition = new Vector3(0, 1.1f, -2.35f)
                + new Vector3(Mathf.Sin(t * 37), Mathf.Sin(t * 29), 0) * (.048f * shake);
            float progress = Mathf.Lerp(PreviewProgress(previewElapsed), 1, t / settings.farewellChestDuration);
            DrawChestEnergy(previewElapsed + t, progress);
        }

        void DrawChestEnergy(float time, float progress)
        {
            bodyEnergy.gameObject.SetActive(true);
            bodyEnergy.position = ChestOrigin;
            // Keep the viewpoint outside the halo as the wave fills the shot.
            float diameter = Vector3.Distance(Camera.transform.position, ChestOrigin) * 1.67f;
            bodyEnergy.localScale = Vector3.one * Mathf.Lerp(.055f, diameter, Mathf.Pow(progress, 1.38f));
            bodyGlow.SetFloat("_PhaseTime", time);
            AnimateRibbons(chestRibbons, time);
            Render();
        }


        public void ShowExterior(CabinController controller)
        {
            callRoot.gameObject.SetActive(false);
            exterior.gameObject.SetActive(true);
            // Place the exterior from the final window framing before the cabin camera turns.
            exteriorScale = Mathf.Min(1, cabin.farClipPlane / 150f);
            var viewRotation = Quaternion.Euler(0, 90, 0);
            Vector3 eye = controller.windowObservationPosition;
            float halfHeight = Mathf.Tan(controller.windowFieldOfView * .5f * Mathf.Deg2Rad);
            Vector3 WindowPoint(Vector3 viewport)
            {
                float y = (viewport.y * 2 - 1) * halfHeight;
                float x = (viewport.x * 2 - 1) * halfHeight * cabin.aspect;
                return eye + viewRotation * new Vector3(x, y, 1) * (viewport.z * exteriorScale);
            }

            planetPosition = WindowPoint(new Vector3(.53f, .54f, 112));
            Planet.position = planetPosition;
            Planet.localScale = Vector3.one * (36 * exteriorScale);
            Vector3 surfaceDirection = ((eye - planetPosition).normalized
                + viewRotation * new Vector3(-.17f, .23f, 0)).normalized;
            planetEnergy.position = planetPosition + surfaceDirection * (18.1f * exteriorScale);
            foreach (var piece in pieces)
            {
                piece.Destination = WindowPoint(piece.Viewport);
                piece.Transform.localScale *= exteriorScale;
            }

            remnants[0].SetPositionAndRotation(WindowPoint(new Vector3(.451f, .546f, 108)), Quaternion.Euler(22, -29, 18));
            remnants[1].SetPositionAndRotation(WindowPoint(new Vector3(.557f, .519f, 114)), Quaternion.Euler(-28, 36, 31));
            foreach (var remnant in remnants) remnant.localScale *= exteriorScale;
        }

        public void SamplePlanetEnergy(float elapsed, float duration)
        {
            planetEnergy.gameObject.SetActive(true);
            float t = Mathf.Min(elapsed, duration);
            float progress = t / duration;
            planetEnergy.localScale = Vector3.one * Mathf.Lerp(1.1f, 108, Mathf.SmoothStep(0, 1, progress)) * exteriorScale;
            planetGlow.SetFloat("_PhaseTime", t);
            AnimateRibbons(planetRibbons, t);

            float fractureTime = duration * settings.farewellFractureStart;
            if (t >= fractureTime)
            {
                BeginFracture();
                concealedFractureTime = t - fractureTime;
                MoveFragments(concealedFractureTime);
            }

            // Keep the body opaque and build the flash from the core. The planet is already
            // in pieces when the flash starts, even at the shortest permitted expansion time.
            float flashDuration = Mathf.Min(settings.farewellFinalFlashDuration, duration - fractureTime);
            float flash = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - flashDuration, duration, t));
            planetGlow.SetFloat("_CoreFlash", flash * settings.farewellFinalFlashIntensity);
        }

        public void FinishPlanetEnergy()
        {
            planetEnergy.gameObject.SetActive(false);
            foreach (var remnant in remnants) remnant.gameObject.SetActive(true);
        }

        sealed class Fragment
        {
            public Transform Transform;
            public Vector3 Origin;
            public Vector3 Viewport;
            public Vector3 Destination;
            public Vector3 RotationSpeed;
        }

        void BuildFragments(Material material)
        {
            var random = new System.Random(1726);
            var shapes = new Mesh[8];
            for (int i = 0; i < shapes.Length; i++) shapes[i] = CreateRock(i);

            for (int i = 0; i < remnants.Length; i++)
            {
                var remnant = new GameObject("Stationary planet remnant " + (i + 1)).transform;
                remnant.SetParent(exterior, false);
                remnant.gameObject.layer = 26;
                remnant.gameObject.AddComponent<MeshFilter>().sharedMesh = shapes[i == 0 ? 2 : 6];
                remnant.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                remnant.localScale = i == 0 ? new Vector3(29, 22, 20) : new Vector3(29, 29, 20);
                remnant.gameObject.SetActive(false);
                remnants[i] = remnant;
            }

            // Large foreground silhouettes frame the photograph. The middle stays open.
            var foreground = new[]
            {
                new Vector3(.13f, .14f, 17), new Vector3(.81f, .69f, 25),
                new Vector3(.31f, .76f, 31), new Vector3(.74f, .24f, 35)
            };
            for (int i = 0; i < 160; i++)
            {
                var node = new GameObject("Planet fragment " + (i + 1)).transform;
                node.SetParent(exterior, false);
                node.gameObject.layer = 26;
                node.gameObject.AddComponent<MeshFilter>().sharedMesh = shapes[i % shapes.Length];
                node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                node.gameObject.SetActive(false);

                float width;
                Vector3 viewport;
                if (i < foreground.Length)
                {
                    width = i == 0 ? 8.4f : 5.6f + i * .35f;
                    viewport = foreground[i];
                }
                else
                {
                    bool medium = i < 36;
                    width = medium ? Range(random, 1.1f, 3.4f) : Range(random, .14f, .85f);
                    viewport = new Vector3(Range(random, .02f, .98f), Range(random, .05f, .95f),
                        medium ? Range(random, 32, 85) : Range(random, 40, 128));
                }
                node.localScale = new Vector3(width, width * Range(random, .61f, .91f), width * Range(random, .75f, 1.15f));
                float angle = Range(random, 0, Mathf.PI * 2);
                float height = Range(random, -1, 1);
                float radius = Mathf.Sqrt(1 - height * height);
                pieces.Add(new Fragment
                {
                    Transform = node,
                    Origin = new Vector3(radius * Mathf.Cos(angle), height, radius * Mathf.Sin(angle)) * Range(random, 5, 18),
                    Viewport = viewport,
                    RotationSpeed = new Vector3(Range(random, -28, 28), Range(random, -33, 33), Range(random, -23, 23))
                });
            }
        }

        static float Range(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        Mesh CreateRock(int seed)
        {
            var vertices = new List<Vector3>();
            foreach (int x in new[] { -1, 1 })
            foreach (int y in new[] { -1, 1 })
            foreach (int z in new[] { -1, 1 })
            {
                var a = Vector3.right * x;
                var b = Vector3.up * y;
                var c = Vector3.forward * z;
                if (x * y * z > 0) AddRockFaces(vertices, a, b, c, 2, seed);
                else AddRockFaces(vertices, a, c, b, 2, seed);
            }
            var triangles = new int[vertices.Count];
            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                triangles[i] = i;
                var point = vertices[i];
                uv[i] = new Vector2(.5f + point.x * .71f + point.z * .2f, .5f + point.y * .72f - point.z * .22f);
            }
            var mesh = new Mesh { name = "Faceted farewell stone " + seed };
            mesh.SetVertices(vertices);
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            resources.Add(mesh);
            return mesh;
        }

        static void AddRockFaces(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c, int depth, int seed)
        {
            if (depth == 0)
            {
                vertices.Add(RockPoint(a, seed));
                vertices.Add(RockPoint(b, seed));
                vertices.Add(RockPoint(c, seed));
                return;
            }
            Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
            AddRockFaces(vertices, a, ab, ca, depth - 1, seed);
            AddRockFaces(vertices, ab, b, bc, depth - 1, seed);
            AddRockFaces(vertices, ca, bc, c, depth - 1, seed);
            AddRockFaces(vertices, ab, bc, ca, depth - 1, seed);
        }

        static Vector3 RockPoint(Vector3 direction, int seed)
        {
            float facet = Mathf.PerlinNoise(direction.x * 2.6f + seed * 3.1f, direction.y * 2.4f + direction.z * 1.8f + 10);
            return direction * Mathf.Lerp(.30f, .61f, facet);
        }

        void BeginFracture()
        {
            if (Broken) return;
            Broken = true;
            Planet.gameObject.SetActive(false);
            MoveFragments(0);
            foreach (var piece in pieces) piece.Transform.gameObject.SetActive(true);
        }

        public void SampleFragments(float elapsed)
        {
            MoveFragments(concealedFractureTime + elapsed);
        }

        void MoveFragments(float t)
        {
            float spread = 1 - Mathf.Exp(-t / 1.35f);
            foreach (var piece in pieces)
            {
                var start = planetPosition + piece.Origin * exteriorScale;
                piece.Transform.position = Vector3.Lerp(start, piece.Destination, spread)
                    + piece.Origin.normalized * (t * .14f * exteriorScale);
                piece.Transform.rotation = Quaternion.Euler(piece.RotationSpeed * (t + 1));
            }
        }

        public void BeginPhoto()
        {
            photoStart = cabin.transform.position + cabin.transform.forward * 3.4f + cabin.transform.right * 3.05f;
            photoRotation = cabin.transform.rotation;
            SamplePhoto(0, settings.farewellPhotoDuration);
            Photo.gameObject.SetActive(true);
        }

        public void SamplePhoto(float t, float duration)
        {
            float progress = Mathf.Clamp01(t / duration);
            var offset = new Vector3(-5.55f * progress, .15f + Mathf.Sin(progress * Mathf.PI) * .2f, 0);
            Photo.position = photoStart + photoRotation * offset;
            Photo.rotation = photoRotation * Quaternion.Euler(Mathf.Sin(progress * 6) * 10,
                Mathf.Sin(progress * 4) * 15, Mathf.Lerp(-14, 22, progress));
        }
        public void Dispose(){if(Root){Root.gameObject.SetActive(false);UnityEngine.Object.Destroy(Root.gameObject);}if(Video){Video.Release();UnityEngine.Object.Destroy(Video);}foreach(var r in resources)if(r)UnityEngine.Object.Destroy(r);resources.Clear();}
    }
}


