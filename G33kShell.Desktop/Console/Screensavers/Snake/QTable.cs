using System.Linq;
using CSharp.Core;
using CSharp.Core.Extensions;
using Newtonsoft.Json;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class QTable
{
    private readonly object m_brainLock = new object();
    
    [JsonProperty]
    private NeuralQApproximator m_qNet;

    [JsonProperty]
    private ReplayBuffer m_replayBuffer;

    [JsonProperty]
    public int[] Layers { get; } = [32, 16];

    public QTable()
    {
        var inputSize = new GameState(GameState.Flags.MovingDown, 1, IntPoint.Zero, IntPoint.Zero, 0).ToInputVector().Length;
        m_qNet = new NeuralQApproximator(inputSize, hiddenLayers: Layers, outputSize: 4, learningRate: 0.05);
        m_replayBuffer = new ReplayBuffer(2000);
    }

    public Direction ChooseMove(GameState state, int gamesPlayed, bool allowExploring)
    {
        lock (m_brainLock)
            return (Direction)m_qNet.ChooseAction(state.ToInputVector(), LearningConfig.ExplorationRate(gamesPlayed, allowExploring));
    }

    public void UpdateQValue(GameState state, GameState nextState, Direction direction, double reward)
    {
        lock (m_brainLock)
        {
            var input = state.ToInputVector();
            var nextInput = nextState?.ToInputVector();

            m_replayBuffer.Add(input, (int)direction, reward, nextInput);

            const int batchSize = 32;
            const int warmupThreshold = 500;
            if (m_replayBuffer.Count < warmupThreshold)
                return;

            var batch = m_replayBuffer.Sample(batchSize);
            foreach (var (s, a, r, sNext) in batch)
            {
                var qValues = m_qNet.Predict(s);
                var target = qValues.ToArray();

                var maxNextQ = sNext != null ? m_qNet.Predict(sNext).Max() : 0.0;
                target[a] = r + LearningConfig.DiscountFactor * maxNextQ;

                m_qNet.Train(s, target);
            }
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

    public void Load(byte[] brainData)
    {
        lock (m_brainLock)
            JsonConvert.PopulateObject(brainData.DecompressToString(), this);
    }
}