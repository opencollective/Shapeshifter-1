﻿namespace Shapeshifter.UserInterface.WindowsDesktop.Infrastructure.Threading
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    using Interfaces;

    using Logging.Interfaces;

    [ExcludeFromCodeCoverage]
    class ThreadLoop: IThreadLoop
    {
        readonly ILogger logger;

        public ThreadLoop(ILogger logger)
        {
            this.logger = logger;
        }

        public bool IsRunning { get; private set; }

        public void Start(Func<Task> action, CancellationToken token)
        {
            lock (this)
            {
                if (IsRunning)
                {
                    throw new InvalidOperationException("Can't start the thread loop twice.");
                }

                IsRunning = true;
            }

            RunAsync(action, token)
                .ConfigureAwait(false);
        }

        async Task RunAsync(Func<Task> action, CancellationToken token)
        {
            while (!token.IsCancellationRequested && IsRunning)
            {
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    logger.Error("An error occured in the thread loop: " + ex);
                    Stop();
                    throw;
                }
            }

            Stop();
        }

        public void Stop()
        {
            lock (this)
            {
                IsRunning = false;
            }
        }
    }
}