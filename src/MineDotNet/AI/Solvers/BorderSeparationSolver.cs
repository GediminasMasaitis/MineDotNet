﻿using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MineDotNet.Common;

namespace MineDotNet.AI.Solvers
{
    public class BorderSeparationSolver : ISolver
    {
        public int PartialBorderSolveFrom { get; set; }
        public int GiveUpFrom { get; set; }
        public int MaxPartialBorderSize { get; set; }
        public bool OnlyTrivialSolving { get; set; }

        public BorderSeparationSolver()
        {
            PartialBorderSolveFrom = 22;
            GiveUpFrom = 25;
            MaxPartialBorderSize = 16;
        }

#if DEBUG
        public event Action<string> Debug;
#endif

        private void OnDebug(string str)
        {
#if DEBUG
            Debug?.Invoke(str);
#endif
        }

        private void OnDebugLine(string str)
        {
#if DEBUG
            OnDebug(str + Environment.NewLine);
#endif
        }

        public IDictionary<Coordinate, SolverResult> Solve(IMap givenMap)
        {
            // We first attempt trivial solving.
            // If it's all the user wants, we return immediately.
            var trivialResults = SolveTrivial(givenMap);
            if (OnlyTrivialSolving)
            {
                return trivialResults;
            }

            // We prepare some intial data we will work with.
            // It's important to deep copy the cells, in order to not modify the original map.
            var map = new BorderSeparationSolverMap(givenMap);
            map.BuildNeighbourCache();
            var allVerdicts = new Dictionary<Coordinate, bool>();
            var allProbabilities = new Dictionary<Coordinate, decimal>();
            var fullySolvedBorders = new List<Border>();

            // We find a set of all border cells - the common border.
            var commonBorder = FindCommonBorder(map);
            OnDebugLine($"Common border calculated, found {commonBorder.Cells.Count} cells");

            // We make two HashSets of the common border coordinates.
            // One which will stay unmodified - to be able to identify border cells without recalculating,
            // another which will get coordinates removed as we progress with solving.
            var originalCommonBorderCoords = new HashSet<Coordinate>(commonBorder.Cells.Select(x => x.Coordinate));
            var commonBorderCoords = new HashSet<Coordinate>(originalCommonBorderCoords);

            // We separate the common border into multiple, non-connecting borders, which can be solved independently.
            var borders = SeparateBorders(commonBorder, map).OrderBy(x => x.Cells.Count).ToList();
            OnDebugLine($"Common border split into {borders.Count} separate borders.");

            // We iterate over each border, and attempt to solve it
            foreach (var border in borders)
            {
                SolveBorder(border, map, true);
                foreach (var borderVerdict in border.Verdicts)
                {
                    allVerdicts[borderVerdict.Key] = borderVerdict.Value;
                    commonBorderCoords.Remove(borderVerdict.Key);
                }
                if (border.SolvedFully)
                {
                    fullySolvedBorders.Add(border);
                }
                else
                {
                    foreach (var cell in border.Cells)
                    {
                        commonBorderCoords.Remove(cell.Coordinate);
                    }
                }
                foreach (var probability in border.Probabilities)
                {
                    allProbabilities[probability.Key] = probability.Value;
                }
            }
            
            if (map.RemainingMineCount.HasValue)
            {
                foreach (var border in fullySolvedBorders)
                {
                    var guaranteedOtherCombinations = fullySolvedBorders.Where(x => x != border).Sum(x => x.MinMineCount);
                    TrimValidCombinationsByMineCount(border, map.RemainingMineCount.Value, map.UndecidedCount, guaranteedOtherCombinations);
                }
            }

            var bordersWithExactMineCount = fullySolvedBorders.Where(x => x.MinMineCount == x.MaxMineCount).ToList();
            var bordersWithVariableMineCount = fullySolvedBorders.Where(x => x.MinMineCount != x.MaxMineCount).ToList();

            foreach (var border in bordersWithExactMineCount)
            {
                foreach (var cell in border.Cells)
                {
                    commonBorderCoords.Remove(cell.Coordinate);
                }
            }

            commonBorder.Cells = commonBorder.Cells.Where(x => commonBorderCoords.Contains(x.Coordinate)).ToList();

            commonBorder.ValidCombinations = GetCommonBorderValidCombinations(bordersWithVariableMineCount);

            if (map.RemainingMineCount.HasValue)
            {

                var exactMineCount = bordersWithExactMineCount.Sum(x => x.MaxMineCount);
                TrimValidCombinationsByMineCount(commonBorder, map.RemainingMineCount.Value, map.UndecidedCount, exactMineCount);
            }

            commonBorder.Probabilities = GetBorderProbabilities(commonBorder);
            foreach (var probability in commonBorder.Probabilities)
            {
                allProbabilities[probability.Key] = probability.Value;
            }

            var nonBorderProbabilities = GetNonBorderProbabilitiesByMineCount(map, allProbabilities, originalCommonBorderCoords);
            foreach (var probability in nonBorderProbabilities)
            {
                allProbabilities[probability.Key] = probability.Value;
            }
            //var probabilities = commonBorder.Probabilities.Concat(nonBorderProbabilities).ToDictionary(x => x.Key, x => x.Value);

            var results = GetFinalResults(allProbabilities, allVerdicts);
            OnDebugLine("Found " + allVerdicts.Count + " guaranteed moves.");
            return results;
        }

