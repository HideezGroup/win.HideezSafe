﻿using ServiceLibrary;
using System;

namespace ServiceLibrary.Implementation
{
    class ServiceClientSession
    {
        private readonly ICallbacks callbacks;

        public ServiceClientSession(ICallbacks callbacks)
        {
            this.callbacks = callbacks;
            Id = new Guid().ToString();
        }

        public string Id { get; }

        public void LockClientWorkstation()
        {
            callbacks.LockWorkstationRequest();
        }
    }
}
