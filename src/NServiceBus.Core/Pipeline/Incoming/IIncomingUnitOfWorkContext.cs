namespace NServiceBus.Pipeline
{
    using System.Collections.Generic;
    using Persistence;

    /// <summary>
    /// A context of behavior execution in unit of work processing stage.
    /// </summary>
    public interface IIncomingUnitOfWorkContext : IIncomingContext
    {
        /// <summary>
        /// Message being handled.
        /// </summary>
        LogicalMessage Message { get; }

        /// <summary>
        /// Headers for the incoming message.
        /// </summary>
        Dictionary<string, string> Headers { get; }

        /// <summary>
        /// Handlers to invoke.
        /// </summary>
        IReadOnlyCollection<MessageHandler> HandlersToInvoke { get; }
        
            /// <summary>
        /// Synchronized storage session.
        /// </summary>
        SynchronizedStorageSession SynchronizedStorageSession { get; }
    }
}