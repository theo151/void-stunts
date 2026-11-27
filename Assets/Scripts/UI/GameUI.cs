using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidStunts
{
    /// <summary>
    /// The interface, built in code. One calm system everywhere:
    /// paper cards (#F2F1ED) with ink type, a soft-grey field for secondary controls, ink for the one primary action,
    /// dark glass capsules over the 3D view, volt only as a small signal (fuel, combo, selected dot), red only for the record dot and bails.
    /// Nothing sits over the car: the HUD lives in the top band, hints sit by the thumb they refer to.
    /// The menu and sheets are rebuilt when the device rotates (portrait = bottom card, landscape = right column).
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public Game game; public bool free;

        // palette
        static readonly Color PaperC = new(.949f, .945f, .929f, .96f), Field = new(.886f, .882f, .867f, 1), Ink = new(.067f, .07f, .075f, 1),
            Ink60 = new(.067f, .07f, .075f, .6f), Ink35 = new(.067f, .07f, .075f, .35f), Ink10 = new(.067f, .07f, .075f, .1f), GlassC = new(.06f, .062f, .068f, .52f),
            White = new(.97f, .965f, .95f, 1), White70 = new(.97f, .965f, .95f, .72f), Volt = new(.831f, 1, 0, 1), Red = new(1, .16f, .12f, 1);

        RectTransform safe, menu, sheet, hud, pause, end, loading;
        int sheetKind = -1; bool portrait, built;

        // hud parts
        Text score, mult, speed, timer, toast, bankPts, bankWhat, countT, coachT;
        RectTransform bank, toastBox, coachBox, countBox, fuelFill; CanvasGroup bankG, toastG, countG, coachG; Image bankBg;
        float bankT, toastT, countT0 = -1; string countS = "";
        readonly List<Image> thumbDots = new();
        // pause / end
        Text pauseGoals, endScore, endBest, endStats, endGoals; Text loadingT; Image loadingDot;

        // ================================================================ building blocks
        static RectTransform R(string n, Transform p) => UIKit.Rect(n, p);
        static RectTransform At(string n, Transform p, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) => UIKit.Box(n, p, anchor, pivot, pos, size);
        static void Stretch(RectTransform r, float l, float b, float rt, float t) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t); }
        static void TopStrip(RectTransform r, float y, float h, float l = 0, float rt = 0) { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(.5f, 1); r.offsetMin = new Vector2(l, -y - h); r.offsetMax = new Vector2(-rt, -y); }
        static Image Fill(RectTransform r, Color c, Sprite s = null) { var i = r.gameObject.AddComponent<Image>(); i.sprite = s ?? UIKit.CardSprite; i.type = Image.Type.Sliced; i.color = c; return i; }
        static Text T(Transform p, string s, int size, Color c, TextAnchor a, Font f = null) { var t = UIKit.Label(p, s, size, c, a, f ?? UIKit.Mono); t.horizontalOverflow = HorizontalWrapMode.Overflow; return t; }

        /// <summary>A button: shape (pill/card), background, label colour. Haptic tick on press.</summary>
        static Button Btn(Transform p, string label, Color bg, Color fg, int size, System.Action act, Font f = null, bool card = false)
        {
            var r = R("Btn " + label, p); var img = Fill(r, bg, card ? UIKit.CardSprite : UIKit.PillSprite);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = Color.white; cb.selectedColor = Color.white; cb.pressedColor = new Color(.82f, .82f, .82f); cb.fadeDuration = .05f; b.colors = cb;
            b.onClick.AddListener(() => { Haptics.Play(Haptics.Kind.Selection); game_.sound?.Sfx("tick"); act?.Invoke(); });
            if (label.Length > 0) { var t = T(r, label, size, fg, TextAnchor.MiddleCenter, f ?? UIKit.MonoBold); t.name = "Label"; }
            return b;
        }
        static Game game_;

        public void Build()
        {
            game_ = game;
            UIKit.MakeCanvas(transform, out safe);
            portrait = Screen.height >= Screen.width;
            BuildHud(); BuildPause(); BuildEnd(); BuildMenu(); BuildLoading();
            built = true; ShowMenu();
        }

        // ================================================================ menu
        void BuildMenu()
        {
            bool wasActive = menu == null || menu.gameObject.activeSelf;
            if (menu != null) Destroy(menu.gameObject);
            menu = R("Menu", safe); menu.SetSiblingIndex(1);

            // brand, top-left; a quiet status line; the record dot top-right
            UIKit.Matrix(menu, "VOID STUNTS", 17, portrait ? Ink : Ink, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -22));
            var sub = T(menu, "VS-02  ·  PROTOTYPE  ·  V" + Application.version, 9, Ink60, TextAnchor.UpperLeft); var sr = sub.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0, 1); sr.pivot = new Vector2(0, 1); sr.sizeDelta = new Vector2(300, 14); sr.anchoredPosition = new Vector2(23, -46);
            UIKit.Dot(menu, new Vector2(1, 1), new Vector2(-24, -30), 7, Red);

            // the card
            var card = R("Card", menu); Fill(card, PaperC);
            if (portrait) { card.anchorMin = new Vector2(0, 0); card.anchorMax = new Vector2(1, 0); card.pivot = new Vector2(.5f, 0); card.offsetMin = new Vector2(10, 10); card.offsetMax = new Vector2(-10, 10 + 292); }
            else { card.anchorMin = new Vector2(1, 0); card.anchorMax = new Vector2(1, 1); card.pivot = new Vector2(1, .5f); card.offsetMin = new Vector2(-332, 10); card.offsetMax = new Vector2(-10, -10); }

            float y = 18;
            var lab = T(card, "TRACK", 9, Ink60, TextAnchor.UpperLeft, UIKit.MonoBold); TopStrip(lab.rectTransform, y, 12, 18, 18); y += 20;

            // level tiles: a row of three in portrait, a stack in landscape
            var L = game.Levels; int n = L.Length;
            float tileH = portrait ? 86 : 58, gap = 6;
            for (int i = 0; i < n; i++)
            {
                int k = i; bool on = Settings.level == i;
                var b = Btn(card, "", on ? Ink : Field, Ink, 10, () => { if (Settings.level != k) game.SelectLevel(k); }, null, true);
                var r = (RectTransform)b.transform;
                if (portrait) { r.anchorMin = new Vector2((float)i / n, 1); r.anchorMax = new Vector2((float)(i + 1) / n, 1); r.pivot = new Vector2(.5f, 1); r.offsetMin = new Vector2(i == 0 ? 16 : gap / 2, -y - tileH); r.offsetMax = new Vector2(i == n - 1 ? -16 : -gap / 2, -y); }
                else TopStrip(r, y + i * (tileH + gap), tileH, 16, 16);
                Color fg = on ? White : Ink, fg2 = on ? White70 : Ink60;
                var nm = T(r, L[i].Name, portrait ? 12 : 13, fg, TextAnchor.UpperLeft, UIKit.Wide); TopStrip(nm.rectTransform, 11, 16, 12, 8);
                var bl = T(r, L[i].Blurb, 8, fg2, TextAnchor.UpperLeft); bl.horizontalOverflow = HorizontalWrapMode.Wrap; TopStrip(bl.rectTransform, portrait ? 32 : 31, portrait ? 24 : 12, 12, 8);
                int best = Settings.Best(L[i].Id);
                var be = T(r, best > 0 ? "BEST " + best.ToString("N0") : "—", 8, fg2, portrait ? TextAnchor.LowerLeft : TextAnchor.UpperRight, UIKit.MonoBold);
                if (portrait) { var br = be.rectTransform; br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(0, 0); br.offsetMin = new Vector2(12, 9); br.offsetMax = new Vector2(-8, 21); }
                else TopStrip(be.rectTransform, 12, 12, 12, 12);
                if (on) UIKit.Dot(r, new Vector2(1, 1), portrait ? new Vector2(-12, -14) : new Vector2(-14, -38), 6, Volt);
            }
            y += portrait ? tileH + 14 : n * (tileH + gap) + 8;

            // mode: a segmented control
            var seg = R("Mode", card); Fill(seg, Field, UIKit.PillSprite); TopStrip(seg, y, 36, 16, 16);
            string[] modes = { "RUN · 3:00", "FREE ROAM" };
            for (int i = 0; i < 2; i++)
            {
                bool on = (i == 1) == free; int k = i;
                var b = Btn(seg, modes[i], on ? White : new Color(0, 0, 0, 0), on ? Ink : Ink60, 10, () => { free = k == 1; BuildMenu(); });
                var r = (RectTransform)b.transform; r.anchorMin = new Vector2(i * .5f, 0); r.anchorMax = new Vector2((i + 1) * .5f, 1); r.offsetMin = new Vector2(i == 0 ? 3 : 1.5f, 3); r.offsetMax = new Vector2(i == 0 ? -1.5f : -3, -3);
            }
            y += 46;

            // the one primary action
            var go = Btn(card, "DRIVE", Ink, White, 14, () => game.StartRun(free), UIKit.Wide); TopStrip((RectTransform)go.transform, y, 54, 16, 16);
            UIKit.Dot(go.transform, new Vector2(0, .5f), new Vector2(26, 0), 7, Volt);
            UIKit.Label(go.transform, "→", 16, White, TextAnchor.MiddleRight, UIKit.MonoBold).rectTransform.offsetMax = new Vector2(-22, 0);
            y += 64;

            // secondary: four quiet text buttons
            string[] links = { "GARAGE", "LOOKS", "ATMOS", "SETTINGS" };
            var row = R("Links", card);
            if (portrait) TopStrip(row, y, 30, 10, 10); else { row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, 0); row.pivot = new Vector2(.5f, 0); row.offsetMin = new Vector2(10, 10); row.offsetMax = new Vector2(-10, 40); }
            for (int i = 0; i < 4; i++)
            {
                int k = i; var b = Btn(row, links[i], new Color(0, 0, 0, 0), Ink, 9, () => OpenSheet(k));
                var r = (RectTransform)b.transform; r.anchorMin = new Vector2(i / 4f, 0); r.anchorMax = new Vector2((i + 1) / 4f, 1); r.offsetMin = r.offsetMax = Vector2.zero;
                if (i > 0) { var d = UIKit.Dot(row, new Vector2(i / 4f, .5f), Vector2.zero, 3, Ink35); }
            }
            if (!wasActive) menu.gameObject.SetActive(false);
        }
        public void RefreshMenu() { if (built) BuildMenu(); }

        // ================================================================ sheets
        void OpenSheet(int k) { sheetKind = k; game.cam.sheetOpen = true; BuildSheet(); }
        void CloseSheet() { sheetKind = -1; if (sheet) Destroy(sheet.gameObject); sheet = null; game.cam.sheetOpen = false; Settings.Save(); BuildMenu(); }

        void BuildSheet()
        {
            if (sheet) Destroy(sheet.gameObject);
            if (sheetKind < 0) return;
            var items = new List<(string label, string tag, bool on, System.Action act)>(); string head = ""; string hint = "";
            if (sheetKind == 0)
            {
                head = "GARAGE";
                for (int i = 0; i < CarVisual.LIVERIES.Length; i++) { int k = i; items.Add((CarVisual.LIVERIES[i].name, "LIVERY", Settings.livery == i, () => { Settings.livery = k; game.visual.ApplyLivery(k); })); }
                for (int i = 0; i < CarVisual.PLUMES.Length; i++) { int k = i; items.Add((CarVisual.PLUMES[i].name, "PLUME", Settings.plume == i, () => { Settings.plume = k; game.visual.ApplyPlume(k); game.PreviewBoost(); })); }
            }
            else if (sheetKind == 1)
            {
                head = "LOOKS"; hint = "TAP TO USE  ·  TAP AGAIN TO STAR  ·  STARRED LOOKS JOIN THE MOMENTUM LADDER";
                for (int i = 0; i < Looks.NAMES.Length; i++)
                {
                    int k = i; bool fav = System.Array.IndexOf(Settings.favs, i) >= 0;
                    items.Add((Looks.NAMES[i], (fav ? "STARRED  ·  " : "") + Looks.SUBS[i], Settings.look == i, () =>
                    {
                        if (Settings.look == k) { var l = new List<int>(Settings.favs); if (l.Contains(k)) l.Remove(k); else l.Add(k); Settings.favs = l.ToArray(); game.looks.favs = l; }
                        else { Settings.look = k; game.looks.SetBase(k); }
                    }));
                }
                items.Add(("AUTO LADDER", Settings.autoLooks ? "ON" : "OFF", Settings.autoLooks, () => { Settings.autoLooks = !Settings.autoLooks; game.looks.auto = Settings.autoLooks; }));
            }
            else if (sheetKind == 2)
            {
                head = "ATMOSPHERE";
                for (int i = 0; i < Atmos.PRESETS.Length; i++) { int k = i; items.Add((Atmos.PRESETS[i].name, "", Settings.atmos == i, () => { Settings.atmos = k; game.atmos.Apply(k); })); }
            }
            else
            {
                head = "SETTINGS";
                string[] qd = { "SMOOTHEST", "DEFAULT", "SHARPEST" };
                for (int i = 0; i < 3; i++) { int k = i; items.Add((Settings.QUALITY[i], qd[i], Settings.quality == i, () => { Settings.quality = k; Settings.ApplyQuality(game.veg); })); }
                items.Add(("120 HZ", Settings.hz120 ? "ON" : "OFF", Settings.hz120, () => { Settings.hz120 = !Settings.hz120; Settings.ApplyQuality(game.veg); }));
                items.Add(("SOUND", Settings.sound ? "ON" : "OFF", Settings.sound, () => { Settings.sound = !Settings.sound; game.sound.muted = !Settings.sound; }));
                items.Add(("HAPTICS", Settings.haptics ? "ON" : "OFF", Settings.haptics, () => { Settings.haptics = !Settings.haptics; Haptics.enabled = Settings.haptics; }));
                items.Add(("CONTROL HINTS", Settings.coach ? "ON" : "OFF", Settings.coach, () => { Settings.coach = !Settings.coach; }));
            }

            sheet = R("Sheet", safe);
            var dim = R("Dim", sheet); dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .18f); dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseSheet);
            var card = R("Card", sheet); Fill(card, PaperC);
            int cols = !portrait && items.Count > 7 ? 2 : 1; float rowH = 38; int rows = Mathf.CeilToInt(items.Count / (float)cols);
            float hintH = hint.Length > 0 ? 30 : 0, h = 62 + rows * rowH + hintH + 14;
            if (portrait) { card.anchorMin = new Vector2(0, 0); card.anchorMax = new Vector2(1, 0); card.pivot = new Vector2(.5f, 0); card.offsetMin = new Vector2(10, 10); card.offsetMax = new Vector2(-10, 10 + h); }
            else { float w = cols == 2 ? 470 : 340; card.anchorMin = new Vector2(1, 0); card.anchorMax = new Vector2(1, 1); card.pivot = new Vector2(1, .5f); card.offsetMin = new Vector2(-10 - w, 10); card.offsetMax = new Vector2(-10, -10); }

            var title = T(card, head, 14, Ink, TextAnchor.MiddleLeft, UIKit.Wide); TopStrip(title.rectTransform, 18, 26, 20, 100);
            var done = Btn(card, "DONE", Ink, White, 10, CloseSheet); var dr = (RectTransform)done.transform; dr.anchorMin = dr.anchorMax = new Vector2(1, 1); dr.pivot = new Vector2(1, 1); dr.sizeDelta = new Vector2(76, 32); dr.anchoredPosition = new Vector2(-14, -15);

            for (int i = 0; i < items.Count; i++)
            {
                var (label, tag, on, act) = items[i]; int c = i / rows, rI = i % rows;
                var b = Btn(card, "", new Color(0, 0, 0, 0), Ink, 10, () => { act(); BuildSheet(); });
                var r = (RectTransform)b.transform; r.anchorMin = new Vector2(c / (float)cols, 1); r.anchorMax = new Vector2((c + 1) / (float)cols, 1); r.pivot = new Vector2(.5f, 1);
                r.offsetMin = new Vector2(c == 0 ? 14 : 6, -62 - (rI + 1) * rowH); r.offsetMax = new Vector2(c == cols - 1 ? -14 : -6, -62 - rI * rowH);
                var lt = T(r, label, 11, Ink, TextAnchor.MiddleLeft, UIKit.MonoBold); Stretch(lt.rectTransform, 28, 0, 8, 0);
                if (tag.Length > 0) { var tt = T(r, tag, 8, Ink60, TextAnchor.MiddleRight); Stretch(tt.rectTransform, 120, 0, 8, 0); }
                var d = UIKit.Dot(r, new Vector2(0, .5f), new Vector2(12, 0), 9, on ? Ink : Ink35, !on);
                if (on) UIKit.Dot(r, new Vector2(0, .5f), new Vector2(12, 0), 4, Volt);
                if (rI < rows - 1) { var line = R("Rule", r); line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0); line.offsetMin = new Vector2(4, 0); line.offsetMax = new Vector2(-4, 1); line.gameObject.AddComponent<Image>().color = Ink10; }
            }
            if (hintH > 0) { var ht = T(card, hint, 8, Ink60, TextAnchor.UpperLeft); ht.horizontalOverflow = HorizontalWrapMode.Wrap; TopStrip(ht.rectTransform, 62 + rows * rowH + 10, hintH, 20, 20); }
            sheet.SetAsLastSibling(); loading.SetAsLastSibling();
        }

        // ================================================================ HUD
        void BuildHud()
        {
            hud = R("Hud", safe);
            // left: score, combo, speed
            score = T(hud, "0", 24, White, TextAnchor.UpperLeft, UIKit.Wide); Corner(score.rectTransform, new Vector2(0, 1), new Vector2(18, -14), new Vector2(240, 30));
            Shade(score);
            mult = T(hud, "", 10, Ink, TextAnchor.MiddleCenter, UIKit.MonoBold); var mb = mult.transform.parent;
            var multBox = At("Combo", hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -48), new Vector2(46, 18)); Fill(multBox, Volt, UIKit.PillSprite);
            mult.transform.SetParent(multBox, false); Stretch(mult.rectTransform, 0, 0, 0, 0);
            speed = T(hud, "0 KM/H", 9, White70, TextAnchor.UpperLeft, UIKit.MonoBold); Corner(speed.rectTransform, new Vector2(0, 1), new Vector2(19, -70), new Vector2(160, 14)); Shade(speed);

            // centre: timer capsule with the boost-fuel line under it
            var tb = At("Timer", hud, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(72, 26)); Fill(tb, GlassC, UIKit.PillSprite);
            timer = T(tb, "3:00", 12, White, TextAnchor.MiddleCenter, UIKit.MonoBold); Stretch(timer.rectTransform, 0, 0, 0, 0);
            var fb = At("Fuel", hud, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -46), new Vector2(96, 3)); var fbi = Fill(fb, new Color(1, 1, 1, .22f), UIKit.PillSprite); fbi.raycastTarget = false;
            fuelFill = R("Fill", fb); var ffi = Fill(fuelFill, Volt, UIKit.PillSprite); ffi.raycastTarget = false;

            // right: pause
            var pb = Btn(hud, "", GlassC, White, 10, () => game.Pause(true)); var pr = (RectTransform)pb.transform; pr.anchorMin = pr.anchorMax = new Vector2(1, 1); pr.pivot = new Vector2(1, 1); pr.sizeDelta = new Vector2(38, 38); pr.anchoredPosition = new Vector2(-14, -10);
            for (int i = 0; i < 2; i++) { var bar = At("Bar", pb.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(i == 0 ? -4 : 4, 0), new Vector2(3, 12)); Fill(bar, White, UIKit.PillSprite).raycastTarget = false; }

            // scoring popup (moves with orientation), toast, count-in, coach
            bank = R("Bank", hud); bankBg = Fill(bank, GlassC); bankBg.raycastTarget = false; bankG = bank.gameObject.AddComponent<CanvasGroup>(); bankG.blocksRaycasts = false;
            bankPts = T(bank, "", 20, White, TextAnchor.UpperCenter, UIKit.Wide); TopStrip(bankPts.rectTransform, 10, 26, 10, 10);
            bankWhat = T(bank, "", 9, White70, TextAnchor.UpperCenter, UIKit.MonoBold); bankWhat.horizontalOverflow = HorizontalWrapMode.Wrap; TopStrip(bankWhat.rectTransform, 40, 26, 12, 12);
            bank.gameObject.SetActive(false);
            toastBox = At("Toast", hud, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -60), new Vector2(300, 26)); Fill(toastBox, GlassC, UIKit.PillSprite).raycastTarget = false; toastG = toastBox.gameObject.AddComponent<CanvasGroup>(); toastG.blocksRaycasts = false;
            toast = T(toastBox, "", 9, White, TextAnchor.MiddleCenter, UIKit.MonoBold); Stretch(toast.rectTransform, 12, 0, 12, 0); toastBox.gameObject.SetActive(false);
            countBox = At("Count", hud, new Vector2(.5f, .64f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 90)); countG = countBox.gameObject.AddComponent<CanvasGroup>(); countG.blocksRaycasts = false;
            countT = T(countBox, "", 54, White, TextAnchor.MiddleCenter, UIKit.Wide); Stretch(countT.rectTransform, 0, 0, 0, 0); Shade(countT); countBox.gameObject.SetActive(false);
            coachBox = R("Coach", hud); Fill(coachBox, GlassC, UIKit.PillSprite).raycastTarget = false; coachG = coachBox.gameObject.AddComponent<CanvasGroup>(); coachG.blocksRaycasts = false;
            coachT = T(coachBox, "", 9, White, TextAnchor.MiddleCenter, UIKit.MonoBold); Stretch(coachT.rectTransform, 14, 0, 14, 0); coachBox.gameObject.SetActive(false);
            for (int i = 0; i < 4; i++) { var d = UIKit.Dot(hud, Vector2.zero, Vector2.zero, i % 2 == 0 ? 58 : 16, new Color(1, 1, 1, i % 2 == 0 ? .5f : .7f), i % 2 == 0); thumbDots.Add(d); d.enabled = false; }
            PlaceBank();
            hud.gameObject.SetActive(false);
        }
        static void Corner(RectTransform r, Vector2 a, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = a; r.pivot = a; r.sizeDelta = size; r.anchoredPosition = pos; }
        static void Shade(Text t) { var s = t.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .35f); s.effectDistance = new Vector2(0, -1); }
        void PlaceBank()
        {
            if (portrait) { bank.anchorMin = bank.anchorMax = new Vector2(.5f, .78f); bank.pivot = new Vector2(.5f, .5f); bank.sizeDelta = new Vector2(280, 72); bank.anchoredPosition = Vector2.zero; }
            else { bank.anchorMin = bank.anchorMax = new Vector2(1, 1); bank.pivot = new Vector2(1, 1); bank.sizeDelta = new Vector2(260, 72); bank.anchoredPosition = new Vector2(-14, -58); }
        }

        public void Bank(string pts, string what, bool bail)
        {
            bankPts.text = pts; bankWhat.text = what; bankPts.color = bail ? Red : White;
            bankT = 2.1f; bank.gameObject.SetActive(true);
        }
        public void Toast(string t) { toast.text = t; toastT = 2.6f; toastBox.sizeDelta = new Vector2(Mathf.Clamp(t.Length * 6.2f + 32, 120, 380), 26); toastBox.gameObject.SetActive(true); }
        public void Count(string s) { countS = s; countT.text = s; countT0 = Time.unscaledTime; countBox.gameObject.SetActive(s.Length > 0); }
        public void Coach(string s)
        {
            if (!Settings.coach || string.IsNullOrEmpty(s)) { coachBox.gameObject.SetActive(false); return; }
            if (coachT.text != s) coachT.text = s;
            float w = Mathf.Clamp(s.Length * 5.9f + 34, 140, 360);
            Vector2 a = s.StartsWith("LEFT") ? new Vector2(0, 0) : s.StartsWith("RIGHT") ? new Vector2(1, 0) : new Vector2(.5f, 0);
            coachBox.anchorMin = coachBox.anchorMax = a; coachBox.pivot = a; coachBox.sizeDelta = new Vector2(w, 26); coachBox.anchoredPosition = new Vector2(a.x == 0 ? 16 : a.x == 1 ? -16 : 0, 18);
            coachBox.gameObject.SetActive(true);
        }

        // ================================================================ pause / end / loading
        RectTransform Modal(string name, float w, float h, out RectTransform card)
        {
            var m = R(name, safe); var dim = R("Dim", m); dim.gameObject.AddComponent<Image>().color = new Color(.04f, .04f, .045f, .4f);
            card = At("Card", m, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(w, h)); Fill(card, PaperC);
            return m;
        }
        static Button Row(RectTransform card, string label, bool primary, float y, float h, System.Action a, float l = 16, float r = 16)
        {
            var b = Btn(card, label, primary ? Ink : Field, primary ? White : Ink, primary ? 12 : 10, a, primary ? UIKit.Wide : null);
            var rt = (RectTransform)b.transform; rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0); rt.offsetMin = new Vector2(l, y); rt.offsetMax = new Vector2(-r, y + h); return b;
        }
        void BuildPause()
        {
            pause = Modal("Pause", 312, 360, out var c);
            var t = T(c, "PAUSED", 16, Ink, TextAnchor.UpperLeft, UIKit.Wide); TopStrip(t.rectTransform, 20, 22, 20, 20);
            UIKit.Dot(c, new Vector2(1, 1), new Vector2(-24, -30), 7, Red);
            var g = T(c, "GOALS", 9, Ink60, TextAnchor.UpperLeft, UIKit.MonoBold); TopStrip(g.rectTransform, 54, 12, 20, 20);
            pauseGoals = T(c, "", 9, Ink, TextAnchor.UpperLeft); pauseGoals.lineSpacing = 1.25f; TopStrip(pauseGoals.rectTransform, 72, 140, 20, 20);
            Row(c, "RESUME", true, 104, 48, () => game.Pause(false));
            var a = Row(c, "RESTART", false, 58, 38, () => { game.Pause(false); game.StartRun(free); }, 16, 160);
            Row(c, "MENU", false, 58, 38, () => { game.Pause(false); game.ToMenu(); }, 160, 16);
            Row(c, "RESET CAR", false, 14, 38, () => { game.Pause(false); game.ResetCar(); });
            pause.gameObject.SetActive(false);
        }
        void BuildEnd()
        {
            end = Modal("End", 320, 440, out var c);
            var t = T(c, "RUN COMPLETE", 9, Ink60, TextAnchor.UpperLeft, UIKit.MonoBold); TopStrip(t.rectTransform, 20, 12, 20, 20);
            endScore = T(c, "", 34, Ink, TextAnchor.UpperLeft, UIKit.Wide); TopStrip(endScore.rectTransform, 38, 42, 18, 18);
            endBest = T(c, "", 9, Ink, TextAnchor.UpperLeft, UIKit.MonoBold); TopStrip(endBest.rectTransform, 86, 14, 20, 20);
            var rule = R("Rule", c); TopStrip(rule, 110, 1, 20, 20); rule.gameObject.AddComponent<Image>().color = Ink10;
            endStats = T(c, "", 9, Ink, TextAnchor.UpperLeft); endStats.lineSpacing = 1.3f; TopStrip(endStats.rectTransform, 122, 70, 20, 20);
            var rule2 = R("Rule", c); TopStrip(rule2, 200, 1, 20, 20); rule2.gameObject.AddComponent<Image>().color = Ink10;
            endGoals = T(c, "", 9, Ink, TextAnchor.UpperLeft); endGoals.lineSpacing = 1.25f; TopStrip(endGoals.rectTransform, 212, 140, 20, 20);
            Row(c, "AGAIN", true, 14, 48, () => game.StartRun(false), 16, 128);
            Row(c, "MENU", false, 14, 48, () => game.ToMenu(), 200, 16);
            end.gameObject.SetActive(false);
        }
        void BuildLoading()
        {
            loading = R("Loading", transform.GetChild(0)); loading.gameObject.AddComponent<Image>().color = new Color(.949f, .945f, .929f, 1);
            UIKit.Matrix(loading, "VOID STUNTS", 20, Ink, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 26));
            loadingT = T(loading, "", 9, Ink60, TextAnchor.MiddleCenter, UIKit.MonoBold); var lr = loadingT.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(.5f, .5f); lr.sizeDelta = new Vector2(400, 16); lr.anchoredPosition = new Vector2(0, -12);
            loadingDot = UIKit.Dot(loading, new Vector2(.5f, .5f), new Vector2(0, -40), 7, Red);
            loading.gameObject.SetActive(false);
        }
        public void Loading(bool on, string text = "") { loading.gameObject.SetActive(on); loadingT.text = text; loading.SetAsLastSibling(); }

        string GoalList(Scoring s)
        {
            var sb = new System.Text.StringBuilder(); var L = game.Levels[Settings.level];
            foreach (var g in L.Goals) sb.Append(s != null && s.goals.Contains(g.id) ? "[×] " : "[ ] ").Append(g.text).Append('\n');
            return sb.ToString();
        }
        public void ShowPause(bool on, Scoring s) { pause.gameObject.SetActive(on); if (on) { pauseGoals.text = GoalList(s); pause.SetAsLastSibling(); } }
        public void ShowEnd(Scoring s, int best, bool record)
        {
            end.gameObject.SetActive(true); end.SetAsLastSibling(); hud.gameObject.SetActive(false);
            endScore.text = s.score.ToString("N0"); endBest.text = record ? "NEW BEST" : "BEST  " + best.ToString("N0"); endBest.color = record ? Red : Ink;
            endStats.text = $"BEST LANDING    {s.statBestLand:N0}\nLONGEST AIR     {s.statLongAir:0.0} S\nROTATIONS       {s.statFlips}\nRINGS           {s.statRings}";
            endGoals.text = GoalList(s);
        }
        public void ShowMenu()
        {
            hud.gameObject.SetActive(false); end.gameObject.SetActive(false); pause.gameObject.SetActive(false);
            if (sheet) { Destroy(sheet.gameObject); sheet = null; sheetKind = -1; }
            BuildMenu(); menu.gameObject.SetActive(true);
        }
        public void ShowHud(bool timed)
        {
            menu.gameObject.SetActive(false); if (sheet) { Destroy(sheet.gameObject); sheet = null; sheetKind = -1; } game.cam.sheetOpen = false;
            end.gameObject.SetActive(false); hud.gameObject.SetActive(true); timer.transform.parent.gameObject.SetActive(timed);
            bank.gameObject.SetActive(false); toastBox.gameObject.SetActive(false); coachBox.gameObject.SetActive(false); countBox.gameObject.SetActive(false);
        }

        // ================================================================ per frame
        void Update()
        {
            if (!built) return;
            bool p = Screen.height >= Screen.width;
            if (p != portrait) { portrait = p; PlaceBank(); if (menu.gameObject.activeSelf) BuildMenu(); if (sheet) BuildSheet(); }
            float dt = Time.unscaledDeltaTime;
            if (bankT > 0) { bankT -= dt; bankG.alpha = Mathf.Clamp01(bankT / .35f); float s = 1 + Mathf.Max(0, bankT - 1.95f) * .5f; bank.localScale = Vector3.one * s; if (bankT <= 0) bank.gameObject.SetActive(false); }
            if (toastT > 0) { toastT -= dt; toastG.alpha = Mathf.Clamp01(toastT / .35f); if (toastT <= 0) toastBox.gameObject.SetActive(false); }
            if (countT0 >= 0)
            {
                // each number pops in and settles; "GO" holds briefly then fades away
                float a = Time.unscaledTime - countT0; float life = countS == "GO" ? .9f : 1f;
                countBox.localScale = Vector3.one * (1 + .22f * Mathf.Exp(-a * 9));
                countG.alpha = countS == "GO" ? Mathf.Clamp01((life - a) / .35f) : 1;
                if (a > life) { countBox.gameObject.SetActive(false); countT0 = -1; }
            }
            if (!hud.gameObject.activeSelf || game.score == null) { foreach (var d in thumbDots) d.enabled = false; return; }
            var sc = game.score; score.text = sc.score.ToString("N0");
            mult.transform.parent.gameObject.SetActive(sc.mult > 1); if (sc.mult > 1) mult.text = "×" + sc.mult;
            int tt = Mathf.CeilToInt(Mathf.Max(0, sc.time)); timer.text = (tt / 60) + ":" + (tt % 60).ToString("00"); timer.color = sc.time < 10 && !free ? Red : White;
            fuelFill.anchorMin = Vector2.zero; fuelFill.anchorMax = new Vector2(Mathf.Clamp01(game.car.fuel), 1); fuelFill.offsetMin = fuelFill.offsetMax = Vector2.zero;
            fuelFill.GetComponent<Image>().color = game.car.boosting ? White : Volt;
            speed.text = Mathf.RoundToInt(game.car.rb.linearVelocity.magnitude * 3.6f) + " KM/H";
            int i = 0;
            foreach (var t in game.input.thumbs.Values) { if (i > 2) break; Thumb(thumbDots[i], t.p0); Thumb(thumbDots[i + 1], t.p); i += 2; }
            for (; i < 4; i++) thumbDots[i].enabled = false;
        }
        void Thumb(Image d, Vector2 pt)
        {
            // touch points arrive in input points; convert to pixels, then to this canvas (origin = safe-area corner)
            var px = pt / TouchInput.PointsPerPixel; var o = Screen.safeArea.min;
            d.enabled = true; d.rectTransform.anchoredPosition = (px - o) / UIScale.Factor;
        }
    }
}
