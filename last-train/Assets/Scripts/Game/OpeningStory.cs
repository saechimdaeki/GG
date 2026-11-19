using UnityEngine;
using UnityEngine.InputSystem;

namespace LastTrain
{
    public partial class Game
    {
        enum Lesson { None, Briefing, Move, Supplies, Stealth, Radio }
        Lesson _lesson;
        int _tutorialZombie = -1;
        City.Container _trainingCrate, _storyCache;
        bool _menuOpen, _storyGuardTriggered;
        int _storyGuards, _peopleHelped;
        bool _rescuedSomeone;
        internal bool InPractice => _lesson != Lesson.None;

        static readonly Vector2 TrainingCratePos = new(2.2f, -7.65f);
        static readonly Vector2 TrainingZombiePos = new(4f, -1.15f);
        static readonly Vector2 RadioPos = new(1.7f, -10.15f);
        static readonly string[] Broadcasts =
        {
            "세운역입니다. 봉쇄선 밖으로 나갈 방법을 찾아요. 역과 시장, 남쪽 시설반에 연락할 수 있습니다.",
            "시장 수리공 윤서가 다쳤습니다. 붕대가 있으면 장터로 가 주세요. 약국 비상함에도 붕대가 남아 있습니다.",
            "역무원 지호가 정비 광장에서 구조 신호를 보냈습니다. 근처에서 비명이 들리면 놈들이 더 몰려올 겁니다.",
            "아직 골목에 남은 분이 있습니다. 다른 통로로 나가더라도 서로 챙길 수 있으면 챙겨요.",
            "역 정비 광장에 비축품이 남아 있어요. 탈출 통로를 정했다면 준비를 마치세요.",
        };

        string RadioLog => $"세운역 운행 기록 · {Day}일차\n{Broadcasts[Mathf.Clamp(Day - 1, 0, Broadcasts.Length - 1)]}";

        void BeginTutorial()
        {
            _trainingCrate = LeaveSupplies(TrainingCratePos, ("pipe", 1));
            _trainingCrate.kind = "쉼터 비상 공구함";
            AddLoot(_trainingCrate.loot, "bandage", 1);
            AddLoot(_trainingCrate.loot, "food", 1);
            if (PlayerPrefs.GetInt("LastTrain.OpeningPracticed", 0) != 0 && !_args.ContainsKey("scene"))
            {
                StartCityPopulation();
                Say("쉼터 앞 비상 공구함에 쇠파이프가 있다.");
                return;
            }
            _lesson = Lesson.Briefing;
        }

        void StartCityPopulation()
        {
            for (int i = 0; i < T.spawn.dayStart; i++) SpawnAnywhere(14f);
            foreach (Vector2 at in City.InteriorZombies) Horde.Spawn(at, 0);
        }

        void FinishTutorial()
        {
            Horde.Despawn(_tutorialZombie);
            _tutorialZombie = -1;
            _lesson = Lesson.None;
            ResumeMenu();
            if (!_args.ContainsKey("scene"))
            {
                PlayerPrefs.SetInt("LastTrain.OpeningPracticed", 1);
                PlayerPrefs.Save();
            }
            StartCityPopulation();
            Say(Broadcasts[0]);
        }

        bool HandleOpeningMenu(Keyboard keys)
        {
            if (Phase == "won" || Phase == "lost" || keys == null) return false;
            if (_lesson == Lesson.Briefing)
            {
                if (keys.enterKey.wasPressedThisFrame || _args.ContainsKey("autotutorial")) _lesson = Lesson.Move;
                else if (keys.escapeKey.wasPressedThisFrame) FinishTutorial();
                return true;
            }
            if (keys.escapeKey.wasPressedThisFrame && _lootSource == null && !_bagOpen && !Trading)
            {
                if (_menuOpen) ResumeMenu();
                else _menuOpen = true;
                _searching = null;
                _prompt = "";
                return true;
            }
            if (_lesson == Lesson.Stealth && keys.rKey.wasPressedThisFrame && !_menuOpen)
            {
                Me.Pos = City.Door.center + Vector2.down * 1.65f;
                ResetPracticeZombie();
            }
            return _menuOpen;
        }

        void ResumeMenu()
        {
            _menuOpen = false;
            _blockAttackUntilRelease = true;
        }

