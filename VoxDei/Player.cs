using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

public class Player
{

    // TODO [NECESSARY]: Débugger la compréhension de la map pour Vandalisme
    // TODO [NECESSARY]: Quand la séquence est finie, recommencer ?
    // TODO [NECESSARY]: Débugger La barre de fer -> pourquoi on pose une bombe sur une node -_-
    // TODO [NECESSARY]: Débugger 4 noeuds furtifs une bombe -> essayer de supprimer le temps d'attente après la compréhension de la map ?
    // TODO [LOW PRIORITY]: Modéliser le déclenchement en série des bombes
    // TODO [LOW PRIORITY]: Vérifier le temps d'amorçage des bombes ? (seulement 2 tours)
    // 100ms... trololol, on est déjà à 200 sur le premier

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
        int turnsToUnderstandMoves = 0;

        List<Node> nodesWithDirections = new List<Node>();
        List<Node> nodesWithoutDirections = firstMap.LiveNodes;

        List<Map> maps = new List<Map>() { firstMap };
        while (nodesWithoutDirections.Count > 0)
        {
            turnsToUnderstandMoves++;
            IoManager.ExecuteCommand(null);
            maps.Add(IoManager.ParseTurn(Height));
            Stopwatch sw1 = Stopwatch.StartNew();

            Scout.TryToUnderstandMoves(maps, nodesWithoutDirections, nodesWithDirections);
            sw1.Stop();
            Logger.Log("Understood map (or not) in {0}ms", sw1.ElapsedMilliseconds);
        }
        firstMap.LiveNodes = nodesWithDirections;
        for (int ii = 0; ii < turnsToUnderstandMoves; ii++)
        {
            firstMap = firstMap.GetNextMap();
        }

        //ExecuteCommand(null); // WAIT one last time, we're gonna need all the time we can get to compute the solution
        //ParseTurn();

        Stopwatch sw2 = Stopwatch.StartNew();
        var strategist = new Strategist2();
        //var strategist = new Strategist(firstMap);
        List<Position?> positionsToBomb = strategist.PositionsToBomb(firstMap);
        sw2.Stop();
        Logger.Log("Found a series of command in {0}ms", sw2.ElapsedMilliseconds);

        foreach (Position? positionToBomb in positionsToBomb)
        {
            IoManager.ExecuteCommand(positionToBomb);
            Map _ = IoManager.ParseTurn(Height);
        }
    }
}

public class Strategist2
{
    public List<Position?> PositionsToBomb(Map map)
    {
        var allNodesKey = (int)Math.Pow(2, map.LiveNodes.Count) - 1;
        var bombsLeft = map.BombsLeft;
        //var turnsLeft = map.RoundsLeft - Constants.BOMB_TIMEOUT;
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
        var positionsToBomb = WalkTree(allFutureMaps, 0, 0, allNodesKey, allFutureNodePositions, bombsLeft, new Position?[allFutureMaps.Length]);
        positionsToBomb.AddRange(Enumerable.Repeat<Position?>(null, Constants.BOMB_TIMEOUT));
        return positionsToBomb;
    }

    private int[][,] GetAllFutureNodePositions(Map[] allFutureMaps)
    {
        var width = allFutureMaps[0].Width;
        var height = allFutureMaps[0].Height;
        var allFutureNodePositions = new int[allFutureMaps.Length][,];
        for (int ii = 0; ii < allFutureMaps.Length; ii++)
        {
            var futureMap = allFutureMaps[ii];
            allFutureNodePositions[ii] = new int[width, height];
            foreach (var node in futureMap.LiveNodes)
            {
                allFutureNodePositions[ii][node.Position.Col, node.Position.Row] |= node.Id;
            }
        }
        return allFutureNodePositions;
    }

