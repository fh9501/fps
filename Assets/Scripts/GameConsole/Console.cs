﻿using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEngine;
using Utils;
using Utils.DebugOverlay;
using Utils.Pool;

namespace GameConsole
{
    public interface IConsoleUi
    {
        void Init();
        void Shutdown();
        void OutputString(string message);
        bool IsOpen();
        void SetOpen(bool open);
        void ConsoleUpdate();
        void ConsoleLateUpdate();
    }

    public class Console
    {
        public delegate void MethodDelegate(string[] args);

        private const int HistoryCount = 50;

        [ConfigVar(name = "config.showlastline", defaultValue = "0",
            description = "Show last logged line briefly at top of screen")]
        private static ConfigVar _consoleShowLastLine;

        private static IConsoleUi _consoleUi;
        private static string _lastMessage = "";
        private static double _timeLastMessage;
        private static readonly Dictionary<string, ConsoleCommand> Commands = new Dictionary<string, ConsoleCommand>();
        private static readonly List<string> PendingCommands = new List<string>();
        private static readonly string[] History = new string[HistoryCount];
        private static int HistoryNextIndex = 0;
        private static int HistoryIndex = 0;
        public static int pendingCommandsWaitForFrames;

        public static void Init(IConsoleUi consoleUi)
        {
            Debug.Assert(consoleUi != null);

            _consoleUi = consoleUi;
            _consoleUi.Init();
            AddCommand("help", CmdHelp, "help <command>: Show available commands");
            AddCommand("vars", CmdVars, "Show available variables");
            AddCommand("wait", CmdWait, "Wait for next frame or level");
            AddCommand("waitload", CmdWaitLoad, "Wait for level load");
            AddCommand("exec", CmdExec, "Executes commands from file");
            Write("Console ready");
        }

        public static void Shutdown()
        {
            _consoleUi.Shutdown();
        }

        private static void OutputString(string message)
        {
            _consoleUi?.OutputString(message);
        }

        public static void Write(string message)
        {
            if (_consoleShowLastLine?.IntValue > 0)
            {
                _lastMessage = message;
                _timeLastMessage = Game.Main.GameRoot.frameTime;
            }

            OutputString(message);
        }

        public static void AddCommand(string cmd, MethodDelegate func, string description, int tag = 0)
        {
            cmd = cmd.ToLower();
            if (Commands.ContainsKey(cmd))
            {
                Write($"Cannot add command {cmd} twice");
                return;
            }

            Commands.Add(cmd, new ConsoleCommand(cmd, func, description, tag));
        }

        public static void RemoveCommand(string cmd)
        {
            Commands.Remove(cmd);
        }

        public static void RemoveCommandsWithTag(int tag)
        {
            var removeList = SimpleObjectPool.Pop<List<string>>();
            foreach (ConsoleCommand command in Commands.Values)
                if (command.tag == tag)
                    removeList.Add(command.name);

            foreach (string name in removeList)
                RemoveCommand(name);

            removeList.Clear();
            SimpleObjectPool.Push(removeList);
        }

        public static void ProcessCommandLineArgument(string[] arguments)
        {
            OutputString($"ProcessCommandLineArguments: {string.Join(" ", arguments)}");
            var commands = SimpleObjectPool.Pop<List<string>>();
            for (var i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];

                // '+' means new command, and '-' means optional argument
                bool newCommandStarting = argument.StartsWith("+") || argument.StartsWith("-");

                if (newCommandStarting)
                {
                    commands.Add(argument.Substring(1));
                }
                else
                {
                    int count = commands.Count;
                    if (count > 0)
                    {
                        string command = $"{commands[count - 1]} {argument}";
                        commands[count - 1] = command;
                    }
                }
            }

            for (var i = 0; i < commands.Count; i++)
            {
                string command = commands[i];
                if (command.StartsWith("+"))
                    EnqueueCommandNoHistory(command);
            }

            commands.Clear();
            SimpleObjectPool.Push(commands);
        }

        public static void EnqueueCommandNoHistory(string command)
        {
            GameDebug.Log($"cmd: {command}");
            PendingCommands.Add(command);
        }

        public static void EnqueueCommand(string command)
        {
            History[HistoryNextIndex % HistoryCount] = command;
            HistoryNextIndex++;
            HistoryIndex = HistoryNextIndex;
            EnqueueCommandNoHistory(command);
        }

        public static bool IsOpen()
        {
            return _consoleUi.IsOpen();
        }

        public static void SetOpen(bool open)
        {
            _consoleUi.SetOpen(open);
        }

        public static void ConsoleUpdate()
        {
            double lastMessageTime = Game.Main.GameRoot.frameTime - _timeLastMessage;
            if (lastMessageTime < 1) DebugOverlay.WriteString(0, 0, _lastMessage);
        }

        private static void CmdExec(string[] args)
        {
            var silent = false;
            string fileName;
            if (args.Length == 1)
            {
                fileName = args[0];
            }
            else if (args.Length == 2 && args[0] == "-s")
            {
                silent = true;
                fileName = args[1];
            }
            else
            {
                OutputString("Usage: exec [-s] <filename>");
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(fileName);
                PendingCommands.InsertRange(0, lines);
                if (PendingCommands.Count > 128)
                {
                    PendingCommands.Clear();
                    OutputString("Command overflow. Flushing pending commands!!!");
                }
            }
            catch (Exception e)
            {
                if (!silent)
                {
                    OutputString($"Exec failed: {e.Message}");
                }
            }
        }

        private static void CmdWaitLoad(string[] args)
        {
            throw new NotImplementedException();
        }

        private static void CmdWait(string[] args)
        {
            throw new NotImplementedException();
        }

        private static void CmdVars(string[] args)
        {
            throw new NotImplementedException();
        }

        private static void CmdHelp(string[] args)
        {
            if (args.Length == 0)
            {
                OutputString("Available commands:");
                foreach (ConsoleCommand command in Commands.Values)
                {
                    OutputString($"{command.name}: {command.description}");
                }
            }
            else
            {
                foreach (string command in args)
                {
                    OutputString(Commands.TryGetValue(command, out ConsoleCommand cmd)
                        ? $"{command}: {cmd.description}"
                        : $"{command}: [There is no such command.]");
                }
            }
        }

        public static void WriteLog(string message, string stacktrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    message = $"[Error] {message}";
                    break;
                case LogType.Assert:
                    message = $"[Assert] {message}";
                    break;
                case LogType.Warning:
                    message = $"[Warning] {message}";
                    break;
                case LogType.Log:
                    message = $"[Log] {message}";
                    break;
                case LogType.Exception:
                    message = $"[Exception] {message}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Write(message);
        }

        private class ConsoleCommand
        {
            public readonly string name;
            public readonly int tag;
            public string description;
            public MethodDelegate method;

            public ConsoleCommand(string name, MethodDelegate method, string description, int tag)
            {
                this.name = name;
                this.method = method;
                this.description = description;
                this.tag = tag;
            }
        }
    }
}