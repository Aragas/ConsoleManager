using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ConsoleManager
{
    public static class FastConsole
    {
        private static Thread ConsoleManagerThread { get; set; }

        public static string TitleFormatted { get; set; } = "FastConsole FPS: {0}";

        public static int ScreenFPS { get { return _screenFPS; } set { _screenFPS = value; _excecutionMilliseconds = 1000 / _screenFPS; UpdateTitle(); } }
        private static int _screenFPS;

        private static int ExcecutionMilliseconds { get { return _excecutionMilliseconds; } set { _excecutionMilliseconds = value; _screenFPS = 1000 / _excecutionMilliseconds; UpdateTitle(); } }
        private static int _excecutionMilliseconds;
        
        private static char[][] ScreenBuffer { get; set; } = new char[0][];
        private static int ScreenWidth => Console.WindowWidth - 1;
        private static int ScreenHeight => Console.WindowHeight - 1;

        private static StreamWriter StandardOutput { get; } = new StreamWriter(Console.OpenStandardOutput());

        private static List<string> ConsoleOutput { get; } = new List<string>();
        private static int ConsoleOutputLength => ScreenHeight - 2;
        private static Queue<string> ConsoleInput { get; } = new Queue<string>();
        private static string CurrentConsoleInput { get; set; } = string.Empty;
        private static int CurrentLine { get; set; }
        private static int CurrentLineCharPos { get; set; }
        private static bool CurrentLineCharPosVisible { get; set; }

        private static List<string> InputHistory { get; } = new List<string>(20);
        private static int InputHistoryPos { get; set; }

        public static bool InputAvailable => !Stopped && ConsoleInput.Count > 0;

        public static bool Enabled => !Stopped;
        private static bool Stopped { get; set; }

        private static ConcurrentDictionary<string, Func<object[]>> ConstantLines { get; } = new ConcurrentDictionary<string, Func<object[]>>();
        public static void ConstantAddLine(string format, Func<object[]> @params)
        {
            if(@params != null)
                ConstantLines.TryAdd(format, @params);
        }
        public static void ConstantClearLines() { ConstantLines.Clear(); }

        internal static void Write(string text = "")
        {
            if (!Stopped)
            {
                if (ConsoleOutput.Count == 0 || ConsoleOutput[ConsoleOutput.Count - 1].EndsWith(Environment.NewLine))
                    ConsoleOutput.Add(text);
                else
                    ConsoleOutput[ConsoleOutput.Count - 1] += text;
            }
        }
        internal static void WriteLine(string text = "")
        {
            if (!Stopped)
            {
                if (ConsoleOutput.Count == 0 || ConsoleOutput[ConsoleOutput.Count - 1].EndsWith(Environment.NewLine))
                    ConsoleOutput.Add(text + Environment.NewLine);
                else
                    ConsoleOutput[ConsoleOutput.Count - 1] += text + Environment.NewLine;

                if (ConsoleOutput.Count > ConsoleOutputLength)
                    ConsoleOutput.RemoveAt(0);
            }
        }
        public static string ReadLine() => Stopped ? string.Empty : ConsoleInput.Dequeue();

        public static void Start(int fps = 20, bool cursorVisible = false)
        {
            if (ConsoleManagerThread != null && ConsoleManagerThread.IsAlive)
                Stop();

            ScreenFPS = fps;
            Console.CursorVisible = cursorVisible;

            Console.SetOut(new ConsoleOutWriter());

            ConsoleManagerThread = new Thread(Cycle) { IsBackground = true, Name = "ConsoleManagerThread" };
            ConsoleManagerThread.Start();
        }
        public static void Stop()
        {
            Stopped = true;
            while (ConsoleManagerThread != null && ConsoleManagerThread.IsAlive)
                Thread.Sleep(ExcecutionMilliseconds);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }


        public static long ConsoleManagerThreadTime { get; private set; }
        private static void Cycle()
        {
            var watch = Stopwatch.StartNew();
            var watch2 = Stopwatch.StartNew();
            while (!Stopped)
            {
                if (ScreenBuffer.Length != ScreenHeight)
                {
                    ScreenBuffer = new char[ScreenHeight][];
                    for (var y = 0; y < ScreenBuffer.Length; y++)
                        ScreenBuffer[y] = new char[ScreenWidth];
                    Console.Clear();
                }

                var emptyLine = string.Empty.PadRight(ScreenWidth).ToCharArray();
                for (var cy = 0; cy < ScreenBuffer.Length; cy++)
                    ScreenBuffer[cy] = emptyLine;
                CurrentLine = 0;

                foreach (var line in ConstantLines)
                    DrawLine(string.Format(line.Key, line.Value()));

                for (var i = 0; i < ConsoleOutput.Count; i++)
                    DrawLine(ConsoleOutput[i]);

                HandleInput();
                DrawLine(CurrentConsoleInput, ScreenBuffer.Length > 0 ? ScreenBuffer.Length - 1 : ScreenBuffer.Length);

                DrawScreen();


                // -- Default Windows Console Cursor Blink Rate
                if (watch2.ElapsedMilliseconds > 530)
                {
                    CurrentLineCharPosVisible = !CurrentLineCharPosVisible;

                    watch2.Reset();
                    watch2.Start();
                }

                if (watch.ElapsedMilliseconds < ExcecutionMilliseconds)
                {
                    ConsoleManagerThreadTime = watch.ElapsedMilliseconds;

                    var time = (int)(ExcecutionMilliseconds - watch.ElapsedMilliseconds);
                    if (time < 0) time = 0;
                    Thread.Sleep(time);
                }
                watch.Reset();
                watch.Start();
            }

            //Console.Clear();
        }

        private static void HandleInput()
        {
            if (!Console.KeyAvailable)
                return;

            var input = Console.ReadKey();
            switch (input.Key)
            {
                case ConsoleKey.Enter:
                    //Write(Environment.NewLine);
                    WriteLine(CurrentConsoleInput);
                    ConsoleInput.Enqueue(CurrentConsoleInput);

                    InputHistory.Add(CurrentConsoleInput);
                    if (InputHistory.Count > 20)
                        InputHistory.RemoveAt(InputHistory.Count - 1);

                    CurrentConsoleInput = string.Empty;
                    CurrentLineCharPos = 0;
                    break;

                case ConsoleKey.Backspace:
                    if (CurrentLineCharPos <= 0)
                        break;

                    if (CurrentConsoleInput.Length > 0)
                    {
                        CurrentConsoleInput = CurrentConsoleInput.Remove(CurrentLineCharPos - 1, 1);
                        CurrentLineCharPos--;
                    }
                    break;
                case ConsoleKey.Delete:
                    if(CurrentLineCharPos > CurrentConsoleInput.Length)
                        break;

                    if (CurrentLineCharPos < CurrentConsoleInput.Length)
                        CurrentConsoleInput = CurrentConsoleInput.Remove(CurrentLineCharPos, 1);
                    break;

                case ConsoleKey.Escape:

                case ConsoleKey.UpArrow:
                    if (InputHistoryPos < InputHistory.Count)
                    {
                        InputHistoryPos++;
                        CurrentConsoleInput = InputHistory[InputHistory.Count - InputHistoryPos];
                    }
                    break;
                case ConsoleKey.DownArrow:
                    if (InputHistoryPos > 1)
                    {
                        InputHistoryPos--;
                        CurrentConsoleInput = InputHistory[InputHistory.Count - InputHistoryPos];
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (CurrentLineCharPos > 0)
                        CurrentLineCharPos--;
                    break;
                case ConsoleKey.RightArrow:
                    if (CurrentLineCharPos < CurrentConsoleInput.Length)
                        CurrentLineCharPos++;
                    break;
                case ConsoleKey.Home:
                    CurrentLineCharPos = 0;
                    break;
                case ConsoleKey.End:
                    CurrentLineCharPos = CurrentConsoleInput.Length;
                    break;

                case ConsoleKey.Tab:
                    break;

                default:
                    CurrentConsoleInput = CurrentConsoleInput.Insert(CurrentLineCharPos, input.KeyChar.ToString());
                    CurrentLineCharPos++;

                    //if(char.IsLetterOrDigit(input.KeyChar) || input.KeyChar == ' ' || input.KeyChar == '/')
                    //    CurrentConsoleInput += input.KeyChar;
                    //CurrentLineCharPos++;
                    break;
            }
        }

        private static void DrawLine(string text = "")
        {
            if (text.Length > ScreenWidth)
            {
                DrawLineInternal(text.Substring(0, ScreenWidth));
                DrawLine(text.Remove(0, ScreenWidth));
            }
            else
                DrawLineInternal(text);
        }
        private static void DrawLine(string text, int y)
        {
            if (text.Length > ScreenWidth)
                DrawLineInternal(text.Substring(0, ScreenWidth), y);
            else
                DrawLineInternal(text, y);
        }
        private static void DrawLineInternal(string text)
        {
            if (ScreenBuffer.Length > CurrentLine)
                ScreenBuffer[CurrentLine] = text.PadRight(ScreenWidth).ToCharArray();

            CurrentLine++;
        }
        private static void DrawLineInternal(string text, int y)
        {
            if (ScreenBuffer.Length > y)
                ScreenBuffer[y] = text.PadRight(ScreenWidth).ToCharArray();
        }
        private static void DrawCurrentLine()
        {
            if (!CurrentLineCharPosVisible)
                return;


            var x = CurrentLineCharPos;
            var y = ScreenBuffer.Length > 0 ? ScreenBuffer.Length - 1 : ScreenBuffer.Length;

            var text = CurrentConsoleInput;
            if (text.Length > ScreenWidth)
                text = text.Substring(0, ScreenWidth);
            text = text.PadRight(ScreenWidth);


            Console.MoveBufferArea(x, y, 1, 1, 0, ScreenHeight, text[CurrentLineCharPos], ConsoleColor.White, ConsoleColor.DarkRed);
            Console.MoveBufferArea(0 + 1, ScreenHeight, 1, 1, 0, ScreenHeight);
        }
        private static void DrawScreen()
        {
            Console.SetCursorPosition(0, 0);
            
            for (var y = 0; y < ScreenBuffer.Length; ++y)
                StandardOutput.WriteLine(new string(ScreenBuffer[y]).Replace(Environment.NewLine, string.Empty));
            
            StandardOutput.Flush();

            DrawCurrentLine();
        }

        private static void UpdateTitle() => Console.Title = string.Format(TitleFormatted, ScreenFPS);

        public static void ClearOutput()
        {
            if (!Stopped)
                ConsoleOutput.Clear();
        }
    }
}
