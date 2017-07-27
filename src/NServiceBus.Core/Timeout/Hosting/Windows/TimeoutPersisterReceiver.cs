namespace NServiceBus.Timeout.Hosting.Windows
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CircuitBreakers;
    using Core;
    using Logging;
    using Transports;
    using Unicast;
    using Unicast.Transport;

    class TimeoutPersisterReceiver : IDisposable
    {
        public TimeoutPersisterReceiver(Func<DateTime> currentTimeProvider)
        {
            nextRetrieval = currentTimeProvider();
            this.currentTimeProvider = currentTimeProvider;
        }
        
        public IPersistTimeouts TimeoutsPersister { get; set; }
        public ISendMessages MessageSender { get; set; }
        public int SecondsToSleepBetweenPolls { get; set; }
        public DefaultTimeoutManager TimeoutManager { get; set; }
        public CriticalError CriticalError { get; set; }
        public Address DispatcherAddress { get; set; }
        public TimeSpan TimeToWaitBeforeTriggeringCriticalError { get; set; }

        public void Dispose()
        {
            //Injected
        }

        public void Start()
        {
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("TimeoutStorageConnectivity", TimeToWaitBeforeTriggeringCriticalError,
                ex =>
                {
                    CriticalError.Raise("Repeated failures when fetching timeouts from storage, endpoint will be terminated.", ex);
                });

            TimeoutManager.TimeoutPushed = TimeoutsManagerOnTimeoutPushed;

            SecondsToSleepBetweenPolls = 1;

            tokenSource = new CancellationTokenSource();

            StartPoller();
        }

        public void Stop()
        {
            TimeoutManager.TimeoutPushed = null;
            tokenSource.Cancel();
            resetEvent.WaitOne();
        }

        void StartPoller()
        {
            var token = tokenSource.Token;

            Task.Factory
                .StartNew(Poll, token, TaskCreationOptions.LongRunning)
                .ContinueWith(t =>
                {
                    t.Exception.Handle(ex =>
                    {
                        Logger.Warn("Failed to fetch timeouts from the timeout storage", ex);
                        circuitBreaker.Failure(ex);
                        return true;
                    });

                    StartPoller();
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        void Poll(object obj)
        {
            var cancellationToken = (CancellationToken)obj;

            var startSlice = currentTimeProvider().AddYears(-10);

            resetEvent.Reset();

            while (!cancellationToken.IsCancellationRequested)
            {
                SpinOnce(startSlice, cancellationToken);
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(SecondsToSleepBetweenPolls));
            }

            resetEvent.Set();
        }

        void SpinOnce(DateTime startSlice, CancellationToken cancellationToken)
        {
            if (nextRetrieval > currentTimeProvider() || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Logger.DebugFormat("Polling for timeouts at {0}.", currentTimeProvider);

            DateTime nextTimeToQuery;
            var timeoutDatas = TimeoutsPersister.GetNextChunk(startSlice, out nextTimeToQuery);

            foreach (var timeoutData in timeoutDatas)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                MessageSender.Send(CreateTransportMessage(timeoutData.Item1), new SendOptions(DispatcherAddress));

                if (startSlice < timeoutData.Item2)
                {
                    startSlice = timeoutData.Item2;
                }
            }

            lock (lockObject)
            {
                //Check if nextRetrieval has been modified (This means that a push come in) and if it has check if it is earlier than nextTimeToQuery time
                if (timeoutPushed && nextRetrieval < nextTimeToQuery)
                {
                    nextTimeToQuery = nextRetrieval;
                }

                timeoutPushed = false;

                // we cap the next retrieval to max 1 minute this will make sure that we trip the circuit breaker if we
                // loose connectivity to our storage. This will also make sure that timeouts added (during migration) direct to storage
                // will be picked up after at most 1 minute
                var maxNextRetrieval = currentTimeProvider() + TimeSpan.FromMinutes(1);

                nextRetrieval = nextTimeToQuery > maxNextRetrieval ? maxNextRetrieval : nextTimeToQuery;

                Logger.DebugFormat("Polling next retrieval is at {0}.", nextRetrieval.ToLocalTime());
            }
            
            circuitBreaker.Success();
        }

        static TransportMessage CreateTransportMessage(string timeoutId)
        {
            //use the dispatcher as the reply to address so that retries go back to the dispatcher q
            // instead of the main endpoint q
            var transportMessage = ControlMessage.Create();

            transportMessage.Headers["Timeout.Id"] = timeoutId;

            return transportMessage;
        }

        void TimeoutsManagerOnTimeoutPushed(TimeoutData timeoutData)
        {
            lock (lockObject)
            {
                if (nextRetrieval > timeoutData.Time)
                {
                    nextRetrieval = timeoutData.Time;
                    timeoutPushed = true;
                }
            }
        }

        static ILog Logger = LogManager.GetLogger<TimeoutPersisterReceiver>();

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        readonly object lockObject = new object();
        ManualResetEvent resetEvent = new ManualResetEvent(true);
        DateTime nextRetrieval;
        volatile bool timeoutPushed;
        CancellationTokenSource tokenSource;
        private Func<DateTime> currentTimeProvider;
    }
}