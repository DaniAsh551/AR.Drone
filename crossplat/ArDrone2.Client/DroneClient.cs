using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArDrone2.Client.Command;
using ArDrone2.Client.Navigation;
using ArDrone2.Client.Video;
using ArDrone2.Data;
using ArDrone2.Data.Navigation;
using ArDrone2.Infrastructure;
using ArDrone2.Client.Configuration;

namespace ArDrone2.Client
{
    public class DroneClient : WorkerBase
    {
        private const string DefaultHostname = "192.168.1.1";
        private const int AckControlAndWaitForConfirmationTimeout = 1000;

        private readonly ConcurrentQueue<AtCommand> _commandQueue;
        private NavigationData _navigationData;
        private StateRequest _stateRequest;

        private readonly CommandSender _commandSender;
        private readonly NetworkConfiguration _networkConfiguration;
        private readonly NavdataAcquisition _navdataAcquisition;
        private readonly VideoAcquisition _videoAcquisition;

        public DroneClient(string hostname)
        {
            _networkConfiguration = new NetworkConfiguration(hostname);
            _commandQueue = new ConcurrentQueue<AtCommand>();
            _navigationData = new NavigationData();

            _commandSender = new CommandSender(NetworkConfiguration, _commandQueue);
            _navdataAcquisition = new NavdataAcquisition(NetworkConfiguration, OnNavdataPacketAcquired, OnNavdataAcquisitionStarted, OnNavdataAcquisitionStopped);
            _videoAcquisition = new VideoAcquisition(NetworkConfiguration, OnVideoPacketAcquired);
        }

        public DroneClient() : this(DefaultHostname)
        {
        }

        /// <summary>
        /// Watchdog.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        protected override void Loop(CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                if (_navdataAcquisition.IsAlive == false) _navdataAcquisition.Start();
                Thread.Sleep(10);
            }

