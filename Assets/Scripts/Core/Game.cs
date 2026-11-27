using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VoidStunts
{
    /// <summary>
    /// Boots everything in code (the scene only needs to exist): renderer state, camera, atmosphere, looks, the frozen VS-02,
    /// input, audio and UI; builds levels on demand; runs the menu → count-in → run → results loop.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public enum State { Menu, Loading, Count, Play, Free, End }
        public State state = State.Menu; bool paused;
        public Level[] Levels = { new CityLevel(), new CanyonLevel(), new ProvingLevel() };
        public Camera camera3d; public ChaseCamera cam; public Atmos atmos; public Looks looks; public CarController car; public CarVisual visual; public TyreTracks tracks;
        public TouchInput input; public SynthAudio sound; public GameUI ui; public Fx fx; public Vegetation veg; public Scoring score;
        LevelContext ctx; Transform levelRoot; int builtLevel = -1; float countT, previewBoost; readonly float[] hubY = new float[4], spin = new float[4];
        float coachT; int coachStep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindFirstObjectByType<Game>() == null) new GameObject("VoidStunts").AddComponent<Game>(); }

        void Awake()
        {
            Settings.Load();
            Time.fixedDeltaTime = 1 / 120f; Physics.gravity = new Vector3(0, -CarController.G, 0); Physics.defaultSolverIterations = 8;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; Input.multiTouchEnabled = true;
            Lib.Init(); UIKit.Init();

            var cg = new GameObject("Camera"); cg.transform.SetParent(transform, false); camera3d = cg.AddComponent<Camera>(); cg.tag = "MainCamera";
            camera3d.nearClipPlane = .1f; camera3d.farClipPlane = 4200; camera3d.clearFlags = CameraClearFlags.Skybox; camera3d.allowHDR = true;
            var cd = camera3d.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cd.antialiasingQuality = AntialiasingQuality.Medium;
            cg.AddComponent<AudioListener>();

            atmos = new GameObject("Atmos").AddComponent<Atmos>(); atmos.transform.SetParent(transform, false); atmos.Init();
            looks = new GameObject("Looks").AddComponent<Looks>(); looks.transform.SetParent(transform, false);
            looks.favs = new System.Collections.Generic.List<int>(Settings.favs); looks.auto = Settings.autoLooks; looks.baseLook = Settings.look; looks.Init(camera3d);

            var carGo = new GameObject("VS-02"); carGo.transform.SetParent(transform, false); carGo.AddComponent<Rigidbody>();
            car = carGo.AddComponent<CarController>(); car.Setup();
            visual = carGo.AddComponent<CarVisual>(); visual.Build(); visual.ApplyLivery(Settings.livery); visual.ApplyPlume(Settings.plume);
            foreach (var t in carGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CarController.CarLayer;
            tracks = new GameObject("Tracks").AddComponent<TyreTracks>(); tracks.transform.SetParent(transform, false); tracks.Init(car);

            cam = cg.AddComponent<ChaseCamera>(); cam.cam = camera3d; cam.car = car;
            input = gameObject.AddComponent<TouchInput>(); input.car = car; input.onVerb = v => { sound.Sfx("trick"); Haptics.Play(Haptics.Kind.Selection); CoachDone(v); };
            sound = new GameObject("Audio").AddComponent<SynthAudio>(); sound.transform.SetParent(transform, false); sound.muted = !Settings.sound;
            fx = new GameObject("Fx").AddComponent<Fx>(); fx.transform.SetParent(transform, false); fx.Init();
            ui = new GameObject("UI").AddComponent<GameUI>(); ui.transform.SetParent(transform, false); ui.game = this; ui.Build();
            input.blocked = OverUI;

            atmos.Apply(Settings.atmos);
            StartCoroutine(LoadLevel(Settings.level, () => ToMenu()));
        }

        static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> hits = new();
        static bool OverUI(Vector2 px)
        {
            var es = UnityEngine.EventSystems.EventSystem.current; if (es == null) return false;
            hits.Clear(); es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = px }, hits); return hits.Count > 0;
        }

        // ---------------- levels ----------------
        IEnumerator LoadLevel(int i, System.Action done)
        {
            state = State.Loading; var L = Levels[i]; ui.Loading(true, "BUILDING " + L.Name + " · " + L.Blurb);
            yield return null; yield return null;
            if (levelRoot) Destroy(levelRoot.gameObject);
            levelRoot = new GameObject("Level_" + L.Id).transform; levelRoot.SetParent(transform, false);
            veg = levelRoot.gameObject.AddComponent<Vegetation>(); veg.Init(camera3d, 7 + i);
            ctx = new LevelContext { kit = new MeshKit(levelRoot), veg = veg, root = levelRoot, rng = new System.Random(1337 + i * 101) };
            L.Build(ctx); ctx.kit.Build(true);
            Settings.ApplyQuality(veg);
            builtLevel = i; Physics.SyncTransforms();
            car.frozen = true; car.Place(ctx.spawn, ctx.spawnYaw, .9f); tracks.ResetAll(); cam.Snap(ctx.spawnYaw);
            yield return new WaitForFixedUpdate();
            ui.Loading(false); done?.Invoke();
        }
        public void CycleLevel(int d)
        {
            if (state != State.Menu) return;
            Settings.level = (Settings.level + d + Levels.Length) % Levels.Length; Settings.Save(); ui.RefreshMenu();
            StartCoroutine(LoadLevel(Settings.level, () => ToMenu()));
        }

        // ---------------- flow ----------------
        public void ToMenu()
        {
            state = State.Menu; paused = false; Time.timeScale = 1; input.active = false; cam.menu = true; car.frozen = true;
            score = null; looks.ResetLadder(); ui.ShowMenu();
            if (ctx != null) { car.Place(ctx.spawn, ctx.spawnYaw, .9f); }
        }
        public void StartRun(bool free)
        {
            if (builtLevel != Settings.level) { StartCoroutine(LoadLevel(Settings.level, () => StartRun(free))); return; }
            ui.free = free; paused = false; Time.timeScale = 1;
            car.Place(ctx.spawn, ctx.spawnYaw, .9f); car.fuel = 1; tracks.ResetAll(); cam.menu = false; cam.Snap(ctx.spawnYaw);
            foreach (var r in ctx.rings) { r.active = true; r.cool = 0; r.side = float.NaN; }
            score = new Scoring(car, Levels[Settings.level], ctx);
            score.onBank = (p, w, bail) => { ui.Bank(p, w, bail); if (bail) { looks.Bail(); Haptics.Play(Haptics.Kind.Error); } else { looks.Bank(score.score); Haptics.Play(Haptics.Kind.Success); } };
            score.onToast = ui.Toast; score.onSfx = sound.Sfx; score.onGoal = g => Haptics.Play(Haptics.Kind.Success);
            score.onLand = (s, p, n) => { fx.Land(p, n, s); sound.Thud(s); cam.Shake(Mathf.Min(1, s * .05f)); Haptics.Play(s > 14 ? Haptics.Kind.Heavy : Haptics.Kind.Medium); };
            score.onRing = r => { fx.RingPass(r); Haptics.Play(Haptics.Kind.Success); };
            score.onPad = p => { cam.Kick(); Haptics.Play(Haptics.Kind.Heavy); };
            score.onRespawn = () => { cam.Snap(score.safeYaw); tracks.ResetAll(); };
            looks.ResetLadder(); ui.ShowHud(!free);
            state = State.Count; countT = 3.2f; car.frozen = true; input.active = true; coachStep = 0; coachT = 0;
            if (free) { state = State.Free; car.frozen = false; score.scoringOn = true; score.time = 0; ui.Count("GO"); sound.Sfx("go"); }
        }
        public void Pause(bool on)
        {
            if (state != State.Play && state != State.Free && state != State.Count) return;
            paused = on; Time.timeScale = on ? 0 : 1; input.active = !on; ui.ShowPause(on, score); if (on) sound.Drive(car, false);
        }
        public void ResetCar() { if (score != null) score.Respawn(); }
        public void PreviewBoost() => previewBoost = 1.6f;
        void End()
        {
            state = State.End; input.active = false; car.frozen = true; sound.Sfx("end");
            var id = Levels[Settings.level].Id; int best = Settings.Best(id); bool rec = score.score > best; if (rec) Settings.SetBest(id, score.score);
            ui.ShowEnd(score, Mathf.Max(best, score.score), rec);
        }
        void OnApplicationPause(bool p) { if (p && (state == State.Play || state == State.Free)) Pause(true); }

        // ---------------- coach ----------------
        static readonly (string verb, string text)[] COACH = {
            ("gas", "RIGHT THUMB · HOLD FOR GAS"), ("steer", "LEFT THUMB · SLIDE SIDEWAYS TO STEER"), ("boost", "RIGHT THUMB · SLIDE OR FLICK UP FOR ROCKET"),
            ("hop", "LEFT THUMB · FLICK UP TO HOP"), ("flick", "IN THE AIR · SLIDE OR FLICK TO ROTATE · HOLD STILL TO CATCH") };
        void CoachDone(string v) { if (coachStep < COACH.Length && COACH[coachStep].verb == v) { coachStep++; coachT = 0; } }
        void CoachTick(float dt)
        {
            if (coachStep >= COACH.Length || !Settings.coach) { ui.Coach(""); return; }
            coachT += dt; var c = COACH[coachStep];
            if (c.verb == "gas" && car.inp.gas > 0 && coachT > .8f) CoachDone("gas");
            if (c.verb == "steer" && Mathf.Abs(car.inp.steer) > .3f && coachT > .8f) CoachDone("steer");
            if (c.verb == "boost" && car.boosting) CoachDone("boost");
            if (c.verb == "flick" && car.grounded == 0 && (car.inp.pitch != 0 || car.inp.roll != 0 || car.inp.spin != 0)) CoachDone("flick");
            ui.Coach(coachStep < COACH.Length ? COACH[coachStep].text : "");
        }

        // ---------------- frame ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            if (state == State.Count && !paused)
            {
                float prev = countT; countT -= dt;
                for (int k = 3; k >= 1; k--) if (prev > k && countT <= k) { ui.Count(k.ToString()); sound.Sfx("count"); Haptics.Play(Haptics.Kind.Light); }
                if (countT <= 0) { state = State.Play; car.frozen = false; score.scoringOn = true; ui.Count("GO"); sound.Sfx("go"); Haptics.Play(Haptics.Kind.Heavy); }
            }
            if ((state == State.Play || state == State.Free) && !paused)
            {
                score.Tick(dt);
                if (state == State.Play) { score.time -= dt; if (score.time <= 0) { score.time = 0; End(); } } else score.time += dt;
                looks.Tick(dt, true, car.grounded == 0, car.drifting, car.rb.position, m => { ui.Toast("LOOK · " + Looks.NAMES[m] + " · " + Looks.SUBS[m]); sound.Sfx("mode"); });
                CoachTick(dt);
                if (Input.GetKeyDown(KeyCode.R)) ResetCar();
            }
            else { car.ClearEvents(); looks.Tick(dt, false, false, false, car.rb.position, null); }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) Pause(!paused);
            if (state == State.Count) ui.Coach(Settings.coach ? "TWO THUMBS · LEFT STEERS THE BODY · RIGHT IS POWER" : "");

            // visual: wheels, steering, plume
            car.HubHeights(hubY, spin); previewBoost = Mathf.Max(0, previewBoost - dt);
            visual.Pose(hubY, spin, car.steerAngle, car.boosting || previewBoost > 0, car.inp.brake > 0, dt);
            sound.Drive(car, state == State.Play || state == State.Free || state == State.Count);
            fx.Tick(dt, ctx); if (builtLevel >= 0) Levels[builtLevel].Tick(dt);
        }
    }
}
