using System;
using System.Collections.Generic;

namespace Game.Events
{
    /// <summary>
    /// 订阅集合，集中释放
    /// </summary>
    public sealed class SubscriptionBag : IDisposable
    {
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        public void Add(IDisposable subscription)
        {
            if (subscription == null) 
                throw new ArgumentNullException(nameof(subscription));
            subscriptions.Add(subscription);
        }

        public void Clear()
        {
            for (int index = subscriptions.Count - 1; index >= 0; index--) 
            {
                subscriptions[index].Dispose();
            }
            subscriptions.Clear();
        }

        public void Dispose() 
        {
             Clear(); 
        }
    }
}
