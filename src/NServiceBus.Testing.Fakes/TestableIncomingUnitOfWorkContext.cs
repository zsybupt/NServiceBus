// ReSharper disable PartialTypeWithSinglePart
namespace NServiceBus.Testing
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Persistence;
    using Pipeline;
    using Unicast.Messages;

    /// <summary>
    /// A testable implementation of <see cref="IIncomingUnitOfWorkContext" />.
    /// </summary>
    public partial class TestableIncomingUnitOfWorkContext : TestableIncomingContext, IIncomingUnitOfWorkContext
    {
        /// <summary>
        /// Creates a new instance of <see cref="TestableIncomingUnitOfWorkContext" />.
        /// </summary>
        public TestableIncomingUnitOfWorkContext(IMessageCreator messageCreator = null) : base(messageCreator)
        {
        }

        /// <summary>
        /// Message being handled.
        /// </summary>
        public LogicalMessage Message { get; set; } = new LogicalMessage(new MessageMetadata(typeof(object)), new object());

        /// <summary>
        /// Headers for the incoming message.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Handlers to invoke.
        /// </summary>
        public IList<MessageHandler> HandlersToInvoke { get; set; } = new List<MessageHandler>();
        
        /// <summary>
        /// Synchronized storage session.
        /// </summary>
        public SynchronizedStorageSession SynchronizedStorageSession { get; set; }
        
        IReadOnlyCollection<MessageHandler> IIncomingUnitOfWorkContext.HandlersToInvoke => new ReadOnlyCollection<MessageHandler>(HandlersToInvoke);
    }
}