        public IDictionary<Coordinate, SolverResult> SolveTrivial(IMap map)
        {
            var results = new Dictionary<Coordinate, SolverResult>();
            var initialVerdictCount = -1;
            while (results.Count != initialVerdictCount)
            {
                initialVerdictCount = results.Count;
                var allCells = map.AllCells.Where(x => x.State == CellState.Empty);
                foreach (var cell in allCells)
                {
                    var cellNeighbours = map.GetNeighboursOf(cell);
                    var filledNeighbours = cellNeighbours.Where(x => x.State == CellState.Filled && (!results.ContainsKey(x.Coordinate) || results[x.Coordinate].Verdict != Verdict.DoesntHaveMine)).ToList();
                    var markedNeighbours = filledNeighbours.Where(x => x.Flag == CellFlag.HasMine || (results.ContainsKey(x.Coordinate) && results[x.Coordinate].Verdict == Verdict.HasMine)).ToList();
                    if (filledNeighbours.Count == markedNeighbours.Count)
                    {
                        continue;
                    }
                    if (filledNeighbours.Count == cell.Hint)
                    {
                        var neighboursToFlag = filledNeighbours.Where(x => x.Flag != CellFlag.HasMine && !results.ContainsKey(x.Coordinate));
                        foreach (var neighbour in neighboursToFlag)
                        {
                            var result = new SolverResult(neighbour.Coordinate, 1, Verdict.HasMine);
                            results.Add(neighbour.Coordinate, result);
                        }
                    }
                    if (markedNeighbours.Count == cell.Hint)
                    {
                        var unmarkedNeighbours = filledNeighbours.Where(x => x.Flag != CellFlag.HasMine);
                        var neighboursToClick = unmarkedNeighbours.Where(x => !results.ContainsKey(x.Coordinate));
                        foreach (var neighbour in neighboursToClick)
                        {
                            var result = new SolverResult(neighbour.Coordinate, 0, Verdict.DoesntHaveMine);
                            results.Add(neighbour.Coordinate, result);
                        }
                    }
                }
            }
            return results;
        }

        private class PartialBorderCheckResult
        {
            public PartialBorderCheckResult(HashSet<Coordinate> partialBorderCoordinates, IMap partialMap, Border partialBorder)
            {
                PartialBorderCoordinates = partialBorderCoordinates;
                PartialMap = partialMap;
                PartialBorder = partialBorder;
            }

            public HashSet<Coordinate> PartialBorderCoordinates { get; set; }
            public IMap PartialMap { get; set; }
            public Border PartialBorder { get; set; }
        }

        private void SolveBorder(Border border, BorderSeparationSolverMap map, bool allowPartialBorderSolving)
        {
            OnDebugLine("Solving " + border.Cells.Count + " cell border");
            if (allowPartialBorderSolving && border.Cells.Count > PartialBorderSolveFrom)
            {
                TrySolveBorderByPartialBorders(border, map);
                if (border.SolvedFully)
                {
                    return;
                }
            }

            if (border.Cells.Count > GiveUpFrom)
            {
                border.SolvedFully = false;
                return;
            }

            border.ValidCombinations = FindValidBorderCellCombinations(map, border);

            OnDebugLine("Found " + border.ValidCombinations.Count + " valid combinations");
            if (border.ValidCombinations.Count == 0)
            {
                // TODO Must be invalid map... Handle somehow
            }

            border.MinMineCount = border.ValidCombinations.Min(x => x.Count(y => y.Value == Verdict.HasMine));
            border.MaxMineCount = border.ValidCombinations.Max(x => x.Count(y => y.Value == Verdict.HasMine));
            border.Probabilities = GetBorderProbabilities(border);
            border.Verdicts = GetVerdictsFromProbabilities(border.Probabilities);
            SetCellsByVerdicts(map, border.Verdicts);
            foreach (var wholeBorderResult in border.Verdicts)
            {
                border.Probabilities.Remove(wholeBorderResult.Key);
                var cell = map[wholeBorderResult.Key];
                border.Cells.Remove(cell);
                foreach (var validCombination in border.ValidCombinations)
                {
                    validCombination.Remove(wholeBorderResult.Key);
                }
            }
            border.SolvedFully = true;
        }

