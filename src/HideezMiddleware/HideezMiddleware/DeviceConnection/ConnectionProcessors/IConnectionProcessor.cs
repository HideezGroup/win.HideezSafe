using System;

namespace HideezMiddleware.DeviceConnection.ConnectionProcessors
{
    public interface IConnectionProcessor
    {
        void Start();

        void Stop();
    }
}