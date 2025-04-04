using CSharp.Core;
using CSharp.Core.Extensions;
using Newtonsoft.Json;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class Brain
{
    private readonly object m_brainLock = new object();
    
    [JsonProperty]
    private NeuralQApproximator m_qNet;

    [JsonProperty]
    public static int[] Layers { get; } = [32, 16, 16];

    public Brain()
    {
        var inputSize = new GameState(new Snake(16, 16), IntPoint.Zero).ToInputVector().Length;
        m_qNet = new NeuralQApproximator(inputSize, hiddenLayers: Layers, outputSize: 4, learningRate: 0.05);
    }

    public Direction ChooseMove(GameState state)
    {
        lock (m_brainLock)
            return (Direction)m_qNet.ChooseAction(state.ToInputVector());
    }
    
    public void Clear()
    {
        lock (m_brainLock)
            m_qNet.Clear();
    }

    public byte[] Save()
    {
        lock (m_brainLock)
            return JsonConvert.SerializeObject(this).Compress();
    }

    public void Load(byte[] brainBytes)
    {
        lock (m_brainLock)
            JsonConvert.PopulateObject(brainBytes.DecompressToString(), this);
    }

    public void AverageWith(Brain other) => m_qNet = m_qNet.AverageWith(other.m_qNet).NudgeWeights();
    public void MixWith(Brain other) => m_qNet = m_qNet.MixWith(other.m_qNet).NudgeWeights();

    public Brain Clone()
    {
        var clone = new Brain();
        lock (m_brainLock)
            clone.m_qNet = m_qNet.Clone();
        return clone;
    }
}