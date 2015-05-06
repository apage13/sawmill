using System;
using Microsoft.SPOT;
using System.IO.Ports;

namespace StepperMotor
{
    public class SerialLCD
    {
        private SerialPort sPort;
        private byte[] buffer;

        public SerialLCD(string portName)
        {
            sPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
            buffer = new byte[48];
        }

        ~SerialLCD()
        {
            sPort.Close();
            sPort.Dispose();
        }

        public void Print(string msg)
        {
            if (!sPort.IsOpen)
                sPort.Open();

            int sendChars = msg.Length;
            
            for (int i=0; i<sendChars && i<buffer.Length; i++)
                buffer[i] = (byte)msg[i];

            //Clear Screen
            sPort.WriteByte(254);
            sPort.WriteByte(1);
            //Update Message
            sPort.Write(buffer, 0, sendChars);
        }
    }
}
