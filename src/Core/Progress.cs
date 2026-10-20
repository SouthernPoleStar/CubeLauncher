using System;
using System.Threading;

namespace CubeLauncher.Core
{
    public sealed class ProgressSink
    {
        private readonly object _gate = new object();
        private double _lastFraction = -1;

        public CancellationToken Token = CancellationToken.None;
        public Action<string> Stage;
        public Action<double, string> Value;

        public bool Cancelled
        {
            get { return Token.IsCancellationRequested; }
        }

        public void ThrowIfCancelled()
        {
            if (Token.IsCancellationRequested) throw new OperationCanceledException(Token);
        }

        public void SetStage(string text)
        {
            lock (_gate)
            {
                _lastFraction = -1;
                if (Stage != null) Stage(text);
            }
            Log.Info(text);
        }

        public void Report(double fraction, string detail)
        {
            if (fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;
            lock (_gate)
            {
                if (fraction < _lastFraction) return;
                _lastFraction = fraction;
                if (Value != null)
                {
                    try { Value(fraction, detail ?? ""); }
                    catch { }
                }
            }
        }

        public static ProgressSink Console
        {
            get
            {
                ProgressSink sink = new ProgressSink();
                sink.Stage = delegate(string s) { System.Console.WriteLine(s); };
                sink.Value = delegate(double f, string d) { System.Console.WriteLine((int)(f * 100) + "% " + d); };
                return sink;
            }
        }
    }
}