    private List<Position?> WalkTree(Map[] allFutureMaps, int index, int destroyedNodes, int allNodesKey, int[][,] allFutureNodePositions, int bombsLeft, Position?[] plantedBombs)
    {
        // check that no node is there when we want to drop the bomb
        // check that there are no chain explosions
        var futureRound = index + Constants.BOMB_TIMEOUT;
        if (futureRound >= allFutureMaps.Length)
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

    private Map[] GetAllFutureMaps(Map map)
    {
        var totalRounds = map.RoundsLeft;
        map.ComputePotentialDamage();
        var allFutureMaps = new Map[totalRounds];
        while (map.RoundsLeft > 0)
        {
            allFutureMaps[totalRounds - map.RoundsLeft] = map;
            map = map.GetNextMap();
            map.ComputePotentialDamage();
        }
        return allFutureMaps;
    }
}

public class Strategist
{
    public Dictionary<Direction, Node>[,] NextTurnPositions;

    public Strategist(Map map)
    {
        NextTurnPositions = GetFuturePositions(map, 1);
    }

    public Dictionary<Direction, Node>[,] GetFuturePositions(Map map, int depthIntoTheFuture)
    {
        var result = new Dictionary<Direction, Node>[map.Width, map.Height];

        for (int col = 0; col < map.Width; col++)
        {
            for (int row = 0; row < map.Height; row++)
            {
                var localFuturePositions = new Dictionary<Direction, Node>();
                localFuturePositions[Direction.IDLE] = ComputeFuturePosition(map, col, row, Direction.IDLE, depthIntoTheFuture);
                localFuturePositions[Direction.UP] = ComputeFuturePosition(map, col, row, Direction.UP, depthIntoTheFuture);
                localFuturePositions[Direction.DOWN] = ComputeFuturePosition(map, col, row, Direction.DOWN, depthIntoTheFuture);
                localFuturePositions[Direction.LEFT] = ComputeFuturePosition(map, col, row, Direction.LEFT, depthIntoTheFuture);
                localFuturePositions[Direction.RIGHT] = ComputeFuturePosition(map, col, row, Direction.RIGHT, depthIntoTheFuture);

                result[col, row] = localFuturePositions;
            }
        }
        return result;
    }

    public Node ComputeFuturePosition(Map map, int col, int row, Direction direction, int depthIntoTheFuture)
    {
        try
        {
            int resultCol = col;
            int resultRow = row;

            int colIncr = 0;
            int rowIncr = 0;

            switch (direction)
            {
                case Direction.UP:
                    rowIncr--;
                    break;
                case Direction.DOWN:
                    rowIncr++;
                    break;
                case Direction.LEFT:
                    colIncr--;
                    break;
                case Direction.RIGHT:
                    colIncr++;
                    break;
                default:
                    break;
            }
            for (int ii = 0; ii < depthIntoTheFuture; ii++)
            {
                if (resultCol + colIncr == -1 || resultCol + colIncr == map.Width || map.Blocks[resultCol + colIncr, resultRow])
                    colIncr *= -1;
                if (resultRow + rowIncr == -1 || resultRow + rowIncr == map.Height || map.Blocks[resultCol, resultRow + rowIncr])
                    rowIncr *= -1;
                resultCol += colIncr;
                resultRow += rowIncr;
            }
            Position position = new Position(resultCol, resultRow);
            return new Node()
            {
                Position = position,
                Direction = GetDirection(colIncr, rowIncr)
            };
        }
        catch (IndexOutOfRangeException)
        {
            // That is so fucking lazy, I'm not even ashamed
            return new Node();
        }
    }

    public Direction GetDirection(int colIncr, int rowIncr)
    {
        if (rowIncr == -1) return Direction.UP;
        if (rowIncr == 1) return Direction.DOWN;
        if (colIncr == -1) return Direction.LEFT;
        if (colIncr == 1) return Direction.RIGHT;
        return Direction.IDLE;
    }