        private void TrySolveBorderByPartialBorders(Border border, BorderSeparationSolverMap map)
        {
            OnDebugLine($"Attempting to solve border via partial borders with max partial border size {MaxPartialBorderSize}");
            IList<PartialBorderCheckResult> checkedSets = new List<PartialBorderCheckResult>();
            var verdictsFoundByPartialBorders = 0;
            for (var i = 0; i < border.Cells.Count; i++)
            {
                var targetCoordinate = border.Cells[i].Coordinate;
                var partialBorderSize = MaxPartialBorderSize;

                var partialBorder = GetPartialBorder(border, map, targetCoordinate, partialBorderSize);
                var partialMap = GetPartialMap(partialBorder, map);
                while (partialBorder.Cells.Count < MaxPartialBorderSize && partialBorderSize < border.Cells.Count)
                {
                    partialBorderSize++;
                    var newPartialBorder = GetPartialBorder(border, map, targetCoordinate, partialBorderSize);
                    var newPartialMap = GetPartialMap(newPartialBorder, map);
                    if (newPartialBorder.Cells.Count > MaxPartialBorderSize)
                    {
                        break;
                    }
                    if (newPartialBorder.Cells.Count != partialBorder.Cells.Count)
                    {
                        partialBorder = newPartialBorder;
                        partialMap = newPartialMap;
                    }
                }
                var currentCoords = new HashSet<Coordinate>(partialBorder.Cells.Select(x => x.Coordinate));
                var checkResult = checkedSets.FirstOrDefault(x => x.PartialBorderCoordinates.IsSupersetOf(currentCoords));
                if (checkResult != null)
                {
                    decimal probability;
                    if (checkResult.PartialBorder.Probabilities.TryGetValue(targetCoordinate, out probability))
                    {
                        border.Probabilities[targetCoordinate] = probability;
                    }
                    continue;
                }

                partialMap.BuildNeighbourCache();
                SolveBorder(partialBorder, partialMap, false);
                SetCellsByVerdicts(map, partialBorder.Verdicts);
                foreach (var verdict in partialBorder.Verdicts)
                {
                    var cell = map[verdict.Key];
                    border.Verdicts[verdict.Key] = verdict.Value;
                    var cellIndex = border.Cells.IndexOf(cell);
                    if (cellIndex <= i)
                    {
                        i--;
                    }
                    border.Cells.RemoveAt(cellIndex);
                }
                verdictsFoundByPartialBorders += partialBorder.Verdicts.Count;
                checkResult = new PartialBorderCheckResult(currentCoords, partialMap, partialBorder);
                checkedSets.Add(checkResult);
                var resultsContainTarget = partialBorder.Verdicts.ContainsKey(targetCoordinate);
                if (!resultsContainTarget)
                {
                    border.Probabilities[targetCoordinate] = checkResult.PartialBorder.Probabilities[targetCoordinate];
                }
            }

            if (verdictsFoundByPartialBorders > 0)
            {
                var resplitBorders = SeparateBorders(border, map).ToList();
                if (resplitBorders.Count > 1)
                {
                    foreach (var resplitBorder in resplitBorders)
                    {
                        SolveBorder(resplitBorder, map, false);
                        foreach (var resplitBorderVerdict in resplitBorder.Verdicts)
                        {
                            border.Verdicts[resplitBorderVerdict.Key] = resplitBorderVerdict.Value;
                        }
                        if (resplitBorder.SolvedFully)
                        {
                            foreach (var probability in resplitBorder.Probabilities)
                            {
                                border.Probabilities[probability.Key] = probability.Value;
                            }
                        }
                    }
                    if (resplitBorders.All(x => x.SolvedFully))
                    {
                        border.ValidCombinations = GetCommonBorderValidCombinations(resplitBorders);
                        border.SolvedFully = true;
                    }
                }
            }
        }