            if (_navdataAcquisition.IsAlive) _navdataAcquisition.Stop();
            if (_commandSender.IsAlive) _commandSender.Stop();
            if (_videoAcquisition.IsAlive) _videoAcquisition.Stop();
        }

        #region Private

        private void OnNavdataAcquisitionStarted()
        {
            if (_commandSender.IsAlive == false) _commandSender.Start();
            if (_videoAcquisition.IsAlive == false) _videoAcquisition.Start();
        }

        private void OnNavdataAcquisitionStopped()
        {
            if (_commandSender.IsAlive) _commandSender.Stop();
            if (_videoAcquisition.IsAlive) _videoAcquisition.Stop();
        }

        private void OnNavdataPacketAcquired(NavigationPacket packet)
        {
            if (NavigationPacketAcquired != null)
                NavigationPacketAcquired(packet);

            UpdateNavigationData(packet);
        }

        private void UpdateNavigationData(NavigationPacket packet)
        {
            NavigationData navigationData;
            if (NavigationPacketParser.TryParse(ref packet, out navigationData))
            {
                OnNavigationDataAcquired(navigationData);

                _navigationData = navigationData;

                ProcessStateTransitions(navigationData.State);
            }
        }

        private void OnNavigationDataAcquired(NavigationData navigationData)
        {
            if (NavigationDataAcquired != null)
                NavigationDataAcquired(navigationData);
        }

        private void ProcessStateTransitions(NavigationState state)
        {
            if (state.HasFlag(NavigationState.Bootstrap))
            {
                _commandQueue.Flush();
                var settings = new Settings();
                settings.General.NavdataDemo = false;
                Send(settings);
            }

            if (state.HasFlag(NavigationState.Watchdog))
            {
                Trace.TraceWarning("Communication Watchdog!");
            }

            switch (_stateRequest)
            {
                case StateRequest.None:
                    return;
                case StateRequest.Emergency:
                    if (state.HasFlag(NavigationState.Flying))
                        Send(RefCommand.Emergency);
                    else
                        _stateRequest = StateRequest.None;
                    break;
                case StateRequest.ResetEmergency:
                    Send(RefCommand.Emergency);
                    _stateRequest = StateRequest.None;
                    break;
                case StateRequest.Land:
                    if (state.HasFlag(NavigationState.Flying) &&
                        state.HasFlag(NavigationState.Landing) == false)
                    {
                        Send(RefCommand.Land);
                    }
                    else
                        _stateRequest = StateRequest.None;
                    break;
                case StateRequest.Fly:
                    if (state.HasFlag(NavigationState.Landed) &&
                        state.HasFlag(NavigationState.Takeoff) == false &&
                        state.HasFlag(NavigationState.Emergency) == false)
                    {
                        Send(RefCommand.Takeoff);
                    }
                    else
                        _stateRequest = StateRequest.None;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnVideoPacketAcquired(VideoPacket packet)
        {
            if (VideoPacketAcquired != null)
                VideoPacketAcquired(packet);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event queue for all listeners interested in NavigationPacketAcquired events.
        /// This event will be dispatched on NavdataAcquisition thread.
        /// </summary>
        public event Action<NavigationPacket> NavigationPacketAcquired;

        /// <summary>
        /// Event queue for all listeners interested in NavigationDataAcquired events. 
        /// This event will be dispatched on NavdataAcquisition thread.
        /// </summary>
        public event Action<NavigationData> NavigationDataAcquired;

        /// <summary>
        /// Event queue for all listeners interested in VideoPacketAcquired events.
        /// This event will be dispatched on VideoAcquisition thread.
        /// </summary>
        public event Action<VideoPacket> VideoPacketAcquired;

        #endregion

        #region Properties

        public bool IsActive
        {
            get { return IsAlive; }
        }

        public bool IsConnected
        {
            get { return _navdataAcquisition.IsAcquiring; }
        }

        public NetworkConfiguration NetworkConfiguration
        {
            get { return _networkConfiguration; }
        }

        public NavigationData NavigationData
        {
            get { return _navigationData; }
        }

        #endregion

        #region Api

        public Task<Settings> GetConfigurationTask()
        {
            var configurationAcquisition = new ConfigurationAcquisition(this);
            var task = configurationAcquisition.CreateTask();
            return task;
        }

        public void Send(AtCommand command)
        {
            _commandQueue.Enqueue(command);
        }

        public void Send(Settings settings)
        {
            KeyValuePair<string, string> item;
            while (settings.Changes.TryDequeue(out item))
            {
                if (string.IsNullOrEmpty(settings.Custom.SessionId) == false &&
                    string.IsNullOrEmpty(settings.Custom.ProfileId) == false &&
                    string.IsNullOrEmpty(settings.Custom.ApplicationId) == false)
                    Send(new ConfigIdsCommand(settings.Custom.SessionId, settings.Custom.ProfileId, settings.Custom.ApplicationId));

                Send(new ConfigCommand(item.Key, item.Value));
            }
        }

        public bool AckControlAndWaitForConfirmation()
        {
            Stopwatch swTimeout = Stopwatch.StartNew();

            var state = NavigationState.Unknown;
            Action<NavigationData> onNavigationData = nd => state = nd.State;
            NavigationDataAcquired += onNavigationData;
            try
            {
                bool ackControlSent = false;
                while (swTimeout.ElapsedMilliseconds < AckControlAndWaitForConfirmationTimeout)
                {
                    if (state.HasFlag(NavigationState.Command))
                    {
                        Send(ControlCommand.AckControlMode);
                        ackControlSent = true;
                    }

                    if (ackControlSent && state.HasFlag(NavigationState.Command) == false)
                    {
                        return true;
                    }
                    Thread.Sleep(5);
                }
                return false;
            }
            finally
            {
                NavigationDataAcquired -= onNavigationData;
                Trace.Write(string.Format("AckCommand done in {0} ms.", swTimeout.ElapsedMilliseconds));
            }
        }

        public void Emergency()
        {
            _stateRequest = StateRequest.Emergency;
        }

        public void ResetEmergency()
        {
            _stateRequest = StateRequest.ResetEmergency;
        }

        public void Land()
        {
            if (_navigationData.State.HasFlag(NavigationState.Flying))
                _stateRequest = StateRequest.Land;
        }

        public void Takeoff()
        {
            if (_navigationData.State.HasFlag(NavigationState.Landed))
                _stateRequest = StateRequest.Fly;
        }

        public void FlatTrim()
        {
            if (_navigationData.State.HasFlag(NavigationState.Landed))
                Send(FlatTrimCommand.Default);
        }

        public void Hover()
        {
            if (_navigationData.State.HasFlag(NavigationState.Flying))
                Send(new ProgressCommand(FlightMode.Hover, 0, 0, 0, 0));
        }

        /// <summary>
        /// This command controls the drone flight motions.
        /// </summary>
        /// <param name="mode">Enabling the use of progressive commands and/or the Combined Yaw mode (bitfield).</param>
        /// <param name="roll">Drone left-right tilt - value in range [−1..1].</param>
        /// <param name="pitch">Drone front-back tilt - value in range [−1..1].</param>
        /// <param name="yaw">Drone angular speed - value in range [−1..1].</param>
        /// <param name="gaz">Drone vertical speed - value in range [−1..1].</param>
        public void Progress(FlightMode mode, float roll = 0, float pitch = 0, float yaw = 0, float gaz = 0)
        {
            if (roll > 1 || roll < -1)
                throw new ArgumentOutOfRangeException("roll");
            if (pitch > 1 || pitch < -1)
                throw new ArgumentOutOfRangeException("pitch");
            if (yaw > 1 || yaw < -1)
                throw new ArgumentOutOfRangeException("yaw");
            if (gaz > 1 || gaz < -1)
                throw new ArgumentOutOfRangeException("gaz");

            if (_navigationData.State.HasFlag(NavigationState.Flying))
                Send(new ProgressCommand(mode, roll, pitch, yaw, gaz));
        }

        /// <summary>
        /// This command controls the drone flight motions.
        /// </summary>
        /// <param name="mode">Enabling the use of progressive commands and/or the Combined Yaw mode (bitfield).</param>
        /// <param name="roll">Drone left-right tilt - value in range [−1..1].</param>
        /// <param name="pitch">Drone front-back tilt - value in range [−1..1].</param>
        /// <param name="yaw">Drone angular speed - value in range [−1..1].</param>
        /// <param name="gaz">Drone vertical speed - value in range [−1..1].</param>
        /// <param name="psi">Magneto psi - value in range [−1..1]</param>
        /// <param name="accuracy">Magneto psi accuracy - value in range [−1..1].</param>
        public void ProgressWithMagneto(FlightMode mode, float roll = 0, float pitch = 0, float yaw = 0, float gaz = 0, float psi = 0, float accuracy = 0)
        {
            if (roll > 1 || roll < -1)
                throw new ArgumentOutOfRangeException("roll");
            if (pitch > 1 || pitch < -1)
                throw new ArgumentOutOfRangeException("pitch");
            if (yaw > 1 || yaw < -1)
                throw new ArgumentOutOfRangeException("yaw");
            if (gaz > 1 || gaz < -1)
                throw new ArgumentOutOfRangeException("gaz");
            if (psi > 1 || psi < -1)
                throw new ArgumentOutOfRangeException("psi");
            if (accuracy > 1 || accuracy < -1)
                throw new ArgumentOutOfRangeException("accuracy");

            if (_navigationData.State.HasFlag(NavigationState.Flying))
                Send(new ProgressWithMagnetoCommand(mode, roll, pitch, yaw, gaz, psi, accuracy));
        }

        #endregion

        #region FlightMoveCommands
        //Up
        public void GoUp(float speed = 1.0f)
        {
            if (speed >= 0.1)
                Send(new ProgressCommand(FlightMode.Progressive, 0, 0, 0, speed));
            else
                throw new ArgumentOutOfRangeException(nameof(speed));
        }

        /// <summary>
        /// Makes the drone go up while the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="predicate"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoUpWhile(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (predicate(this))
                    GoUp(speed);
            else
                Task.Factory.StartNew(() => GoUpWhile(predicate, speed, false));
        }

        /// <summary>
        /// Makes the drone go up until the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoUpUntil(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (!predicate(this))
                    GoUp(speed);
            else
                Task.Factory.StartNew(() => GoUpUntil(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go up for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="milliseconds"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoUpFor(int milliseconds, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);
            GoUpUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        /// <summary>
        /// Makes the drone go up for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="timeSpan"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoUpFor(TimeSpan timeSpan, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.Add(timeSpan);
            GoUpUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }


        //Down
        public void GoDown(float speed = 1.0f)
        {
            if (speed >= 0.1)
                Send(new ProgressCommand(FlightMode.Progressive, 0, 0, 0, -1.0f * speed));
            else
                throw new ArgumentOutOfRangeException(nameof(speed));
        }

        /// <summary>
        /// Makes the drone go down while the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="predicate"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoDownWhile(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (predicate(this))
                    GoDown(speed);
            else
                Task.Factory.StartNew(() => GoDownWhile(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go down until the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoDownUntil(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (!predicate(this))
                    GoDown(speed);
            else
                Task.Factory.StartNew(() => GoDownUntil(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go down for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="milliseconds"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoDownFor(int milliseconds, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);
            GoDownUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        /// <summary>
        /// Makes the drone go down for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="timeSpan"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoDownFor(TimeSpan timeSpan, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.Add(timeSpan);
            GoDownUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }


        //Forward
        public void GoForward(float speed = 1.0f)
        {
            if (speed >= 0.1)
                Send(new ProgressCommand(FlightMode.Progressive, 0, -1.0f * speed, 0, 0));
            else
                throw new ArgumentOutOfRangeException(nameof(speed));
        }

        /// <summary>
        /// Makes the drone go forward while the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="predicate"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoForwardWhile(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (predicate(this))
                    GoForward(speed);
            else
                Task.Factory.StartNew(() => GoForwardWhile(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go forward until the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoForwardUntil(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (!predicate(this))
                    GoForward(speed);
            else
                Task.Factory.StartNew(() => GoForwardUntil(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go forward for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="milliseconds"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoForwardFor(int milliseconds, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);
            GoForwardUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        /// <summary>
        /// Makes the drone go forward for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="timeSpan"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoForwardFor(TimeSpan timeSpan, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.Add(timeSpan);
            GoForwardUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }


        //Backward
        public void GoBack(float speed = 1.0f)
        {
            if (speed >= 0.1)
                Send(new ProgressCommand(FlightMode.Progressive, 0, speed, 0, 0));
            else
                throw new ArgumentOutOfRangeException(nameof(speed));
        }

        /// <summary>
        /// Makes the drone go back while the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="predicate"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoBackWhile(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (predicate(this))
                    GoBack(speed);
            else
                Task.Factory.StartNew(() => GoBackWhile(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go back until the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoBackUntil(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (!predicate(this))
                    GoBack(speed);
            else
                Task.Factory.StartNew(() => GoBackUntil(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go back for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="milliseconds"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoBackFor(int milliseconds, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);
            GoBackUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        /// <summary>
        /// Makes the drone go back for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="timeSpan"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoBackFor(TimeSpan timeSpan, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.Add(timeSpan);
            GoBackUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }


        //Left

        public void GoLeft(float speed = 1.0f)
        {
            if (speed >= 0.1)
                Send(new ProgressCommand(FlightMode.Progressive, -1.0f * speed, 0, 0, 0));
            else
                throw new ArgumentOutOfRangeException(nameof(speed));
        }

        /// <summary>
        /// Makes the drone go Left while the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="predicate"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoLeftWhile(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (predicate(this))
                    GoLeft(speed);
            else
                Task.Factory.StartNew(() => GoLeftWhile(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go Left until the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoLeftUntil(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (!predicate(this))
                    GoLeft(speed);
            else
                Task.Factory.StartNew(() => GoLeftUntil(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go Left for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="milliseconds"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoLeftFor(int milliseconds, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);
            GoLeftUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        /// <summary>
        /// Makes the drone go Left for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="timeSpan"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoLeftFor(TimeSpan timeSpan, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.Add(timeSpan);
            GoLeftUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        public void GoRight(float speed = 1.0f)
        {
            if (speed >= 0.1)
                Send(new ProgressCommand(FlightMode.Progressive, speed, 0, 0, 0));
            else
                throw new ArgumentOutOfRangeException(nameof(speed));
        }

        /// <summary>
        /// Makes the drone go Right while the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="predicate"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoRightWhile(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (predicate(this))
                    GoRight(speed);
            else
                Task.Factory.StartNew(() => GoRightWhile(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go Right until the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void GoRightUntil(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (!predicate(this))
                    GoRight(speed);
            else
                Task.Factory.StartNew(() => GoRightUntil(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone go Right for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="milliseconds"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoRightFor(int milliseconds, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);
            GoRightUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        /// <summary>
        /// Makes the drone go Right for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="timeSpan"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void GoRightFor(TimeSpan timeSpan, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.Add(timeSpan);
            GoRightUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }


        //Left

        public void TurnLeft(float speed = 1.0f)
        {
            if (speed >= 0.1)
                Send(new ProgressCommand(FlightMode.Progressive, 0, 0, -1.0f * speed, 0));
            else
                throw new ArgumentOutOfRangeException(nameof(speed));
        }

        /// <summary>
        /// Makes the drone turn Left while the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="predicate"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void TurnLeftWhile(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (predicate(this))
                    TurnLeft(speed);
            else
                Task.Factory.StartNew(() => TurnLeftWhile(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone turn Left until the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void TurnLeftUntil(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (!predicate(this))
                    TurnLeft(speed);
            else
                Task.Factory.StartNew(() => TurnLeftUntil(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone turn Left for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="milliseconds"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void TurnLeftFor(int milliseconds, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);
            TurnLeftUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        /// <summary>
        /// Makes the drone turn Left for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="timeSpan"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void TurnLeftFor(TimeSpan timeSpan, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.Add(timeSpan);
            TurnLeftUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }


        //Turn Right
        public void TurnRight(float speed = 1.0f)
        {
            if (speed >= 0.1)
                Send(new ProgressCommand(FlightMode.Progressive, 0, 0, speed, 0));
            else
                throw new ArgumentOutOfRangeException(nameof(speed));
        }

        /// <summary>
        /// Makes the drone turn Right while the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="predicate"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void TurnRightWhile(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (predicate(this))
                    TurnRight(speed);
            else
                Task.Factory.StartNew(() => TurnRightWhile(predicate, speed, false));
            
        }

        /// <summary>
        /// Makes the drone turn Right until the given predicate is true.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        public void TurnRightUntil(Func<DroneClient, bool> predicate, float speed = 1.0f, bool blockUntilDone = false)
        {
            if (blockUntilDone)
                while (!predicate(this))
                    TurnRight(speed);
            else
                Task.Factory.StartNew(() => TurnRightUntil(predicate, speed, false));
        }

        /// <summary>
        /// Makes the drone turn Right for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="milliseconds"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void TurnRightFor(int milliseconds, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);
            TurnRightUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }

        /// <summary>
        /// Makes the drone turn Right for a specified length in time.
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="timeSpan"></param>
        /// <param name="blockUntilDone">Whether the drone thread must be blocked until the execution finishes.</param>
        /// <returns></returns>
        public void TurnRightFor(TimeSpan timeSpan, float speed = 1.0f, bool blockUntilDone = false)
        {
            var until = DateTime.Now.Add(timeSpan);
            TurnRightUntil(x => DateTime.Now >= until, speed, blockUntilDone);
        }
        
        #endregion


        protected override void DisposeOverride()
        {
            base.DisposeOverride();

            _navdataAcquisition.Dispose();
            _commandSender.Dispose();
            _videoAcquisition.Dispose();
        }
    }
}