    public List<Position?> PositionsToBomb(Map map)
    {
        var commandsAndAssociatedDamage = GetPositionsAndAssociatedDamage(map);

        List<Position> positionsToBomb = commandsAndAssociatedDamage.Keys.ToList();
        positionsToBomb.Sort((p1, p2) =>
            commandsAndAssociatedDamage[p2].Count.CompareTo(
            commandsAndAssociatedDamage[p1].Count));

        foreach (Position positionToBomb in positionsToBomb)
        {
            if (map.LiveNodes.Count == commandsAndAssociatedDamage[positionToBomb].Count)
                return new List<Position?>() { positionToBomb, null, null, null };
            Map nextMap = GetNextMap(map, commandsAndAssociatedDamage[positionToBomb]);

            if (nextMap.BombsLeft == 0 || nextMap.RoundsLeft == 0)
                continue;

            List<Position?> result = PositionsToBomb(nextMap);
            if (result != null)
            {
                result.Insert(0, positionToBomb);
                return result;
            }
        }
        // Case WAIT
        {
            Map nextMap = GetNextMap(map, new List<int>());

            if (nextMap.RoundsLeft > 0)
            {
                List<Position?> result = PositionsToBomb(nextMap);
                if (result != null)
                {
                    result.Insert(0, null);
                    return result;
                }
            }
        }

        return null;
    }

    public Map GetNextMap(Map map, List<int> positionsHurt)
    {
        Map nextMap = new Map(map.Height, map.Width)
        {
            RoundsLeft = map.RoundsLeft - 1,
            BombsLeft = map.BombsLeft - (positionsHurt.Count > 0 ? 1 : 0),
        };

        for (int ii = 0; ii < map.LiveNodes.Count; ii++)
        {
            Node node = map.LiveNodes[ii];
            Node newNode = NextTurnPositions[node.Position.Col, node.Position.Row][node.Direction];
            newNode.Id = node.Id;
            if (positionsHurt.Count > 0)
                ProcessNode(newNode, positionsHurt, nextMap);
            else
                nextMap.LiveNodes.Add(newNode);
        }
        for (int ii = 0; ii < map.NodesAboutToDie.Count; ii++)
        {
            Node node = map.NodesAboutToDie[ii];
            Node newNode = NextTurnPositions[node.Position.Col, node.Position.Row][node.Direction];
            newNode.Id = node.Id;
            if (newNode.TimeToLive > 0)
            {
                newNode.TimeToLive--;
                nextMap.NodesAboutToDie.Add(newNode);
            }
        }

        return nextMap;
    }

