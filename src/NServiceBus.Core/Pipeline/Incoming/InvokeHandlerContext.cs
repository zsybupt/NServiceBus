namespace NServiceBus
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Persistence;
    using Pipeline;
    using Unicast.Messages;

    class IncomingUnitOfWorkContext : IncomingContext, IIncomingUnitOfWorkContext
    {
        internal IncomingUnitOfWorkContext(IReadOnlyCollection<MessageHandler> handlersToInvoke, SynchronizedStorageSession storageSession, IIncomingLogicalMessageContext parentContext)
        {
            
        }
        
        internal IncomingUnitOfWorkContext(string messageId, string replyToAddress, IReadOnlyDictionary<string, string> headers, IBehaviorContext parentContext) : base(messageId, replyToAddress, headers, parentContext)
        {
        }

        public LogicalMessage Message { get; }
        public Dictionary<string, string> Headers { get; }
        public IReadOnlyCollection<MessageHandler> HandlersToInvoke { get; }
        public SynchronizedStorageSession SynchronizedStorageSession { get; }
    }

    class InvokeHandlerContext : IncomingContext, IInvokeHandlerContext
    {
        internal InvokeHandlerContext(MessageHandler handler, IIncomingUnitOfWorkContext parentContext)
            : this(handler, parentContext.MessageId, parentContext.ReplyToAddress, parentContext.Headers, parentContext.Message.Metadata, parentContext.Message.Instance, parentContext.SynchronizedStorageSession, parentContext)
        {
        }

        internal InvokeHandlerContext(MessageHandler handler, string messageId, string replyToAddress, Dictionary<string, string> headers, MessageMetadata messageMetadata, object messageBeingHandled, SynchronizedStorageSession storageSession, IBehaviorContext parentContext)
            : base(messageId, replyToAddress, headers, parentContext)
        {
            MessageHandler = handler;
            Headers = headers;
            MessageBeingHandled = messageBeingHandled;
            MessageMetadata = messageMetadata;
            Set(storageSession);
        }

        public MessageHandler MessageHandler { get; }

        public SynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (storageSession == null)
                {
                    storageSession = Get<SynchronizedStorageSession>();
                }
                return storageSession;
            }
        }

        public Dictionary<string, string> Headers { get; }

        public object MessageBeingHandled { get; }

        public bool HandlerInvocationAborted { get; private set; }

        public MessageMetadata MessageMetadata { get; }

        public bool HandleCurrentMessageLaterWasCalled { get; private set; }

        public async Task HandleCurrentMessageLater()
        {
            await MessageOperationsInvokeHandlerContext.HandleCurrentMessageLater(this).ConfigureAwait(false);
            HandleCurrentMessageLaterWasCalled = true;
            DoNotContinueDispatchingCurrentMessageToHandlers();
        }

        public void DoNotContinueDispatchingCurrentMessageToHandlers()
        {
            HandlerInvocationAborted = true;
        }

        SynchronizedStorageSession storageSession;
    }
}