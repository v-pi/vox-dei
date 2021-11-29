using NUnit.Framework;
using System.Collections.Generic;
using VoxDei;

namespace VoxDeiTests
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Need to start methods with a number")]
    public class Tests
    {
        [Test]
        public void _01_un_noeud_furtif()
        {
            var nodes = new List<Node>
            {
                new Node(1, 4, Direction.RIGHT)
            };
            var blocks = new List<Position>
            {
            };
            var gameEngine = new GameEngine(12, 9, 30, 1, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        public void _02_2_noeuds_furtifs_2_bombes()
        {
            var nodes = new List<Node>
            {
                new Node(2, 8, Direction.UP),
                new Node(5, 4, Direction.RIGHT)
            };
            var blocks = new List<Position>
            {
                new Position(0, 0),
                new Position(11, 0),
                new Position(0, 8),
                new Position(11, 8)
            };
            var gameEngine = new GameEngine(12, 9, 30, 2, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        public void _03_6_noeuds_furtifs_6_bombes()
        {
            var nodes = new List<Node>
            {
                new Node(2, 0, Direction.IDLE),
                new Node(7, 0, Direction.DOWN),
                new Node(11, 1, Direction.UP),
                new Node(1, 2, Direction.LEFT),
                new Node(5, 3, Direction.DOWN),
                new Node(0, 4, Direction.RIGHT),
                new Node(1, 7, Direction.RIGHT),
            };
            var blocks = new List<Position>
            {
            };
            var gameEngine = new GameEngine(12, 9, 50, 7, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        public void _04_2_noeuds_furtifs_1_bombe()
        {
            var nodes = new List<Node>
            {
                new Node(2, 8, Direction.UP),
                new Node(5, 4, Direction.RIGHT)
            };
            var blocks = new List<Position>
            {
                new Position(0, 0),
                new Position(11, 0),
                new Position(0, 8),
                new Position(11, 8)
            };
            var gameEngine = new GameEngine(12, 9, 30, 1, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        public void _05_9_noeuds_furtifs_9_bombes()
        {
            var nodes = new List<Node>
            {
                new Node(3, 0, Direction.RIGHT),
                new Node(9, 1, Direction.LEFT),
                new Node(6, 2, Direction.RIGHT),
                new Node(5, 3, Direction.RIGHT),
                new Node(4, 4, Direction.RIGHT),
                new Node(3, 5, Direction.RIGHT),
                new Node(2, 6, Direction.RIGHT),
                new Node(1, 7, Direction.RIGHT),
                new Node(0, 8, Direction.RIGHT),
            };
            var blocks = new List<Position>
            {
                new Position(1, 0),
                new Position(2, 1),
                new Position(9, 2),
                new Position(10, 3),
                new Position(8, 4),
                new Position(11, 5),
                new Position(9, 6),
                new Position(8, 7),
                new Position(7, 8)
            };
            var gameEngine = new GameEngine(12, 9, 99, 9, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        public void _06_4_noeuds_furtifs_1_bombe()
        {
            var nodes = new List<Node>
            {
                new Node(6, 2, Direction.DOWN),
                new Node(2, 3, Direction.RIGHT),
                new Node(11, 5, Direction.LEFT),
                new Node(5, 8, Direction.UP),
            };
            var blocks = new List<Position>
            {
                new Position(0, 0),
                new Position(11, 0),
                new Position(0, 8),
                new Position(11, 8),
                new Position(4, 2),
                new Position(7, 2),
                new Position(3, 4),
                new Position(8, 4),
                new Position(4, 6),
                new Position(7, 6),
            };
            var gameEngine = new GameEngine(12, 9, 55, 1, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        public void _07_la_barre_de_fer()
        {
            var nodes = new List<Node>
            {
                new Node(8, 0, Direction.LEFT),
                new Node(7, 1, Direction.RIGHT),
                new Node(6, 2, Direction.DOWN),
                new Node(5, 3, Direction.RIGHT),
                new Node(4, 4, Direction.UP),
                new Node(3, 5, Direction.LEFT),
                new Node(2, 6, Direction.RIGHT),
                new Node(1, 7, Direction.RIGHT),
                new Node(0, 8, Direction.RIGHT)
            };
            var blocks = new List<Position>
            {
                new Position(1, 2),
                new Position(10, 2),
                new Position(0, 7),
                new Position(9, 7)
            };
            var gameEngine = new GameEngine(12, 9, 60, 9, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        public void _08_la_barre_de_fer_4_bombes()
        {
            var nodes = new List<Node>
            {
                new Node(8, 0, Direction.LEFT),
                new Node(7, 1, Direction.RIGHT),
                new Node(6, 2, Direction.DOWN),
                new Node(5, 3, Direction.RIGHT),
                new Node(4, 4, Direction.UP),
                new Node(3, 5, Direction.LEFT),
                new Node(2, 6, Direction.RIGHT),
                new Node(1, 7, Direction.DOWN),
                new Node(0, 8, Direction.RIGHT)
            };
            var blocks = new List<Position>
            {
                new Position(1, 2),
                new Position(3, 2),
                new Position(5, 2),
                new Position(7, 2),
                new Position(9, 2),
                new Position(11, 2),
                new Position(0, 4),
                new Position(2, 4),
                new Position(5, 4),
                new Position(8, 4),
                new Position(10, 4)
            };
            var gameEngine = new GameEngine(12, 9, 60, 4, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        public void _09_patience()
        {
            var nodes = new List<Node>
            {
                new Node(5, 1, Direction.IDLE),
                new Node(5, 3, Direction.IDLE),
                new Node(5, 5, Direction.IDLE),
                new Node(5, 7, Direction.IDLE),
                new Node(2, 4, Direction.IDLE),
                new Node(4, 4, Direction.IDLE),
                new Node(6, 4, Direction.IDLE),
                new Node(8, 4, Direction.IDLE),
                new Node(10, 1, Direction.IDLE),
                new Node(1, 7, Direction.IDLE),
                new Node(0, 0, Direction.RIGHT),
                new Node(0, 2, Direction.DOWN),
                new Node(9, 8, Direction.LEFT),
                new Node(11, 8, Direction.UP),
            };
            var blocks = new List<Position>
            {
            };
            var gameEngine = new GameEngine(12, 9, 90, 3, nodes, blocks);
            gameEngine.TestPlayer();
        }

        [Test]
        //[Ignore("Takes too much time")]
        [Timeout(5000)]
        public void _10_vandalisme()
        {
            var nodes = new List<Node>
            {
                new Node(4, 0, Direction.LEFT),
                new Node(10, 0, Direction.IDLE),
                new Node(11, 0, Direction.IDLE),
                new Node(12, 0, Direction.IDLE),
                new Node(14, 0, Direction.IDLE),
                new Node(15, 0, Direction.IDLE),
                new Node(13, 1, Direction.LEFT),
                new Node(4, 3, Direction.IDLE),
                new Node(11, 3, Direction.LEFT),
                new Node(0, 4, Direction.DOWN),
                new Node(3, 4, Direction.IDLE),
                new Node(5, 4, Direction.IDLE),
                new Node(8, 4, Direction.UP),
                new Node(4, 5, Direction.IDLE),
                new Node(11, 6, Direction.IDLE),
                new Node(7, 7, Direction.UP),
                new Node(10, 7, Direction.IDLE),
                new Node(12, 7, Direction.IDLE),
                new Node(15, 7, Direction.DOWN),
                new Node(4, 8, Direction.LEFT),
                new Node(11, 8, Direction.IDLE),
                new Node(7, 9, Direction.IDLE),
                new Node(6, 10, Direction.IDLE),
                new Node(8, 10, Direction.IDLE),
                new Node(0, 11, Direction.RIGHT),
                new Node(11, 11, Direction.RIGHT),
            };
            var blocks = new List<Position>
            {
                new Position(0, 0),
                new Position(1, 0),
                new Position(8, 0),
                new Position(9, 0),
                new Position(0, 1),
                new Position(1, 1),
                new Position(7, 1),
                new Position(9, 1),
                new Position(10, 1),
                new Position(2, 2),
                new Position(6, 2),
                new Position(9, 2),
                new Position(10, 2),
                new Position(11, 2),
                new Position(12, 2),
                new Position(13, 2),
                new Position(14, 2),
                new Position(15, 2),
                new Position(3, 3),
                new Position(5, 3),
                new Position(9, 3),
                new Position(10, 3),
                new Position(15, 3),
                new Position(9, 4),
                new Position(14, 4),
                new Position(3, 5),
                new Position(5, 5),
                new Position(6, 5),
                new Position(7, 5),
                new Position(8, 5),
                new Position(9, 5),
                new Position(13, 5),
                new Position(2, 6),
                new Position(6, 6),
                new Position(10, 6),
                new Position(12, 6),
                new Position(1, 7),
                new Position(6, 7),
                new Position(0, 8),
                new Position(6, 8),
                new Position(7, 8),
                new Position(10, 8),
                new Position(12, 8),
                new Position(0, 9),
                new Position(1, 9),
                new Position(2, 9),
                new Position(3, 9),
                new Position(4, 9),
                new Position(5, 9),
                new Position(6, 9),
                new Position(8, 9),
                new Position(9, 9),
                new Position(13, 9),
                new Position(9, 10),
                new Position(14, 10),
                new Position(15, 10),
                new Position(10, 11),
                new Position(14, 11),
            };
            var gameEngine = new GameEngine(16, 12, 99, 10, nodes, blocks);
            gameEngine.TestPlayer();
        }


        [Test]
        public void _custom_test_split()
        {
            var nodes = new List<Node>
            {
                new Node(4, 2, Direction.LEFT),
                new Node(9, 1, Direction.RIGHT),
                new Node(10, 6, Direction.UP),
            };
            var blocks = new List<Position>
            {
                new Position(7, 0),
                new Position(7, 1),
                new Position(7, 2),
                new Position(7, 3),
                new Position(7, 4),
                new Position(7, 5),
                new Position(7, 6),
                new Position(7, 7),
                new Position(7, 8),
            };
            var gameEngine = new GameEngine(12, 9, 60, 2, nodes, blocks);
            gameEngine.TestPlayer();
        }
    }
}