        void VerifyOpening()
        {
            BeginTutorial();
            _lesson = Lesson.Move;
            float originalClock = PhaseT;
            Me.Pos = City.Start + Vector2.left * 1.2f;
            Simulate(0.02f);
            if (_lesson != Lesson.Supplies) throw new System.InvalidOperationException("Opening: movement checkpoint");

            Me.Pos = TrainingCratePos + Vector2.right;
            Interact(false, true, 0f);
            if (_lootSource != _trainingCrate || Me.Slots.Count != 1)
                throw new System.InvalidOperationException("Opening: inspecting loot should not take anything");
            ChooseLoot(0);
            if (Me.Held.id != "pipe" || _trainingCrate.loot.Count != 2 || Me.Bag["bandage"] != 0)
                throw new System.InvalidOperationException("Opening: individual loot selection");
            CloseLoot();
            Interact(false, true, 0f);
            TakeAllLoot();
            if (_lootSource != null || !_trainingCrate.searched || Me.Bag["bandage"] != 1 || Me.Bag["food"] != 2)
                throw new System.InvalidOperationException("Opening: remaining stacks should be available on reopen");
            Simulate(0.02f);
            if (_lesson != Lesson.Stealth) throw new System.InvalidOperationException("Opening: practice zombie was not armed");

            Me.Pos = TrainingZombiePos + Vector2.down * 1.25f;
            Me.Aim = Vector2.up;
            Me.Crouching = true;
            Simulate(0.02f);
            if (Me.StealthTarget(this) < 0 || !Me.Attack(TrainingZombiePos, this) || !Me.LastWasStealthKill)
                throw new System.InvalidOperationException("Opening: preview and crouched rear attack must agree");
            ConfirmStealthKill();
            Simulate(0.02f);
            Me.Pos = RadioPos;
            Interact(false, true, 0f);
            if (InPractice || PhaseT != originalClock || Horde.Count == 0)
                throw new System.InvalidOperationException("Opening: radio should start the timed run");
            Debug.Log("[opening] movement → selective loot → stealth → radio verified");
        }

        void TickTutorial()
        {
            if (_lesson == Lesson.Move && Vector2.Distance(Me.Pos, City.Start) >= 1.1f) _lesson = Lesson.Supplies;
            if (_lesson == Lesson.Supplies && Me.Slots.Exists(slot => slot.id == "pipe"))
            {
                _lesson = Lesson.Stealth;
                ResetPracticeZombie();
            }
        }

        void ResetPracticeZombie()
        {
            Horde.Despawn(_tutorialZombie);
            _tutorialZombie = Horde.Spawn(TrainingZombiePos, 0);
            Horde.Heading[^1] = Vector2.up;
        }

        void ConfirmStealthKill()
        {
            if (_lesson == Lesson.Stealth)
            {
                _lesson = Lesson.Radio;
                Say("암살 성공. 쉼터에서 마지막 무전을 확인하자");
            }
            else
            {
                StealthKills++;
                Say("조용히 처치했다");
            }
        }

        bool InteractRadio(bool pressed)
        {
            if (Vector2.Distance(Me.Pos, RadioPos) >= 1.55f) return false;
            _prompt = "E · 쉼터 무전기";
            if (!pressed) return true;
            if (_lesson == Lesson.Radio) FinishTutorial();
            else if (!InPractice) Say(Broadcasts[Mathf.Clamp(Day - 1, 0, Broadcasts.Length - 1)]);
            return true;
        }

        void DeliverBroadcast()
        {
            Say(Broadcasts[Mathf.Clamp(Day - 1, 0, Broadcasts.Length - 1)]);
            _storyGuardTriggered = false;
            _storyGuards = 0;
            _storyCache = null;
            switch (Day)
            {
                case 2:
                    var pharmacyAt = City.ClinicCenter + new Vector2(0.4f, 2.25f);
                    _storyCache = LeaveSupplies(Horde.PushOut(pharmacyAt, 0.5f, City.Walls), ("bandage", 2));
                    _storyCache.kind = "약국 비상함";
                    _storyCache.room = City.Containers.Find(c => c.room.HasValue && c.room.Value.Contains(City.ClinicCenter))?.room;
                    AddLoot(_storyCache.loot, "food", 1);
                    CreatePharmacyCase(_storyCache);
                    break;
                case 3:
                    _storyCache = LeaveSupplies(City.MarketSquare + Vector2.down * 3.5f, ("fuse", 1));
                    _storyCache.kind = "시장 정비함";
                    AddLoot(_storyCache.loot, "scrap", 1);
                    AddLoot(_storyCache.loot, "bandage", 1);
                    _storyGuards = T.story.marketGuards;
                    break;
                case 5:
                    _storyCache = LeaveSupplies(City.StationCourt, ("food", 2));
                    _storyCache.kind = "승강장 비축품";
                    AddLoot(_storyCache.loot, "nails", 12);
                    AddLoot(_storyCache.loot, "fireworks", 1);
                    _storyGuards = T.story.stationGuards;
                    break;
            }
        }

