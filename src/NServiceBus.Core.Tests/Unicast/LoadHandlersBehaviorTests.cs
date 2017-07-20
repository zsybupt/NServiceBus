namespace NServiceBus.Unicast.Tests
{
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class UnitOfWorkConnectorTests
    {
        [Test]
        public void Should_throw_when_there_are_no_registered_message_handlers()
        {
            var behavior = new UnitOfWorkConnector(new MessageHandlerRegistry(new Conventions()), new InMemorySynchronizedStorage(), new InMemoryTransactionalSynchronizedStorageAdapter());

            var context = new TestableIncomingLogicalMessageContext();

            Assert.That(async () => await behavior.Invoke(context, c => TaskEx.CompletedTask), Throws.InvalidOperationException);
        }
    }
}