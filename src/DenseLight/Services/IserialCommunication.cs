using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DenseLight.Services
{
    public interface IserialCommunication : IDisposable
    {
        event EventHandler<string> DataReceived;
        event EventHandler<string> CommunicationError;

        bool IsOpen { get; }
        string PortName { get; }

        Task OpenAsync(string portName);


    }
}
