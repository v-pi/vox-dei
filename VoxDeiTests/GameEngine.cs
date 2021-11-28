using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using VoxDei;

namespace VoxDeiTests
{
    public class GameEngine : IIoProvider
    {
        public int Columns { get; }
        public int Rows { get; }
        public int Rounds { get; }
        public int Bombs { get; }

        private int turnCounter;

        Map currentMap;

        public Position?[] Explosions { get; }

        private Queue<string> _toRead;

        public GameEngine(int cols, int rows, int rounds, int bombs, List<Node> nodes, List<Position> blocks)
        {
            Logger.StandardLog = true;
            Columns = cols;
            Rows = rows;
            Explosions = new Position?[rounds];
            Bombs = bombs;

            currentMap = new Map(rows, cols)
            {
                BombsLeft = bombs,
                RoundsLeft = rounds,
                Nodes = nodes,
                Blocks = GetBlockArray(blocks, cols, rows)
            };
            var firstLines = new List<string> { $"{Columns} {Rows}" };
            var followingLines = currentMap.ToString().Split(Environment.NewLine).ToList();
            var allLines = firstLines.Concat(followingLines).ToList();

            _toRead = new Queue<string>(allLines);
        }

        private static bool[,] GetBlockArray(List<Position> blocks, int cols, int rows)
        {
            var array = new bool[cols, rows];
            foreach (var block in blocks)
            {
                array[block.Col, block.Row] = true;
            }
            return array;
        }

        public void TestPlayer()
        {
            var v = new WilliamRockwood(this);
            v.Play();
            Assert.Fail("Player stopped playing but there are still nodes");
        }

        public string ReadLine()
        {
            return _toRead.Dequeue();
        }

        public void WriteLine(string s)
        {
            if (s != "WAIT")
            {
                if (currentMap.BombsLeft == 0)
                {
                    Assert.Fail("No bomb left");
                }
                var bombPlanted = new Position(s);
                if (currentMap.Nodes.Any(n => n.Position == bombPlanted))
                {
                    Assert.Fail($"You tried to place a fork-bomb on ({bombPlanted}) but a firewall node is there");
                }
                currentMap.BombsLeft--;
                Explosions[turnCounter + Constants.BOMB_TIMEOUT - 1] = bombPlanted;
            }
            if (currentMap.RoundsLeft < 0)
            {
                Assert.Fail("No turn left");
            }
            if (currentMap.Nodes.Count == 0)
            {
                Assert.Pass("All nodes destroyed");
            }
            currentMap = currentMap.GetNextMap(Explosions, turnCounter);
            turnCounter++;
            Console.WriteLine(s);
            _toRead = new Queue<string>(currentMap.ToString().Split(Environment.NewLine));
        }

    }
}
