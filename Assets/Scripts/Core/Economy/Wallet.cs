using System;

namespace Doggiehood.Core.Economy
{
    /// <summary>
    /// The player's coin balance (#24, #25). Coins enter only via quest
    /// completion (QuestManager is the sole depositor) and can never go
    /// negative — a rejected spend leaves the balance untouched.
    /// </summary>
    public sealed class Wallet
    {
        public int Coins { get; private set; }

        public void Deposit(int amount)
        {
            RequirePositive(amount);
            Coins += amount;
        }

        public bool TrySpend(int amount)
        {
            RequirePositive(amount);
            if (amount > Coins)
            {
                return false;
            }

            Coins -= amount;
            return true;
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
