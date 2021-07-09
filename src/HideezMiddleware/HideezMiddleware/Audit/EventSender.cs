using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.WorkstationEvents;
using Newtonsoft.Json;

namespace HideezMiddleware.Audit
{
    public class EventSender : Logger
    {
        // Once per 15 minutes seems ok for release.
        // It equals to 36 checks per work day
#if DEBUG
        const double AUTO_SEND_INTERVAL = 30_000;
#else
        const double AUTO_SEND_INTERVAL = 900_000;
# endif

        const int MIN_EVENTS_FOR_SEND = 20;
        const int EVENTS_PER_SET = 25;
        const int SET_INTERVAL = 5_000; // Interval between multiple sets

        readonly IHesAppConnection _hesAppConnection;
        readonly FileSystemWatcher _fileSystemWatcher;
        readonly EventSaver _eventSaver;

        readonly System.Timers.Timer automaticEventSendingTimer = new System.Timers.Timer(AUTO_SEND_INTERVAL);

        // Ensures that only one sending is being performed at a time
        int _sendingThreadSafetyInt = 0;

        // Used to synchronize sending with task completion source creation and change
        readonly object _sendingTcsLock = new object();

        TaskCompletionSource<int> _sendingTcs = null;
        bool _skipSendingInterval = false;

        public EventSender(IHesAppConnection hesAppConnection, EventSaver eventSaver, ILog log)
            : base(nameof(EventSender), log)
        {
            _eventSaver = eventSaver;
            _eventSaver.UrgentEventSaved += EventSaver_UrgentEventSaved;

            _hesAppConnection = hesAppConnection;
            _fileSystemWatcher = new FileSystemWatcher(_eventSaver.EventsDirectoryPath);
            _fileSystemWatcher.Created += FileSystemWatcher_OnFileCreated;
            _fileSystemWatcher.EnableRaisingEvents = true;

            automaticEventSendingTimer.Elapsed += SendTimer_Elapsed;
            automaticEventSendingTimer.Start();
            SendTimer_Elapsed(null, null); // Trigger 15 minute events check
        }

        async void EventSaver_UrgentEventSaved(object sender, EventArgs e)
        {
            try
            {
                await SendEventsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        async void FileSystemWatcher_OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (GetEventsCount() >= MIN_EVENTS_FOR_SEND)
                {
                    await SendEventsAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        async void SendTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                await SendEventsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        int GetEventsCount()
        {
            try
            {
                if (Directory.Exists(_eventSaver.EventsDirectoryPath))
                    return Directory.GetFiles(_eventSaver.EventsDirectoryPath).Count();
            }
            catch (Exception) { }

            return 0;
        }

        /// <summary>
        /// Deserializes all events saved in events folder and returns them as List<>
        /// </summary>
        /// <returns>Returns all events deserialized from events folder</returns>
        async Task<List<WorkstationEvent>> DeserializeEvents()
        {
            List<WorkstationEvent> events = new List<WorkstationEvent>();

            if (!Directory.Exists(_eventSaver.EventsDirectoryPath))
                return events;

            try
            {
                var eventFiles = Directory.GetFiles(_eventSaver.EventsDirectoryPath).OrderBy(f => new FileInfo(f).LastWriteTimeUtc);

                foreach (var file in eventFiles)
                {
                    try
                    {
                        string data = File.ReadAllText(file);
                        dynamic jsonObj = JsonConvert.DeserializeObject(data);

                        if (jsonObj == null)
                        {
                            WriteLine($"Could not parse event file. Data: {data}", LogErrorSeverity.Error);
                            await DeleteEventFile(file);
                            continue;
                        }

                        string eventVersion = jsonObj[nameof(WorkstationEvent.Version)];

                        if (eventVersion != WorkstationEvent.ClassVersion)
                            throw new NotSupportedException($"This version: {eventVersion} data for deserialize workstation event is not supported.");

                        WorkstationEvent we = JsonConvert.DeserializeObject<WorkstationEvent>(data);
                        events.Add(we);
                    }
                    catch (JsonException ex)
                    {
                        WriteLine(ex);

                        await DeleteEventFile(file);
                    }
                    catch (NotSupportedException ex)
                    {
                        WriteLine(ex);

                        await DeleteEventFile(file);
                    }
                    catch (Exception ex)
                    {
                        WriteLine(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }

            return events;
        }

        /// <summary>
        /// Delete specified event files in the event folder
        /// </summary>
        /// <param name="workstationEvents"></param>
        async Task DeleteEvents(IEnumerable<WorkstationEvent> workstationEvents)
        {
            foreach (var we in workstationEvents)
            {
                var file = Path.Combine(_eventSaver.EventsDirectoryPath, we.Id);
                await DeleteEventFile(file);
            }
        }

        async Task DeleteEventFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
#if DEBUG
            catch (UnauthorizedAccessException)
            {
                // Generated when trying to access %programdata% without elevation
            }
#endif
            catch (IOException)
            {
                // Todo: file-in-use handling is hard to understand, requires refactoring
                // File in use, try again
                await Task.Delay(250);
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        public async Task SendEventsAsync(bool skipSendingInterval = false)
        {
            //WriteDebugLine($"HASH CODE OF HES CONENCTION {_hesAppConnection.GetHashCode()}");
            if (_hesAppConnection != null
                && _hesAppConnection.State == HesConnectionState.Connected
                && Interlocked.CompareExchange(ref _sendingThreadSafetyInt, 1, 0) == 0)
            {
                try
                {
                    if (GetEventsCount() == 0)
                        return;

                    _sendingTcs = new TaskCompletionSource<int>();

                    automaticEventSendingTimer.Stop();

                    var eventsQueue = await DeserializeEvents();
                    WriteLine($"Sending {eventsQueue.Count} " + (eventsQueue.Count == 1 ? "event" : "events") + " to HES");

                    _skipSendingInterval = skipSendingInterval;
                    await BreakIntoSetsAndSendToServer(eventsQueue);
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                }
                finally
                {
                    if (_sendingTcs != null)
                    {
                        _sendingTcs.SetResult(0);
                        _sendingTcs = null;
                    }
                    automaticEventSendingTimer.Start();
                    Interlocked.Exchange(ref _sendingThreadSafetyInt, 0);
                }
            }
            else
            {
                var tcs = _sendingTcs;
                if (tcs != null)
                {
                    _skipSendingInterval = skipSendingInterval;
                    await tcs.Task;
                }
            }
        }

        async Task BreakIntoSetsAndSendToServer(IEnumerable<WorkstationEvent> events)
        {
            if (_hesAppConnection?.State != HesConnectionState.Connected)
                return;

            var sets = events.ToSets(EVENTS_PER_SET).ToList();

            if (sets.Count > 1)
                WriteLine($"Split events into {sets.Count} sets of {EVENTS_PER_SET} items");

            for (int i = 0; i < sets.Count; i++)
            {
                if (_hesAppConnection?.State == HesConnectionState.Connected && sets[i] != null && sets[i].Any())
                {
                    var IsServerProcessedEvents = await _hesAppConnection.SaveClientEventsAsync(sets[i].ToArray());

                    if (IsServerProcessedEvents)
                    {
                        WriteLine($"Sent events set. Server: ok");
                        await DeleteEvents(sets[i]);
                    }

                    // Add delay between multiple sendings of sets
                    // Skip delay if already sent last set
                    if (_skipSendingInterval)
                        continue;
                    else if (i < sets.Count - 1)
                        await Task.Delay(SET_INTERVAL);
                }
            }
        }
    }
}
