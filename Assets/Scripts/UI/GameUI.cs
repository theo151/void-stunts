using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidStunts
{
    /// <summary>
    /// Menu, sheets (garage / looks / atmosphere / settings), HUD, count-in, pause and results — all built in code.
    /// Portrait puts the card along the bottom; landscape moves it to a right-hand column and the popups to the top-right corner,
    /// so nothing sits over the car.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public Game game;
        RectTransform safe, menu, card, hud, sheet, sheetBody, pause, end, loading, count, coach;
        Text levelName, levelBlurb, levelBest, modeRun, modeFree, sheetTitle, score, mult, timer, speed, toast, bankPts, bankWhat, endScore, endBest, endStats, loadingT, countT, coachT, endGoals, pauseGoals;
        RawImage title; Image fuel, fuelBg, toastBg, bankBg; RectTransform bank; CanvasGroup bankGroup, toastGroup;
        float bankT, toastT, countPulse; bool portrait = true, built; public bool free;
        readonly List<Image> thumbDots = new();

        public void Build()
        {
            UIKit.MakeCanvas(transform, out safe);
            BuildMenu(); BuildHud(); BuildSheet(); BuildPause(); BuildEnd(); BuildLoading();
            built = true; Layout(true); ShowMenu();
        }

        // ---------------- menu ----------------
        void BuildMenu()
        {
            menu = UIKit.Rect("Menu", safe);
            title = UIKit.Matrix(menu, "VOID STUNTS", 22, UIKit.Ink, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -18));
            var sub = UIKit.Label(menu, "VS-02 · PROTOTYPE · " + Application.version, 10, UIKit.InkSoft, TextAnchor.UpperLeft); var sr = sub.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0, 1); sr.pivot = new Vector2(0, 1); sr.sizeDelta = new Vector2(300, 16); sr.anchoredPosition = new Vector2(21, -48);
            UIKit.Dot(menu, new Vector2(1, 1), new Vector2(-26, -28), 8, UIKit.Red);
            card = UIKit.Rect("Card", menu); UIKit.Panel(card, UIKit.Paper);
            var arrows = new[] { ("‹", -1), ("›", 1) };
            levelName = UIKit.Label(card, "", 22, UIKit.Ink, TextAnchor.UpperLeft, UIKit.Wide); Place(levelName.rectTransform, 18, -16, -110, 30);
            levelBlurb = UIKit.Label(card, "", 10, UIKit.InkSoft, TextAnchor.UpperLeft); Place(levelBlurb.rectTransform, 19, -50, -20, 16);
            levelBest = UIKit.Label(card, "", 10, UIKit.InkSoft, TextAnchor.UpperLeft); Place(levelBest.rectTransform, 19, -66, -20, 16);
            UIKit.Pill(card, "‹", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-62, -16), new Vector2(40, 34), () => game.CycleLevel(-1), false, 16);
            UIKit.Pill(card, "›", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(40, 34), () => game.CycleLevel(1), false, 16);
            var run = UIKit.Pill(card, "RUN 3:00", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -94), new Vector2(110, 30), () => { free = false; RefreshMenu(); }, false, 11); modeRun = run.GetComponentInChildren<Text>();
            var fr = UIKit.Pill(card, "FREE ROAM", new Vector2(0, 1), new Vector2(0, 1), new Vector2(132, -94), new Vector2(110, 30), () => { free = true; RefreshMenu(); }, false, 11); modeFree = fr.GetComponentInChildren<Text>();
            UIKit.Pill(card, "DRIVE", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -134), new Vector2(-32, 52), () => game.StartRun(free), true, 15).GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            string[] chips = { "GARAGE", "LOOKS", "ATMOS", "SETTINGS" };
            for (int i = 0; i < 4; i++) { int k = i; var b = UIKit.Pill(card, chips[i], new Vector2(i / 4f, 0), new Vector2(0, 0), new Vector2(i == 0 ? 16 : 4, 16), new Vector2(0, 32), () => OpenSheet(k), false, 10); var r = b.GetComponent<RectTransform>(); r.anchorMax = new Vector2((i + 1) / 4f, 0); r.offsetMax = new Vector2(i == 3 ? -16 : -4, 48); r.offsetMin = new Vector2(i == 0 ? 16 : 4, 16); }
        }
        static void Place(RectTransform r, float x, float y, float right, float h) { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0, 1); r.offsetMin = new Vector2(x, y - h); r.offsetMax = new Vector2(right, y); }

        public void RefreshMenu()
        {
            if (!built) return; var L = game.Levels[Settings.level];
            levelName.text = L.Name; levelBlurb.text = L.Blurb; int b = Settings.Best(L.Id); levelBest.text = b > 0 ? "BEST " + b.ToString("N0") : "NO RUN YET";
            modeRun.transform.parent.GetComponent<Image>().color = free ? UIKit.Paper : UIKit.Volt; modeFree.transform.parent.GetComponent<Image>().color = free ? UIKit.Volt : UIKit.Paper;
        }

        // ---------------- sheets ----------------
        int sheetKind = -1;
        void BuildSheet()
        {
            sheet = UIKit.Rect("Sheet", safe); UIKit.Panel(sheet, UIKit.Paper);
            sheetTitle = UIKit.Label(sheet, "", 16, UIKit.Ink, TextAnchor.UpperLeft, UIKit.Wide); Place(sheetTitle.rectTransform, 18, -16, -80, 24);
            UIKit.Pill(sheet, "DONE", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(70, 30), CloseSheet, true, 11);
            sheetBody = UIKit.Rect("Body", sheet, Vector2.zero, Vector2.one, new Vector2(14, 14), new Vector2(-14, -56));
            sheet.gameObject.SetActive(false);
        }
        void OpenSheet(int k) { sheetKind = k; sheet.gameObject.SetActive(true); game.cam.sheetOpen = true; FillSheet(); Layout(true); }
        void CloseSheet() { sheet.gameObject.SetActive(false); game.cam.sheetOpen = false; sheetKind = -1; Settings.Save(); }

        void FillSheet()
        {
            for (int i = sheetBody.childCount - 1; i >= 0; i--) Destroy(sheetBody.GetChild(i).gameObject);
            var items = new List<(string label, bool on, System.Action act)>(); string head = "";
            if (sheetKind == 0)
            {
                head = "GARAGE";
                for (int i = 0; i < CarVisual.LIVERIES.Length; i++) { int k = i; items.Add((CarVisual.LIVERIES[i].name, Settings.livery == i, () => { Settings.livery = k; game.visual.ApplyLivery(k); })); }
                for (int i = 0; i < CarVisual.PLUMES.Length; i++) { int k = i; items.Add(("PLUME · " + CarVisual.PLUMES[i].name, Settings.plume == i, () => { Settings.plume = k; game.visual.ApplyPlume(k); game.PreviewBoost(); })); }
            }
            else if (sheetKind == 1)
            {
                head = "LOOKS";
                for (int i = 0; i < Looks.NAMES.Length; i++)
                {
                    int k = i; bool fav = System.Array.IndexOf(Settings.favs, i) >= 0;
                    items.Add(((Settings.look == i ? "● " : "") + Looks.NAMES[i] + (fav ? " ★" : ""), Settings.look == i, () =>
                    {
                        if (Settings.look == k) { var l = new List<int>(Settings.favs); if (l.Contains(k)) l.Remove(k); else l.Add(k); Settings.favs = l.ToArray(); game.looks.favs = l; }
                        else { Settings.look = k; game.looks.SetBase(k); }
                    }));
                }
                items.Add(("AUTO LADDER · " + (Settings.autoLooks ? "ON" : "OFF"), Settings.autoLooks, () => { Settings.autoLooks = !Settings.autoLooks; game.looks.auto = Settings.autoLooks; }));
            }
            else if (sheetKind == 2)
            {
                head = "ATMOSPHERE";
                for (int i = 0; i < Atmos.PRESETS.Length; i++) { int k = i; items.Add((Atmos.PRESETS[i].name, Settings.atmos == i, () => { Settings.atmos = k; game.atmos.Apply(k); })); }
            }
            else
            {
                head = "SETTINGS";
                for (int i = 0; i < 3; i++) { int k = i; items.Add((Settings.QUALITY[i], Settings.quality == i, () => { Settings.quality = k; Settings.ApplyQuality(game.veg); })); }
                items.Add(("120 HZ · " + (Settings.hz120 ? "ON" : "OFF"), Settings.hz120, () => { Settings.hz120 = !Settings.hz120; Settings.ApplyQuality(game.veg); }));
                items.Add(("SOUND · " + (Settings.sound ? "ON" : "OFF"), Settings.sound, () => { Settings.sound = !Settings.sound; game.sound.muted = !Settings.sound; }));
                items.Add(("HAPTICS · " + (Settings.haptics ? "ON" : "OFF"), Settings.haptics, () => { Settings.haptics = !Settings.haptics; Haptics.enabled = Settings.haptics; }));
                items.Add(("CONTROL HINTS · " + (Settings.coach ? "ON" : "OFF"), Settings.coach, () => { Settings.coach = !Settings.coach; }));
            }
            sheetTitle.text = head;
            int cols = portrait ? 2 : 2; float h = 34, gap = 6;
            for (int i = 0; i < items.Count; i++)
            {
                var (label, on, act) = items[i]; int c = i % cols, r = i / cols;
                var b = UIKit.Pill(sheetBody, label, new Vector2(c / (float)cols, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero, () => { act(); FillSheet(); }, false, 10);
                var rt = b.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(c / (float)cols, 1); rt.anchorMax = new Vector2((c + 1) / (float)cols, 1);
                rt.offsetMin = new Vector2(c == 0 ? 0 : gap / 2, -(r + 1) * (h + gap) + gap); rt.offsetMax = new Vector2(c == cols - 1 ? 0 : -gap / 2, -r * (h + gap));
                if (on) b.GetComponent<Image>().color = UIKit.Volt;
            }
            if (sheetKind == 1) { var hint = UIKit.Label(sheetBody, "TAP TO SET BASE · TAP AGAIN TO STAR · STARRED LOOKS JOIN THE MOMENTUM LADDER", 9, UIKit.InkSoft, TextAnchor.UpperLeft); Place(hint.rectTransform, 2, -((items.Count + 1) / 2) * (h + gap) - 6, 0, 30); }
        }

        // ---------------- hud ----------------
        void BuildHud()
        {
            hud = UIKit.Rect("Hud", safe);
            score = UIKit.Label(hud, "0", 26, UIKit.White, TextAnchor.UpperLeft, UIKit.Wide); Corner(score.rectTransform, new Vector2(0, 1), new Vector2(18, -14), new Vector2(260, 34));
            score.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .35f);
            mult = UIKit.Label(hud, "", 12, UIKit.Volt, TextAnchor.UpperLeft, UIKit.MonoBold); Corner(mult.rectTransform, new Vector2(0, 1), new Vector2(20, -50), new Vector2(200, 18));
            timer = UIKit.Label(hud, "3:00", 16, UIKit.White, TextAnchor.UpperCenter, UIKit.MonoBold); Corner(timer.rectTransform, new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(120, 22), new Vector2(.5f, 1));
            timer.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .35f);
            UIKit.Pill(hud, "II", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(44, 34), () => game.Pause(true), false, 12);
            var fb = UIKit.Box("Fuel", hud, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(160, 4)); fuelBg = UIKit.Panel(fb, new Color(1, 1, 1, .25f)); fuelBg.raycastTarget = false;
            var ff = UIKit.Rect("Fill", fb); fuel = UIKit.Panel(ff, UIKit.Volt); fuel.raycastTarget = false; ff.pivot = new Vector2(0, .5f);
            speed = UIKit.Label(hud, "0", 10, UIKit.White, TextAnchor.LowerCenter, UIKit.MonoBold); Corner(speed.rectTransform, new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(160, 16), new Vector2(.5f, 0));
            bank = UIKit.Rect("Bank", hud); bankBg = UIKit.Panel(bank, UIKit.Paper); bankGroup = bank.gameObject.AddComponent<CanvasGroup>(); bankGroup.blocksRaycasts = false;
            bankPts = UIKit.Label(bank, "", 22, UIKit.Ink, TextAnchor.UpperCenter, UIKit.Wide); Place(bankPts.rectTransform, 10, -10, -10, 28);
            bankWhat = UIKit.Label(bank, "", 10, UIKit.InkSoft, TextAnchor.UpperCenter, UIKit.MonoBold); Place(bankWhat.rectTransform, 10, -42, -10, 30);
            var tb = UIKit.Box("Toast", hud, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -46), new Vector2(320, 28)); toastBg = UIKit.Panel(tb, UIKit.Ink); toastGroup = tb.gameObject.AddComponent<CanvasGroup>(); toastGroup.blocksRaycasts = false;
            toast = UIKit.Label(tb, "", 10, UIKit.White, TextAnchor.MiddleCenter, UIKit.MonoBold);
            count = UIKit.Rect("Count", hud); countT = UIKit.Label(count, "", 64, UIKit.White, TextAnchor.MiddleCenter, UIKit.Wide); countT.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .3f);
            coach = UIKit.Rect("Coach", hud, new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 46), new Vector2(-20, 90));
            coachT = UIKit.Label(coach, "", 10, new Color(1, 1, 1, .8f), TextAnchor.MiddleCenter, UIKit.MonoBold); coachT.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .4f);
            for (int i = 0; i < 4; i++) { var d = UIKit.Dot(hud, Vector2.zero, Vector2.zero, i % 2 == 0 ? 56 : 18, new Color(1, 1, 1, .35f), i % 2 == 0); d.rectTransform.anchorMin = d.rectTransform.anchorMax = Vector2.zero; thumbDots.Add(d); }
            hud.gameObject.SetActive(false);
        }
        static void Corner(RectTransform r, Vector2 a, Vector2 pos, Vector2 size, Vector2? pivot = null) { r.anchorMin = r.anchorMax = a; r.pivot = pivot ?? a; r.sizeDelta = size; r.anchoredPosition = pos; }

        public void Bank(string pts, string what, bool bail)
        {
            bankPts.text = pts; bankWhat.text = what; bankBg.color = bail ? UIKit.Ink : UIKit.Paper; bankPts.color = bail ? UIKit.Red : UIKit.Ink; bankWhat.color = bail ? new Color(1, 1, 1, .7f) : UIKit.InkSoft;
            bankT = 2; bank.gameObject.SetActive(true);
        }
        public void Toast(string t) { toast.text = t; toastT = 2.6f; toastBg.gameObject.SetActive(true); }
        public void Count(string s) { countT.text = s; countPulse = 1; count.gameObject.SetActive(s.Length > 0); }
        public void Coach(string s) { coachT.text = Settings.coach ? s : ""; }

        // ---------------- pause / end / loading ----------------
        void BuildPause()
        {
            pause = UIKit.Rect("Pause", safe); var bg = UIKit.Rect("Dim", pause); var im = bg.gameObject.AddComponent<Image>(); im.color = new Color(.05f, .05f, .055f, .45f);
            var p = UIKit.Box("Card", pause, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 330)); UIKit.Panel(p, UIKit.Paper);
            var t = UIKit.Label(p, "PAUSED", 18, UIKit.Ink, TextAnchor.UpperLeft, UIKit.Wide); Place(t.rectTransform, 18, -16, -18, 26);
            pauseGoals = UIKit.Label(p, "", 10, UIKit.Ink, TextAnchor.UpperLeft); Place(pauseGoals.rectTransform, 18, -50, -18, 140);
            UIKit.Pill(p, "RESUME", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 108), new Vector2(264, 40), () => game.Pause(false), true, 13);
            UIKit.Pill(p, "RESTART", new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(-3, 60), new Vector2(129, 38), () => { game.Pause(false); game.StartRun(free); }, false, 11);
            UIKit.Pill(p, "MENU", new Vector2(.5f, 0), new Vector2(0, 0), new Vector2(3, 60), new Vector2(129, 38), () => { game.Pause(false); game.ToMenu(); }, false, 11);
            UIKit.Pill(p, "RESET CAR", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 16), new Vector2(264, 36), () => { game.Pause(false); game.ResetCar(); }, false, 11);
            pause.gameObject.SetActive(false);
        }
        void BuildEnd()
        {
            end = UIKit.Rect("End", safe); var bg = UIKit.Rect("Dim", end); bg.gameObject.AddComponent<Image>().color = new Color(.05f, .05f, .055f, .35f);
            var p = UIKit.Box("Card", end, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(320, 420)); UIKit.Panel(p, UIKit.Paper);
            var t = UIKit.Label(p, "RUN COMPLETE", 12, UIKit.InkSoft, TextAnchor.UpperLeft, UIKit.MonoBold); Place(t.rectTransform, 18, -16, -18, 18);
            endScore = UIKit.Label(p, "", 34, UIKit.Ink, TextAnchor.UpperLeft, UIKit.Wide); Place(endScore.rectTransform, 16, -36, -18, 44);
            endBest = UIKit.Label(p, "", 10, UIKit.Ink, TextAnchor.UpperLeft, UIKit.MonoBold); Place(endBest.rectTransform, 18, -82, -18, 16);
            endStats = UIKit.Label(p, "", 10, UIKit.InkSoft, TextAnchor.UpperLeft); Place(endStats.rectTransform, 18, -106, -18, 70);
            endGoals = UIKit.Label(p, "", 10, UIKit.Ink, TextAnchor.UpperLeft); Place(endGoals.rectTransform, 18, -178, -18, 150);
            UIKit.Pill(p, "AGAIN", new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(-3, 16), new Vector2(139, 44), () => game.StartRun(false), true, 13);
            UIKit.Pill(p, "MENU", new Vector2(.5f, 0), new Vector2(0, 0), new Vector2(3, 16), new Vector2(139, 44), () => game.ToMenu(), false, 12);
            end.gameObject.SetActive(false);
        }
        void BuildLoading()
        {
            loading = UIKit.Rect("Loading", transform.GetChild(0)); var im = loading.gameObject.AddComponent<Image>(); im.color = new Color(.94f, .935f, .92f, 1);
            loadingT = UIKit.Label(loading, "", 11, UIKit.Ink, TextAnchor.MiddleCenter, UIKit.MonoBold);
            UIKit.Dot(loading, new Vector2(.5f, .5f), new Vector2(0, 28), 8, UIKit.Red);
            loading.gameObject.SetActive(false);
        }
        public void Loading(bool on, string text = "") { loading.gameObject.SetActive(on); loadingT.text = text; loading.SetAsLastSibling(); }

        string GoalList(Scoring s)
        {
            var sb = new System.Text.StringBuilder(); var L = game.Levels[Settings.level];
            foreach (var g in L.Goals) sb.Append(s != null && s.goals.Contains(g.id) ? "■ " : "□ ").Append(g.text).Append('\n');
            return sb.ToString();
        }
        public void ShowPause(bool on, Scoring s) { pause.gameObject.SetActive(on); if (on) pauseGoals.text = GoalList(s); }
        public void ShowEnd(Scoring s, int best, bool record)
        {
            end.gameObject.SetActive(true); hud.gameObject.SetActive(false);
            endScore.text = s.score.ToString("N0"); endBest.text = record ? "● NEW BEST" : "BEST " + best.ToString("N0");
            endStats.text = $"BEST LANDING   {s.statBestLand:N0}\nLONGEST AIR    {s.statLongAir:0.0} S\nROTATIONS      {s.statFlips}\nRINGS          {s.statRings}";
            endGoals.text = GoalList(s);
        }
        public void ShowMenu() { menu.gameObject.SetActive(true); hud.gameObject.SetActive(false); end.gameObject.SetActive(false); pause.gameObject.SetActive(false); RefreshMenu(); }
        public void ShowHud(bool timed) { menu.gameObject.SetActive(false); sheet.gameObject.SetActive(false); game.cam.sheetOpen = false; end.gameObject.SetActive(false); hud.gameObject.SetActive(true); timer.gameObject.SetActive(timed); bank.gameObject.SetActive(false); toastBg.gameObject.SetActive(false); }

        // ---------------- per frame ----------------
        void Layout(bool force)
        {
            bool p = Screen.height >= Screen.width; if (!force && p == portrait) return; portrait = p;
            if (portrait)
            {
                card.anchorMin = new Vector2(0, 0); card.anchorMax = new Vector2(1, 0); card.pivot = new Vector2(.5f, 0); card.offsetMin = new Vector2(12, 12); card.offsetMax = new Vector2(-12, 262);
                sheet.anchorMin = new Vector2(0, 0); sheet.anchorMax = new Vector2(1, 0); sheet.pivot = new Vector2(.5f, 0); sheet.offsetMin = new Vector2(12, 12); sheet.offsetMax = new Vector2(-12, 360);
                bank.anchorMin = bank.anchorMax = new Vector2(.5f, .7f); bank.pivot = new Vector2(.5f, .5f); bank.sizeDelta = new Vector2(300, 78); bank.anchoredPosition = Vector2.zero;
            }
            else
            {
                card.anchorMin = new Vector2(1, 0); card.anchorMax = new Vector2(1, 1); card.pivot = new Vector2(1, .5f); card.offsetMin = new Vector2(-330, 12); card.offsetMax = new Vector2(-12, -64);
                sheet.anchorMin = new Vector2(1, 0); sheet.anchorMax = new Vector2(1, 1); sheet.pivot = new Vector2(1, .5f); sheet.offsetMin = new Vector2(-380, 12); sheet.offsetMax = new Vector2(-12, -12);
                // landscape: results go to the top-right corner, well clear of the car in the lower middle
                bank.anchorMin = bank.anchorMax = new Vector2(1, 1); bank.pivot = new Vector2(1, 1); bank.sizeDelta = new Vector2(280, 72); bank.anchoredPosition = new Vector2(-14, -56);
            }
            if (sheet.gameObject.activeSelf) FillSheet();
        }

        void Update()
        {
            if (!built) return; Layout(false);
            float dt = Time.unscaledDeltaTime;
            if (bankT > 0) { bankT -= dt; float a = Mathf.Clamp01(bankT / .3f), s = 1 + Mathf.Max(0, bankT - 1.8f) * .4f; bank.localScale = Vector3.one * s; bankGroup.alpha = a; if (bankT <= 0) bank.gameObject.SetActive(false); }
            if (toastT > 0) { toastT -= dt; toastGroup.alpha = Mathf.Clamp01(toastT / .3f); if (toastT <= 0) toastBg.gameObject.SetActive(false); }
            if (countPulse > 0) { countPulse = Mathf.Max(0, countPulse - dt * 1.4f); count.localScale = Vector3.one * (1 + countPulse * .25f); }
            if (!hud.gameObject.activeSelf || game.score == null) { foreach (var d in thumbDots) d.enabled = false; return; }
            var sc = game.score; score.text = sc.score.ToString("N0"); mult.text = sc.mult > 1 ? "×" + sc.mult + "  COMBO" : "";
            int tt = Mathf.CeilToInt(Mathf.Max(0, sc.time)); timer.text = (tt / 60) + ":" + (tt % 60).ToString("00"); timer.color = sc.time < 10 ? UIKit.Red : UIKit.White;
            var fr = fuel.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(game.car.fuel, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;
            fuel.color = game.car.boosting ? Color.white : UIKit.Volt;
            speed.text = Mathf.RoundToInt(game.car.rb.linearVelocity.magnitude * 3.6f) + " KM/H";
            // thumbs: a hollow anchor and a filled dot where the thumb is
            int i = 0;
            foreach (var t in game.input.thumbs.Values) { if (i > 2) break; Thumb(thumbDots[i], t.p0); Thumb(thumbDots[i + 1], t.p); i += 2; }
            for (; i < 4; i++) thumbDots[i].enabled = false;
        }
        void Thumb(Image d, Vector2 pt) { d.enabled = true; d.rectTransform.anchoredPosition = pt - SafeOrigin(); }
        Vector2 SafeOrigin() => new(Screen.safeArea.xMin * TouchInput.PointsPerPixel, Screen.safeArea.yMin * TouchInput.PointsPerPixel);
    }
}
