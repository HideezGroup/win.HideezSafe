﻿using ServiceLibrary;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ServiceLibrary.Implementation
{
    class ServiceClientSessionManager
    {
        readonly object sessionsLock = new object();

        public IReadOnlyCollection<ServiceClientSession> Sessions { get; } = new List<ServiceClientSession>();

        public ServiceClientSessionManager()
        {
        }

        internal ServiceClientSession Add(ICallbacks callbacks)
        {
            var session = new ServiceClientSession(callbacks);
            lock (sessionsLock)
            {
                (Sessions as List<ServiceClientSession>).Add(session);
            }
            return session;
        }

        internal void Remove(ServiceClientSession session)
        {
            lock (sessionsLock)
            {
                (Sessions as List<ServiceClientSession>).Remove(session);
            }
        }

        ServiceClientSession GetSessionById(string id)
        {
            lock (sessionsLock)
            {
                return Sessions.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
