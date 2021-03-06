﻿using System.Collections.Generic;
using System.Linq;
using MineDotNet.Common;

namespace MineDotNet.Game.Models
{
    public class GameMap : IGameMap
    {
        public int Width { get; }
        public int Height { get; }
        public int? RemainingMineCount { get; set; }

        public bool DisplayMineCount { get; set; }
        public GameCell[,] Cells { get; }
        public IEnumerable<GameCell> AllCells => Cells.Cast<GameCell>().Where(x => x != null);
        public Coordinate StartingPosition { get; }
        public bool GuaranteedOpening { get; }

        public GameMap(int width, int height, int? remainingMineCount, Coordinate startingPosition, bool guaranteedOpening, bool createCells = false, CellState fillWithState = CellState.Empty)
        {
            Width = width;
            Height = height;
            Cells = new GameCell[Width, Height];
            for (var i = 0; i < Width; i++)
            {
                for (var j = 0; j < Height; j++)
                {
                    var cell = createCells ? new GameCell(i, j, false, fillWithState) : null;
                    Cells[i, j] = cell;
                }
            }
            RemainingMineCount = remainingMineCount;
            DisplayMineCount = true;
            StartingPosition = startingPosition;
            GuaranteedOpening = guaranteedOpening;
        }

        public GameCell this[Coordinate coordinate]
        {
            get => Cells[coordinate.X, coordinate.Y];
            set => Cells[coordinate.X, coordinate.Y] = value;
        }

        public bool CellExists(Coordinate coord)
        {
            return coord.X >= 0 && coord.Y >= 0 && coord.X < Width && coord.Y < Height &&  Cells[coord.X, coord.Y] != null && Cells[coord.X, coord.Y].State != CellState.Wall;
        }

        public IList<GameCell> CalculateNeighboursOf(Coordinate coord, bool includeSelf = false)
        {
            var validOffsets = Map.NeighbourOffsets.Select(x => coord + x).Where(CellExists).ToList();
            if (includeSelf && CellExists(coord))
            {
                validOffsets.Add(coord);
            }
            var neighbours = validOffsets.Select(x => Cells[x.X, x.Y]).ToList();
            return neighbours;
        }

        public Map ToRegularMap()
        {
            var cellCopy = AllCells.Select(x => new Cell(x.Coordinate, x.State, x.Flag, x.Hint)).ToList();
            foreach (var cell in cellCopy.Where(x => x.State != CellState.Empty))
            {
                cell.Hint = 0;
            }
            var mineCount = DisplayMineCount ? RemainingMineCount : (int?)null;
            return new Map(cellCopy, mineCount);
        }

        public GameMap Clone()
        {
            var cellCopy = AllCells.Select(x => new GameCell(x.Coordinate, x.HasMine, x.State, x.Flag, x.Hint)).ToList();
            var map = new GameMap(Width, Height, RemainingMineCount, StartingPosition, GuaranteedOpening);
            map.DisplayMineCount = DisplayMineCount;
            foreach (var cell in cellCopy)
            {
                map[cell.Coordinate] = cell;
            }
            return map;
        }

        public static GameMap FromRegularMap(Map map)
        {
            var gm = new GameMap(map.Width, map.Height, 0, new Coordinate(map.Width/2, map.Height/2), true);
            for (var i = 0; i < gm.Width; i++)
            {
                for (var j = 0; j < gm.Height; j++)
                {
                    var cell = map.Cells[i, j];
                    gm.Cells[i, j] = new GameCell(cell.Coordinate, cell.Flag == CellFlag.HasMine, CellState.Filled, CellFlag.None, 0);
                }
            }
            foreach (var cell in gm.AllCells)
            {
                if (cell.HasMine)
                {
                    gm.RemainingMineCount++;
                }
                else
                {
                    var neighbours = gm.CalculateNeighboursOf(cell.Coordinate, false);
                    cell.Hint = neighbours.Count(x => x.HasMine);
                }
            }
            return gm;
        }

        public Map ToDisplayMap(bool showHints)
        {
            var cellCopy = new List<Cell>();
            foreach (var gameCell in AllCells)
            {
                var state = gameCell.HasMine ? CellState.Filled : CellState.Empty;
                var flag = gameCell.HasMine ? CellFlag.HasMine : CellFlag.DoesntHaveMine;
                var hint = showHints ? gameCell.Hint : 0;
                var cell = new Cell(gameCell.Coordinate, state, flag, hint);
                cellCopy.Add(cell);
            }
            var map = new Map(cellCopy, null);
            return map;
        }
    }

}
