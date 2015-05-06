using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace StepperMotor
{
    class ButtonMonitor
    {
        private InputPort buttonInput;
        public bool ButtonOn;
        public bool ButtonPressed;

        private long pressedTime;
        public long HoldTime
        {
            get 
            {
                //If the button is not currently being pushed, return a hold time of zero
                if (!ButtonOn) return 0;

                //Otherwise return the number of ticks between now and when it was pressed
                return Utility.GetMachineTime().Ticks - pressedTime;
            }
        }

        public ButtonMonitor(Cpu.Pin inputPin)
        {
            buttonInput = new InputPort(inputPin, true, Port.ResistorMode.PullUp);
        }

        public void Update()
        {
            ButtonPressed = false;

            //Read True = Button Not Pressed
            if (buttonInput.Read())
            {
                
                if (ButtonOn)
                {
                    ButtonOn = false;
                    ButtonPressed = true;
                }
            }
            else
            {
                if (!ButtonOn)
                {
                    pressedTime = Utility.GetMachineTime().Ticks;
                    ButtonOn = true;
                }
            }
        }
    }
}
