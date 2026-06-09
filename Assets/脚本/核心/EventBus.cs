using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _events = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> callback) where T : struct
        {
            Type eventType = typeof(T);
            if (_events.ContainsKey(eventType))
            {
                _events[eventType] = Delegate.Combine(_events[eventType], callback);
            }
            else
            {
                _events[eventType] = callback;
            }
        }

        public static void Unsubscribe<T>(Action<T> callback) where T : struct
        {
            Type eventType = typeof(T);
            if (_events.ContainsKey(eventType))
            {
                _events[eventType] = Delegate.Remove(_events[eventType], callback);
            }
        }

        public static void Publish<T>(T eventData) where T : struct
        {
            Type eventType = typeof(T);
            if (_events.ContainsKey(eventType))
            {
                (_events[eventType] as Action<T>)?.Invoke(eventData);
            }
        }

        public static void Clear()
        {
            _events.Clear();
        }
    }

    #region Event Data Structures

    public struct GameStartEvent { }
    public struct GamePauseEvent { public bool IsPaused; }
    public struct GameOverEvent { public bool IsVictory; }
    public struct WaveStartEvent { public int WaveIndex; }
    public struct WaveEndEvent { public int WaveIndex; }
    public struct EnemySpawnEvent { public GameObject Enemy; }
    public struct EnemyDeathEvent { public GameObject Enemy; public int Reward; }
    public struct EnemyReachEndEvent { public GameObject Enemy; public int Damage; }
    public struct TowerPlacedEvent { public GameObject Tower; public int Cost; }
    public struct TowerUpgradedEvent { public GameObject Tower; public int Level; }
    public struct TowerSoldEvent { public GameObject Tower; public int Refund; }
    public struct GoldChangedEvent { public int CurrentGold; public int Change; }
    public struct LifeChangedEvent { public int CurrentLife; public int Change; }
    public struct SkillUsedEvent { public string SkillName; }
    public struct ExpChangedEvent { public int Level; public int CurrentExp; public int ExpToNext; }
    public struct LevelUpEvent { public int OldLevel; public int NewLevel; }
    public struct TreeChoppedEvent { public Vector3 TreePosition; public int WoodAmount; public int GoldAmount; }

    #endregion
}