        private void SetCellsByVerdicts(BorderSeparationSolverMap map, IDictionary<Coordinate, bool> verdicts)
        {
            foreach (var result in verdicts)
            {
                var cell = map[result.Key];
                switch (result.Value)
                {
                    case true:
                        cell.Flag = CellFlag.HasMine;
                        map.FlaggedCount++;
                        if (map.RemainingMineCount.HasValue)
                        {
                            map.RemainingMineCount--;
                        }
                        break;
                    case false:
                        cell.State = CellState.Wall;
                        map.FilledCount--;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            if (verdicts.Count > 0)
            {
                map.BuildNeighbourCache();
            }
        }

        private IDictionary<Coordinate, decimal> GetNonBorderProbabilitiesByMineCount(IMap map, IDictionary<Coordinate, decimal> commonBorderProbabilities, HashSet<Coordinate> originalCommonBorder)
        {
            var probabilities = new Dictionary<Coordinate, decimal>();
            if (!map.RemainingMineCount.HasValue)
            {
                return probabilities;
            }

            //var combinationsMineCount = commonBorder.ValidCombinations.Count > 0 ? commonBorder.ValidCombinations.Sum(x => x.Count(y => y.Value == Verdict.HasMine))/(decimal) commonBorder.ValidCombinations.Count : 0;
            //var commonBorderCoordinateSet = new HashSet<Coordinate>(commonBorder.Cells.Select(x => x.Coordinate));
            var probabilitySum = commonBorderProbabilities.Sum(x => x.Value);
            var combinationsMineCount = Convert.ToInt32(probabilitySum*1000000)/(decimal)1000000;
            var nonBorderUndecidedCells = map.AllCells.Where(x => !originalCommonBorder.Contains(x.Coordinate) && x.State == CellState.Filled && x.Flag == CellFlag.None).ToList();
            if (nonBorderUndecidedCells.Count == 0)
            {
                return probabilities;
            }
            var nonBorderProbability = (map.RemainingMineCount.Value - combinationsMineCount)/nonBorderUndecidedCells.Count;
            foreach (var nonBorderFilledCell in nonBorderUndecidedCells)
            {
                probabilities[nonBorderFilledCell.Coordinate] = nonBorderProbability;
            }

            return probabilities;
        }

        private IList<IDictionary<Coordinate, Verdict>> GetCommonBorderValidCombinations(ICollection<Border> borders)
        {
            if (borders.Count == 0)
            {
                return new List<IDictionary<Coordinate, Verdict>>();
            }
            var combinationCombinations = borders.Select(x => x.ValidCombinations);
            var combinationsCartesianProduct = combinationCombinations.MultiCartesian(x => (IDictionary<Coordinate,Verdict>)x.SelectMany(y => y).ToDictionary(y => y.Key, y => y.Value)).ToList();
            return combinationsCartesianProduct;
        }

        private void TrimValidCombinationsByMineCount(Border border, int minesRemaining, int undecidedCellsRemaining, int minesElsewhere)
        {
            for (var i = 0; i < border.ValidCombinations.Count; i++)
            {
                var combination = border.ValidCombinations[i];
                var minePredictionCount = combination.Count(x => x.Value == Verdict.HasMine);
                if (minePredictionCount + minesElsewhere > minesRemaining)
                {
                    border.ValidCombinations.RemoveAt(i);
                    i--;
                    continue;
                }
                if (undecidedCellsRemaining == combination.Count && minePredictionCount != minesRemaining)
                {
                    border.ValidCombinations.RemoveAt(i);
                    i--;
                }
            }
        }

        private bool IsCoordinateABorder(IMap map, Cell cell)
        {
            if (cell.State != CellState.Filled || cell.Flag == CellFlag.HasMine)
            {
                return false;
            }
            var neighbours = map.GetNeighboursOf(cell);
            var hasOpenedNeighbour = neighbours.Any(x => x.State == CellState.Empty);
            return hasOpenedNeighbour;
        }

        private Border FindCommonBorder(IMap map)
        {
            var borderCells = map.AllCells.Where(x => IsCoordinateABorder(map, x)).ToList();
            var border = new Border(borderCells);
            return border;
        }

        private BorderSeparationSolverMap GetPartialMap(Border border, IMap parentMap)
        {
            //Debugging.Visualize(parentMap, border.Cells.Select(x=>x.Coordinate));

            var borderCoordinateSet = new HashSet<Coordinate>(border.Cells.Select(x => x.Coordinate));
            var allSurroundingEmpty = borderCoordinateSet.SelectMany(x => parentMap.NeighbourCache[x].ByState[CellState.Empty]).Distinct();
            var onlyInfluencingBorder = allSurroundingEmpty.Where(x => parentMap.NeighbourCache[x.Coordinate].ByState[CellState.Filled].All(y => borderCoordinateSet.Contains(y.Coordinate))).ToList();
            var onlyInflucencingBorderSet = new HashSet<Coordinate>(onlyInfluencingBorder.Select(x => x.Coordinate));
            var newNonBorderCells = border.Cells.Where(x => parentMap.NeighbourCache[x.Coordinate].ByState[CellState.Empty].Any(y => onlyInflucencingBorderSet.Contains(y.Coordinate))).ToList();
            border.Cells = newNonBorderCells;
            if (border.Cells.Count == 0)
            {
                return null;
            }
            var allCoordinateSet = new HashSet<Coordinate>(border.Cells.Select(x => x.Coordinate).Concat(onlyInflucencingBorderSet));
            var newWidth = allCoordinateSet.Max(x => x.X) + 1;
            var newHeight = allCoordinateSet.Max(x => x.Y) + 1;
            var partialMap = new Map(newWidth, newHeight, null, true, CellState.Wall);
            foreach (var cell in border.Cells)
            {
                partialMap[cell.Coordinate] = cell;
            }
            foreach (var cell in onlyInfluencingBorder)
            {
                partialMap[cell.Coordinate] = cell;
            }
            //Debugging.Visualize(partialMap);
            var solverMap = new BorderSeparationSolverMap(partialMap);
            return solverMap;
        }

        private Border GetPartialBorder(Border commonBorder, IMap map, Coordinate targetCoordinate, int partialBorderSize)
        {
            var commonCoords = new HashSet<Coordinate>(commonBorder.Cells.Select(x => x.Coordinate));
            var coordQueue = new Queue<Coordinate>();
            var currentCells = new List<Cell>();
            coordQueue.Enqueue(targetCoordinate);
            commonCoords.Remove(targetCoordinate);
            while (coordQueue.Count > 0 && currentCells.Count < partialBorderSize)
            {
                var coord = coordQueue.Dequeue();
                var cell = map[coord];
                currentCells.Add(cell);
                var neighbors = map.NeighbourCache[coord].ByState[CellState.Filled].Where(x => commonCoords.Contains(x.Coordinate));
                foreach (var neighbor in neighbors)
                {
                    commonCoords.Remove(neighbor.Coordinate);
                    coordQueue.Enqueue(neighbor.Coordinate);
                }
            }
            var border = new Border(currentCells);
            return border;
        }

        private IEnumerable<Border> SeparateBorders(Border commonBorder, IMap map)
        {
            var commonCoords = new HashSet<Coordinate>(commonBorder.Cells.Select(x => x.Coordinate));
            var visited = new HashSet<Coordinate>();
            while (commonCoords.Count > 0)
            {
                var initialCoord = commonCoords.First();
                var coordQueue = new Queue<Coordinate>();
                var currentCells = new List<Cell>();
                coordQueue.Enqueue(initialCoord);
                while (coordQueue.Count > 0)
                {
                    var coord = coordQueue.Dequeue();
                    var cell = map[coord];
                    if (commonCoords.Contains(coord))
                    {
                        currentCells.Add(cell);
                        commonCoords.Remove(coord);
                    }
                    visited.Add(cell.Coordinate);
                    var neighbors = map.GetNeighboursOf(cell).Where(x => x.Flag != CellFlag.HasMine && ((cell.State == CellState.Filled && x.State == CellState.Empty) || (cell.State == CellState.Empty && x.State == CellState.Filled)));
                    foreach (var neighbor in neighbors)
                    {
                        if (visited.Add(neighbor.Coordinate))
                        {
                            coordQueue.Enqueue(neighbor.Coordinate);
                        }
                    }
                }
                var border = new Border(currentCells);
                yield return border;
            }
        }

        private IList<IDictionary<Coordinate, Verdict>> FindValidBorderCellCombinations(BorderSeparationSolverMap map, Border border)
        {
            var borderLength = border.Cells.Count;
            const int maxSize = 31;
            if (borderLength > maxSize)
            {
                throw new InvalidDataException($"Border with {borderLength} cells is too large, maximum {maxSize} cells allowed");
            }
            var totalCombinations = 1 << borderLength;
            OnDebugLine("Attempting " + (1 << border.Cells.Count) + " combinations");
            var allRemainingCellsInBorder = map.UndecidedCount == borderLength;
            var validPredictions = new ConcurrentBag<IDictionary<Coordinate, Verdict>>();
            var emptyCells = map.AllCells.Where(x => x.State == CellState.Empty).ToList();
            var combos = Enumerable.Range(0, totalCombinations);
            Parallel.ForEach(combos, combo =>
            {
                var bitsSet = SWAR(combo);
                if (map.RemainingMineCount.HasValue)
                {
                    if (bitsSet > map.RemainingMineCount.Value)
                    {
                        return;
                    }

                    if (allRemainingCellsInBorder && bitsSet != map.RemainingMineCount)
                    {
                        return;
                    }
                }
                var predictions = new Dictionary<Coordinate, Verdict>(borderLength);
                for (var j = 0; j < borderLength; j++)
                {
                    var coord = border.Cells[j].Coordinate;
                    var hasMine = (combo & (1 << j)) > 0;
                    var verd = hasMine ? Verdict.HasMine : Verdict.DoesntHaveMine;
                    predictions[coord] = verd;
                }
                var valid = IsPredictionValid(map, predictions, emptyCells);
                if (valid)
                {
                    validPredictions.Add(predictions);
                }
            });
            return validPredictions.ToList();
        }

        private int SWAR(int i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F)*0x01010101) >> 24;
        }

