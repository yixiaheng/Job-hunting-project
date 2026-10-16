using System;
using System.Collections.Generic;

namespace Game.Events
{
    public abstract class EventBus<TCategory>
    {
        #region Field 

        //最大发布深度
        public const int MaxPublishDepth = 64;
        //Type是指的是事件类型
        //Panel代表着一个通道
        private readonly Dictionary<Type, IChannel> channels = new Dictionary<Type, IChannel>();
        /// <summary>
        /// 异常;某个类型;委托
        /// 播报异常，不传入则为null
        /// </summary>
        private readonly Action<Exception, Type, Delegate> reportException;
        /// <summary>
        /// 播报发布,用于日志/监控
        /// </summary>
        private readonly Action<Type, int> reportPublish;
        
        private int publishDepth;
        //是否启用发布日志。
        public bool EnableLogging { get; set; }

        #endregion
        
        //构造函数
        protected EventBus(Action<Exception, Type, Delegate> reportException = null,
            Action<Type, int> reportPublish = null)
        {
            this.reportException = reportException;
            this.reportPublish = reportPublish;
        }

        /// <summary>
        /// 订阅方法，泛型约束
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="handler"></param>
        /// <returns>返回IDisposable,调用Dispose()即可取消</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : TCategory
        {
            
            if (handler == null) 
                throw new ArgumentNullException(nameof(handler));

            //拿到Type，再去用于字典的查询
            Type eventType = typeof(TEvent);
            if (!channels.TryGetValue(eventType, out IChannel channel))
            {
                //如果不存在本频道，创建频道，并且加入字典
                channel = new Channel<TEvent>(this);
                channels.Add(eventType, channel);
            }

            return ((Channel<TEvent>)channel).Subscribe(handler);
        }

        public void Publish<TEvent>(TEvent message) where TEvent : TCategory
        {
            if (publishDepth >= MaxPublishDepth)
                throw new InvalidOperationException("事件嵌套发布超过上限，请检查事件循环。");
            publishDepth++;
            try
            {
                if (EnableLogging && reportPublish != null)
                {
                    try { reportPublish(typeof(TEvent), GetSubscriberCount<TEvent>()); }
                    catch (Exception exception) { Report(exception, typeof(TEvent), reportPublish); }
                }
                if (channels.TryGetValue(typeof(TEvent), out IChannel channel))
                    ((Channel<TEvent>)channel).Publish(message);
            }
            finally { publishDepth--; }
        }

        public int GetSubscriberCount<TEvent>() where TEvent : TCategory
        {
            return channels.TryGetValue(typeof(TEvent), out IChannel channel) ? channel.Count : 0;
        }

        public void Clear()
        {
            foreach (IChannel channel in channels.Values) channel.Clear();
            channels.Clear();
        }

        private void Report(Exception exception, Type eventType, Delegate handler)
        {
            try
            {
                if (reportException != null) reportException(exception, eventType, handler);
                else System.Diagnostics.Trace.TraceError("Event {0}, handler {1}: {2}", eventType, handler.Method, exception);
            }
            catch (Exception reportingException)
            {
                System.Diagnostics.Trace.TraceError("Event error reporter failed: {0}; original: {1}", reportingException, exception);
            }
        }

        /// <summary>
        /// 通道接口，Count:数目 Clear():清空的方法
        /// </summary>
        private interface IChannel
        {
            int Count { get; }
            void Clear();
        }

        private sealed class Channel<TEvent> : IChannel
        {
            private readonly EventBus<TCategory> bus;
            private readonly List<Subscription> subscriptions = new List<Subscription>();
            private int dispatchDepth;
            private bool needsCompaction;
            public int Count { get; private set; }

            //频道初始化
            public Channel(EventBus<TCategory> bus) 
            { 
                this.bus = bus; 
            }

            public IDisposable Subscribe(Action<TEvent> handler)
            {
                var subscription = new Subscription(this, handler);
                subscriptions.Add(subscription);
                Count++;
                return subscription;
            }

            public void Publish(TEvent message)
            {
                int limit = subscriptions.Count;
                dispatchDepth++;
                try
                {
                    for (int index = 0; index < limit; index++)
                    {
                        Action<TEvent> handler = subscriptions[index].Handler;
                        if (handler == null) continue;
                        try { handler(message); }
                        catch (Exception exception) { bus.Report(exception, typeof(TEvent), handler); }
                    }
                }
                finally
                {
                    dispatchDepth--;
                    if (dispatchDepth == 0 && needsCompaction)
                    {
                        int destination = 0;
                        for (int index = 0; index < subscriptions.Count; index++)
                            if (subscriptions[index].Handler != null) subscriptions[destination++] = subscriptions[index];
                        subscriptions.RemoveRange(destination, subscriptions.Count - destination);
                        needsCompaction = false;
                    }
                }
            }

            public void Clear()
            {
                foreach (Subscription subscription in subscriptions) subscription.Release();
                Count = 0;
                if (dispatchDepth == 0) subscriptions.Clear();
                else needsCompaction = true;
            }

            private void Remove(Subscription subscription)
            {
                subscription.Release();
                Count--;
                if (dispatchDepth == 0) subscriptions.Remove(subscription);
                else needsCompaction = true;
            }

            private sealed class Subscription : IDisposable
            {
                private Channel<TEvent> owner;
                public Action<TEvent> Handler { get; private set; }

                public Subscription(Channel<TEvent> owner, Action<TEvent> handler)
                {
                    this.owner = owner;
                    Handler = handler;
                }

                public void Dispose() { owner?.Remove(this); }

                public void Release()
                {
                    owner = null;
                    Handler = null;
                }
            }
        }
    }
}
