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
            Blocking = !Application.isBatchMode && Array.IndexOf(arguments, "-solo") < 0;
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

            backdrop ??= Resources.Load<Texture2D>("HaerimMenu");
            var viewport = new Rect(0, 0, Screen.width, Screen.height);
            if (backdrop != null)
                GUI.DrawTexture(viewport, backdrop, ScaleMode.ScaleAndCrop);
            else
                Fill(viewport, Surface);

            float scale = Mathf.Min(Screen.height / 720f, Screen.width / 800f);
            float w = Screen.width / scale;
            float h = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, w, h), new Color(0.004f, 0.009f, 0.013f, 0.32f));
            Fill(new Rect(0, 0, 442, h), new Color(0.012f, 0.024f, 0.026f, 1f));
            Fill(new Rect(442, 0, 1, h), Border);
            float top = (h - 720) * 0.5f;
            Label(new Rect(48, top + 48, 340, 26), "RESIDUAL SIGNAL", 15, Amber, bold: true);
            Label(new Rect(45, top + 77, 370, 82), "잔류신호", 60, Ink, bold: true);
            Fill(new Rect(48, top + 173, 346, 1), Border);

            Event input = Event.current;
            if (!busy && input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape && page != ScreenPage.Briefing)
            {
                Navigate(page == ScreenPage.Join ? ScreenPage.Online : ScreenPage.Briefing);
                input.Use();
            }

            bool quickDeploy = !busy && page == ScreenPage.Briefing && input.type == EventType.KeyDown &&
                input.keyCode is KeyCode.Return or KeyCode.KeypadEnter;
            if (quickDeploy)
                input.Use();

            GUI.enabled = !busy;
            switch (page)
            {
                case ScreenPage.Briefing:
                    Label(new Rect(48, top + 201, 346, 24), "해림역 · 3번 승강장", 16, Amber, bold: true);
                    Label(new Rect(47, top + 230, 350, 57), "열한 번째 이름", 36, Ink, bold: true);
                    Label(new Rect(48, top + 305, 350, 30), "역에 남은 구조 요청을 확인하세요.", 17, Muted);
                    if (Button(new Rect(48, top + 405, 346, 56), "혼자 출격   /   Enter", true) || quickDeploy)
                        Deploy();
                    if (Button(new Rect(48, top + 473, 346, 54), "온라인 협동"))
                        Navigate(ScreenPage.Online);
                    Label(new Rect(48, top + 546, 346, 30), "혼자 출격 · AI 동료 3명", 15, Muted);
                    break;
                case ScreenPage.Online:
                    Label(new Rect(48, top + 220, 346, 55), "온라인 협동", 38, Ink, bold: true);
                    Label(new Rect(48, top + 291, 346, 30), "최대 4명  /  해림역 3번 승강장", 17, Muted);
                    Label(new Rect(48, top + 325, 346, 30), "방을 만든 뒤 친구에게 참가 코드를 보내세요.", 16, Muted);
                    if (Button(new Rect(48, top + 405, 346, 56), busy ? "방 만드는 중…" : "방 만들기", true))
                        Host();
                    if (Button(new Rect(48, top + 473, 346, 54), "코드로 참가"))
                        Navigate(ScreenPage.Join);
                    break;
                case ScreenPage.Join:
                    DrawJoin(top);
                    break;
            }

            if (page != ScreenPage.Briefing && Button(new Rect(48, top + 630, 118, 37), "뒤로  Esc"))
                Navigate(page == ScreenPage.Join ? ScreenPage.Online : ScreenPage.Briefing);
            GUI.enabled = true;
            if (status.Length > 0)
                Label(new Rect(48, top + 548, 346, 64), status, 16, busy ? Muted : Amber, wrap: true);
            if (page == ScreenPage.Briefing)
            {
                Fill(new Rect(48, top + 623, 346, 1), Border);
                Label(new Rect(48, top + 641, 346, 26), "WASD 이동     E 상호작용     Q 분대 지시", 15, Muted);
            }
            GUI.matrix = previous;
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
            status = "";
            GUI.FocusControl(null);
            focusCode = next == ScreenPage.Join;
        }

        void DrawJoin(float top)
        {
            Label(new Rect(48, top + 220, 346, 55), "코드로 참가", 38, Ink, bold: true);
            Label(new Rect(48, top + 315, 346, 28), "친구에게 받은 참가 코드", 17, Muted);
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
            Fill(new Rect(48, top + 356, 346, 54), GUI.GetNameOfFocusedControl() == "JoinCode" ? Amber : Border);
            Fill(new Rect(49, top + 357, 344, 52), Surface);
            GUI.SetNextControlName("JoinCode");
            code = GUI.TextField(new Rect(49, top + 357, 344, 52), code, 8, field).ToUpperInvariant();
            if (focusCode && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("JoinCode");
                focusCode = false;
            }
            GUI.enabled = !busy && !string.IsNullOrWhiteSpace(code);
            bool submit = Button(new Rect(48, top + 432, 346, 56), busy ? "연결 중…" : "방에 참가", true);
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
