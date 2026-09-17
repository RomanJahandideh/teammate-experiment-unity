using System;
using Unity.Netcode;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>
    /// Server-authoritative order board: spawns new orders from the recipe pool on a
    /// timer, expires ones nobody got to, and resolves delivery attempts from
    /// DeliveryCounter. Orders are exposed as a NetworkList so any client can render an
    /// order board, but only the server ever mutates it.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class OrderQueue : NetworkBehaviour
    {
        [Header("Config")]
        public DishRecipe[] recipePool;
        public float orderIntervalSeconds = 25f;
        public int maxActiveOrders = 4;

        public NetworkList<OrderTicket> Orders;

        /// <summary>Server-side only: (result, orderId, recipeId).</summary>
        public event Action<DeliveryResult, int, int> OnDeliveryResolved;
        /// <summary>Server-side only: an order's deadline passed with nobody delivering it.</summary>
        public event Action<OrderTicket> OnOrderExpired;

        private int _nextOrderId = 1;
        private float _timeSinceLastSpawn;
        private bool _spawning;

        void Awake()
        {
            Orders = new NetworkList<OrderTicket>();
        }

        public void ServerBeginSpawning()
        {
            if (!IsServer) return;
            _spawning = true;
            _timeSinceLastSpawn = orderIntervalSeconds; // spawn one immediately
        }

        public void ServerStopSpawning() => _spawning = false;

        public void ServerClearAllOrders()
        {
            if (!IsServer) return;
            Orders.Clear();
        }

        void Update()
        {
            if (!IsServer || !_spawning) return;

            _timeSinceLastSpawn += Time.deltaTime;
            if (_timeSinceLastSpawn >= orderIntervalSeconds && ActiveOrderCount() < maxActiveOrders)
            {
                _timeSinceLastSpawn = 0f;
                SpawnOrder();
            }

            CheckExpirations();
        }

        private int ActiveOrderCount()
        {
            int n = 0;
            for (int i = 0; i < Orders.Count; i++)
                if (!Orders[i].Fulfilled) n++;
            return n;
        }

        private int SpawnOrder(DishRecipe forcedRecipe = null)
        {
            if (recipePool == null || recipePool.Length == 0) return -1;

            var recipe = forcedRecipe != null ? forcedRecipe : recipePool[UnityEngine.Random.Range(0, recipePool.Length)];
            double now = NetworkManager.ServerTime.Time;
            int orderId = _nextOrderId++;

            Orders.Add(new OrderTicket
            {
                OrderId = orderId,
                RecipeId = recipe.recipeId,
                SpawnServerTime = now,
                DeadlineServerTime = now + recipe.deliveryWindowSeconds,
                Fulfilled = false,
            });

            return orderId;
        }

        /// <summary>
        /// Ingredient Bottleneck: force two simultaneous orders onto the same recipe (so
        /// the same ingredient is suddenly needed twice), regardless of the normal spawn
        /// timer or maxActiveOrders cap. Returns both order ids so a caller (the pressure
        /// event scheduler) can watch specifically for their resolution.
        /// </summary>
        public (int orderIdA, int orderIdB) ServerForceSpawnDuplicatePair()
        {
            if (!IsServer || recipePool == null || recipePool.Length == 0) return (-1, -1);

            var recipe = recipePool[UnityEngine.Random.Range(0, recipePool.Length)];
            return (SpawnOrder(recipe), SpawnOrder(recipe));
        }

        /// <summary>Service Rush: several orders land at once, piling up delivery/plating work for the Runner. Returns the spawned order ids.</summary>
        public int[] ServerForceSpawnRush(int count)
        {
            if (!IsServer) return System.Array.Empty<int>();

            var ids = new int[count];
            for (int i = 0; i < count; i++)
                ids[i] = SpawnOrder();
            return ids;
        }

        private void CheckExpirations()
        {
            double now = NetworkManager.ServerTime.Time;
            for (int i = Orders.Count - 1; i >= 0; i--)
            {
                var order = Orders[i];
                if (order.Fulfilled) continue;
                if (now <= order.DeadlineServerTime) continue;

                OnOrderExpired?.Invoke(order);
                Orders.RemoveAt(i);
            }
        }

        /// <summary>Called by DeliveryCounter's ServerRpc. Consumes the dish either way.</summary>
        public DeliveryResult ServerTryDeliver(int deliveredRecipeId)
        {
            double now = NetworkManager.ServerTime.Time;
            int matchIndex = -1;
            double earliestDeadline = double.MaxValue;

            for (int i = 0; i < Orders.Count; i++)
            {
                var order = Orders[i];
                if (order.Fulfilled || order.RecipeId != deliveredRecipeId) continue;
                if (order.DeadlineServerTime < earliestDeadline)
                {
                    earliestDeadline = order.DeadlineServerTime;
                    matchIndex = i;
                }
            }

            if (matchIndex < 0)
            {
                OnDeliveryResolved?.Invoke(DeliveryResult.NoMatchingOrder, -1, deliveredRecipeId);
                return DeliveryResult.NoMatchingOrder;
            }

            var matched = Orders[matchIndex];
            bool late = now > matched.DeadlineServerTime;
            Orders.RemoveAt(matchIndex);

            var result = late ? DeliveryResult.Late : DeliveryResult.OnTime;
            OnDeliveryResolved?.Invoke(result, matched.OrderId, deliveredRecipeId);
            return result;
        }
    }
}
