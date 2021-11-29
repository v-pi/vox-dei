using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace VoxDei
{
    public class Player
    {
        public static void Main(string[] args)
        {
            var v = new WilliamRockwood();
            v.Play();
        }
    }

    public class WilliamRockwood
    {
        #region Properties

        public int Width;
        public int Height;
        public IoManager IoManager;

        public WilliamRockwood()
        {
            IoManager = new IoManager(null);
        }

        public WilliamRockwood(IIoProvider inputProvider)
        {
            IoManager = new IoManager(inputProvider);
        }

        #endregion Properties

        public void Play()
        {
            IoManager.ParseParameters(out Width, out Height);
            Map firstMap = IoManager.ParseTurn(Height);
            firstMap.RoundsLeft = Math.Min(firstMap.RoundsLeft, 50);
            int turnsToUnderstandMoves = 0;

            var nodesWithDirections = new List<Node>();
            var nodesWithoutDirections = firstMap.Nodes;

            var maps = new List<Map>() { firstMap };
            while (nodesWithoutDirections.Count > 0)
            {
                turnsToUnderstandMoves++;
                IoManager.ExecuteCommand(null);
                maps.Add(IoManager.ParseTurn(Height));
                Stopwatch sw1 = Stopwatch.StartNew();

                Scout.TryToUnderstandMoves(maps, nodesWithoutDirections, nodesWithDirections);
                sw1.Stop();
                Logger.Log($"Understood map (or not) in {sw1.ElapsedMilliseconds}ms");
            }
            firstMap.Nodes = nodesWithDirections;
            for (int ii = 0; ii < turnsToUnderstandMoves; ii++)
            {
                firstMap = firstMap.GetNextMap();
            }

            //ExecuteCommand(null); // WAIT one last time, we're gonna need all the time we can get to compute the solution
            //ParseTurn();

            var split = MapSplitter.Split(firstMap);
            while (split.Count > 1)
            {
                Stopwatch sw2 = Stopwatch.StartNew();
                List<Position?> positionsToBomb = Strategist.PositionsToBomb(split);
                sw2.Stop();
                Logger.Log($"Found a series of command in {sw2.ElapsedMilliseconds}ms");
                foreach (Position? positionToBomb in positionsToBomb)
                {
                    IoManager.ExecuteCommand(positionToBomb);
                    Map _ = IoManager.ParseTurn(Height);
                }
            }
            Stopwatch sw3 = Stopwatch.StartNew();
            var endSequence = Strategist.PositionsToBomb(split.Single());
            endSequence.AddRange(Enumerable.Repeat<Position?>(null, Constants.BOMB_TIMEOUT));
            foreach (Position? positionToBomb in endSequence)
            {
                IoManager.ExecuteCommand(positionToBomb);
                Map _ = IoManager.ParseTurn(Height);
            }
            sw3.Stop();
            Logger.Log($"Found the end sequence in {sw3.ElapsedMilliseconds}ms");

        }
    }

    public static class MapSplitter
    {
        public static List<Map> Split(Map map)
        {
            var emptyCells = new HashSet<Position>();
            for (int col = 0; col < map.Width; col++)
            {
                for (int row = 0; row < map.Height; row++)
                {
                    if (!map.Blocks[col, row])
                    {
                        emptyCells.Add(new Position(col, row));
                    }
                }
            }
            var zones = new List<HashSet<Position>>();
            while (emptyCells.Count > 0)
            {
                var zone = new HashSet<Position>();
                var currentCell = emptyCells.First();
                var toExplore = new Queue<Position>();
                toExplore.Enqueue(currentCell);
                emptyCells.Remove(currentCell);
                zone.Add(currentCell);
                while (toExplore.TryDequeue(out currentCell))
                {
                    foreach (var direction in Enum.GetValues(typeof(Direction)))
                    {
                        var neighbour = Map.UnsafeNextPosition(currentCell, (Direction)direction);
                        if (emptyCells.Remove(neighbour))
                        {
                            zone.Add(neighbour);
                            toExplore.Enqueue(neighbour);
                        }
                    }
                }
                zones.Add(zone);
            }
            var split = new List<Map>();
            foreach (var zone in zones)
            {
                var splitMap = new Map(map.Height, map.Width)
                {
                    Blocks = map.Blocks,
                    BombsLeft = map.BombsLeft,
                    RoundsLeft = map.RoundsLeft,
                };

                map.Nodes.Where(n => zone.Contains(n.Position)).ToList().ForEach(n => splitMap.AddNode(n.Position, n.Direction));
                if (splitMap.Nodes.Count > 0)
                {
                    split.Add(splitMap);
                }
            }
            return split;
        }
    }

    public static class Strategist
    {
        public static List<Position?> PositionsToBomb(List<Map> maps)
        {
            var maxBomb = maps[0].BombsLeft;
            var totalRounds = maps[0].RoundsLeft;
            int bombsUsed;
            KeyValuePair<Map, List<Position?>> bestCandidate = new KeyValuePair<Map, List<Position?>>();

            for (bombsUsed = 1; bombsUsed < maxBomb; bombsUsed++)
            {
                var candidates = new Dictionary<Map, List<Position?>>();
                foreach (var map in maps)
                {
                    map.BombsLeft = bombsUsed;
                    map.RoundsLeft = totalRounds / maps.Count;
                    var sw = Stopwatch.StartNew();
                    var commands = PositionsToBomb(map);
                    Logger.Log($"PositionsToBomb in {sw.ElapsedMilliseconds}ms");
                    if (commands != null)
                    {
                        candidates[map] = commands;
                    }
                }
                if (candidates.Count > 0)
                {
                    bestCandidate = candidates.OrderBy(kv => kv.Value.Count).First();
                    maps.Remove(bestCandidate.Key);
                    break;
                }
            }
            for (int ii = 0; ii < maps.Count; ii++)
            {
                for (int jj = 0; jj < bestCandidate.Value.Count; jj++)
                {
                    maps[ii] = maps[ii].GetNextMap();
                }
                maps[ii].BombsLeft = maxBomb - bombsUsed;
                maps[ii].RoundsLeft = totalRounds - bestCandidate.Value.Count;
            }

            return bestCandidate.Value;
        }

        public static List<Position?> PositionsToBomb(Map map)
        {
            var allNodesKey = (int)Math.Pow(2, map.Nodes.Count) - 1;
            var bombsLeft = map.BombsLeft;
            var allFutureMaps = GetAllFutureMaps(map);
            var allFutureNodePositions = GetAllFutureNodePositions(allFutureMaps);
            // si il reste autant de bombes que de nodes => yolo
            // sinon, on divise le nombre de nodes par rapport au nombre de bombes
            // la première bombe doit détruire au moins autant de bombes (arrondi à l'entier supérieur)
            // ensuite on recommence au début

            // pour chaque tour, on calcule pour chaque case les dégâts
            // on trie par ordre décroissant pour chaque tour les dégâts
            // les dégâts doivent être stockés dans un entier pour faire des opérations binaires
            // on fait un parcours d'arbre sur ces éléments triés.

            // TODO : start performance profiler for the lulz
            // split problem !!
            var positionsToBomb = WalkTree(allFutureMaps, 0, 0, allNodesKey, allFutureNodePositions, bombsLeft, new Position?[allFutureMaps.Length]);
            return positionsToBomb;
        }

        private static int[][,] GetAllFutureNodePositions(Map[] allFutureMaps)
        {
            var width = allFutureMaps[0].Width;
            var height = allFutureMaps[0].Height;
            var allFutureNodePositions = new int[allFutureMaps.Length][,];
            for (int ii = 0; ii < allFutureMaps.Length; ii++)
            {
                var futureMap = allFutureMaps[ii];
                allFutureNodePositions[ii] = new int[width, height];
                foreach (var node in futureMap.Nodes)
                {
                    allFutureNodePositions[ii][node.Position.Col, node.Position.Row] |= node.Id;
                }
            }
            return allFutureNodePositions;
        }

        private static List<Position?> WalkTree(Map[] allFutureMaps, int index, int destroyedNodes, int allNodesKey, int[][,] allFutureNodePositions, int bombsLeft, Position?[] plantedBombs)
        {
            // check that no node is there when we want to drop the bomb
            // check that there are no chain explosions
            // TODO : replace all foreach with for
            // TODO : replace all lists with arrays
            var futureRound = index + Constants.BOMB_TIMEOUT;
            if (futureRound >= allFutureMaps.Length)
            {
                return null;
            }

            // DONE : si la meilleure des BestOption sur tous les rounds restants est inférieure au nombre de nodes restante divisé par le nombre de bombes restantes, on peut renvoyer null
            // TODO : hardcore should continue : check best options regarding allready destroyedNodes
            int remainingBestOption = 0;
            for (int ii = futureRound; ii < allFutureMaps.Length; ii++)
            {
                var bestOptions = allFutureMaps[ii].BestOptions;
                if (bestOptions.Count > 0)
                {
                    remainingBestOption = Math.Max(remainingBestOption, bestOptions[0].Count);
                }
            }
            var remainingNodesKey = allNodesKey & ~destroyedNodes;
            var remainingNodesCount = (float)Utils.CountSetBits(remainingNodesKey);
            if (remainingBestOption < Math.Ceiling(remainingNodesCount / bombsLeft))
            {
                return null;
            }
            var currentNodePositions = allFutureNodePositions[index];
            var futureMap = allFutureMaps[futureRound];
            List<Position> forbiddenPositions = null;
            for (int ii = 0; ii < Constants.BOMB_TIMEOUT; ii++)
            {
                if (plantedBombs[index + ii].HasValue)
                {
                    if (forbiddenPositions == null)
                    {
                        forbiddenPositions = new List<Position>();
                    }
                    forbiddenPositions.Add(plantedBombs[index + ii].Value);
                }
            }
            List<Position?> commands;
            foreach (var option in futureMap.BestOptions)
            {
                var boom = option.Position.Value;
                if (forbiddenPositions != null)
                {
                    var mightGetChainTriggered = false;
                    foreach (var position in forbiddenPositions)
                    {
                        if (position.Col == boom.Col || position.Row == boom.Row)
                        {
                            mightGetChainTriggered = true;
                            break;
                        }
                    }
                    if (mightGetChainTriggered)
                    {
                        continue;
                    }
                }
                // would be better to check if the node is the node is still alive, but we immediately mark it as dead even though it dies in 3 rounds
                //if ((currentNodePositions[boom.Col, boom.Row] & ~destroyedNodes) != 0)
                if (currentNodePositions[boom.Col, boom.Row] != 0)
                {
                    continue;
                }
                var nextDestroyedNodes = destroyedNodes | option.NodesKey;
                if (nextDestroyedNodes == destroyedNodes) // this bomb was useless, only destroying already dead nodes
                {
                    continue;
                }
                if (nextDestroyedNodes == allNodesKey)
                {
                    return new List<Position?> { option.Position };
                }
                if (bombsLeft == 1) // this was out last bomb but we did not destroy everything... Wrong path
                {
                    continue;
                }
                var nextPlantedBomds = plantedBombs.ToArray();
                nextPlantedBomds[futureRound] = boom;
                commands = WalkTree(allFutureMaps, index + 1, nextDestroyedNodes, allNodesKey, allFutureNodePositions, bombsLeft - 1, nextPlantedBomds);
                if (commands != null)
                {
                    commands.Insert(0, option.Position);
                    return commands;
                }
            }
            commands = WalkTree(allFutureMaps, index + 1, destroyedNodes, allNodesKey, allFutureNodePositions, bombsLeft, plantedBombs);
            if (commands != null)
            {
                commands.Insert(0, null);
                return commands;
            }
            return commands;
        }

        private static Map[] GetAllFutureMaps(Map firstMap)
        {
            var totalRounds = firstMap.RoundsLeft;
            var allFutureMaps = new Map[totalRounds];
            var currentMap = firstMap;
            while (currentMap.RoundsLeft > 0)
            {
                allFutureMaps[totalRounds - currentMap.RoundsLeft] = currentMap;
                currentMap = currentMap.GetNextMap();
                currentMap.ComputePotentialDamage();
            }
            //RemoveInferiorOptions(allFutureMaps);
            for (int ii = 0; ii < allFutureMaps.Length; ii++)
            {
                var futureMap = allFutureMaps[ii];
                var potentialDamage = futureMap.ComputePotentialDamage();
                Map mapWithNodes = null;
                if (ii >= 3)
                {
                    mapWithNodes = allFutureMaps[ii - Constants.BOMB_TIMEOUT];
                }
                futureMap.GetDistinctOptions(potentialDamage, mapWithNodes);
            }

            return allFutureMaps;
        }

        private static void RemoveInferiorOptions(Map[] allFutureMaps)
        {
            const int turnsToExplore = 1;
            for (int ii = 0; ii < allFutureMaps.Length; ii++)
            {
                var map = allFutureMaps[ii];
                var otherTurnOptions = new HashSet<int>();
                for (int jj = Math.Max(0, ii - turnsToExplore); jj < Math.Min(allFutureMaps.Length, ii + turnsToExplore); jj++)
                {
                    foreach (var option in allFutureMaps[jj].BestOptions)
                    {
                        otherTurnOptions.Add(option.NodesKey);
                    }
                }
                for (int kk = 0; kk < map.BestOptions.Count;)
                {
                    var bestOption = map.BestOptions[kk];
                    if (otherTurnOptions.Any(o => o.IsStrictlyBetterOption(bestOption.NodesKey)))
                    {
                        map.BestOptions.RemoveAt(kk);
                    }
                    else
                    {
                        kk++;
                    }
                }
            }
        }
    }

    public class IoManager
    {
        public IIoProvider IoProvider;

        public IoManager(IIoProvider ioProvider)
        {
            IoProvider = ioProvider;
        }

        public void ExecuteCommand(Position? position)
        {
            if (position.HasValue)
                WriteLine(position.Value.ToString());
            else
                WriteLine("WAIT");
        }

        public void ParseParameters(out int width, out int height)
        {
            string[] inputs = ReadLine().Split(' ');
            width = int.Parse(inputs[0]); // width of the firewall grid
            height = int.Parse(inputs[1]); // height of the firewall grid
        }

        public Map ParseTurn(int height)
        {
            var lines = new string[height + 1];
            for (int ii = 0; ii <= height; ii++)
            {
                lines[ii] = ReadLine();
            }
            return new Map(lines);
        }

        public string ReadLine()
        {
            if (IoProvider != null)
            {
                var input = IoProvider.ReadLine();
                Logger.Log(input);
                return input;
            }
            else
            {
                var input = Console.ReadLine();
                Logger.Log(input);
                return input;
            }
        }

        public void WriteLine(string s)
        {
            if (IoProvider != null)
            {
                IoProvider.WriteLine(s);
            }
            else
            {
                Console.WriteLine(s);
            }
        }
    }

    public static class Scout
    {

        public static void TryToUnderstandMoves(List<Map> maps, List<Node> nodesWithoutDirections, List<Node> nodesWithDirections)
        {
            if (maps.Count < 2) return;

            for (int ii = 0; ii < nodesWithoutDirections.Count; ii++)
            {
                Node node = nodesWithoutDirections[ii];
                var matchingDirection = GetMatchingDirection(maps, node);
                if (matchingDirection != null)
                {
                    node.Direction = matchingDirection.Value;
                    nodesWithDirections.Add(node);
                    nodesWithoutDirections.RemoveAt(ii);
                    ii--;
                }
            }
        }

        public static Direction? GetMatchingDirection(List<Map> maps, Node node)
        {
            Direction? matchingDirection = null;
            var firstMap = maps[0];
            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                if (node.Position.Row == 0 && direction == Direction.UP) continue;
                if (node.Position.Row == firstMap.Height - 1 && direction == Direction.DOWN) continue;
                if (node.Position.Col == 0 && direction == Direction.LEFT) continue;
                if (node.Position.Col == firstMap.Width - 1 && direction == Direction.RIGHT) continue;
                if (direction == Direction.UP && firstMap.Blocks[node.Position.Col, node.Position.Row - 1]) continue;
                if (direction == Direction.DOWN && firstMap.Blocks[node.Position.Col, node.Position.Row + 1]) continue;
                if (direction == Direction.LEFT && firstMap.Blocks[node.Position.Col - 1, node.Position.Row]) continue;
                if (direction == Direction.RIGHT && firstMap.Blocks[node.Position.Col + 1, node.Position.Row]) continue;


                if (DirectionMatches(node, direction, maps))
                {
                    if (matchingDirection.HasValue)
                        return null;
                    else
                        matchingDirection = direction;
                }
            }
            return matchingDirection;
        }

        public static bool DirectionMatches(Node node, Direction direction, List<Map> maps)
        {
            var hypotheticalNode = node.Clone();
            hypotheticalNode.Direction = direction;
            for (int ii = 1; ii < maps.Count; ii++)
            {
                Map map = maps[ii];
                hypotheticalNode = map.SafeNextPosition(hypotheticalNode);
                if (!map.ContainsAnyNode(hypotheticalNode.Position.Col, hypotheticalNode.Position.Row))
                    return false;
            }
            return true;
        }
    }

    public class Map
    {
        public int BombsLeft;

        public int RoundsLeft;

        public int Height;

        public int Width;

        public bool[,] Blocks;

        public List<Node> Nodes = new List<Node>();

        public List<PositionAndDamage> BestOptions;

        private int _nodeCount;
        private int NodeCount
        {
            get
            {
                return _nodeCount++;
            }
        }

        public Map(string[] lines)
        {
            string[] inputs = lines[0].Split(' ');
            Width = lines[1].Length;
            Height = lines.Length - 1;
            Blocks = new bool[Width, Height];

            for (int row = 0; row < Height; row++)
            {
                string mapRow = lines[row + 1]; // one line of the firewall grid
                for (int col = 0; col < Width; col++)
                {
                    switch (mapRow[col])
                    {
                        case '.':
                            break;
                        case '#':
                            Blocks[col, row] = true;
                            break;
                        case '@':
                            AddNode(col, row);
                            break;
                        default:
                            break;
                    }
                }
            }

            RoundsLeft = int.Parse(inputs[0]); // number of rounds left before the end of the game
            BombsLeft = int.Parse(inputs[1]); // number of bombs left
        }

        public void AddNode(int col, int row)
        {
            var position = new Position(col, row);
            AddNode(position);
        }

        public void AddNode(Position position, Direction direction = default(Direction))
        {
            Nodes.Add(new Node() { Id = (int)Math.Pow(2, NodeCount), Position = position, Direction = direction });
        }

        public Map(int height, int width)
        {
            Height = height;
            Width = width;
            Blocks = new bool[width, height];
        }

        public Map GetNextMap(Position?[] explosions, int turnCounter)
        {
            var impacts = GetImpacts(explosions, turnCounter);
            return GetNextMap(impacts);
        }

        public Map GetNextMap()
        {
            return GetNextMap(new List<Position>());
        }

        private Map GetNextMap(List<Position> impacts)
        {
            var nextMap = (Map)MemberwiseClone();
            nextMap.RoundsLeft--;
            nextMap.Nodes = GetUpdatedNodes(Nodes, impacts).ToList();

            return nextMap;
        }

        private IEnumerable<Node> GetUpdatedNodes(IEnumerable<Node> nodes, List<Position> impacts)
        {
            foreach (var n in nodes)
            {
                Node node = SafeNextPosition(n);
                if (!impacts.Contains(node.Position))
                {
                    yield return node;
                }
            }
        }

        public Node SafeNextPosition(Node n)
        {
            var node = n.Clone();
            switch (node.Direction)
            {
                case Direction.IDLE:
                    break;
                default:
                    if (IsFreeCell(node.Position, node.Direction, out var newPosition))
                    {
                        node.Position = newPosition;
                    }
                    else
                    {
                        var newDirection = (Direction)(((int)(node.Direction + 2)) % 4);
                        node.Direction = newDirection;
                        node.Position = UnsafeNextPosition(node.Position, newDirection);
                    }
                    break;
            }

            return node;
        }

        public List<Position> GetImpacts(Position?[] explosions, int turnCounter)
        {
            var impacts = new List<Position>();
            if (explosions == null)
            {
                return impacts;
            }
            var explosion = explosions[turnCounter];
            explosions[turnCounter] = null;
            impacts.AddRange(GetImpacts(explosion));
            var chainReaction = true;
            while (chainReaction)
            {
                chainReaction = false;
                for (int ii = turnCounter + 1; ii <= turnCounter + Constants.BOMB_TIMEOUT; ii++)
                {
                    if (explosions[ii].HasValue && impacts.Contains(explosions[ii].Value))
                    {
                        chainReaction = true;
                        impacts.AddRange(GetImpacts(explosions[ii].Value));
                        explosions[ii] = null;
                    }
                }
            }

            return impacts;
        }

        public List<Position> GetImpacts(Position? explosion)
        {
            var impacts = new List<Position>(13);
            if (explosion != null)
            {
                var boom = explosion.Value;
                impacts.Add(boom);
                for (int ii = boom.Col + 1; ii <= boom.Col + Constants.BOMB_RANGE && ii < Width; ii++)
                {
                    if (Blocks[ii, boom.Row]) break;
                    impacts.Add(new Position(ii, boom.Row));
                }
                for (int ii = boom.Col - 1; ii >= boom.Col - Constants.BOMB_RANGE && ii >= 0; ii--)
                {
                    if (Blocks[ii, boom.Row]) break;
                    impacts.Add(new Position(ii, boom.Row));
                }
                for (int ii = boom.Row + 1; ii <= boom.Row + Constants.BOMB_RANGE && ii < Height; ii++)
                {
                    if (Blocks[boom.Col, ii]) break;
                    impacts.Add(new Position(boom.Col, ii));
                }
                for (int ii = boom.Row - 1; ii >= boom.Row - Constants.BOMB_RANGE && ii >= 0; ii--)
                {
                    if (Blocks[boom.Col, ii]) break;
                    impacts.Add(new Position(boom.Col, ii));
                }
            }
            return impacts;
        }

        public bool ContainsAnyNode(int col, int row)
        {
            for (int ii = 0; ii < Nodes.Count; ii++)
            {
                Node node = Nodes[ii];
                if (node.Position.Col == col && node.Position.Row == row) return true;
            }
            return false;
        }

        public bool IsFreeCell(Position position, Direction direction, out Position newPosition)
        {
            newPosition = UnsafeNextPosition(position, direction);
            return newPosition.Col >= 0 && newPosition.Col < Width && newPosition.Row >= 0 && newPosition.Row < Height && !Blocks[newPosition.Col, newPosition.Row];
        }

        public static Position UnsafeNextPosition(Position position, Direction direction)
        {
            return new Position(
                position.Col + (((int)direction - 2) % 2),
                position.Row + (((int)direction - 1) % 2)
                );
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{RoundsLeft} {BombsLeft}");

            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    if (ContainsAnyNode(col, row))
                        sb.Append('@');
                    else if (Blocks[col, row])
                        sb.Append('#');
                    else
                        sb.Append('.');

                }
                sb.Append(Environment.NewLine);
            }
            return sb.ToString().Trim('\r', '\n');
        }

        public List<PositionAndDamage> ComputePotentialDamage()
        {
            var potentialDamagedNodes = new List<PositionAndDamage>();
            for (int col = 0; col < Width; col++)
            {
                for (int row = 0; row < Height; row++)
                {
                    if (!Blocks[col, row])
                    {
                        var position = new Position(col, row);
                        var impacts = GetImpacts(position).ToArray();

                        var aggDestroyed = 0;
                        foreach (var node in Nodes)
                        {
                            foreach (var impact in impacts)
                            {
                                if (node.Position.Col == impact.Col && node.Position.Row == impact.Row)
                                {
                                    aggDestroyed |= node.Id;
                                }
                            }
                        }
                        if (aggDestroyed != 0)
                        {
                            var pad = new PositionAndDamage(aggDestroyed, position);
                            potentialDamagedNodes.Add(pad);
                        }
                    }
                }
            }

            return potentialDamagedNodes;
        }

        public void GetDistinctOptions(List<PositionAndDamage> potentialDamagedNodes, Map mapWithNodes)
        {
            BestOptions = new List<PositionAndDamage>();
            var options = potentialDamagedNodes.OrderByDescending(pad => pad.Count).ToList();
            foreach (var option in options)
            {
                var isAmongTheBest = true;
                for (var ii = 0; ii < BestOptions.Count; ii++)
                {
                    var bestOption = BestOptions[ii];
                    if (bestOption.NodesKey == option.NodesKey)
                    {
                        if (mapWithNodes != null && mapWithNodes.Nodes.Any(n => n.Position == bestOption.Position))
                        {
                            BestOptions[ii] = option;
                            break;
                        }
                    }
                    if (bestOption.IsBetterThan(option))
                    {
                        isAmongTheBest = false;
                        break;
                    }
                }

                if (isAmongTheBest)
                {
                    BestOptions.Add(option);
                }
            }
        }
    }

    public static class Utils
    {
        /// <summary>
        /// Function to get no of set bits in binary representation of passed binary number
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public static int CountSetBits(int n)
        {
            int count = 0;
            while (n > 0)
            {
                n &= (n - 1);
                count++;
            }
            return count;
        }

        public static bool IsBetterOption(this int nk1, int nk2)
        {
            return (nk1 | nk2) == nk1;
        }

        public static bool IsStrictlyBetterOption(this int nk1, int nk2)
        {
            return (nk1 | nk2) == nk1 && nk1 > nk2;
        }
    }

    public struct PositionAndDamage
    {
        public PositionAndDamage(int nodesKey, Position position)
        {
            NodesKey = nodesKey;
            Position = position;
            Count = GetDamageCount(NodesKey);
        }

        public int Count;

        public int NodesKey;

        public Position? Position;

        private static int GetDamageCount(int value)
        {
            return Utils.CountSetBits(value);
        }

        public bool IsBetterThan(PositionAndDamage option)
        {
            return this.NodesKey.IsBetterOption(option.NodesKey);
        }
    }

    public struct Node
    {
        public int Id;

        public Direction Direction;

        public Position Position;

        private static int NodeCount;

        public Node(int col, int row, Direction direction)
        {
            Id = (int)Math.Pow(2, NodeCount);
            Direction = direction;
            Position = new Position(col, row);
            NodeCount++;
        }

        public Node Clone()
        {
            return (Node)MemberwiseClone();
        }
    }

    public enum Direction
    {
        UP,
        LEFT,
        DOWN,
        RIGHT,
        IDLE
    }

    public struct Position
    {
        public int Row;

        public int Col;

        public Position(int col, int row)
        {
            Col = col;
            Row = row;
        }

        public Position(string s)
        {
            var split = s.Split(' ');
            Col = Int32.Parse(split[0]);
            Row = Int32.Parse(split[1]);
        }

        public override string ToString()
        {
            return Col + " " + Row;
        }

        public static bool operator ==(Position p1, Position p2)
        {
            return p1.Col == p2.Col && p1.Row == p2.Row;
        }

        public static bool operator !=(Position p1, Position p2)
        {
            return !(p1 == p2);
        }

        public override bool Equals(object obj)
        {
            return (Position)obj == this;
        }

        public override int GetHashCode()
        {
            return 23 * Col + Row; // even unique since Col < 20 and Row < 20
        }
    }

    public static class Logger
    {
        public static bool StandardLog;

        public static void Log(string message)
        {
            if (StandardLog)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.Error.WriteLine(message);
            }
        }

        public static void Log(string messageFormat, params object[] values)
        {
            Log(String.Format(messageFormat, values));
        }
    }

    public static class Constants
    {
        public const int BOMB_RANGE = 3;
        public const int BOMB_TIMEOUT = 3;
    }

    public interface IIoProvider
    {
        string ReadLine();

        void WriteLine(string s);
    }
}