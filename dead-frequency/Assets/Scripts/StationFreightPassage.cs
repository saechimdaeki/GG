using UnityEngine;

namespace ResidualSignal
{
    public sealed partial class StationTrial
    {
        [SerializeField] Transform freightDoor;
        [SerializeField] Transform freightPower;
        [SerializeField] Transform freightGate;
        [SerializeField] Transform freightRelease;
        [SerializeField] Transform freightExit;
        Vector3 freightDoorClosed;
        Vector3 freightGateClosed;
        bool freightArrivalRush;

        void PrepareFreightPassage()
        {
            if (freightDoor == null)
                return;
            freightDoorClosed = freightDoor.position;
            freightGateClosed = freightGate.position;
        }

        void UpdateFreightPassage()
        {
            if (phase is not (Phase.FreightPower or Phase.FreightRelease or Phase.FreightEscape))
                return;
            RaiseFreightGate(freightDoor, freightDoorClosed);
            if (phase != Phase.FreightPower)
                RaiseFreightGate(freightGate, freightGateClosed);
            if (!Authority)
                return;
            if (phase == Phase.FreightPower && !freightArrivalRush && SquadFurthestZ() > 74f)
            {
                freightArrivalRush = true;
                QueueReinforcements(5 + HumanPlayerCount(), new Vector3(-5.6f, 0, 85f), true,
                    spawnSpecials ? InfectedKind.Leaper : InfectedKind.Common);
            }
            if (phase == Phase.FreightEscape && witness.HasArrived && SquadAt(freightExit))
                SetPhase(Phase.Complete);
        }

        void RaiseFreightGate(Transform gate, Vector3 closed)
        {
            gate.position = Vector3.MoveTowards(gate.position, closed + Vector3.up * 3.65f,
                Time.deltaTime * shutterSpeed);
        }

        void StartFreightBeat(Phase next)
        {
            switch (next)
            {
                case Phase.FreightPower:
                    Signal("적재장 끝에 화물 연결로가 있어. 오른쪽 배전함부터 살려.", 4f);
                    break;
                case Phase.FreightRelease:
                    Signal("화물 차단문 개방. 반대편 신호함에서 서비스문 잠금을 풀어.", 4f);
                    if (Authority)
                    {
                        QueueReinforcements(6 + HumanPlayerCount(), new Vector3(5.6f, 0f, 96f), true,
                            spawnSpecials ? InfectedKind.Dragger : InfectedKind.Common);
                        QueueReinforcements(4 + HumanPlayerCount(), new Vector3(-5.6f, 0f, 89f), true);
                        witness.EvacuateWith(this, new Vector3(-1.6f, 0f, 100f));
                    }
                    break;
                case Phase.FreightEscape:
                    Signal("서비스문 열렸다. 서이경과 같이 끝까지 뛰어!", 4f);
                    if (Authority)
                    {
                        QueueReinforcements(6 + HumanPlayerCount(), new Vector3(-5.6f, 0f, 112f), true,
                            spawnSpecials ? InfectedKind.Caller : InfectedKind.Common);
                        QueueReinforcements(4 + HumanPlayerCount(), new Vector3(5.6f, 0f, 100f), true);
                        witness.EvacuateWith(this, freightExit);
                    }
                    break;
            }
        }
    }
}
