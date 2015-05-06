using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace StepperMotor
{
    class StepperMotor
    {
        private const Int64 TICKS_PER_MS = TimeSpan.TicksPerMillisecond;
        private int stepNumber;
        private int speed;
        private bool reverse;
        private long lastStepTime;
        private int numberOfSteps;
        private long stepDelay;
        private double maxCurrentDrawOnMove;

        private OutputPort coilA;
        private OutputPort coilB;
        private OutputPort brakeA;
        private OutputPort brakeB;
        private PWM pwmA;
        private PWM pwmB;
        private AnalogInput curSenseA;
        private AnalogInput curSenseB;

        const int MAX_AMPS = 2;

        public StepperMotor(int number_of_steps, Cpu.Pin motorPin1, Cpu.Pin motorPin2,
            Cpu.PWMChannel pwmPin1, Cpu.PWMChannel pwmPin2,
            Cpu.AnalogChannel curSensePin1, Cpu.AnalogChannel curSensePin2,
            Cpu.Pin brakeAPin, Cpu.Pin brakeBPin) 
        {
            stepNumber = 0;      // which step the motor is on 
            speed = 0;        // the motor speed, in revolutions per minute 
            reverse = false;      // motor direction 
            lastStepTime = 0;    // time stamp in ms of the last step taken 
            numberOfSteps = number_of_steps;    // total number of steps for this motor 

            // Netduino pins for the motor control connection: 
            coilA = new OutputPort(motorPin1, false);
            coilB = new OutputPort(motorPin2, false);
            pwmA = new PWM(pwmPin1, 10000, 1, false);
            pwmB = new PWM(pwmPin2, 10000, 1, false);
            curSenseA = new AnalogInput(curSensePin1);
            curSenseB = new AnalogInput(curSensePin2);
            brakeA = new OutputPort(brakeAPin, true);
            brakeB = new OutputPort(brakeBPin, true);
        }

        public float MaxCurrentOnMove
        {
            get { return (float)maxCurrentDrawOnMove * MAX_AMPS; }
        }

        public int CurrentPosition
        {
            get { return stepNumber; }
        }
 
        /* Sets the speed in revs per minute */ 
        public void SetSpeed(long whatSpeed) 
        { 
            stepDelay = 60 * 1000 / numberOfSteps / whatSpeed; 
        } 
 
        /* Moves the motor steps_to_move steps.  If the number is negative,  
           the motor moves in the reverse direction. */ 
        public void Step(int stepsToMove) 
        {
            long curMS;
            int stepsLeft = System.Math.Abs(stepsToMove);  // how many steps to take 
            
            // Clear our max current sense value for this move
            maxCurrentDrawOnMove = 0;

            // determine direction based on whether stepsToMove is + or - 
            reverse = (stepsToMove < 0);

            //Make sure outputs are set before starting the PWMs
            StepMotor(stepNumber % 4);

            //pwmA.Start();
            //pwmB.Start();
            brakeA.Write(false);
            brakeB.Write(false);

            // decrement the number of steps, moving one step each time: 
            while(stepsLeft > 0)
            {
                // move only if the appropriate delay has passed:
                curMS = Utility.GetMachineTime().Ticks / TICKS_PER_MS;
                if (curMS - lastStepTime >= stepDelay)
                {
                    // get the timeStamp of when you stepped: 
                    lastStepTime = curMS;

                    // Get the current right before we move as it will be greatest at this point
                    double curSenseTemp = curSenseA.Read();
                    if (curSenseTemp > maxCurrentDrawOnMove)
                        maxCurrentDrawOnMove = curSenseTemp;
                    curSenseTemp = curSenseB.Read();
                    if (curSenseTemp > maxCurrentDrawOnMove)
                        maxCurrentDrawOnMove = curSenseTemp;

                    //If our maximum current gets close to 2 amps, we need to drop our pwm
                    //output voltage to limit the current.
                    //If 90% of maximum 2 amp current
                    if (maxCurrentDrawOnMove >= 0.9)
                    {
                        //Drop by 10%
                        pwmA.DutyCycle -= 0.1;
                        pwmB.DutyCycle = pwmA.DutyCycle;
                    }

                    // increment or decrement the step number, depending on direction:
                    if (!reverse)
                    {
                        stepNumber++;
                        if (stepNumber == numberOfSteps)
                        {
                            stepNumber = 0;
                        }
                    }
                    else
                    {
                        if (stepNumber == 0)
                        {
                            stepNumber = numberOfSteps;
                        }
                        stepNumber--;
                    }

                    // decrement the steps left: 
                    stepsLeft--;

                    // step the motor to step number 0, 1, 2, or 3: 
                    StepMotor(stepNumber % 4);
                }
            }

            pwmA.Stop();
            pwmB.Stop();

            brakeA.Write(true);
            brakeB.Write(true);
        }
 
        /* Moves the motor forward or backwards. */
        private void StepMotor(int thisStep)
        {
            switch (thisStep)
            {
                case 0:    //11
                    pwmB.Stop();
                    coilA.Write(true);
                    coilB.Write(true);
                    pwmA.Start();
                    break;

                case 1:    //01
                    pwmA.Stop();
                    coilA.Write(false);
                    coilB.Write(true);
                    pwmB.Start();
                    break;

                case 2:    //00 
                    pwmB.Stop();
                    coilA.Write(false);
                    coilB.Write(false);
                    pwmA.Start();
                    break;

                case 3:    //10
                    pwmA.Stop();
                    coilA.Write(true);
                    coilB.Write(false);
                    pwmB.Start();
                    break;
            }
        }
    }
}
