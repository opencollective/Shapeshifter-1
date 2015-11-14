﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Shapeshifter.UserInterface.WindowsDesktop.Infrastructure.Logging.Interfaces;
using Shapeshifter.UserInterface.WindowsDesktop.Infrastructure.Threading.Interfaces;

namespace Shapeshifter.UserInterface.WindowsDesktop.Infrastructure.Threading
{
    internal class ConsumerThreadLoop : IConsumerThreadLoop
    {
        private readonly IThreadLoop internalLoop;
        private readonly ILogger logger;

        private int countAvailable;

        public ConsumerThreadLoop(
            IThreadLoop internalLoop,
            ILogger logger)
        {
            this.internalLoop = internalLoop;
            this.logger = logger;
        }

        public bool IsRunning => internalLoop.IsRunning;

        public void Stop()
        {
            lock (this)
            {
                internalLoop.Stop();
                countAvailable = 0;
            }
        }

        public void Notify(Func<Task> action, CancellationToken token)
        {
            lock (this)
            {
                var newCount = Interlocked.Increment(ref countAvailable);
                logger.Information($"Consumer count incremented to {countAvailable}.");

                if (newCount > 0 && !internalLoop.IsRunning)
                {
                    SpawnThread(action, token);
                }
            }
        }
        
        private void SpawnThread(Func<Task> action, CancellationToken token)
        {
            internalLoop.Start(async () => await Tick(action, token), token);
        }
        
        private async Task Tick(Func<Task> action, CancellationToken token)
        {
            lock (this)
            {
                DecrementAvailableWorkCount();

                if (ShouldAbort(token))
                {
                    internalLoop.Stop();
                    return;
                }
            }

            await action();
        }

        private bool ShouldAbort(CancellationToken token)
        {
            return token.IsCancellationRequested || countAvailable == 0;
        }

        private void DecrementAvailableWorkCount()
        {
            Interlocked.Decrement(ref countAvailable);
            logger.Information($"Consumer count decremented to {countAvailable}.");
        }
    }
}