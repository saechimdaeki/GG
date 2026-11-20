using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastTrain
{
    /// <summary>
    /// Screen UI built in code (uGUI): clock, health and hunger, the weapon hotbar and bag,
    /// interaction prompt with a search progress bar, stealth status, barricade bar, arrows to
    /// the shelter and the station, center messages, sleep fade and the end panel.
    /// Korean text uses the OS font (Apple SD Gothic Neo on mac).
    /// </summary>
    public partial class Hud : MonoBehaviour
    {
        public static Font Font;
        static readonly Color Cream = new(0.95f, 0.91f, 0.84f);
        static readonly Color Ink = new(0.035f, 0.045f, 0.05f, 0.82f);
        static readonly Color Accent = new(1f, 0.62f, 0.24f);
        static Sprite _round;
        Image _hpChip;
        float _hpChipHold;

        Text _clock, _phaseIcon, _hpText, _hungerText, _sprintText, _bag, _msg, _prompt, _status, _evacText, _endText, _barText;
        Image _hp, _hunger, _sprint, _statusPanel, _evac, _hurt, _search, _barricade, _sleep;
        GameObject _evacRoot, _endRoot, _searchRoot, _barRoot;
        Marker _station, _shelter, _encounterMarker, _coveredPlayer;
        GameObject _bagPanel;
        Text _bagContents;
        readonly List<(Image box, Text name, Text info, Image wear)> _slots = new();
        float _msgT;

        class Marker { public RectTransform arrow; public Text label; }

        public void Build()
        {
            Font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleSDGothicNeo-Bold", "Malgun Gothic", "Noto Sans CJK KR", "Arial Unicode MS" }, 32);
            var canvasGo = new GameObject("HUD");
            canvasGo.transform.SetParent(transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;
            _root = root;

            _hurt = Img(root, "hurt", new Color(0.6f, 0f, 0f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _sleep = Img(root, "sleep", new Color(0, 0, 0.02f, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Vector2 topLeft = Vector2.up;
            var clockBox = Img(root, "Clock", Ink, topLeft, topLeft, new Vector2(28, -28), new Vector2(318, 66), pivot: topLeft);
            _phaseIcon = Txt(clockBox.transform, "☀", 31, TextAnchor.MiddleCenter, Vector2.zero, Vector2.up,
                new Vector2(29, 0), new Vector2(48, 0));
            _clock = Txt(clockBox.transform, "", 27, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                new Vector2(23, 0), new Vector2(-80, 0));

            _statusPanel = Img(root, "Threat", Ink, Vector2.one, Vector2.one, new Vector2(-28, -28),
                new Vector2(416, 54), pivot: Vector2.one);
            _status = Txt(_statusPanel.transform, "", 24, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-32, 0));

            var vitals = Img(root, "Vitals", Ink, Vector2.zero, Vector2.zero, new Vector2(28, 28),
                new Vector2(362, 134), pivot: Vector2.zero).transform;
            (_hpText, _hp, _hpChip) = Vital(vitals, "Health", 101, new Color(0.92f, 0.32f, 0.28f), chip: true);
            (_sprintText, _sprint, _) = Vital(vitals, "Sprint", 61, new Color(0.43f, 0.79f, 0.92f));
            (_hungerText, _hunger, _) = Vital(vitals, "Hunger", 21, new Color(0.92f, 0.73f, 0.37f));

            // bottom-center: weapon hotbar
            for (int k = 0; k < 4; k++)
            {
                var box = Img(root, "slot" + k, Ink, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((k - 1.5f) * 190, 40), new Vector2(178, 96), pivot: new Vector2(0.5f, 0));
                Txt(box.transform, (k + 1).ToString(), 20, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(10, -6), new Vector2(-20, -12)).color = new Color(1, 1, 1, 0.4f);
                var name = Txt(box.transform, "", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0, 8), new Vector2(-10, -30));
                var info = Txt(box.transform, "", 20, TextAnchor.LowerCenter, Vector2.zero, Vector2.one, new Vector2(0, 14), new Vector2(-10, -20));
                var wearBack = Img(box.transform, "wearBack", new Color(0, 0, 0, 0.6f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(-24, 6), pivot: new Vector2(0.5f, 0));
                var wear = Fill(wearBack.transform, new Color(0.6f, 0.9f, 0.5f), inset: 0f);
                _slots.Add((box, name, info, wear));
            }
            // bottom-right: bag
            _bag = Txt(root, "", 24, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 36), new Vector2(460, 200), pivot: new Vector2(1, 0));
            _bag.lineSpacing = 1.15f;
            _bagPanel = Img(root, "Backpack", Ink, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(475, 394)).gameObject;
            _bagContents = Txt(_bagPanel.transform, "", 25, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-52, -34));
            _bagContents.lineSpacing = 1.27f;
            _bagPanel.SetActive(false);

            // prompt + search bar above the hotbar
            Vector2 bottom = new(0.5f, 0);
            _promptPanel = Img(root, "Interaction", Ink, bottom, bottom, new Vector2(0, 168), new Vector2(760, 58)).gameObject;
            _prompt = Txt(_promptPanel.transform, "", 25, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-36, -4));
            _searchRoot = Img(root, "searchBack", Ink, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 205), new Vector2(360, 16)).gameObject;
            _search = Fill(_searchRoot.transform, new Color(0.95f, 0.85f, 0.5f));

            // barricade bar, top-center
            _barRoot = new GameObject("barricade", typeof(RectTransform));
            _barRoot.transform.SetParent(root, false);
            Stretch(_barRoot);
            var barBack = Img(_barRoot.transform, "barBack", Ink, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(560, 20));
            _barricade = Fill(barBack.transform, new Color(0.75f, 0.55f, 0.3f));
            _barText = Txt(_barRoot.transform, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -74), new Vector2(560, 34));

            Vector2 top = new(0.5f, 1);
            _messagePanel = Img(root, "Notification", Ink, top, top, new Vector2(0, -134), new Vector2(820, 60)).gameObject;
            _msg = Txt(_messagePanel.transform, "", 25, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-38, -8));

            Vector2 corner = Vector2.one;
            var objectivePanel = Img(root, "Objectives", Ink, corner, corner, new Vector2(-28, -92), new Vector2(416, 82), pivot: corner);
            _objectives = Txt(objectivePanel.transform, "", 21, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-36, -10));
            _eventPanel = Img(root, "Street event", Ink, corner, corner, new Vector2(-28, -184), new Vector2(416, 52), pivot: corner).gameObject;
            Img(_eventPanel.transform, "Signal", new Color(0.67f, 0.81f, 0.72f), Vector2.zero, Vector2.up,
                Vector2.zero, new Vector2(4, 0), pivot: new Vector2(0, 0.5f));
            _streetEvent = Txt(_eventPanel.transform, "", 20, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-36, -8));
            _objectives.lineSpacing = 1.2f;
            _tradeRoot = Img(root, "trade", new Color(0.06f, 0.05f, 0.06f, 0.94f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(760, 380)).gameObject;
            _tradeText = Txt(_tradeRoot.transform, "", 30, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-80, -60));
            _tradeText.lineSpacing = 1.35f;
            _tradeRoot.SetActive(false);
            _dropMark = MakeMarker(root, new Color(1f, 0.3f, 0.25f));
            _coveredPlayer = MakeMarker(root, new Color(1f, 0.67f, 0.33f));
            _station = MakeMarker(root, new Color(0.35f, 1f, 0.55f));
            _shelter = MakeMarker(root, new Color(1f, 0.75f, 0.4f));
            _encounterMarker = MakeMarker(root, new Color(0.55f, 0.9f, 0.76f));

            _evacRoot = new GameObject("evac", typeof(RectTransform));
            _evacRoot.transform.SetParent(root, false);
            Stretch(_evacRoot);
            var evacBack = Img(_evacRoot.transform, "evacBack", Ink, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 290), new Vector2(700, 26));
            _evac = Fill(evacBack.transform, new Color(0.3f, 1f, 0.5f));
            _evacText = Txt(_evacRoot.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(900, 40));

            _endRoot = Img(root, "end", new Color(0, 0, 0, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            _endText = Txt(_endRoot.transform, "", 46, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _endRoot.SetActive(false);
        }

        Marker MakeMarker(Transform root, Color c)
        {
            var go = new GameObject("marker", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(70, 70);
            var tri = Txt(rt, "▲", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            tri.color = c;
            var label = Txt(root, "", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 30));
            label.color = c;
            return new Marker { arrow = rt, label = label };
        }

        public void ShowEnd(string text)
        {
            _endRoot.SetActive(true);
            _endText.text = text;
        }

        public void Say(string text)
        {
            _msg.text = text;
            _msgT = 4f;
        }

        (Text label, Image fill, Image chip) Vital(Transform parent, string name, float y, Color color, bool chip = false)
        {
            var label = Txt(parent, "", 19, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero,
                new Vector2(16, y + 14), new Vector2(330, 24), pivot: new Vector2(0, 0.5f));
            var track = Img(parent, name, new Color(1f, 1f, 1f, 0.14f), Vector2.zero, Vector2.zero,
                new Vector2(16, y - 4), new Vector2(330, 7), pivot: Vector2.zero);
            var trailing = chip ? Fill(track.transform, Cream, inset: 0f) : null;
            return (label, Fill(track.transform, color, inset: 0f), trailing);
        }

        public struct State
        {
            public int day, current, chasing, investigating;
            public string phase, prompt;
            public float sprintEnergy;
            public bool sprinting, exhausted;
            public float phaseT, hp, maxHp, hunger, hungerMax, evacT, evacNeed, searchK, barricade;
            public bool downed, sleeping, crouching, showBarricade, showShelter;
            public List<(string name, int durability, int maxDurability, int ammo)> slots;
            public Dictionary<string, int> bag;
            public int bagLoad, bagCapacity, bagRow;
            public bool bagOpen, menuOpen, canSkipPractice;
            public string radioLog;
            public LootState loot;
            public TutorialState tutorial;
            public (Vector3 screen, bool visible) stealth;
            public (List<Vector3> warnings, Vector3 direction, float flash) combat;
            public (Vector3 screen, string label, bool visible) encounter;
            public Vector3 stationScreen, shelterScreen, playerMarker;
            public bool stationOnScreen, stationOpen, shelterOnScreen;
            public float shelterDist;
            public string routeLabel;
            public List<(Vector3 screen, string text, Color color, float alpha)> pops;
            public List<Vector3> lootMarks;
            public string objectives, streetEvent;
            public bool beaconActive, nightLighting;
            public List<string> trades;
            public List<(Vector3 screen, string name, Color color)> shops;
            public Vector3 dropScreen;
            public bool showDrop;
            public float dropDist;
        }

        Text _objectives, _tradeText, _streetEvent;
        GameObject _eventPanel, _promptPanel, _messagePanel;
        GameObject _tradeRoot;
        Marker _dropMark;
        readonly List<Text> _shopLabels = new();

        readonly List<Text> _popTexts = new(), _attackFlags = new(), _damageArrows = new();
        readonly List<Text> _marks = new();
        Transform _root;

        /// <summary>Positions pooled labels at screen points (pops float up; marks sit over containers).</summary>
        void Labels(List<Text> pool, int n, Func<int, (Vector3 sp, string text, Color c, int size)> get)
        {
            var canvasSize = ((RectTransform)_root).rect.size;
            while (pool.Count < n) pool.Add(Txt(_root, "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 40)));
            for (int i = 0; i < pool.Count; i++)
            {
                bool on = i < n;
                if (on)
                {
                    var (sp, text, c, size) = get(i);
                    on = sp.z > 0;
                    if (on)
                    {
                        ((RectTransform)pool[i].transform).anchoredPosition = new Vector2(sp.x / Screen.width, sp.y / Screen.height) * canvasSize - canvasSize * 0.5f;
                        pool[i].text = text;
                        pool[i].color = c;
                        pool[i].fontSize = size;
                    }
                }
                pool[i].gameObject.SetActive(on);
            }
        }

        public void Refresh(State s, float dt)
        {
            int t = Mathf.Max(Mathf.CeilToInt(s.phaseT), 0);
            _phaseIcon.text = !s.nightLighting ? "☀" : "☾";
            _phaseIcon.color = !s.nightLighting ? new Color(1f, 0.85f, 0.4f) : new Color(0.65f, 0.75f, 1f);
            _clock.text = s.phase switch
            {
                "day" => $"{s.day}일차 · 낮 {t / 60}:{t % 60:00}",
                "night" => $"{s.day}일차 · 밤 {t / 60}:{t % 60:00}",
                "evac" => "탈출 준비 중",
                _ => _clock.text,
            };

            float hpK = s.hp / s.maxHp;
            if (hpK < _hp.fillAmount - 0.001f) _hpChipHold = 0.45f;
            _hp.fillAmount = hpK;
            _hpChipHold -= dt;
            _hpChip.fillAmount = _hpChipHold > 0 ? Mathf.Max(_hpChip.fillAmount, hpK) : Mathf.MoveTowards(_hpChip.fillAmount, hpK, dt * 0.8f);
            float beat = hpK < 0.3f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 9f) : 0f;
            _hp.color = Color.Lerp(new Color(0.92f, 0.25f, 0.22f), new Color(1f, 0.55f, 0.45f), beat);
            _hpText.text = s.downed ? "쓰러짐!" : $"체력 {Mathf.CeilToInt(s.hp)} / {Mathf.RoundToInt(s.maxHp)}";
            _sprint.fillAmount = s.sprintEnergy;
            _sprint.color = s.exhausted ? new Color(0.98f, 0.59f, 0.34f) : new Color(0.43f, 0.79f, 0.92f);
            _sprintText.text = s.exhausted ? "숨 고르는 중" : "질주  ·  Shift";
            _hunger.fillAmount = s.hunger / s.hungerMax;
            _hungerText.text = s.hunger <= 0 ? "<color=#ff6050>굶주림! (F: 먹기)</color>" : s.hunger < 30 ? "배고프다 (F: 먹기)" : "배고픔";

            for (int k = 0; k < _slots.Count; k++)
            {
                var ui = _slots[k];
                bool has = k < s.slots.Count;
                bool sel = k == s.current && has;
                ui.box.color = sel ? new Color(0.30f, 0.20f, 0.12f, 0.95f) : has ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.45f);
                ui.name.color = sel ? Accent : Cream;
                ui.name.text = has ? s.slots[k].name : "";
                ui.info.text = !has ? "" : s.slots[k].ammo >= 0 ? $"× {s.slots[k].ammo}" : "";
                bool wears = has && s.slots[k].maxDurability > 0;
                ui.wear.transform.parent.gameObject.SetActive(wears);
                if (wears)
                {
                    float f = (float)s.slots[k].durability / s.slots[k].maxDurability;
                    ui.wear.fillAmount = f;
                    ui.wear.color = Color.Lerp(new Color(0.95f, 0.3f, 0.2f), new Color(0.6f, 0.9f, 0.5f), f);
                }
            }
            var b = s.bag;
            string capacity = $"가방 {s.bagLoad / 10f:0.0}/{s.bagCapacity / 10f:0.0} kg";
            _bag.text = $"<size=20><color=#{(s.bagLoad >= s.bagCapacity - 3 ? "ffb373" : "a9b8b0")}>{capacity}  ·  Tab</color></size>\n<color=#ffd08a>F</color> 음식 {b["food"]}   <color=#ffd08a>H</color> 붕대 {b["bandage"]}\n고철 {b["scrap"]}   못 {b["nails"]}   폭죽 {b["fireworks"]}";
            _bagPanel.SetActive(s.bagOpen);
            if (s.bagOpen)
            {
                _bagPanel.transform.SetAsLastSibling();
                var lines = new List<string> { $"<color=#d0eadb>{capacity}</color>", "" };
                for (int i = 0; i < Survivor.SupplyItems.Length; i++)
                {
                    string item = Survivor.SupplyItems[i];
                    string line = $"{Survivor.Name(item)}   ×{b[item]}    <size=20>{b[item] * Tuning.Data.bag.Weight(item) / 10f:0.0} kg</size>";
                    lines.Add(i == s.bagRow ? $"<color=#ffcf87>›   {line}</color>" : $"     {line}");
                }
                lines.Add("<size=18>↑↓ 선택   X 내려놓기   Shift+X 전부   Tab 닫기</size>");
                _bagContents.text = string.Join("\n", lines);
            }

            _promptPanel.SetActive(!string.IsNullOrEmpty(s.prompt));
            _prompt.text = s.prompt;
            _searchRoot.SetActive(s.searchK >= 0);
            _search.fillAmount = Mathf.Clamp01(s.searchK);

            _status.text = s.sleeping ? "자는 중  ·  E 일어나기"
                : s.chasing > 0 ? $"<color=#ff9e8f>추격 {s.chasing}</color>  ·  골목에서 시야 끊기"
                : s.investigating > 0 ? $"<color=#ffd197>수색 {s.investigating}</color>  ·  발소리 줄이기"
                : s.sprinting ? "질주 소음  ·  주변 좀비가 들을 수 있음"
                : s.crouching ? "잠행  ·  C 일어서기" : "";
            _statusPanel.gameObject.SetActive(_status.text.Length > 0);

            _barRoot.SetActive(s.showBarricade);
            _barricade.fillAmount = s.barricade;
            _barText.text = s.barricade <= 0 ? "<color=#ff5040>바리케이드 무너짐!</color>" : $"쉼터 바리케이드 {Mathf.RoundToInt(s.barricade * 100)}%";

            _evacRoot.SetActive(s.phase == "evac");
            _evac.fillAmount = s.evacT / s.evacNeed;
            _evacText.text = $"탈출 준비 {s.evacT:0} / {s.evacNeed:0}초 · 구역 안에서 진행";

            _msgT -= dt;
            bool practicing = !string.IsNullOrEmpty(s.tutorial.Title);
            _messagePanel.SetActive(_msgT > 0 && !practicing);
            _msg.color = new Color(Cream.r, Cream.g, Cream.b, Mathf.Clamp01(_msgT));
            float low = 1 - s.hp / s.maxHp;
            _hurt.color = new Color(0.55f, 0f, 0f, Mathf.Max(Mathf.Clamp01(low - 0.45f) * 0.6f, s.combat.flash * 0.19f));
            _sleep.color = new Color(0, 0, 0.02f, Mathf.MoveTowards(_sleep.color.a, s.sleeping ? 0.75f : 0f, dt * 1.5f));

            float blink = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
            Labels(_marks, s.lootMarks?.Count ?? 0, i =>
            {
                var screen = s.lootMarks[i];
                if (screen.y < Screen.height * 0.21f || screen.y > Screen.height * 0.90f) screen.z = -1;
                return (screen, "▼", new Color(1f, 0.85f, 0.4f, blink), 24);
            });
            Labels(_attackFlags, s.combat.warnings?.Count ?? 0,
                i => (s.combat.warnings[i], "!", new Color(1f, 0.41f, 0.15f, 1f), 49));
            Labels(_damageArrows, s.combat.flash > 0f ? 1 : 0,
                i => (s.combat.direction, "◆", new Color(1f, 0.28f, 0.18f, s.combat.flash), 28));
            Labels(_popTexts, s.pops?.Count ?? 0, i => (s.pops[i].screen, s.pops[i].text, new Color(s.pops[i].color.r, s.pops[i].color.g, s.pops[i].color.b, s.pops[i].alpha), 30));

            _objectives.transform.parent.gameObject.SetActive(!practicing);
            _objectives.text = s.objectives;
            _eventPanel.SetActive(!string.IsNullOrEmpty(s.streetEvent));
            _streetEvent.text = s.streetEvent;
            _tradeRoot.SetActive(s.trades != null);
            if (s.trades != null)
            {
                var lines = new List<string> { "<size=34><color=#ffd080>역무실 생존자</color></size>", "<size=22><color=#a09a90>\"오늘 내놓을 수 있는 건 이 정도야.\"</color></size>", "" };
                for (int i = 0; i < s.trades.Count; i++) lines.Add($"<color=#ffd080>{i + 1}</color>   {s.trades[i]}");
                lines.Add("");
                lines.Add($"<size=22><color=#a09a90>가진 것 — 고철 {s.bag["scrap"]} · 먹을 것 {s.bag["food"]} · 붕대 {s.bag["bandage"]} · 못 {s.bag["nails"]}    E: 닫기</color></size>");
                _tradeText.text = string.Join("\n", lines);
            }
            Labels(_shopLabels, s.shops?.Count ?? 0, i => (s.shops[i].screen, s.shops[i].name, s.shops[i].color, 26));
            Color signalColor = s.beaconActive ? new Color(0.47f, 0.81f, 1f) : new Color(1f, 0.38f, 0.29f);
            _dropMark.arrow.GetComponentInChildren<Text>().color = signalColor;
            _dropMark.label.color = signalColor;
            Place(_dropMark, s.dropScreen, s.showDrop && OnScreen(s.dropScreen), $"{(s.beaconActive ? "유인등" : "보급품")} {s.dropDist:0}m", s.showDrop, 1f);

            Place(_encounterMarker, s.encounter.screen, s.encounter.visible && OnScreen(s.encounter.screen),
                s.encounter.label, s.encounter.visible && !s.bagOpen, 1f);
            Place(_coveredPlayer, s.playerMarker, true, "민서", OnScreen(s.playerMarker), 1f);
            Place(_station, s.stationScreen, s.stationOnScreen, s.routeLabel, !practicing && s.phase != "evac", s.stationOpen ? 1f : 0.55f);
            Place(_shelter, s.shelterScreen, s.shelterOnScreen, $"쉼터 {s.shelterDist:0}m", s.showShelter, 1f);
            RefreshInteractionPanels(s);
        }

        static bool OnScreen(Vector3 sp) => sp.z > 0 && sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height;

        void Place(Marker mk, Vector3 sp, bool onScreen, string text, bool show, float alpha)
        {
            mk.arrow.gameObject.SetActive(show);
            mk.label.gameObject.SetActive(show);
            if (!show) return;
            var canvasSize = ((RectTransform)mk.arrow.parent).rect.size;
            var center = new Vector2(Screen.width, Screen.height) * 0.5f;
            var dir = new Vector2(sp.x, sp.y) - center;
            if (sp.z < 0) dir = -dir;
            Vector2 pos;
            if (onScreen) pos = new Vector2(sp.x / Screen.width, sp.y / Screen.height) * canvasSize - canvasSize * 0.5f + new Vector2(0, 90);
            else
            {
                var d = dir.normalized;
                var half = canvasSize * 0.5f - new Vector2(90, 150);
                float k = Mathf.Min(Mathf.Abs(half.x / (d.x == 0 ? 1e-4f : d.x)), Mathf.Abs(half.y / (d.y == 0 ? 1e-4f : d.y)));
                pos = d * k;
            }
            if (!onScreen) pos.y = Mathf.Max(pos.y, -canvasSize.y * 0.5f + 270f);
            mk.arrow.anchoredPosition = pos;
            mk.arrow.localRotation = Quaternion.Euler(0, 0, onScreen ? 180 : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90);
            ((RectTransform)mk.label.transform).anchoredPosition = pos + (onScreen ? new Vector2(0, 50) : -pos.normalized * 64);
            mk.label.text = text;
            var tri = mk.arrow.GetComponentInChildren<Text>();
            tri.color = new Color(tri.color.r, tri.color.g, tri.color.b, alpha);
            mk.label.color = new Color(mk.label.color.r, mk.label.color.g, mk.label.color.b, alpha);
        }

        // ---------------------------------------------------------------- tiny builders

        static void Stretch(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        static Image Img(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c;
            if (!(aMin == Vector2.zero && aMax == Vector2.one))
            {
                img.sprite = Round;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1.6f;
            }
            return img;
        }

        /// <summary>A soft rounded panel with a faint light rim, 9-sliced so any box can use it.</summary>
        static Sprite Round
        {
            get
            {
                if (_round) return _round;
                const int n = 48, r = 14;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r), 0), dy = Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r), 0);
                    float d = r - Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(d + 0.5f);
                    float rim = Mathf.Clamp01(1.6f - Mathf.Abs(d - 1.2f)) * 0.35f;
                    tex.SetPixel(x, y, new Color(1f + rim, 1f + rim, 1f + rim, a));
                }
                tex.Apply();
                _round = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _round;
            }
        }

        static Image Fill(Transform back, Color c, float inset = 3f)
        {
            var img = Img(back, "fill", c, Vector2.zero, Vector2.one, Vector2.zero, Vector2.one * (-2f * inset));
            var tex = Texture2D.whiteTexture;
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.raycastTarget = false;
            return img;
        }

        static Text Txt(Transform parent, string s, int size, TextAnchor align, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sizeDelta, Vector2? pivot = null)
        {
            var go = new GameObject("text", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Cream;
            t.text = s;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(2, -2);
            return t;
        }
    }
}