    public Dictionary<Position, List<int>> GetPositionsAndAssociatedDamage(Map map)
    {
        var positionsAndAssociatedDamage = new Dictionary<Position, List<int>>();

        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                if (map.ContainsAnyNode(col, row)) continue;
                if (map.Blocks[col, row]) continue;
                Position position = new Position(col, row);
                List<int> damagedNodes = GetDamagedNodes(map, position);
                if (damagedNodes.Count == 0) continue;
                if (!BetterPositionExists(positionsAndAssociatedDamage.Values, damagedNodes))
                {
                    for (int ii = 0; ii < positionsAndAssociatedDamage.Count;)
                    {
                        var positionToCheck = positionsAndAssociatedDamage.Keys.ElementAt(ii);
                        if (IsBetterPosition(damagedNodes, positionsAndAssociatedDamage[positionToCheck]))
                        {
                            positionsAndAssociatedDamage.Remove(positionToCheck);
                        }
                        else
                        {
                            ii++;
                        }
                    }
                    positionsAndAssociatedDamage[position] = damagedNodes;
                }
            }
        }
        return positionsAndAssociatedDamage;
    }

    public bool BetterPositionExists(IEnumerable<List<int>> allExistingDamages, List<int> newDamage)
    {
        foreach (var existingDamages in allExistingDamages)
        {
            if (IsBetterPosition(existingDamages, newDamage))
                return true;
        }
        return false;
    }

    public bool IsBetterPosition(List<int> damages1, List<int> damages2)
    {
        if (damages1.Count < damages2.Count) return false;
        foreach (var damage in damages2)
        {
            if (!damages1.Contains(damage))
                return false;
        }
        return true;
    }

    public List<int> GetDamagedNodes(Map map, Position position)
    {
        var futureMap = map;
        for (int ii = 0; ii < Constants.BOMB_TIMEOUT; ii++)
        {
            futureMap = futureMap.GetNextMap();
        }
        var impacts = futureMap.GetImpacts(position);
        return futureMap.LiveNodes.Where(n => impacts.Contains(n.Position)).Select(n => n.Id).ToList();
    }

    public void ProcessNode(Node node, List<int> nodesHurt, Map map)
    {
        foreach (int nodeId in nodesHurt)
        {
            if (nodeId == node.Id)
            {
                node.TimeToLive = Constants.BOMB_TIMEOUT;
                map.NodesAboutToDie.Add(node);
                return;
            }
        }
        map.LiveNodes.Add(node);
        return;
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

    //public int[,] PotentialDamagedNodes;

    public List<Node> LiveNodes = new List<Node>();

    public List<Node> NodesAboutToDie = new List<Node>();

    public List<Position> BombsPlanted = new List<Position>();

    public List<PositionAndDamage> BestOptions;

    public Map(string[] lines)
    {
        string[] inputs = lines[0].Split(' ');
        Width = lines[1].Length;
        Height = lines.Length - 1;
        Blocks = new bool[Width, Height];
        int nodeCount = 0;

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
                        LiveNodes.Add(new Node() { Id = (int)Math.Pow(2, nodeCount), Position = new Position(col, row) });
                        nodeCount++;
                        break;
                    default:
                        break;
                }
            }
        }

        RoundsLeft = int.Parse(inputs[0]); // number of rounds left before the end of the game
        BombsLeft = int.Parse(inputs[1]); // number of bombs left
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
        nextMap.LiveNodes = GetUpdatedNodes(LiveNodes, impacts).ToList();
        //clone.NodesAboutToDie = LiveNodes.ToList();

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
        List<Position> impacts = new List<Position>();
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
        for (int ii = 0; ii < LiveNodes.Count; ii++)
        {
            Node node = LiveNodes[ii];
            if (node.Position.Col == col && node.Position.Row == row) return true;
        }
        for (int ii = 0; ii < NodesAboutToDie.Count; ii++)
        {
            Node node = NodesAboutToDie[ii];
            if (node.Position.Col == col && node.Position.Row == row) return true;
        }
        return false;
    }

    public bool IsFreeCell(Position position, Direction direction, out Position newPosition)
    {
        newPosition = UnsafeNextPosition(position, direction);
        return newPosition.Col >= 0 && newPosition.Col < Width && newPosition.Row >= 0 && newPosition.Row < Height && !Blocks[newPosition.Col, newPosition.Row];
    }

    public Position UnsafeNextPosition(Position position, Direction direction)
    {
        return new Position(
            position.Col + (((int)direction - 2) % 2),
            position.Row + (((int)direction - 1) % 2)
            );
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
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

    public void ComputePotentialDamage()
    {
        //PotentialDamagedNodes = new int[Width, Height];
        var potentialDamagedNodes = new List<PositionAndDamage>();
        for (int col = 0; col < Width; col++)
        {
            for (int row = 0; row < Height; row++)
            {
                if (!Blocks[col, row])
                {
                    var position = new Position(col, row);
                    var impacts = GetImpacts(position);
                    var destroyed = LiveNodes.Where(n => impacts.Contains(n.Position)).ToList();
                    if (destroyed.Any())
                    {
                        var aggDestroyed = destroyed.Select(n => n.Id).Aggregate((id1, id2) => id1 | id2);
                        //PotentialDamagedNodes[col, row] = aggDestroyed;
                        var pad = new PositionAndDamage(aggDestroyed, position);
                        potentialDamagedNodes.Add(pad);
                    }
                }
            }
        }
        BestOptions = potentialDamagedNodes.OrderByDescending(pad => pad.Count).ToList();
        //BestOptions.Add(new PositionAndDamage { Position = null, Count = 0, NodesKey = 0 });
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
        return CountSetBits(value);
    }

    /// <summary>
    /// Function to get no of set bits in binary representation of passed binary number
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    private static int CountSetBits(int n)
    {
        int count = 0;
        while (n > 0)
        {
            n &= (n - 1);
            count++;
        }
        return count;
    }
}

public struct Node
{
    public int Id;

    public Direction Direction;

    public Position Position;

    public int? TimeToLive;

    private static int NodeCount;

    public Node(int col, int row, Direction direction)
    {
        Id = (int)Math.Pow(2, NodeCount);
        Direction = direction;
        Position = new Position(col, row);
        TimeToLive = null;
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