        void TickStoryLocation()
        {
            if (Phase != "day" || _storyGuardTriggered || _storyGuards == 0 || _storyCache == null || _storyCache.searched)
                return;
            float distance = T.story.guardTriggerDistance;
            if ((_storyCache.pos - Me.Pos).sqrMagnitude > distance * distance) return;

            _storyGuardTriggered = true;
            int spawned = 0;
            for (int i = 0; i < _storyGuards && Horde.Count < T.spawn.maxAlive; i++)
                if (SpawnOne(_storyCache.pos, 3.25f, 5.8f) >= 0) spawned++;
            if (spawned > 0) Say("비축품 근처에서 좀비가 어슬렁거린다");
        }

        string EndingStory()
        {
            string platform = _escapeRoute switch
            {
                EscapeRoute.Road => "운송차가 검문소 차단기를 지나 외곽 도로로 들어섰다.",
                EscapeRoute.Drain => "민서는 수문 아래를 지나 봉쇄선 밖 수로로 나왔다.",
                _ => "박 기관사가 신호를 보내자 열차가 봉쇄된 역을 떠났다.",
            };
            string passengers = _rescuedRelay ? "구조한 역무원 지호가 박 기관사에게 합류해 다음 대피 신호를 보냈다."
                : _helpedMechanic ? "윤서가 수리한 운송차도 피난민을 태우고 시장을 떠났다."
                : _rescuedSomeone ? "골목에서 구한 생존자도 봉쇄 구역을 빠져나왔다."
                : _peopleHelped > 0 ? "물자를 나눠 준 생존자가 밖에서 민서에게 손을 흔들었다." : "민서는 마지막으로 쉼터가 있던 골목을 돌아봤다.";
            return $"{platform}\n{passengers}";
        }

        (Vector3 screen, string label, bool visible) TrackedOpportunity()
        {
            var encounter = EncounterHud();
            if (encounter.visible || _storyCache == null || _storyCache.searched || InPractice) return encounter;
            float distance = Vector2.Distance(Me.Pos, _storyCache.pos);
            return (_cam.WorldToScreenPoint(Fx.V3(_storyCache.pos, 1.05f)), $"{_storyCache.kind} {distance:0}m", true);
        }

        Hud.TutorialState TutorialHud()
        {
            return _lesson switch
            {
                Lesson.Briefing => new("막차까지", "도시가 멈춘 지 사흘. 시설 기사 민서는 세운역에서 흘러나온 마지막 무전을 듣는다.\n\n“봉쇄선을 건너는 길이 남아 있어요. 연락을 놓치지 마세요.”", true, default, ""),
                Lesson.Move => new("쉼터에서", "WASD로 움직이기", false, default, ""),
                Lesson.Supplies => new("비상 공구함", "공구함에 다가가 E를 눌러 열고 쇠파이프 챙기기", false,
                    _cam.WorldToScreenPoint(Fx.V3(TrainingCratePos, 1f)), "비상 공구함"),
                Lesson.Stealth => new("골목의 좀비", "C로 숙여(켜기/끄기) 등 뒤로 접근 → 조준 → 클릭\n들켰다면 R로 다시 연습", false,
                    _cam.WorldToScreenPoint(Fx.V3(TrainingZombiePos, 2f)), "연습 대상"),
                Lesson.Radio => new("세운역 무전", "쉼터로 돌아가 무전기에서 E", false,
                    _cam.WorldToScreenPoint(Fx.V3(RadioPos, 1f)), "무전기"),
                _ => default,
            };
        }

        (Vector3 screen, bool visible) GetStealthHud()
        {
            if (Sleeping || _menuOpen || _lootSource != null || _bagOpen || Trading) return default;
            int target = Me.StealthTarget(this);
            if (target < 0) return default;
            return (_cam.WorldToScreenPoint(Fx.V3(Horde.Pos[target], 1.75f)), true);
        }
    }
}
