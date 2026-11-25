using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Start screen: solo, host an online session (Relay join code) or join one by code.
    /// Test hooks: -netHost / -netJoin &lt;ip&gt; skip the menu and connect directly over UDP.
    /// </summary>
    public sealed class SessionMenu : MonoBehaviour
    {
        const int MaxPlayers = 4;
        const ushort DirectPort = 7787;

        [SerializeField] NetworkObject missionSyncPrefab;

        public static bool Blocking { get; private set; } = true;

        ISession session;
        string code = "";
        string status = "";
        bool busy;
        enum ScreenPage { Briefing, Online, Join }
        ScreenPage page;
        Texture2D backdrop;
        GUIStyle field;
        GUIStyle action;
        GUIStyle text;
        bool focusCode;
        static readonly Color Ink = new(0.93f, 0.94f, 0.9f);
        static readonly Color Muted = new(0.76f, 0.80f, 0.80f);
        static readonly Color Amber = new(1f, 0.76f, 0.38f);
        static readonly Color Surface = new(0.065f, 0.095f, 0.10f);
        static readonly Color Border = new(0.22f, 0.29f, 0.30f);

        void Start()
        {
            NetworkManager.Singleton.OnServerStarted += () =>
                Instantiate(missionSyncPrefab).Spawn();
            string[] arguments = Environment.GetCommandLineArgs();
            int host = Array.IndexOf(arguments, "-netHost");
            int join = Array.IndexOf(arguments, "-netJoin");
            // Relay test hooks: -relayHost prints the code, -relayJoin CODE joins it (both headless-friendly).
            int relayHost = Array.IndexOf(arguments, "-relayHost");
            int relayJoin = Array.IndexOf(arguments, "-relayJoin");
            if (relayHost >= 0 || relayJoin >= 0)
            {
                Blocking = false;
                NetworkManager.Singleton.OnClientConnectedCallback += id => Debug.Log($"RELAY CLIENT CONNECTED {id}");
                if (relayHost >= 0)
                    Host();
                else
                {
                    code = arguments[relayJoin + 1];
                    Join();
                }
                return;
            }
            if (host >= 0 || join >= 0)
            {
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetConnectionData(join >= 0 ? arguments[join + 1] : "127.0.0.1", DirectPort, "0.0.0.0");
                Blocking = false;
                if (host >= 0)
                    NetworkManager.Singleton.StartHost();
                else
                    NetworkManager.Singleton.StartClient();
                return;
            }
            Difficulty.ReadCommandLine(arguments);
            Blocking = !Application.isBatchMode && Array.IndexOf(arguments, "-solo") < 0;
            // -menuShot PATH: photograph the start screen (UI included) after the camera settles, then quit.
            int shot = Array.IndexOf(arguments, "-menuShot");
            if (shot >= 0)
                StartCoroutine(ShootMenu(arguments[shot + 1], Array.IndexOf(arguments, "-menuHover") >= 0));
        }

        System.Collections.IEnumerator ShootMenu(string path, bool secondItem)
        {
            if (secondItem)
                selected = 1;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-menuOnline") >= 0)
                Navigate(ScreenPage.Online);
            yield return new WaitForSecondsRealtime(4f);
            // -menuDeploy: press 혼자 출격 first, to check the hand-over back to the game camera.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-menuDeploy") >= 0)
            {
                Deploy();
                yield return new WaitForSecondsRealtime(2.5f);
            }
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }

        async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // Separate sign-in profiles let two copies on one computer act as two players.
                string[] arguments = Environment.GetCommandLineArgs();
                int profile = Array.IndexOf(arguments, "-authProfile");
                var options = new InitializationOptions();
                if (profile >= 0)
                    options.SetProfile(arguments[profile + 1]);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        async void Host()
        {
            busy = true;
            status = "세션 만드는 중...";
            try
            {
                Debug.Log("RELAY HOST START");
                await SignIn();
                Debug.Log($"RELAY SIGNED IN {AuthenticationService.Instance.PlayerId}");
                session = await MultiplayerService.Instance.CreateSessionAsync(
                    new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork());
                Debug.Log($"RELAY SESSION CODE {session.Code}");
                Deploy();
                status = "";
            }
            catch (Exception error)
            {
                status = Explain(error);
                Debug.LogError($"RELAY HOST FAILED {error}");
            }
            busy = false;
        }

        async void Join()
        {
            busy = true;
            status = "참가하는 중...";
            try
            {
                await SignIn();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                Debug.Log($"RELAY JOINED {session.Code}");
                Deploy();
                status = "";
            }
            catch (Exception error)
            {
                status = Explain(error);
                Debug.LogError($"RELAY JOIN FAILED {error}");
            }
            busy = false;
        }

        static string Explain(Exception error)
        {
            string text = error.Message;
            if (text.Contains("project", StringComparison.OrdinalIgnoreCase) || error is ServicesInitializationException)
                return "온라인 서버에 연결하지 못했어요. 인터넷 연결을 확인해 주세요.";
            if (text.Contains("not found", StringComparison.OrdinalIgnoreCase) || text.Contains("404"))
                return "그 코드의 방을 찾지 못했어요.";
            if (text.Contains("full", StringComparison.OrdinalIgnoreCase))
                return "방이 가득 찼어요.";
            return "연결 실패: " + (text.Length > 100 ? text[..100] : text);
        }

        // ---- Start screen (L4D2-style: live station behind, a dark wash on the left, big text menu) ----

        const float MenuX = 64f;
        static readonly Color Wash = new(0.01f, 0.016f, 0.02f);
        int selected;
        Texture2D sideWash;
        Texture2D floorWash;
        Camera menuCamera;
        readonly System.Collections.Generic.List<GameObject> hiddenForMenu = new();
        Camera gameCamera;
        float menuOpenedAt;

        /// <summary>The station behind the menu: a second camera drifting slowly through the forecourt at night.</summary>
        void UpdateMenuCamera()
        {
            if (!Blocking || Application.isBatchMode)
            {
                if (menuCamera != null)
                {
                    Destroy(menuCamera.gameObject);
                    menuCamera = null;
                    if (gameCamera != null)
                        gameCamera.enabled = true;
                    foreach (var child in hiddenForMenu)
                        if (child != null)
                            child.SetActive(true);
                    hiddenForMenu.Clear();
                }
                return;
            }
            if (menuCamera == null)
            {
                gameCamera = Camera.main;
                if (gameCamera == null)
                    return;
                var node = new GameObject("Menu camera");
                menuCamera = node.AddComponent<Camera>();
                menuCamera.CopyFrom(gameCamera);
                if (gameCamera.TryGetComponent(out UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData source))
                    source.CopyTo(node.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>());
                menuCamera.fieldOfView = 52f;
                gameCamera.enabled = false;
                // The player's gun, hands and flashlight hang off the game camera; keep them out of the shot.
                foreach (Transform child in gameCamera.transform)
                    if (child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(false);
                        hiddenForMenu.Add(child.gameObject);
                    }
                menuOpenedAt = Time.unscaledTime;
            }
            // A slow push in toward the station entrance with a gentle sway, so the scene breathes.
            float t = Time.unscaledTime - menuOpenedAt;
            float push = Mathf.PingPong(t * 0.11f, 2.4f);
            menuCamera.transform.SetPositionAndRotation(new Vector3(-1.3f + Mathf.Sin(t * 0.13f) * 0.35f, 1.7f + Mathf.Sin(t * 0.21f) * 0.04f, -21.4f + push),
                Quaternion.Euler(-2.5f + Mathf.Sin(t * 0.17f) * 0.6f, 9f + Mathf.Sin(t * 0.09f) * 2.2f, 0f));
        }

        void Update() => UpdateMenuCamera();

        void OnGUI()
        {
            if (Application.isBatchMode)
                return;

            if (!Blocking)
            {
                if (session != null)
                {
                    Rect badge = new(Screen.width - 244, 22, 220, 38);
                    Fill(badge, Surface);
                    Label(badge, $"참가 코드  {session.Code}    {session.PlayerCount}/{MaxPlayers}", 15, Ink, TextAnchor.MiddleCenter);
                }
                return;
            }

            // Without the live camera (it failed to start), fall back to the still photograph.
            if (menuCamera == null)
            {
                backdrop ??= Resources.Load<Texture2D>("HaerimMenu");
                var viewport = new Rect(0, 0, Screen.width, Screen.height);
                if (backdrop != null)
                    GUI.DrawTexture(viewport, backdrop, ScaleMode.ScaleAndCrop);
                else
                    Fill(viewport, Surface);
            }

            float scale = Screen.height / 720f;
            float w = Screen.width / scale;
            float h = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));

            // Dark wash from the left edge and up from the floor, instead of a solid panel.
            // Solid behind the text column, then fading out across the scene.
            sideWash ??= Gradient(256, 1, x => x < 0.42f ? 0.9f : Mathf.Lerp(0.9f, 0f, Mathf.SmoothStep(0f, 1f, (x - 0.42f) / 0.58f)));
            floorWash ??= Gradient(1, 128, y => Mathf.Lerp(0f, 0.85f, y * y));
            DrawTinted(new Rect(0, 0, Mathf.Max(760f, w * 0.62f), h), sideWash, Wash);
            DrawTinted(new Rect(0, h - 190, w, 190), floorWash, Wash);

            Event input = Event.current;
            HandleKeys(input);

            float top = Mathf.Max(0f, (h - 720f) * 0.4f);
            Label(new Rect(MenuX, top + 54, 420, 20), "R E S I D U A L    S I G N A L", 13, Amber, bold: true);
            Label(new Rect(MenuX - 4, top + 74, 560, 96), "잔류신호", 76, Ink, bold: true);
            // The signal: a dot that pulses like a carrier on the radio.
            float pulse = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            Fill(new Rect(MenuX + 2, top + 180, 8, 8), new Color(Amber.r, Amber.g, Amber.b, pulse));
            Label(new Rect(MenuX + 20, top + 172, 520, 24), "수신 중  ·  해림역 3번 승강장  ·  봉쇄 19일째", 14, Muted);

            GUI.enabled = !busy;
            switch (page)
            {
                case ScreenPage.Briefing:
                    if (MenuItem(0, top + 232, "혼자 출격", "AI 동료 3명과 함께"))
                        Deploy();
                    if (MenuItem(1, top + 286, "온라인 협동", "최대 4명 · 참가 코드"))
                        Navigate(ScreenPage.Online);
                    DrawOperation(top + 370);
                    break;
                case ScreenPage.Online:
                    Label(new Rect(MenuX, top + 232, 400, 24), "온라인 협동 · 전체 캠페인 · 방장의 난이도", 15, Muted);
                    if (MenuItem(0, top + 268, busy ? "방 만드는 중…" : "방 만들기", "친구에게 참가 코드를 보내세요"))
                    {
                        Difficulty.Selected = Difficulty.Mission.Campaign;
                        Host();
                    }
                    if (MenuItem(1, top + 322, "코드로 참가", "받은 코드를 입력"))
                        Navigate(ScreenPage.Join);
                    if (MenuItem(2, top + 376, "뒤로", "Esc"))
                        Navigate(ScreenPage.Briefing);
                    break;
                case ScreenPage.Join:
                    DrawJoin(top);
                    if (MenuItem(2, top + 470, "뒤로", "Esc"))
                        Navigate(ScreenPage.Online);
                    break;
            }
            GUI.enabled = true;
            if (status.Length > 0)
                Label(new Rect(MenuX, top + 560, 460, 48), status, 16, busy ? Muted : Amber, wrap: true);

            DrawKeys(h - 58, w);
            GUI.matrix = previous;
        }

        void HandleKeys(Event input)
        {
            if (busy || input.type != EventType.KeyDown)
                return;
            int items = page == ScreenPage.Briefing ? 2 : 3;
            switch (input.keyCode)
            {
                case KeyCode.Escape when page != ScreenPage.Briefing:
                    Navigate(page == ScreenPage.Join ? ScreenPage.Online : ScreenPage.Briefing);
                    break;
                case KeyCode.UpArrow:
                case KeyCode.W when page != ScreenPage.Join:
                    selected = (selected + items - 1) % items;
                    break;
                case KeyCode.DownArrow:
                case KeyCode.S when page != ScreenPage.Join:
                    selected = (selected + 1) % items;
                    break;
                case KeyCode.LeftArrow when page == ScreenPage.Briefing:
                    Difficulty.Selected = (Difficulty.Mission)(((int)Difficulty.Selected + 3) % 4);
                    break;
                case KeyCode.RightArrow when page == ScreenPage.Briefing:
                    Difficulty.Selected = (Difficulty.Mission)(((int)Difficulty.Selected + 1) % 4);
                    break;
                case KeyCode.Return or KeyCode.KeypadEnter when page == ScreenPage.Briefing:
                    if (selected == 0) Deploy(); else Navigate(ScreenPage.Online);
                    break;
                case KeyCode.Return or KeyCode.KeypadEnter when page == ScreenPage.Online:
                    if (selected == 0) { Difficulty.Selected = Difficulty.Mission.Campaign; Host(); }
                    else if (selected == 1) Navigate(ScreenPage.Join);
                    else Navigate(ScreenPage.Briefing);
                    break;
                default:
                    return;
            }
            input.Use();
        }

        /// <summary>A big text menu entry: amber with a bar when hovered or picked by keyboard.</summary>
        bool MenuItem(int index, float y, string label, string caption)
        {
            var rect = new Rect(MenuX - 16, y, 470, 46);
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            if (hover && Event.current.type == EventType.MouseMove)
                selected = index;
            bool lit = GUI.enabled && (hover || selected == index);
            float shift = lit ? 10f : 0f;
            if (lit)
                Fill(new Rect(MenuX - 16, y + 9, 4, 28), Amber);
            float labelWidth = Mathf.Min(300f, Measure(label, 30) + 8f);
            Label(new Rect(MenuX + shift, y, labelWidth, 46), label, 30, lit ? Amber : Ink, bold: true);
            Label(new Rect(MenuX + shift + labelWidth + 14, y + 4, 240, 42), caption, 14, lit ? Ink : Muted);
            action ??= new GUIStyle(GUI.skin.button) { normal = { background = null }, hover = { background = null }, active = { background = null }, focused = { background = null } };
            return GUI.Button(rect, GUIContent.none, action);
        }

        static string MissionTag => Difficulty.Selected switch
        {
            Difficulty.Mission.Annex => "캠페인 후반  ·  약 8분",
            Difficulty.Mission.Survival => "생존  ·  동 4분 · 은 7분 · 금 10분",
            Difficulty.Mission.Scavenge => "연료 수집  ·  8통 · 시간 제한",
            _ => "전체 캠페인  ·  15~20분",
        };

        /// <summary>The operation card: pick the mission with arrows and the difficulty directly.</summary>
        void DrawOperation(float y)
        {
            const float width = 470f;
            Fill(new Rect(MenuX - 16, y, width, 1), Border);
            Label(new Rect(MenuX, y + 16, 200, 18), "작 전", 12, Amber, bold: true);
            int mission = (int)Difficulty.Selected;
            if (ArrowButton(new Rect(MenuX - 4, y + 42, 30, 40), "<"))
                Difficulty.Selected = (Difficulty.Mission)((mission + 3) % 4);
            float titleWidth = Measure(Difficulty.MissionTitle, 26);
            Label(new Rect(MenuX + 34, y + 42, titleWidth + 10, 40), Difficulty.MissionTitle, 26, Ink, TextAnchor.MiddleLeft, bold: true);
            if (ArrowButton(new Rect(MenuX + 44 + titleWidth, y + 42, 30, 40), ">"))
                Difficulty.Selected = (Difficulty.Mission)((mission + 1) % 4);
            // Four little ticks show where in the list you are.
            for (int i = 0; i < 4; i++)
                Fill(new Rect(MenuX + 36 + i * 18, y + 86, 12, 3), i == mission ? Amber : Border);
            Label(new Rect(MenuX + 118, y + 79, 300, 16), MissionTag, 12, Muted);
            Label(new Rect(MenuX, y + 96, width - 40, 44), Difficulty.MissionLine, 15, Muted, wrap: true);

            Label(new Rect(MenuX, y + 150, 200, 18), "난 이 도", 12, Amber, bold: true);
            string[] levels = { "쉬움", "보통", "고급", "전문가" };
            for (int i = 0; i < levels.Length; i++)
            {
                var box = new Rect(MenuX + i * 108, y + 176, 100, 36);
                bool chosen = (int)Difficulty.Current == i;
                bool hover = GUI.enabled && box.Contains(Event.current.mousePosition);
                Fill(box, chosen ? Amber : hover ? new Color(Amber.r, Amber.g, Amber.b, 0.6f) : new Color(1f, 1f, 1f, 0.16f));
                if (!chosen)
                    Fill(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), new Color(Wash.r, Wash.g, Wash.b, 0.82f));
                Label(box, levels[i], 16, chosen ? Surface : hover ? Amber : Ink, TextAnchor.MiddleCenter, bold: chosen);
                if (GUI.Button(box, GUIContent.none, action))
                    Difficulty.Current = (Difficulty.Level)i;
            }
        }

        bool ArrowButton(Rect rect, string glyph)
        {
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Fill(new Rect(rect.x, rect.y + 4, rect.width, rect.height - 8), hover ? new Color(Amber.r, Amber.g, Amber.b, 0.25f) : new Color(1f, 1f, 1f, 0.08f));
            Label(rect, glyph, 22, hover ? Amber : Ink, TextAnchor.MiddleCenter, bold: true);
            return GUI.Button(rect, GUIContent.none, action);
        }

        /// <summary>Keycaps along the bottom: the controls worth knowing before the first fight.</summary>
        void DrawKeys(float y, float width)
        {
            (string key, string what)[] keys = page == ScreenPage.Briefing
                ? new[] { ("WASD", "이동"), ("E", "상호작용"), ("V", "밀치기"), ("G", "투척"), ("4", "진통제"), ("Q", "분대 지시") }
                : new[] { ("Enter", "선택"), ("Esc", "뒤로") };
            float x = MenuX;
            foreach (var (key, what) in keys)
            {
                float capWidth = Mathf.Max(28f, Measure(key, 13) + 16f);
                Fill(new Rect(x, y, capWidth, 26), new Color(1f, 1f, 1f, 0.2f));
                Fill(new Rect(x + 1, y + 1, capWidth - 2, 24), new Color(Wash.r, Wash.g, Wash.b, 0.9f));
                Label(new Rect(x, y, capWidth, 26), key, 13, Ink, TextAnchor.MiddleCenter, bold: true);
                Label(new Rect(x + capWidth + 8, y, 90, 26), what, 14, Muted);
                x += capWidth + 8 + Measure(what, 14) + 22;
            }
            if (page == ScreenPage.Briefing)
                Label(new Rect(width - 360, y, 320, 26), "↑↓ 메뉴   ←→ 작전   Enter 출격", 13, Muted, TextAnchor.MiddleRight);
        }

        float Measure(string value, int size)
        {
            text ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset() };
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            return text.CalcSize(new GUIContent(value)).x;
        }

        static Texture2D Gradient(int width, int height, Func<float, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float t = width > 1 ? x / (width - 1f) : y / (height - 1f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(255 * alpha(width > 1 ? t : 1f - t)));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static void DrawTinted(Rect rect, Texture texture, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        static void Deploy()
        {
            if (Blocking && !Application.isBatchMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            Blocking = false;
        }

        void Navigate(ScreenPage next)
        {
            page = next;
            selected = 0;
            status = "";
            GUI.FocusControl(null);
            focusCode = next == ScreenPage.Join;
        }

        void DrawJoin(float top)
        {
            Label(new Rect(MenuX, top + 232, 400, 46), "코드로 참가", 30, Amber, bold: true);
            Label(new Rect(MenuX, top + 284, 400, 24), "친구에게 받은 참가 코드", 15, Muted);
            if (field == null)
            {
                field = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 22, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 16, 0, 0)
                };
                foreach (GUIStyleState state in new[] { field.normal, field.hover, field.active, field.focused })
                {
                    state.background = null;
                    state.textColor = Ink;
                }
            }
            Fill(new Rect(MenuX, top + 316, 346, 54), GUI.GetNameOfFocusedControl() == "JoinCode" ? Amber : Border);
            Fill(new Rect(MenuX + 1, top + 317, 344, 52), Surface);
            GUI.SetNextControlName("JoinCode");
            code = GUI.TextField(new Rect(MenuX + 1, top + 317, 344, 52), code, 8, field).ToUpperInvariant();
            if (focusCode && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("JoinCode");
                focusCode = false;
            }
            GUI.enabled = !busy && !string.IsNullOrWhiteSpace(code);
            bool submit = Button(new Rect(MenuX, top + 390, 346, 52), busy ? "연결 중…" : "방에 참가", true);
            Event input = Event.current;
            if (GUI.enabled && input.type == EventType.KeyDown && input.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                input.Use();
                submit = true;
            }
            if (submit)
                Join();
            GUI.enabled = !busy;
        }

        bool Button(Rect rect, string label, bool primary = false)
        {
            if (action == null)
            {
                action = new GUIStyle(GUI.skin.button);
                foreach (GUIStyleState state in new[] { action.normal, action.hover, action.active, action.focused })
                    state.background = null;
            }
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Fill(rect, primary ? (GUI.enabled ? (hover ? new Color(1f, 0.85f, 0.57f) : Amber) : Border) : (hover ? Amber : Border));
            if (!primary)
                Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Surface);
            Label(new Rect(rect.x + 15, rect.y, rect.width - 30, rect.height), label, 18,
                primary && GUI.enabled ? Surface : Ink, bold: true);
            return GUI.Button(rect, GUIContent.none, action);
        }

        void Label(Rect rect, string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft,
            bool bold = false, bool wrap = false)
        {
            text ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset() };
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = alignment;
            text.wordWrap = wrap;
            // A soft drop shadow keeps text readable over the live scene.
            if (color.a > 0.5f && Blocking)
            {
                text.normal.textColor = new Color(0f, 0f, 0f, 0.55f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), value, text);
            }
            text.normal.textColor = color;
            GUI.Label(rect, value, text);
        }

        static void Fill(Rect rect, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
