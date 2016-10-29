using System;
using System.Collections.Generic;
using System.Linq;
using MineDotNet.Common;

namespace MineDotNet.AI.Solvers
{
    public abstract class SolverBase : ISolver
    {
        public event Action<string> Debug;
        protected virtual void OnDebug(string s) => Debug?.Invoke(s);
        protected virtual void OnDebugLine(string s) => OnDebug(s + Environment.NewLine);

        public abstract IDictionary<Coordinate, SolverResult> Solve(Map map, IDictionary<Coordinate, SolverResult> previousResults = null);
    }
}