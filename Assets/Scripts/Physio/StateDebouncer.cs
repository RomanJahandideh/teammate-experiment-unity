using System.Collections.Generic;

namespace Teammate.Physio
{
    /// <summary>
    /// Implements the paper's persistence rule: "the visible cue changed only once a new
    /// state held across several consecutive samples ... a short debounce that filtered
    /// single-sample noise without adding perceptible lag." HR is sampled at 1 Hz, so
    /// each Sample() call here corresponds to one classification tick.
    ///
    /// Kept generic over HRState/HRVState so the same debounce logic drives both channels.
    /// Constrained to `struct` only (not IEquatable&lt;T&gt;, which plain enum types don't
    /// implement) and compares via EqualityComparer&lt;T&gt;.Default, which handles enums
    /// correctly out of the box.
    /// </summary>
    public sealed class StateDebouncer<T> where T : struct
    {
        private static readonly EqualityComparer<T> Comparer = EqualityComparer<T>.Default;
        private readonly int _requiredConsecutiveSamples;
        private T _candidate;
        private int _candidateStreak;
        private T _displayed;
        private bool _hasDisplayed;

        /// <param name="requiredConsecutiveSamples">
        /// Number of consecutive samples a new classification must hold before it
        /// replaces the displayed state. Default of 3 matches ~3 seconds of persistence
        /// at the study's 1 Hz sampling rate.
        /// </param>
        public StateDebouncer(int requiredConsecutiveSamples = 3)
        {
            _requiredConsecutiveSamples = System.Math.Max(1, requiredConsecutiveSamples);
        }

        public T Displayed => _displayed;
        public bool HasDisplayed => _hasDisplayed;

        /// <summary>Feed the latest instantaneous classification. Returns true if the displayed state just changed.</summary>
        public bool Sample(T classified)
        {
            if (!_hasDisplayed)
            {
                _displayed = classified;
                _hasDisplayed = true;
                _candidate = classified;
                _candidateStreak = 1;
                return true;
            }

            if (Comparer.Equals(classified, _displayed))
            {
                // Already showing this state; reset any in-progress challenger.
                _candidate = classified;
                _candidateStreak = 1;
                return false;
            }

            if (Comparer.Equals(classified, _candidate))
            {
                _candidateStreak++;
            }
            else
            {
                _candidate = classified;
                _candidateStreak = 1;
            }

            if (_candidateStreak >= _requiredConsecutiveSamples)
            {
                _displayed = _candidate;
                return true;
            }

            return false;
        }
    }
}