        public bool IsPredictionValid(IMap map, IDictionary<Coordinate, Verdict> predictions, IList<Cell> emptyCells)
        {
            foreach (var cell in emptyCells)
            {
                var neighboursWithMine = 0;
                var neighboursWithoutMine = 0;
                var filledNeighbours = map.NeighbourCache[cell.Coordinate].ByState[CellState.Filled];
                bool foundUnknownCell = false;
                foreach (var neighbour in filledNeighbours)
                {
                    if (neighbour.Flag == CellFlag.HasMine)
                    {
                        neighboursWithMine++;
                    }
                    else
                    {
                        Verdict verdict;
                        var success = predictions.TryGetValue(neighbour.Coordinate, out verdict);
                        if (success)
                        {
                            if (verdict == Verdict.HasMine)
                            {
                                neighboursWithMine++;
                            }
                            else
                            {
                                neighboursWithoutMine++;
                            }
                        }
                        else
                        {
                            foundUnknownCell = true;
                        }
                    }
                }
                if (neighboursWithMine > cell.Hint)
                    return false;
                if (filledNeighbours.Count - neighboursWithoutMine < cell.Hint)
                    return false;
                if (foundUnknownCell)
                    continue;
                if (cell.Hint != neighboursWithMine)
                    return false;
            }
            return true;
        }

