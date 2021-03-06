﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Machine;
using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    public class AccelerationBuilder
    {
        /// <summary>
        /// Initial N which is a parameter of acceleration
        /// </summary>
        public readonly int InitialN;

        /// <summary>
        /// Initial delta which is a parameter of acceleration.
        /// </summary>
        public readonly int InitialDeltaT;

        /// <summary>
        /// Delta which is used to compensate acceleration duration
        /// </summary>
        public readonly Int16 BaseDeltaT;

        /// <summary>
        /// Delta which is used to compensate acceleration duration
        /// </summary>
        public readonly Int16 BaseRemainder;

        /// <summary>
        /// How many steps will be taken with the acceleration with direction sign.
        /// </summary>
        public readonly int StepCount;

        /// <summary>
        /// How many steps will be taken with the acceleration - without direction information.
        /// </summary>
        public readonly int StepCountAbsolute;

        /// <summary>
        /// Determines whether acceleration speeds up or slows down the movement.
        /// </summary>
        public readonly bool IsDeceleration;

        /// <summary>
        /// Real delta which this acceleration is continuing from
        /// </summary>
        public readonly int StartDeltaT;

        /// <summary>
        /// End delta which could be continued after acceleration
        /// </summary>
        public readonly int EndDelta;

        /// <summary>
        /// How many ticks the acceleration lasts.
        /// </summary>
        public readonly long TotalTickCount;


        internal AccelerationBuilder(double c0, int targetDelta, int stepCount, long tickCount)
        {
            if (targetDelta < 0)
                throw new NotSupportedException("Target delta has to be positive");

            if (stepCount == 0)
            {
                //keep everything at zero
                return;
            }

            StepCount = stepCount;
            StepCountAbsolute = Math.Abs(stepCount);
            IsDeceleration = c0 < 0;
            EndDelta = targetDelta;


            if (StepCountAbsolute == 1) checked
                {
                    InitialDeltaT = StartDeltaT = (int)tickCount;
                    TotalTickCount = tickCount;
                    return;
                }

            c0 = Math.Abs(c0);
            findInitialDeltaT(c0, targetDelta, out InitialDeltaT, out InitialN);
            StartDeltaT = InitialDeltaT;

            checked
            {
                var sequence = calculateRealSequence();

                var accelerationTickCount = sequence.Sum();
                var tickCountDifference = tickCount - accelerationTickCount;
                var desiredBaseDelta = tickCountDifference / StepCountAbsolute;
                if (desiredBaseDelta > Int16.MaxValue)
                    throw new NotSupportedException("Acceleration error is out of bounds");
                if (desiredBaseDelta < Int16.MinValue)
                    throw new NotSupportedException("Acceleration error is out of bounds");

                BaseDeltaT = (Int16)(desiredBaseDelta);
                if (tickCountDifference < 0)
                    BaseDeltaT -= 1;

                TotalTickCount = BaseDeltaT * StepCountAbsolute + accelerationTickCount;
                BaseRemainder = (Int16)(tickCount - TotalTickCount);
                TotalTickCount += BaseRemainder;
            }
        }


        internal static AccelerationBuilder FromTo(Speed initialSpeed, Speed desiredSpeed, Acceleration acceleration, int stepCountLimit)
        {
            checked
            {
                if (stepCountLimit == 0)
                    return new AccelerationBuilder(0, 0, 0, 0);

                var timeScale = Constants.TimerFrequency;

                Speed reachedSpeed;
                int accelerationSteps;
                var accelerationTime = CalculateTime(initialSpeed, desiredSpeed, acceleration, stepCountLimit, out reachedSpeed, out accelerationSteps);
                var initialSpeedF = 1.0 * initialSpeed.StepCount / initialSpeed.Ticks * timeScale;
                var reachedSpeedF = 1.0 * reachedSpeed.StepCount / reachedSpeed.Ticks * timeScale;
                var plan = FromTo(initialSpeedF, reachedSpeedF, accelerationSteps, accelerationTime);

                return plan;
            }
        }

        internal static AccelerationBuilder FromTo(double initialSpeed, double endSpeed, int stepCount, double exactDuration)
        {
            var absoluteInitialSpeed = Math.Abs(initialSpeed);
            var absoluteEndSpeed = Math.Abs(endSpeed);
            var tickCount = (int)(exactDuration * Constants.TimerFrequency);

            var acceleration = Math.Abs(absoluteEndSpeed - absoluteInitialSpeed) / exactDuration;
            var rawC0 = Constants.TimerFrequency * Math.Sqrt(2 / acceleration);

            var isDeceleration = absoluteInitialSpeed > absoluteEndSpeed;

            var targetDelta = (int)Math.Abs(Math.Round(Constants.TimerFrequency / endSpeed));
            if (targetDelta < 0)
                //overflow when decelerating to stand still
                targetDelta = int.MaxValue;

            if (isDeceleration)
                rawC0 = -rawC0;

            var plan = new AccelerationBuilder(rawC0, targetDelta, stepCount, tickCount);
            return plan;
        }

        internal static AccelerationBuilder FromTo(Speed initialSpeed, Speed targetSpeed, int stepCount, double exactDuration)
        {
            var timeScale = Constants.TimerFrequency;
            var initialSpeedF = 1.0 * initialSpeed.StepCount / initialSpeed.Ticks * timeScale;
            var targetSpeedF = 1.0 * targetSpeed.StepCount / targetSpeed.Ticks * timeScale;

            return FromTo(initialSpeedF, targetSpeedF, stepCount, exactDuration);
        }

        internal static double CalculateTime(Speed initialSpeed, Speed desiredSpeed, Acceleration acceleration, int stepCountLimit)
        {
            Speed reachedSpeed;
            int accelerationDistance;
            return CalculateTime(initialSpeed, desiredSpeed, acceleration, stepCountLimit, out reachedSpeed, out accelerationDistance);
        }

        internal static int CalculateInitialN(double c0, int targetDelta)
        {
            var exactInitialN = targetDelta == int.MaxValue ? 0 : (int)Math.Round(Math.Pow(c0 * c0 - targetDelta * targetDelta, 2) / 4 / c0 / c0 / targetDelta / targetDelta);
            return exactInitialN;
        }

        internal static int FindInitialN(double c0, int startDeltaT)
        {
            throw new NotImplementedException();
        }

        internal static int FindEndN(int c0, int initialN, int tickcount)
        {
            throw new NotImplementedException();
        }

        internal static int FindTargetDelta(int c0, int initialN, int endN)
        {
            throw new NotImplementedException();
        }
        
        internal static double CalculateTime(Speed initialSpeed, Speed desiredSpeed, Acceleration acceleration, int stepCountLimit, out Speed reachedSpeed, out int stepCount)
        {
            if (stepCountLimit == 0)
            {
                reachedSpeed = Speed.Zero;
                stepCount = 0;
                return 0;
            }

            checked
            {
                var absStepCountLimit = Math.Abs(stepCountLimit);
                //TODO verify/improve precision
                var timeScale = Constants.TimerFrequency;
                var initialSpeedF = initialSpeed.Ticks == 0 ? 0.0 : 1.0 * initialSpeed.StepCount / initialSpeed.Ticks * timeScale;
                var desiredSpeedF = desiredSpeed.Ticks == 0 ? 0.0 : 1.0 * desiredSpeed.StepCount / desiredSpeed.Ticks * timeScale;
                var accelerationF = 1.0 * acceleration.Speed.StepCount / acceleration.Speed.Ticks / acceleration.Ticks * timeScale * timeScale;
                var desiredAccelerationTime = Math.Abs(desiredSpeedF - initialSpeedF) / accelerationF;
                var isDeceleration = initialSpeedF > desiredSpeedF;
                var decelerationCoefficient = isDeceleration ? -1 : 1;

                var a = 0.5 * accelerationF;
                var b = initialSpeedF * decelerationCoefficient;
                var c = -absStepCountLimit;
                var availableAccelerationTime = (-b + Math.Sqrt(b * b - 4 * a * c)) / 2 / a;

                var accelerationTime = Math.Min(desiredAccelerationTime, availableAccelerationTime);
                var accelerationDistanceF = (initialSpeedF * accelerationTime + 0.5 * decelerationCoefficient * accelerationF * accelerationTime * accelerationTime);

                stepCount = (int)accelerationDistanceF * Math.Sign(stepCountLimit);
                var reachedSpeedF = initialSpeedF + decelerationCoefficient * accelerationF * accelerationTime;
                reachedSpeed = new Speed((int)reachedSpeedF, timeScale);
                return accelerationTime;
            }
        }

        internal AccelerationInstruction ToInstruction()
        {
            checked
            {
                var initialN = IsDeceleration ? -InitialN : InitialN;
                return new AccelerationInstruction((Int16)StepCount, InitialDeltaT, BaseDeltaT, BaseRemainder, initialN);
            }
        }

        private void findInitialDeltaT(double c0, int targetDelta, out int globalInitialDeltaT, out int globalInitialN)
        {
            var exactInitialN = CalculateInitialN(c0, targetDelta);
            var minimalInitialN = IsDeceleration ? StepCountAbsolute : 0;

            exactInitialN = IsDeceleration ? StepCountAbsolute + exactInitialN : exactInitialN - StepCountAbsolute;
            exactInitialN = Math.Max(0, exactInitialN);

            globalInitialDeltaT = (int)Math.Round(c0);
            globalInitialN = 0;
            var globalRemainderBuffer2 = 0;
            while (true)
            {
                if (globalInitialN >= exactInitialN)
                {
                    //try if the initial conditions match the requirements
                    var endDelta = getEndDelta(globalInitialDeltaT, globalInitialN);
                    //if (targetDelta >= endDelta)
                    return;
                }

                nextStep_SpeedUpDirection(ref globalInitialDeltaT, ref globalInitialN, ref globalRemainderBuffer2);
            }
        }

        private int getEndDelta(int initialDeltaT, int initialN)
        {
            var remainderBuffer2 = 0;
            for (var i = 0; i < StepCountAbsolute; ++i)
            {
                if (initialN < 0 || initialDeltaT < 0)
                    throw new NotSupportedException("Invalid values");

                nextStep_RealDirection(ref initialDeltaT, ref initialN, ref remainderBuffer2);
            }

            if (IsDeceleration && initialN <= 0)
                //deceleration to standstill
                return int.MaxValue;

            nextStep_RealDirection(ref initialDeltaT, ref initialN, ref remainderBuffer2);
            return initialDeltaT;
        }



        private long[] calculateRealSequence()
        {
            var currentDelta = InitialDeltaT;
            var currentN = InitialN;
            var remainderBuffer2 = 0;

            var window = new List<long>();
            for (var i = 0; i < StepCountAbsolute; ++i)
            {
                window.Add(currentDelta);
                nextStep_RealDirection(ref currentDelta, ref currentN, ref remainderBuffer2);
                if (currentDelta < 0)
                    throw new NotSupportedException("Invalid setup");

            }
            return window.ToArray();
        }

        /// <summary>
        /// This steps acceleration/deceleration both from lowest to highest speeds (deceleration is reversed therefore!)
        /// </summary>
        private void nextStep_SpeedUpDirection(ref int currentDelta, ref int currentN, ref int remainderBuffer2)
        {
            checked
            {
                if (currentN == 0) checked
                    {
                        //compensate for initial error (TODO include deceleration properly)
                        currentDelta = currentDelta * 676 / 1000;
                    }

                ++currentN;
                remainderBuffer2 += 2 * currentDelta;
                var change = remainderBuffer2 / (4 * currentN + 1);
                remainderBuffer2 = remainderBuffer2 % (4 * currentN + 1);

                currentDelta = currentDelta - change;

                //System.Diagnostics.Debug.WriteLine(currentDelta + " " + currentN + " " + remainderBuffer2);
            }
        }

        /// <summary>
        /// This steps acceleration/deceleration in precisely same way as the CNC implementation.
        /// </summary>
        private void nextStep_RealDirection(ref int currentDelta, ref int currentN, ref int remainderBuffer2)
        {
            if (currentN == 0) checked
                {
                    //compensate for initial error
                    currentDelta = currentDelta * 676 / 1000;
                }

            currentN += IsDeceleration ? -1 : 1;
            remainderBuffer2 += 2 * currentDelta;
            var change = remainderBuffer2 / (4 * currentN + 1);
            remainderBuffer2 = remainderBuffer2 % (4 * currentN + 1);

            if (IsDeceleration)
                change *= -1;

            currentDelta = currentDelta - change;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var prefix = IsDeceleration ? "PD" : "PA";

            return string.Format(prefix + "({0}, {1}, {2}, {3}:{4})", StepCountAbsolute, TotalTickCount, InitialN, StartDeltaT + BaseDeltaT, EndDelta + BaseDeltaT);
        }
    }
}
