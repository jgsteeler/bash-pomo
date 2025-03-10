using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

class Program
{
    private static int _workTime = 25 * 60; // 25 minutes in seconds
    private static int _shortBreak = 5 * 60; // 5 minutes in seconds
    private static int _longBreak = 15 * 60; // 15 minutes in seconds
    private static int _pomodoroCount = 0;
    private static bool _running = false;
    private static bool _paused = false;
    private static bool _skip = false;
    private static CancellationTokenSource _cts;
    private static AudioFileReader _audioFile;
    private static WaveOutEvent _outputDevice;
    private static string _tickFile = new FileInfo("s_tick.mp3").FullName;
    private static string _workEndFile = new FileInfo("s_pom_alarm.mp3").FullName;
    private static string _breakEndFile = new FileInfo("s_break_alarm.mp3").FullName;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Pomodoro Timer Started.");
        while (true)
        {
            Console.WriteLine("Press Enter to start the work timer...");
            Console.ReadLine();
            await StartTimer(_workTime, "Work for 25 minutes.", true, false);

            _pomodoroCount++;
            if (_pomodoroCount % 4 == 0)
            {
                Console.WriteLine("Press Enter to start the long break timer...");
                Console.ReadLine();
                await StartTimer(_longBreak, "Take a long break for 15 minutes.", false, true);
            }
            else
            {
                Console.WriteLine("Press Enter to start the short break timer...");
                Console.ReadLine();
                await StartTimer(_shortBreak, "Take a short break for 5 minutes.", false, true);
            }
        }
    }

    private static async Task StartTimer(int duration, string message, bool tick, bool abreak)
    {
        _running = true;
        _paused = false;
        _skip = false;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Console.WriteLine(message);
        var timerTask = Task.Run(async () =>
        {
            while (duration > 0 && !token.IsCancellationRequested && !_skip)
            {
                if (!_paused)
                {
                    int mins = duration / 60;
                    int secs = duration % 60;
                    Console.Write($"\r{mins:D2}:{secs:D2}");
                    await Task.Delay(1000);
                    duration--;
                }
            }
        }, token);

        var tickTask = Task.Run(async () =>
        {
            while (duration > 0 && !token.IsCancellationRequested && !_skip)
            {
                if (tick)
                {
                    if (!_paused)
                    {
                        if (_outputDevice?.PlaybackState != PlaybackState.Playing)
                        {
                            _outputDevice?.Dispose();
                            _audioFile?.Dispose();

                            _audioFile = new AudioFileReader(_tickFile);
                            _outputDevice = new WaveOutEvent();
                            _outputDevice.Init(_audioFile);
                            _outputDevice.Volume = 0.5f;
                            _outputDevice.Play();
                        }
                    }
                    else
                    {
                        _outputDevice?.Stop();
                        _outputDevice?.Dispose();
                        _audioFile?.Dispose();
                    }
                }
            }
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _audioFile?.Dispose();

        }, token);

        var inputTask = Task.Run(() =>
        {
            while (!token.IsCancellationRequested && !_skip)
            {
                var key = Console.ReadKey(true).KeyChar;
                if (key == 'p')
                {
                    _paused = !_paused;
                    Console.WriteLine(_paused ? "\nTimer paused." : "\nTimer resumed.");
                }
                else if (key == 's')
                {
                    _skip = true;
                    Console.WriteLine("\nTimer skipped.");
                }

            }
        }, token);


        await Task.WhenAny(timerTask, inputTask, tickTask);

        if (!_paused && !_skip)
        {
            Console.WriteLine("\nTime's up! Alarm ringing...");
            Alarm(abreak ? _breakEndFile : _workEndFile);
        }
        _running = false;
    }


    private static void Alarm(string filename)
    {
        _outputDevice?.Dispose();
        _audioFile?.Dispose();

        _audioFile = new AudioFileReader(filename);
        _outputDevice = new WaveOutEvent();
        _outputDevice.Init(_audioFile);
        _outputDevice.Volume = 0.75f;
        _outputDevice.Play();
        var count = 0;
        while (_outputDevice.PlaybackState == PlaybackState.Playing && count < 5)
        {
            count++;
            Thread.Sleep(500);
        }
        _outputDevice.Stop();
        _outputDevice?.Dispose();
        _audioFile?.Dispose();

    }
}