using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace StepperMotor
{
    public class Program
    {
        public enum Modes
        {
            Manual,
            Auto,
            Setup
        }
        public static Modes CurrentMode = Modes.Manual;
        public static int AbsolutePosition = 0;
        public static float BoardThickness;
        public static float BatteryVoltage;
        static SerialLCD lcd = null;

        const int COUNTS_PER_SIXTEENTH = 40;

        public static void Main()
        {
            // write your code here
            StepperMotor stepper = new StepperMotor(200, Pins.GPIO_PIN_D12, Pins.GPIO_PIN_D13,
                PWMChannels.PWM_PIN_D3, PWMChannels.PWM_PIN_D11,
                AnalogChannels.ANALOG_PIN_A0, AnalogChannels.ANALOG_PIN_A1,
                Pins.GPIO_PIN_D7, Pins.GPIO_PIN_D8);

            ButtonMonitor upButton = new ButtonMonitor(Pins.GPIO_PIN_D4);
            ButtonMonitor downButton = new ButtonMonitor(Pins.GPIO_PIN_D5);
            ButtonMonitor leftButton = new ButtonMonitor(Pins.GPIO_PIN_D10);
            ButtonMonitor rightButton = new ButtonMonitor(Pins.GPIO_PIN_D6);

            const int MAX_VOLTAGE = 16;
            const int MIN_ALLOWED_VOLTAGE = 7;
            AnalogInput batteryLevelInput = new AnalogInput(AnalogChannels.ANALOG_PIN_A2);
            
            const float DISTANCE_FACTOR = 1F;
            AnalogInput distanceSensorInput = new AnalogInput(AnalogChannels.ANALOG_PIN_A3);

            int backoffCounts = 8 * COUNTS_PER_SIXTEENTH;
            BoardThickness = 2;
            int boardThicknessCounts = InchesToCounts(BoardThickness);
            int homePositionCount = 200;
            
            lcd = new SerialLCD(SerialPorts.COM1);
            lcd.Print("Manual          Pos: " + AbsolutePosition);

            while (true)
            {
                //Check battery level
                //BatteryVoltage = (float)(batteryLevelInput.Read() * MAX_VOLTAGE);
                //if (BatteryVoltage <= MIN_ALLOWED_VOLTAGE)
                //{
                //    lcd.Print("LOW BATTERY!    Voltage: " + BatteryVoltage);
                //    Thread.Sleep(1000);
                //    continue;                            
                //}

                //Update button inputs
                upButton.Update();
                downButton.Update();
                leftButton.Update();
                rightButton.Update();

                switch (CurrentMode)
                {
                    case Modes.Manual:
                        //While in manual mode, monitor for up or down input
                        if (upButton.ButtonOn)
                        {
                            lcd.Print("Jogging Up      Pos: " + AbsolutePosition + " " + stepper.MaxCurrentOnMove + "A");
                            Move(stepper, 60, -COUNTS_PER_SIXTEENTH);
                        }
                        else if (downButton.ButtonOn)
                        {
                            lcd.Print("Jogging Down    Pos: " + AbsolutePosition + " " + stepper.MaxCurrentOnMove + "A");
                            Move(stepper, 60, COUNTS_PER_SIXTEENTH);
                        }
                        else if (leftButton.ButtonPressed)                        
                            MenuMoveLeft();                        
                        else if (rightButton.ButtonPressed)                        
                            MenuMoveRight();                        
                        else if (upButton.ButtonPressed || downButton.ButtonPressed)
                            lcd.Print("Manual          Pos: " + AbsolutePosition + " " + stepper.MaxCurrentOnMove + "A");
                        break;
                    case Modes.Auto:
                        if (leftButton.ButtonPressed)                        
                            MenuMoveLeft();                        
                        else if (rightButton.ButtonPressed)                        
                            MenuMoveRight();
                        else if (upButton.ButtonPressed)
                        {
                            //Go to backoff position
                            lcd.Print("Backoff         Pos: " + AbsolutePosition);
                            Move(stepper, 100, -backoffCounts);
                            //Reminder how far we have backed off for moving to the next cut
                            boardThicknessCounts += backoffCounts;
                            lcd.Print("Auto            Pos: " + AbsolutePosition + " " + stepper.MaxCurrentOnMove + "A");
                        }
                        else if (downButton.ButtonPressed)
                        {
                            //Go to next board thickness
                            lcd.Print("Next Cut        Pos: " + AbsolutePosition);

                            //We want to do some sort of ramp to the position.
                            //Maybe integrate by moving half the distance while looping
                            //until we get within a tolerance of the final position.
                            //For now just break it into two moves to update the
                            //display halfway through
                            Move(stepper, 100, boardThicknessCounts/2);
                            lcd.Print("Auto            Pos: " + AbsolutePosition + " " + stepper.MaxCurrentOnMove + "A");
                            Move(stepper, 100, boardThicknessCounts/2);
                            lcd.Print("Auto            Pos: " + AbsolutePosition + " " + stepper.MaxCurrentOnMove + "A");

                            //Reset any backoff counts after moving to next cut
                            boardThicknessCounts = InchesToCounts(BoardThickness);
                        }
                        else if (upButton.HoldTime > (4 * TimeSpan.TicksPerSecond))
                        {
                            //If the up button is held for more than four seconds
                            //move back up to the starting or home position
                            lcd.Print("Moving Home     Pos: " + AbsolutePosition + " " + stepper.MaxCurrentOnMove + "A");
                            Move(stepper, 100, homePositionCount - AbsolutePosition);
                            boardThicknessCounts = InchesToCounts(BoardThickness);
                            lcd.Print("Auto            Pos: " + AbsolutePosition + " " + stepper.MaxCurrentOnMove + "A");
                        }
                        break;
                    case Modes.Setup:
                        if (leftButton.ButtonPressed)
                            MenuMoveLeft();
                        else if (rightButton.ButtonPressed)
                            MenuMoveRight();
                        else if (upButton.ButtonPressed)
                        {
                            if (BoardThickness < 8.0F)
                            {
                                //Increase the board thickness by 1/4"
                                BoardThickness += 0.25F;
                                boardThicknessCounts = InchesToCounts(BoardThickness);
                                lcd.Print("Setup           Cut:" + BoardThickness);
                            }
                        }
                        else if (downButton.ButtonPressed)
                        {
                            if (BoardThickness > 0.25F)
                            {
                                //Decrease the board thickness by 1/4"
                                BoardThickness -= 0.25F;
                                boardThicknessCounts = InchesToCounts(BoardThickness);
                                lcd.Print("Setup           Cut:" + BoardThickness);
                            }
                        }
                        break;
                }
            }
        }

        static void MenuMoveRight()
        {
            switch (CurrentMode)
            {
                case Modes.Auto:
                    //Right to Setup
                    CurrentMode = Modes.Setup;
                    lcd.Print("Setup           Cut:" + BoardThickness);
                    break;
                case Modes.Manual:
                    //Right to Auto
                    CurrentMode = Modes.Auto;
                    lcd.Print("Auto            Pos: " + AbsolutePosition);
                    break;
                case Modes.Setup:
                    //Right to Manual
                    CurrentMode = Modes.Manual;
                    lcd.Print("Manual          Pos: " + AbsolutePosition);                        
                    break;
            }
        }

        static void MenuMoveLeft()
        {
            switch (CurrentMode)
            {
                case Modes.Manual:
                    //Left to Setup
                    CurrentMode = Modes.Setup;
                    lcd.Print("Setup           Cut:" + BoardThickness);
                    break;
                case Modes.Setup:
                    //Left to Auto
                    CurrentMode = Modes.Auto;
                    lcd.Print("Auto            Pos: " + AbsolutePosition);
                    break;
                case Modes.Auto:
                    //Left to Manual
                    CurrentMode = Modes.Manual;
                    lcd.Print("Manual          Pos: " + AbsolutePosition);
                    break;
            }
        }

        static int InchesToCounts(float inches)
        {
            return (int)(inches * 16) * COUNTS_PER_SIXTEENTH;
        }

        static void Move(StepperMotor stepper, long speed, int stepsToMove)
        {
            stepper.SetSpeed(speed);
            stepper.Step(stepsToMove);
            AbsolutePosition += stepsToMove;
        }
    }
}
