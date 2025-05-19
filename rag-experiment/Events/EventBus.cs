namespace rag_experiment.Services.Events
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public static void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.ContainsKey(eventType))
                _handlers[eventType] = new List<Delegate>();
            _handlers[eventType].Add(handler);
        }

        public static void Publish<TEvent>(TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (_handlers.ContainsKey(eventType))
            {
                foreach (var handler in _handlers[eventType])
                {
                    // Run handlers asynchronously so publisher isn't blocked
                    Task.Run(() => ((Action<TEvent>)handler)(eventData));
                }
            }
        }
    }
}