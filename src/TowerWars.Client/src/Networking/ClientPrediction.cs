using Godot;
using TowerWars.Shared.Protocol;

namespace TowerWars.Client.Networking;

public partial class ClientPrediction : Node
{
    private readonly struct PendingInput
    {
        public uint Sequence { get; init; }
        public uint Tick { get; init; }
        public InputFlags Flags { get; init; }
        public float MouseX { get; init; }
        public float MouseY { get; init; }
    }

    private readonly Queue<PendingInput> _pendingInputs = new();
    private uint _lastAckedSequence;

    public void RecordInput(uint sequence, uint tick, InputFlags flags, float mouseX, float mouseY)
    {
        _pendingInputs.Enqueue(new PendingInput
        {
            Sequence = sequence,
            Tick = tick,
            Flags = flags,
            MouseX = mouseX,
            MouseY = mouseY
        });

        while (_pendingInputs.Count > 60)
        {
            _pendingInputs.Dequeue();
        }
    }

    public void AcknowledgeInput(uint sequence)
    {
        _lastAckedSequence = sequence;

        while (_pendingInputs.Count > 0 && _pendingInputs.Peek().Sequence <= sequence)
        {
            _pendingInputs.Dequeue();
        }
    }

    public int PendingInputCount => _pendingInputs.Count;
}
