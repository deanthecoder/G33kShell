using System.Numerics;
using CSharp.Core;
using CSharp.Core.AI;
using CSharp.Core.Extensions;
using Newtonsoft.Json;

namespace G33kShell.Desktop.Console.Screensavers.Pong;

public class Brain
{
    private readonly object m_brainLock = new object();
    
    [JsonProperty]
    private NeuralNetwork m_qNet;

    [JsonProperty]
    public static int[] Layers { get; } = [10];

    public Brain()
    {
        var inputSize = new GameState([Vector2.One, Vector2.One], Vector2.One, Vector2.One, 1, 1).ToInputVector().Length;
        m_qNet = new NeuralNetwork(inputSize, hiddenLayers: Layers, outputSize: 6, learningRate: 0.05);
    }

    public (Direction LeftBat, Direction RightBat) ChooseMoves(GameState state)
    {
        lock (m_brainLock)
        {
            var outputs = m_qNet.Predict(state.ToInputVector());

            var leftBatDirection = Direction.Left; // No move.
            if (outputs[0] > outputs[1] && outputs[0] > outputs[2])
                leftBatDirection = Direction.Up;
            else if (outputs[2] > outputs[0] && outputs[2] > outputs[1])
                leftBatDirection = Direction.Down;

            var rightBatDirection = Direction.Left; // No move.
            if (outputs[3] > outputs[4] && outputs[3] > outputs[5])
                rightBatDirection = Direction.Up;
            else if (outputs[5] > outputs[3] && outputs[5] > outputs[4])
                rightBatDirection = Direction.Down;
            
            return (leftBatDirection, rightBatDirection);
        }
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