using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastTrain
{
    public partial class Hud
    {
        public readonly struct LootState
        {
            public readonly string Title;
            public readonly List<string> Rows;
            public readonly int Selection, Load, Capacity;
            public LootState(string title, List<string> rows, int selection, int load, int capacity)
            {
                Title = title; Rows = rows; Selection = selection; Load = load; Capacity = capacity;
            }
        }

        public readonly struct TutorialState
        {
            public readonly string Title, Body, Marker;
            public readonly bool Briefing;
            public readonly Vector3 Screen;
            public TutorialState(string title, string body, bool briefing, Vector3 screen, string marker)
            {
                Title = title; Body = body; Briefing = briefing; Screen = screen; Marker = marker;
            }
        }

        readonly List<(Image panel, Text label)> _lootRows = new();
        GameObject _lootPanel, _controlsPanel, _lessonPanel, _briefingPanel;
        GameObject _skipPracticeButton;
        Text _lootTitle, _controlsLog, _lessonText, _briefingText;
        Marker _lessonMarker, _stealthMarker;

        public void BuildInteractionPanels(Action<int> select, Action takeAll, Action close, Action resume, Action skipPractice)
        {
            Vector2 middle = Vector2.one * 0.5f;
            Vector2 top = new(0.5f, 1f);
            _lootPanel = Img(_root, "Container contents", Ink, middle, middle, new Vector2(0, 20), new Vector2(680, 505)).gameObject;
            _lootTitle = Txt(_lootPanel.transform, "", 30, TextAnchor.MiddleCenter, top, top, new Vector2(0, -43), new Vector2(612, 62));
            for (int i = 0; i < 6; i++)
            {
                int row = i;
                var button = MakeInteractionButton(_lootPanel.transform, "", top, new Vector2(0, -99 - i * 51),
                    new Vector2(610, 45), () => select(row));
                _lootRows.Add(button);
            }
            MakeInteractionButton(_lootPanel.transform, "F · 전부 챙기기", new Vector2(0.5f, 0),
                new Vector2(-148, 64), new Vector2(280, 52), takeAll);
            MakeInteractionButton(_lootPanel.transform, "Esc · 닫기", new Vector2(0.5f, 0),
                new Vector2(148, 64), new Vector2(280, 52), close);
            Txt(_lootPanel.transform, "↑↓ 선택 · E/Enter 챙기기", 19, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(600, 30));

            _briefingPanel = Img(_root, "Opening briefing", Ink, middle, middle, Vector2.zero, new Vector2(870, 430)).gameObject;
            _briefingText = Txt(_briefingPanel.transform, "", 27, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-105, -74));
            _lessonPanel = Img(_root, "Current lesson", Ink, top, top, new Vector2(0, -140), new Vector2(760, 120)).gameObject;
            _lessonText = Txt(_lessonPanel.transform, "", 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-44, -20));

            _controlsPanel = Img(_root, "Pause and controls", Ink, middle, middle, Vector2.zero, new Vector2(920, 610)).gameObject;
            Txt(_controlsPanel.transform,
                "<size=38>일시 정지</size>\n\n이동  WASD     달리기  Shift     숙이기  C · Ctrl (켜기/끄기)\n조준  마우스     공격  왼쪽 클릭     무기  1–4 / 휠\n" +
                "상호작용 / 수색 / 줍기  E     보급 유인등 예약  G\n가방  Tab     먹기  F     붕대  H     탈출로 추적  J\n" +
                "암살  숙인 채 들키지 않고 등 뒤에 접근 → 조준 → 클릭",
                24, TextAnchor.UpperLeft, top, top, new Vector2(0, -38), new Vector2(798, 330), pivot: new Vector2(0.5f, 1));
            _controlsLog = Txt(_controlsPanel.transform, "", 23, TextAnchor.MiddleLeft, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 175), new Vector2(798, 172));
            MakeInteractionButton(_controlsPanel.transform, "Esc · 계속하기", new Vector2(0.5f, 0),
                new Vector2(-150, 44), new Vector2(285, 54), resume);
            var skip = MakeInteractionButton(_controlsPanel.transform, "연습 건너뛰기", new Vector2(0.5f, 0),
                new Vector2(150, 44), new Vector2(285, 54), skipPractice);
            _skipPracticeButton = skip.Item1.gameObject;
            _lessonMarker = MakeMarker(_root, new Color(0.71f, 0.92f, 0.81f));
            _stealthMarker = MakeMarker(_root, new Color(0.61f, 1f, 0.78f));
            _lootPanel.SetActive(false);
            _briefingPanel.SetActive(false);
            _lessonPanel.SetActive(false);
            _controlsPanel.SetActive(false);
        }

        (Image, Text) MakeInteractionButton(Transform owner, string label, Vector2 anchor, Vector2 at, Vector2 size, Action action)
        {
            var image = Img(owner, label, new Color(0.12f, 0.21f, 0.18f), anchor, anchor, at, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener(() => action());
            var text = Txt(image.transform, label, 23, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-18, -4));
            return (image, text);
        }

        void RefreshInteractionPanels(State state)
        {
            var lesson = state.tutorial;
            bool hasLesson = !string.IsNullOrEmpty(lesson.Title);
            bool hasLoot = state.loot.Rows != null;
            var stealth = state.stealth;
            bool showPlayMarkers = !state.menuOpen && !hasLoot && !state.bagOpen && !lesson.Briefing;
            bool hasMarker = !string.IsNullOrEmpty(lesson.Marker) && showPlayMarkers && !stealth.visible;
            Place(_lessonMarker, lesson.Screen, hasMarker && OnScreen(lesson.Screen), lesson.Marker, hasMarker, 1f);
            Place(_stealthMarker, stealth.screen, true, "클릭 · 암살", stealth.visible && showPlayMarkers && OnScreen(stealth.screen), 1f);

            _lessonPanel.SetActive(hasLesson && !lesson.Briefing && showPlayMarkers);
            if (_lessonPanel.activeSelf)
            {
                _lessonText.text = $"<color=#a5d9bb>{lesson.Title}</color>\n{lesson.Body}";
                _lessonPanel.transform.SetAsLastSibling();
            }
            _briefingPanel.SetActive(lesson.Briefing);
            if (lesson.Briefing)
            {
                _briefingText.text = $"<size=52>{lesson.Title}</size>\n\n{lesson.Body}\n\n<size=23>Enter 연습 시작    Esc 연습 생략</size>";
                _briefingPanel.transform.SetAsLastSibling();
            }

            _lootPanel.SetActive(hasLoot);
            if (hasLoot)
            {
                var loot = state.loot;
                var rect = (RectTransform)_lootPanel.transform;
                rect.sizeDelta = new Vector2(680, 208 + loot.Rows.Count * 51);
                _lootTitle.text = $"{loot.Title}     <size=22><color=#a5d9bb>가방 {loot.Load / 10f:0.0}/{loot.Capacity / 10f:0.0} kg</color></size>";
                for (int i = 0; i < _lootRows.Count; i++)
                {
                    var item = _lootRows[i];
                    item.panel.gameObject.SetActive(i < loot.Rows.Count);
                    if (i >= loot.Rows.Count) continue;
                    item.label.text = loot.Rows[i];
                    item.panel.color = i == loot.Selection ? new Color(0.23f, 0.36f, 0.29f) : new Color(0.10f, 0.17f, 0.14f);
                }
                _lootPanel.transform.SetAsLastSibling();
            }

            _controlsPanel.SetActive(state.menuOpen);
            if (state.menuOpen)
            {
                _skipPracticeButton.SetActive(state.canSkipPractice);
                _controlsLog.text = $"<color=#a5d9bb>{state.radioLog}</color>";
                _controlsPanel.transform.SetAsLastSibling();
            }
        }
    }
}
