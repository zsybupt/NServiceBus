namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;
    using Persistence;
    using Pipeline;
    using Transport;
    using Unicast;

    class UnitOfWorkConnector : StageConnector<IIncomingLogicalMessageContext, IIncomingUnitOfWorkContext>
    {
        public UnitOfWorkConnector(MessageHandlerRegistry messageHandlerRegistry, ISynchronizedStorage synchronizedStorage, ISynchronizedStorageAdapter adapter)
        {
            this.messageHandlerRegistry = messageHandlerRegistry;
            this.synchronizedStorage = synchronizedStorage;
            this.adapter = adapter;
        }

        public override async Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingUnitOfWorkContext, Task> stage)
        {
            var handlersToInvoke = messageHandlerRegistry.GetHandlersFor(context.Message.MessageType);

            if (!context.MessageHandled && handlersToInvoke.Count == 0)
            {
                var error = $"No handlers could be found for message type: {context.Message.MessageType}";
                throw new InvalidOperationException(error);
            }

            var outboxTransaction = context.Extensions.Get<OutboxTransaction>();
            var transportTransaction = context.Extensions.Get<TransportTransaction>();
            using (var storageSession = await AdaptOrOpenNewSynchronizedStorageSession(transportTransaction, outboxTransaction, context.Extensions).ConfigureAwait(false))
            {
                await stage(this.CreateIncomingUnitOfWorkContext(handlersToInvoke, storageSession, context)).ConfigureAwait(false);
                context.MessageHandled = true;
                await storageSession.CompleteAsync().ConfigureAwait(false);
            }
        }

        async Task<CompletableSynchronizedStorageSession> AdaptOrOpenNewSynchronizedStorageSession(TransportTransaction transportTransaction, OutboxTransaction outboxTransaction, ContextBag contextBag)
        {
            return await adapter.TryAdapt(outboxTransaction, contextBag).ConfigureAwait(false)
                   ?? await adapter.TryAdapt(transportTransaction, contextBag).ConfigureAwait(false)
                   ?? await synchronizedStorage.OpenSession(contextBag).ConfigureAwait(false);
        }

        readonly ISynchronizedStorageAdapter adapter;
        readonly ISynchronizedStorage synchronizedStorage;
        readonly MessageHandlerRegistry messageHandlerRegistry;
    }

    class LoadHandlersConnector : StageConnector<IIncomingUnitOfWorkContext, IInvokeHandlerContext>
    {
      public override async Task Invoke(IIncomingUnitOfWorkContext context, Func<IInvokeHandlerContext, Task> stage)
        {
            foreach (var messageHandler in context.HandlersToInvoke)
            {
                messageHandler.Instance = context.Builder.Build(messageHandler.HandlerType);

                var handlingContext = this.CreateInvokeHandlerContext(messageHandler, context);
                await stage(handlingContext).ConfigureAwait(false);

                if (handlingContext.HandlerInvocationAborted)
                {
                    //if the chain was aborted skip the other handlers
                    break;
                }
            }
        }
    }
}