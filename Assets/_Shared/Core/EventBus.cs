using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sttop5.Shared.Core
{
    /// <summary>
    /// 实例化事件总线，每个游戏模块拥有独立的 EventBus 实例，
    /// 实现模块间事件隔离。跨模块通信使用 GlobalEventBus。
    /// </summary>
    public class EventBus
    {
        private readonly Dictionary<Type, Delegate> _events = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> callback) where T : struct
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

        public void Unsubscribe<T>(Action<T> callback) where T : struct
        {
            Type eventType = typeof(T);
            if (_events.ContainsKey(eventType))
            {
                _events[eventType] = Delegate.Remove(_events[eventType], callback);
                if (_events[eventType] == null)
                {
                    _events.Remove(eventType);
                }
            }
        }

        public void Publish<T>(T eventData) where T : struct
        {
            Type eventType = typeof(T);
            if (_events.TryGetValue(eventType, out Delegate handler))
            {
                (handler as Action<T>)?.Invoke(eventData);
            }
        }

        public void Clear()
        {
            _events.Clear();
        }
    }

    /// <summary>
    /// 全局事件总线，用于跨模块通信（如：小游戏完成 → 家园发放奖励）。
    /// 模块内部通信请使用各自的 EventBus 实例。
    /// </summary>
    public static class GlobalEventBus
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
                if (_events[eventType] == null)
                {
                    _events.Remove(eventType);
                }
            }
        }

        public static void Publish<T>(T eventData) where T : struct
        {
            Type eventType = typeof(T);
            if (_events.TryGetValue(eventType, out Delegate handler))
            {
                (handler as Action<T>)?.Invoke(eventData);
            }
        }

        public static void Clear()
        {
            _events.Clear();
        }
    }

    #region 跨模块事件

    /// <summary>
    /// 小游戏完成事件，由小游戏模块发布，家园模块订阅。
    /// </summary>
    public struct MiniGameCompletedEvent
    {
        public string ModuleId;
        public string LevelId;
        public bool IsVictory;
        public int StarsEarned;
        public int GoldReward;
        public int DiamondReward;
        public int ExpReward;
    }

    /// <summary>
    /// 玩家货币变化事件。
    /// </summary>
    public struct PlayerCurrencyChangedEvent
    {
        public string Source;
        public int GoldDelta;
        public int DiamondDelta;
    }

    /// <summary>
    /// 模块切换事件。
    /// </summary>
    public struct ModuleSwitchEvent
    {
        public string FromModule;
        public string ToModule;
    }

    /// <summary>
    /// 玩家等级变化事件。
    /// </summary>
    public struct PlayerLevelChangedEvent
    {
        public int OldLevel;
        public int NewLevel;
        public int CurrentExp;
        public int ExpToNext;
    }

    #endregion
}
