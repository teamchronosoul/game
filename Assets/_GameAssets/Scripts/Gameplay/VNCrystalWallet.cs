using System;
using UnityEngine;

namespace VN
{
    public static class VNCrystalWallet
    {
        private const string BalanceKey = "VN.Crystals";

        public static event Action<int, int> OnChanged;

        public static int Balance => Mathf.Max(0, PlayerPrefs.GetInt(BalanceKey, 0));

        public static bool CanSpend(int amount)
        {
            amount = Mathf.Max(0, amount);
            return Balance >= amount;
        }

        public static bool TrySpend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0)
                return true;

            var current = Balance;
            if (current < amount)
                return false;

            SetBalanceInternal(current - amount, -amount);
            return true;
        }

        public static void Add(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0)
                return;

            SetBalanceInternal(Balance + amount, amount);
        }

        public static void SetBalance(int amount)
        {
            amount = Mathf.Max(0, amount);
            SetBalanceInternal(amount, amount - Balance);
        }

        public static void Reset()
        {
            SetBalanceInternal(0, -Balance);
        }

        private static void SetBalanceInternal(int value, int delta)
        {
            value = Mathf.Max(0, value);
            PlayerPrefs.SetInt(BalanceKey, value);
            PlayerPrefs.Save();
            OnChanged?.Invoke(value, delta);
        }
    }
}