        private IDictionary<Coordinate, decimal> GetBorderProbabilities(Border border)
        {
            var probabilities = new Dictionary<Coordinate, decimal>();
            if (border.ValidCombinations.Count == 0)
            {
                return probabilities;
            }
            foreach (var cell in border.Cells)
            {
                var mineInCount = border.ValidCombinations.Count(x => x[cell.Coordinate] == Verdict.HasMine);
                var probability = (decimal) mineInCount/border.ValidCombinations.Count;
                probabilities.Add(cell.Coordinate, probability);
            }
            return probabilities;
        }

        private IDictionary<Coordinate, bool> GetVerdictsFromProbabilities(IDictionary<Coordinate, decimal> probabilities)
        {
            var verdicts = new Dictionary<Coordinate, bool>();
            foreach (var probability in probabilities)
            {
                bool verdict;
                if (probability.Value == 0)
                {
                    verdict = false;
                }
                else if (probability.Value == 1)
                {
                    verdict = true;
                }
                else
                {
                    continue;
                }
                verdicts[probability.Key] = verdict;
            }
            return verdicts;
        }

        private IDictionary<Coordinate, SolverResult> GetFinalResults(IDictionary<Coordinate, decimal> probabilities, IDictionary<Coordinate, bool> verdicts)
        {
            var results = new Dictionary<Coordinate, SolverResult>();
            foreach (var probability in probabilities)
            {
                results[probability.Key] = new SolverResult(probability.Key, probability.Value, null);
            }
            foreach (var verdict in verdicts)
            {
                results[verdict.Key] = new SolverResult(verdict.Key, verdict.Value ? 1 : 0, verdict.Value ? Verdict.HasMine : Verdict.DoesntHaveMine);
            }
            return results;
        }
    }
}