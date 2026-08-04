using System;

namespace Doggiehood.Core.Economy
{
    /// <summary>
    /// The player's coin balance (#24, #25). In normal play coins enter only
    /// via quest completion (QuestManager is the sole gameplay depositor); the
    /// on-device Debug "Add coins" action (#286) is the one other, debug-only
    /// depositor. The balance can never go negative — a rejected spend leaves
    /// it untouched.
    /// </summary>
    public sealed class Wallet
    {
        public int Coins { get; private set; }

        /// <summary>#542: raised on every <em>visible</em> balance change,
        /// carrying the signed delta (positive on a deposit, negative on a
        /// spend). The HUD chip subscribes to spawn the floating "+N"/"−N"
        /// delta label and drive the count-up tween. A rejected spend and a
        /// zero-valued change raise nothing — there is nothing to animate.</summary>
        public event Action<int> CoinsChanged;

        public void Deposit(int amount)
        {
            RequirePositive(amount);
            Coins += amount;
            if (amount != 0)
            {
                CoinsChanged?.Invoke(amount);
            }
        }

        public bool TrySpend(int amount)
        {
            RequirePositive(amount);
            if (amount > Coins)
            {
                return false;
            }

            Coins -= amount;
            if (amount != 0)
            {
                CoinsChanged?.Invoke(-amount);
            }

            return true;
        }

        /// <summary>#186: lets callers (the conversation panel UI) query
        /// affordability proactively instead of comparing against
        /// <see cref="Coins"/> themselves.</summary>
        public bool CanAfford(int amount)
        {
            RequirePositive(amount);
            return amount <= Coins;
        }

        private static void RequirePositive(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentException("Amount must not be negative.", nameof(amount));
            }
        }
    }
}
